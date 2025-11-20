using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Factory for creating authentication service instances.
/// Intelligently selects between SSH agent and file-based authentication.
/// </summary>
public static class AuthenticationServiceFactory
{
    /// <summary>
    /// Creates an authentication service based on authentication configuration.
    /// </summary>
    /// <param name="authOptions">Authentication configuration options.</param>
    /// <param name="agentClient">SSH agent client instance.</param>
    /// <param name="sshAgentLogger">Logger for SSH agent authentication service.</param>
    /// <param name="sshKeyLogger">Logger for SSH key authentication service.</param>
    /// <returns>A task that resolves to an authentication service instance.</returns>
    /// <remarks>
    /// Selection strategy based on KeySource:
    /// <list type="bullet">
    /// <item>SystemAgent, OnePasswordAgent, SecretiveAgent: Use SSH agent authentication</item>
    /// <item>FileSystem, ManualPath: Use file-based authentication</item>
    /// </list>
    ///
    /// For agent authentication, the public key is loaded from:
    /// <list type="number">
    /// <item>Explicit KeyPath if configured</item>
    /// <item>Default SSH locations (~/.ssh/id_ed25519.pub, ~/.ssh/id_rsa.pub, etc.) if KeyPath not provided</item>
    /// <item>SSH agent identity list if no files are found</item>
    /// </list>
    /// </remarks>
    public static async Task<IAuthenticationService> CreateAsync(
        AuthOptions authOptions,
        ISshAgentClient agentClient,
        ILogger<SshAgentAuthenticationService> sshAgentLogger,
        ILogger<SshKeyAuthenticationService> sshKeyLogger)
    {
        ArgumentNullException.ThrowIfNull(authOptions);
        ArgumentNullException.ThrowIfNull(agentClient);
        ArgumentNullException.ThrowIfNull(sshAgentLogger);
        ArgumentNullException.ThrowIfNull(sshKeyLogger);

        // Determine authentication method based on KeySource
        var useAgentAuth = authOptions.KeySource is SshKeySource.SystemAgent
            or SshKeySource.OnePasswordAgent
            or SshKeySource.SecretiveAgent;

        if (useAgentAuth && !string.IsNullOrWhiteSpace(authOptions.AgentSocketPath))
        {
            // Try to load public key for agent authentication
            // Agent needs the public key to identify which key in the agent to use for signing
            byte[]? publicKey = null;
            string? loadedFrom = null;

            // First, try explicit KeyPath if configured
            if (!string.IsNullOrWhiteSpace(authOptions.KeyPath))
            {
                var result = TryLoadPublicKeyFromPath(authOptions.KeyPath, sshAgentLogger);
                if (result.publicKey != null)
                {
                    publicKey = result.publicKey;
                    loadedFrom = result.path;
                }
            }

            // If no explicit KeyPath or loading failed, try common default locations
            if (publicKey == null)
            {
                var defaultLocations = GetDefaultSshKeyLocations();
                foreach (var location in defaultLocations)
                {
                    var result = TryLoadPublicKeyFromPath(location, sshAgentLogger);
                    if (result.publicKey != null)
                    {
                        publicKey = result.publicKey;
                        loadedFrom = result.path;
                        sshAgentLogger.LogInformation(
                            "Loaded public key from default location: {Path}",
                            loadedFrom);
                        break;
                    }
                }
            }

            // If still no public key, try querying the agent directly
            if (publicKey == null)
            {
                sshAgentLogger.LogInformation("No public key files found, querying SSH agent for available identities");

                try
                {
                    // Connect to agent first
                    var provider = authOptions.KeySource switch
                    {
                        SshKeySource.OnePasswordAgent => SshAgentProvider.OnePassword,
                        SshKeySource.SecretiveAgent => SshAgentProvider.Secretive,
                        SshKeySource.SystemAgent => SshAgentProvider.System,
                        _ => SshAgentProvider.Auto
                    };

                    var connected = await agentClient.ConnectAsync(provider).ConfigureAwait(false);
                    if (!connected)
                    {
                        sshAgentLogger.LogWarning("Failed to connect to SSH agent");
                    }
                    else
                    {
                        // List available identities
                        var identities = await agentClient.ListIdentitiesAsync().ConfigureAwait(false);

                        if (identities.Count > 0)
                        {
                            // Use the first available identity
                            publicKey = identities[0];
                            loadedFrom = $"SSH agent ({authOptions.KeySource})";
                            sshAgentLogger.LogInformation(
                                "Retrieved public key from SSH agent ({Count} identities available)",
                                identities.Count);
                        }
                        else
                        {
                            sshAgentLogger.LogWarning("SSH agent has no identities loaded");
                        }
                    }
                }
                catch (Exception ex)
                {
                    sshAgentLogger.LogWarning(ex, "Failed to query SSH agent for identities");
                }
            }

            // If we successfully loaded a public key, create agent authentication service
            if (publicKey != null)
            {
                sshAgentLogger.LogDebug("Creating SSH agent authentication service with public key from {Source}", loadedFrom);
                return new SshAgentAuthenticationService(agentClient, publicKey, sshAgentLogger);
            }

            // No public key found - log warning and fall back
            sshAgentLogger.LogWarning(
                "SSH agent authentication requested but no public key found. " +
                "Checked: KeyPath ({KeyPath}), default locations ({Defaults}), and SSH agent. " +
                "Falling back to file-based authentication",
                authOptions.KeyPath ?? "(not configured)",
                string.Join(", ", GetDefaultSshKeyLocations()));
        }

        // Fallback to file-based authentication
        return new SshKeyAuthenticationService(sshKeyLogger);
    }

