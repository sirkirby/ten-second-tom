namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Enumeration of supported SSH agent providers.
/// </summary>
public enum SshAgentProvider
{
    /// <summary>
    /// System default SSH agent (ssh-agent, Pageant, etc.).
    /// Uses SSH_AUTH_SOCK environment variable.
    /// </summary>
    System,
    
    /// <summary>
    /// 1Password SSH Agent.
    /// </summary>
    OnePassword,
    
    /// <summary>
    /// Secretive SSH Agent (macOS).
    /// </summary>
    Secretive,
    
    /// <summary>
    /// Automatically detect the best available SSH agent.
    /// Checks in order: 1Password, Secretive, System default.
    /// </summary>
    Auto
}
