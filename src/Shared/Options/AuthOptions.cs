using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for authentication (currently SSH-based).
/// Maps to the "TenSecondTom:Auth" configuration section (VSA-compliant flat structure).
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;AuthOptions&gt; or IOptionsSnapshot&lt;AuthOptions&gt; in services.
///
/// Note: While named AuthOptions for future extensibility, all current properties
/// are SSH-specific.
///
/// Configuration example (config.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Auth": {
///       "KeyPath": "~/.ssh/id_ed25519",
///       "KeySource": "FileSystem",
///       "AgentSocketPath": "/run/user/1000/keyring/ssh",
///       "KeyDisplayName": "My ED25519 Key"
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Auth__KeyPath
/// - TenSecondTom__Auth__KeySource
/// - TenSecondTom__Auth__AgentSocketPath
/// - TenSecondTom__Auth__KeyDisplayName
/// </remarks>
public sealed class AuthOptions
{
    /// <summary>
    /// The configuration section name for authentication settings.
    /// </summary>
    public const string SectionName = "TenSecondTom:Auth";

    /// <summary>
    /// Configuration section path for Auth feature settings (alias for SectionName).
    /// </summary>
    public const string SectionPath = "TenSecondTom:Auth";

    /// <summary>
    /// Gets the path to the SSH key file (null for agent-based keys).
    /// </summary>
    /// <remarks>
    /// SSH-specific property.
    /// Required when <see cref="KeySource"/> is <see cref="SshKeySource.FileSystem"/>
    /// or <see cref="SshKeySource.ManualPath"/>.
    /// Supports tilde expansion (e.g., "~/.ssh/id_ed25519").
    /// Must point to an ED25519 key as per project requirements.
    /// </remarks>
    public string? KeyPath { get; set; }

    /// <summary>
    /// Gets the source of the SSH key.
    /// </summary>
    /// <remarks>
    /// SSH-specific property.
    /// Valid values: <see cref="SshKeySource.SystemAgent"/>,
    /// <see cref="SshKeySource.OnePasswordAgent"/>,
    /// <see cref="SshKeySource.SecretiveAgent"/>,
    /// <see cref="SshKeySource.FileSystem"/>,
    /// <see cref="SshKeySource.ManualPath"/>.
    /// </remarks>
    public SshKeySource? KeySource { get; set; }

    /// <summary>
    /// Gets the path to the SSH agent socket.
    /// </summary>
    /// <remarks>
    /// SSH-specific property.
    /// Required when <see cref="KeySource"/> is an agent-based source
    /// (<see cref="SshKeySource.SystemAgent"/>, <see cref="SshKeySource.OnePasswordAgent"/>,
    /// or <see cref="SshKeySource.SecretiveAgent"/>).
    /// Typically provided via SSH_AUTH_SOCK environment variable on Unix systems.
    /// Example: "/run/user/1000/keyring/ssh" or "/tmp/ssh-agent.sock".
    /// </remarks>
    public string? AgentSocketPath { get; set; }

    /// <summary>
    /// Gets a human-readable identifier for the SSH key (e.g., "id_ed25519 (1Password)").
    /// </summary>
    /// <remarks>
    /// SSH-specific property.
    /// Optional human-readable name for the configured SSH key.
    /// Useful for identifying which key is in use when multiple keys are available.
    /// Example: "Work Laptop ED25519" or "Personal GitHub Key".
    /// </remarks>
    public string? KeyDisplayName { get; set; }

    /// <summary>
    /// Determines whether the SSH configuration is complete and valid.
    /// </summary>
    /// <returns>True if KeySource is configured; otherwise false.</returns>
    public bool IsConfigured()
    {
        return KeySource.HasValue && KeySource.Value != SshKeySource.FileSystem
            || !string.IsNullOrWhiteSpace(KeyPath);
    }
}
