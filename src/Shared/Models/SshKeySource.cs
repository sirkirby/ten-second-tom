namespace TenSecondTom.Shared.Models;

/// <summary>
/// Specifies the source where an SSH key was detected
/// </summary>
public enum SshKeySource
{
    /// <summary>
    /// System SSH agent (ssh-agent)
    /// </summary>
    SystemAgent,

    /// <summary>
    /// 1Password SSH agent
    /// </summary>
    OnePasswordAgent,

    /// <summary>
    /// Secretive SSH agent (macOS)
    /// </summary>
    SecretiveAgent,

    /// <summary>
    /// File system (~/.ssh directory)
    /// </summary>
    FileSystem,

    /// <summary>
    /// User-provided manual path
    /// </summary>
    ManualPath
}
