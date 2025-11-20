using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Display model for system configuration.
/// Represents all configuration settings needed for CLI display and validation.
/// This model is infrastructure-independent and shared across features.
/// </summary>
public sealed record ConfigDisplay
{
    /// <summary>
    /// Gets the root directory for application data storage.
    /// </summary>
    public string RootDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the SSH configuration.
    /// </summary>
    public SshConfiguration Ssh { get; init; } = new();

    /// <summary>
    /// Gets the LLM configuration.
    /// </summary>
    public LlmConfiguration Llm { get; init; } = new();

    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    public StorageSettings Storage { get; init; } = new();

    /// <summary>
    /// Gets the optional configuration.
    /// </summary>
    public OptionalConfiguration Optional { get; init; } = new();

    /// <summary>
    /// Gets the audio configuration display.
    /// </summary>
    public AudioConfigurationDisplay Audio { get; init; } = new();

    /// <summary>
    /// Gets the timestamp when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets the timestamp when the configuration was last modified.
    /// </summary>
    public DateTime? LastModifiedAt { get; init; }

    /// <summary>
    /// Gets the configuration version.
    /// </summary>
    public string ConfigurationVersion { get; init; } = "1.0";
}
