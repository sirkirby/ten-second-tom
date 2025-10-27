namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Constants for SSH agent configuration and authentication.
/// </summary>
public static class SshConstants
{
    /// <summary>
    /// Default SSH agent provider mode.
    /// Auto-detect will try to find the best available agent (1Password, ssh-agent, etc.).
    /// </summary>
    public const string DefaultAgentProvider = "Auto";
}
