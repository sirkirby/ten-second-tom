namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Resolves SSH agent socket paths for different providers.
/// </summary>
public static class SshAgentProviderResolver
{
    /// <summary>
    /// Gets the SSH agent socket path for the specified provider.
    /// </summary>
    /// <param name="provider">The SSH agent provider to use.</param>
    /// <returns>The socket path, or null if not available.</returns>
    public static string? GetSocketPath(SshAgentProvider provider)
    {
        return provider switch
        {
            SshAgentProvider.System => GetSystemAgentPath(),
            SshAgentProvider.OnePassword => GetOnePasswordAgentPath(),
            SshAgentProvider.Secretive => GetSecretiveAgentPath(),
            SshAgentProvider.Auto => GetAutoDetectedAgentPath(),
            _ => null
        };
    }

    /// <summary>
    /// Gets the system default SSH agent socket path from SSH_AUTH_SOCK.
    /// </summary>
    private static string? GetSystemAgentPath()
    {
        var path = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Gets the 1Password SSH agent socket path.
    /// </summary>
    private static string? GetOnePasswordAgentPath()
    {
        // macOS: ~/Library/Group Containers/2BUA8C4S2C.com.1password/t/agent.sock
        // Windows: \\.\pipe\openssh-ssh-agent (handled by 1Password)
        // Linux: ~/.1password/agent.sock
        
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(home, "Library", "Group Containers", "2BUA8C4S2C.com.1password", "t", "agent.sock");
            return File.Exists(path) ? path : null;
        }
        
        if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var path = Path.Combine(home, ".1password", "agent.sock");
            return File.Exists(path) ? path : null;
        }
        
        if (OperatingSystem.IsWindows())
        {
            // 1Password on Windows typically uses the standard Windows SSH agent pipe
            // which is accessed via the SSH_AUTH_SOCK environment variable
            return GetSystemAgentPath();
        }
        
        return null;
    }

    /// <summary>
    /// Gets the Secretive SSH agent socket path (macOS only).
    /// </summary>
    private static string? GetSecretiveAgentPath()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }
        
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var path = Path.Combine(home, "Library", "Containers", "com.maxgoedjen.Secretive.SecretAgent", "Data", "socket.ssh");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Automatically detects the best available SSH agent.
    /// Checks in order: 1Password, Secretive, System default.
    /// </summary>
    private static string? GetAutoDetectedAgentPath()
    {
        // Try 1Password first (most common for modern setups)
        var onePasswordPath = GetOnePasswordAgentPath();
        if (onePasswordPath != null)
        {
            return onePasswordPath;
        }
        
        // Try Secretive (macOS users with hardware keys)
        var secretivePath = GetSecretiveAgentPath();
        if (secretivePath != null)
        {
            return secretivePath;
        }
        
        // Fall back to system default
        return GetSystemAgentPath();
    }

    /// <summary>
    /// Gets a human-readable name for the provider.
    /// </summary>
    public static string GetProviderName(SshAgentProvider provider)
    {
        return provider switch
        {
            SshAgentProvider.System => "System SSH Agent",
            SshAgentProvider.OnePassword => "1Password SSH Agent",
            SshAgentProvider.Secretive => "Secretive SSH Agent",
            SshAgentProvider.Auto => "Auto-detected SSH Agent",
            _ => "Unknown SSH Agent"
        };
    }

    /// <summary>
    /// Detects which provider is currently being used based on the socket path.
    /// </summary>
    public static SshAgentProvider DetectProvider(string socketPath)
    {
        ArgumentNullException.ThrowIfNull(socketPath);
        
        if (socketPath.Contains("1password", StringComparison.OrdinalIgnoreCase))
        {
            return SshAgentProvider.OnePassword;
        }
        
        if (socketPath.Contains("Secretive", StringComparison.OrdinalIgnoreCase))
        {
            return SshAgentProvider.Secretive;
        }
        
        return SshAgentProvider.System;
    }
}
