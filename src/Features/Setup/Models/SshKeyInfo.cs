namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Represents a detected or user-specified SSH key
/// </summary>
public sealed record SshKeyInfo
{
    /// <summary>
    /// Gets the display name for the key (e.g., "[System Agent] id_ed25519")
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the source where this key was detected from
    /// </summary>
    public required SshKeySource Source { get; init; }

    /// <summary>
    /// Gets the public key content
    /// </summary>
    public required string PublicKey { get; init; }

    /// <summary>
    /// Gets the file path for file-based keys
    /// Required when Source is FileSystem or ManualPath
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Gets the agent name for agent-based keys
    /// Required when Source is an SSH agent
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Gets whether this is an ED25519 key (project requirement)
    /// </summary>
    public required bool IsEd25519 { get; init; }

    /// <summary>
    /// Gets the timestamp when this key was detected
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the validation result for this key
    /// </summary>
    public ValidationResult ValidationResult { get; init; } = ValidationResult.NotValidated;

    /// <summary>
    /// Validates that required fields are populated based on the source
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(PublicKey))
            return false;

        if (Source == SshKeySource.FileSystem || Source == SshKeySource.ManualPath)
        {
            if (string.IsNullOrEmpty(FilePath))
                return false;
        }

        if (Source == SshKeySource.SystemAgent || 
            Source == SshKeySource.OnePasswordAgent || 
            Source == SshKeySource.SecretiveAgent)
        {
            if (string.IsNullOrEmpty(AgentName))
                return false;
        }

        // ED25519 keys are required by the project
        if (!IsEd25519)
            return false;

        return true;
    }

    /// <summary>
    /// Marks this key as validated
    /// </summary>
    public SshKeyInfo MarkAsValidated(ValidationResult result) => this with
    {
        ValidationResult = result
    };
}

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

/// <summary>
/// Specifies the validation status of an SSH key
/// </summary>
public enum ValidationResult
{
    /// <summary>
    /// Key has not been validated yet
    /// </summary>
    NotValidated,

    /// <summary>
    /// Key passed validation
    /// </summary>
    Valid,

    /// <summary>
    /// Key has invalid format
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// Key is not an ED25519 key
    /// </summary>
    InvalidKeyType,

    /// <summary>
    /// Key file was not found
    /// </summary>
    FileNotFound
}
