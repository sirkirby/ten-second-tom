using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for authentication (currently SSH-based).
/// Maps to the "TenSecondTom:Ssh" configuration section.
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;AuthOptions&gt; or IOptionsSnapshot&lt;AuthOptions&gt; in services.
///
/// Note: While named AuthOptions for future extensibility, all current properties
/// are SSH-specific. The configuration section name remains "TenSecondTom:Ssh" to
/// avoid breaking changes to existing configuration files.
///
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Ssh": {
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
/// - TenSecondTom__Ssh__KeyPath
/// - TenSecondTom__Ssh__KeySource
/// - TenSecondTom__Ssh__AgentSocketPath
/// - TenSecondTom__Ssh__KeyDisplayName
/// </remarks>
public sealed class AuthOptions
{
    /// <summary>
    /// The configuration section name for authentication settings.
    /// Currently maps to SSH section for backward compatibility.
    /// </summary>
    public const string SectionName = "TenSecondTom:Ssh";

    /// <summary>
    /// Gets or sets the file path to the SSH key.
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
    /// Gets or sets the source where the SSH key is retrieved from.
    /// </summary>
    /// <remarks>
    /// SSH-specific property.
    /// Valid values: <see cref="SshKeySource.SystemAgent"/>,
    /// <see cref="SshKeySource.OnePasswordAgent"/>,
    /// <see cref="SshKeySource.SecretiveAgent"/>,
    /// <see cref="SshKeySource.FileSystem"/>,
    /// <see cref="SshKeySource.ManualPath"/>.
    /// This is a required configuration value.
    /// </remarks>
    public SshKeySource KeySource { get; set; }

    /// <summary>
    /// Gets or sets the socket path for the SSH agent.
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
    /// Gets or sets the display name for the SSH key.
    /// </summary>
    /// <remarks>
    /// SSH-specific property.
    /// Optional human-readable name for the configured SSH key.
    /// Useful for identifying which key is in use when multiple keys are available.
    /// Example: "Work Laptop ED25519" or "Personal GitHub Key".
    /// </remarks>
    public string? KeyDisplayName { get; init; }
}
