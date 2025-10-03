using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Factory for creating authentication service instances.
/// Intelligently selects between SSH agent and file-based authentication.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public factory for DI registration")]
public static class AuthenticationServiceFactory
{
    /// <summary>
    /// Creates an authentication service based on available authentication methods.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="agentClient">SSH agent client instance.</param>
    /// <param name="sshAgentLogger">Logger for SSH agent authentication service.</param>
    /// <param name="sshKeyLogger">Logger for SSH key authentication service.</param>
    /// <returns>An authentication service instance.</returns>
    /// <remarks>
    /// Selection strategy:
    /// <list type="number">
    /// <item>SSH agent authentication if agent is available and public key is configured</item>
    /// <item>File-based authentication as fallback</item>
    /// </list>
    /// </remarks>
    public static IAuthenticationService Create(
        IConfiguration configuration,
        ISshAgentClient agentClient,
        ILogger<SshAgentAuthenticationService> sshAgentLogger,
        ILogger<SshKeyAuthenticationService> sshKeyLogger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(agentClient);
        ArgumentNullException.ThrowIfNull(sshAgentLogger);
        ArgumentNullException.ThrowIfNull(sshKeyLogger);

        // Check if SSH agent is available
        var sshAuthSock = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        
        if (!string.IsNullOrWhiteSpace(sshAuthSock))
        {
            // Check if public key is configured
            var publicKeyBase64 = configuration["TenSecondTom:Auth:PublicKey"];
            var publicKeyPath = configuration["TenSecondTom:Auth:PublicKeyPath"];

            if (!string.IsNullOrWhiteSpace(publicKeyBase64))
            {
                // Parse public key from configuration
                // Can be either:
                // 1. Full SSH public key line: "ssh-ed25519 AAAAC3Nza... comment"
                // 2. Just the base64 data: "AAAAC3Nza..."
                try
                {
                    byte[] publicKey;
                    
                    // Check if it's a full SSH public key line (starts with algorithm type)
                    if (publicKeyBase64.StartsWith("ssh-", StringComparison.OrdinalIgnoreCase) ||
                        publicKeyBase64.StartsWith("ecdsa-", StringComparison.OrdinalIgnoreCase))
                    {
                        // Parse as full SSH public key line
                        var parts = publicKeyBase64.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        
                        if (parts.Length < 2)
                        {
                            throw new FormatException("Invalid SSH public key format. Expected: 'algorithm base64data [comment]'");
                        }

                        var keyDataBase64 = parts[1];
                        publicKey = Convert.FromBase64String(keyDataBase64);
                    }
                    else
                    {
                        // Assume it's just the base64 data
                        publicKey = Convert.FromBase64String(publicKeyBase64);
                    }
                    
                    return new SshAgentAuthenticationService(agentClient, publicKey, sshAgentLogger);
                }
                catch (FormatException ex)
                {
                    sshAgentLogger.LogWarning(ex, 
                        "Invalid public key format in TenSecondTom:Auth:PublicKey configuration, falling back to file-based authentication");
                }
            }
            else if (!string.IsNullOrWhiteSpace(publicKeyPath))
            {
                // Load public key from file
                try
                {
                    var expandedPath = ExpandPath(publicKeyPath);
                    if (File.Exists(expandedPath))
                    {
                        var publicKey = LoadPublicKeyFromFile(expandedPath);
                        return new SshAgentAuthenticationService(agentClient, publicKey, sshAgentLogger);
                    }
                    else
                    {
                        sshAgentLogger.LogWarning(
                            "Public key file not found at {Path}, falling back to file-based authentication",
                            expandedPath);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    sshAgentLogger.LogWarning(ex,
                        "Failed to load public key from {Path}, falling back to file-based authentication",
                        publicKeyPath);
                }
            }
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
}
