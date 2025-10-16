using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Spectre.Console;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Provides SSH key-based authentication and session management.
/// </summary>
public sealed class SshKeyAuthenticationService : IAuthenticationService
{
    private const int MaxPassphraseAttempts = 3;
    private const string SessionFileName = "session.json";
    
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    
    private readonly ILogger<SshKeyAuthenticationService> _logger;
    private readonly string _sessionFilePath;
    private readonly string _sshDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SshKeyAuthenticationService"/> class.
    /// </summary>
    /// <param name="logger">The logger for diagnostic information.</param>
    public SshKeyAuthenticationService(ILogger<SshKeyAuthenticationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _sshDirectory = Path.Combine(homeDir, ".ssh");
        
        string tomConfigDir = Path.Combine(homeDir, ".tom");
        Directory.CreateDirectory(tomConfigDir);
        _sessionFilePath = Path.Combine(tomConfigDir, SessionFileName);
    }

    /// <inheritdoc/>
    public async Task<Result<UserSession>> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if there's an existing valid session
            Result<UserSession> existingSession = await LoadSessionAsync(cancellationToken).ConfigureAwait(false);
            if (existingSession.IsSuccess && existingSession.Value.IsActive)
            {
                return existingSession;
            }

            // Discover SSH key
            Result<string> keyPathResult = DiscoverSshKeyPath();
            if (!keyPathResult.IsSuccess)
            {
                return Result<UserSession>.Failure(keyPathResult.Error ?? "SSH key discovery failed");
            }

            string keyPath = keyPathResult.Value;
            AnsiConsole.MarkupLine($"[blue]Authenticating with SSH key:[/] [grey]{keyPath.EscapeMarkup()}[/]");

            // Load SSH key with passphrase handling
            Result<string> keyHashResult = await LoadSshKeyAndGetHashAsync(keyPath, cancellationToken).ConfigureAwait(false);
            if (!keyHashResult.IsSuccess)
            {
                return Result<UserSession>.Failure(keyHashResult.Error ?? "Failed to load SSH key");
            }

            string keyHash = keyHashResult.Value;

            // Create new session
            var session = new UserSession
            {
                SessionId = Guid.NewGuid(),
                SshKeyHash = keyHash,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow,
                ExpiresAt = null // Sessions don't expire automatically
            };

            // Persist session
            await SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
            
            AnsiConsole.MarkupLine("[green]✓[/] Authentication successful");

            return Result<UserSession>.Success(session);
        }
        catch (OperationCanceledException)
        {
            return Result<UserSession>.Failure("Authentication cancelled");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        Result<UserSession> sessionResult = await LoadSessionAsync(cancellationToken).ConfigureAwait(false);
        return sessionResult.IsSuccess && 
               sessionResult.Value.IsActive &&
               (sessionResult.Value.ExpiresAt == null || sessionResult.Value.ExpiresAt > DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_sessionFilePath))
        {
            return Result<bool>.Failure("No active session to logout.");
        }

        Result<UserSession> sessionResult = await LoadSessionAsync(cancellationToken).ConfigureAwait(false);
        if (!sessionResult.IsSuccess || !sessionResult.Value.IsActive)
        {
            return Result<bool>.Failure("No active session to logout.");
        }

        // Delete session file
        File.Delete(_sessionFilePath);
        
        return Result<bool>.Success(true);
    }

    private Result<string> DiscoverSshKeyPath()
    {
        if (!Directory.Exists(_sshDirectory))
        {
            return Result<string>.Failure($"SSH directory not found: {_sshDirectory}");
        }

        // Prefer Ed25519, fallback to RSA
        string ed25519Path = Path.Combine(_sshDirectory, "id_ed25519");
        if (File.Exists(ed25519Path))
        {
            return Result<string>.Success(ed25519Path);
        }

        string rsaPath = Path.Combine(_sshDirectory, "id_rsa");
        if (File.Exists(rsaPath))
        {
            return Result<string>.Success(rsaPath);
        }

        return Result<string>.Failure($"No SSH key found in {_sshDirectory}. Looked for id_ed25519 or id_rsa.");
    }

    private static async Task<Result<string>> LoadSshKeyAndGetHashAsync(string keyPath, CancellationToken cancellationToken)
    {
        // Try to load key without passphrase first
        try
        {
            using var keyFile = new PrivateKeyFile(keyPath);
            string hash = GenerateSshKeyHash(keyFile);
            return await Task.FromResult(Result<string>.Success(hash)).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Key is encrypted, need passphrase - continue to passphrase prompt
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await Task.FromResult(Result<string>.Failure($"Failed to load SSH key: {ex.Message}")).ConfigureAwait(false);
        }

        // Prompt for passphrase with retry logic
        for (int attempt = 1; attempt <= MaxPassphraseAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? passphrase = AnsiConsole.Prompt(
                new TextPrompt<string>("[yellow]Enter SSH key passphrase:[/]")
                    .PromptStyle("grey")
                    .Secret());

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var keyFile = new PrivateKeyFile(keyPath, passphrase);
                string hash = GenerateSshKeyHash(keyFile);
                return await Task.FromResult(Result<string>.Success(hash)).ConfigureAwait(false);
            }
            catch (InvalidOperationException) when (attempt < MaxPassphraseAttempts)
            {
                int attemptsRemaining = MaxPassphraseAttempts - attempt;
                AnsiConsole.MarkupLine($"[red]✗[/] Incorrect passphrase. [yellow]{attemptsRemaining} attempt{(attemptsRemaining != 1 ? "s" : "")} remaining.[/]");
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt >= MaxPassphraseAttempts)
            {
                return await Task.FromResult(Result<string>.Failure(
                    "Authentication failed. Please check your passphrase or SSH key configuration.")).ConfigureAwait(false);
            }
        }

        return await Task.FromResult(Result<string>.Failure(
            "Authentication failed. Please check your passphrase or SSH key configuration.")).ConfigureAwait(false);
    }

    private static string GenerateSshKeyHash(PrivateKeyFile keyFile)
    {
        // Generate SHA256 hash of the public key bytes
        byte[] publicKeyBytes = Encoding.UTF8.GetBytes(keyFile.ToString() ?? string.Empty);
        byte[] hashBytes = SHA256.HashData(publicKeyBytes);
        string hash = Convert.ToBase64String(hashBytes);
        
        return $"sha256:{hash}";
    }

    private async Task<Result<UserSession>> LoadSessionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_sessionFilePath))
        {
            return Result<UserSession>.Failure("No session file found");
        }

        string json = await File.ReadAllTextAsync(_sessionFilePath, cancellationToken).ConfigureAwait(false);
        UserSession? session = JsonSerializer.Deserialize<UserSession>(json);

        if (session == null)
        {
            return Result<UserSession>.Failure("Failed to deserialize session");
        }

        return Result<UserSession>.Success(session);
    }

    private async Task SaveSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(session, SerializerOptions);
        await File.WriteAllTextAsync(_sessionFilePath, json, cancellationToken).ConfigureAwait(false);
    }
}
