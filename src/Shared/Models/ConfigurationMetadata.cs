namespace TenSecondTom.Shared.Models;

/// <summary>
/// Configuration metadata tracking creation and modification timestamps.
/// Maps to the "TenSecondTom:Configuration" configuration section.
/// </summary>
public sealed record ConfigurationMetadata
{
    /// <summary>
    /// Gets when this configuration was initially created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Gets when this configuration was last modified, if ever.
    /// </summary>
    public DateTime? LastModifiedAt { get; init; }

    /// <summary>
    /// Gets the configuration version identifier.
    /// </summary>
    public string Version { get; init; } = "1.0";
}