    /// <summary>
    /// Expands ~ to home directory in file paths.
    /// </summary>
    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/", StringComparison.Ordinal) || path.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }
        
        return path;
    }

    /// <summary>
    /// Loads SSH public key from a .pub file and converts to wire format.
    /// </summary>
    /// <param name="filePath">Path to the .pub file.</param>
    /// <returns>Public key in SSH wire format.</returns>
    private static byte[] LoadPublicKeyFromFile(string filePath)
    {
        // Read the .pub file (format: "ssh-ed25519 AAAAC3Nza... comment")
        var content = File.ReadAllText(filePath).Trim();
        var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            throw new FormatException($"Invalid SSH public key format in {filePath}");
        }

        var keyType = parts[0]; // e.g., "ssh-ed25519" or "ssh-rsa"
        var keyDataBase64 = parts[1];

        // The base64 data is already in SSH wire format
        // Format: 4 bytes (type length) + type string + 4 bytes (key length) + key data
        var publicKey = Convert.FromBase64String(keyDataBase64);

        return publicKey;
    }

    /// <summary>
    /// Gets common default SSH public key locations to try when KeyPath is not configured.
    /// </summary>
    /// <returns>Array of default SSH public key paths (with ~ notation).</returns>
    private static string[] GetDefaultSshKeyLocations()
    {
        return
        [
            "~/.ssh/id_ed25519.pub",  // Modern default (Ed25519)
            "~/.ssh/id_rsa.pub",      // Common legacy default (RSA)
            "~/.ssh/id_ecdsa.pub",    // ECDSA keys
            "~/.ssh/id_dsa.pub"       // Very old DSA keys (deprecated)
        ];
    }

    /// <summary>
    /// Attempts to load a public key from the specified path.
    /// </summary>
    /// <param name="path">Path to the SSH key (with or without .pub extension).</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <returns>A tuple containing the loaded public key bytes and the actual path used, or (null, null) if loading failed.</returns>
    private static (byte[]? publicKey, string? path) TryLoadPublicKeyFromPath(
        string path,
        ILogger<SshAgentAuthenticationService> logger)
    {
        try
        {
            var expandedPath = ExpandPath(path);

            // Check if this is a public key file (.pub extension)
            var publicKeyPath = expandedPath.EndsWith(".pub", StringComparison.OrdinalIgnoreCase)
                ? expandedPath
                : expandedPath + ".pub";

            if (!File.Exists(publicKeyPath))
            {
                logger.LogDebug("Public key file not found at {Path}", publicKeyPath);
                return (null, null);
            }

            var publicKey = LoadPublicKeyFromFile(publicKeyPath);
            logger.LogDebug("Successfully loaded public key from {Path}", publicKeyPath);
            return (publicKey, publicKeyPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is FormatException)
        {
            logger.LogDebug(ex, "Failed to load public key from {Path}", path);
            return (null, null);
        }
    }
}
