namespace TenSecondTom.Shared.Models;

/// <summary>
/// Configuration settings for memory storage.
/// </summary>
public record StorageConfiguration
{
    /// <summary>
    /// Gets the directory path where memory entries are stored.
    /// Default: ".memory"
    /// </summary>
    public required string MemoryDirectory { get; init; }

    /// <summary>
    /// Gets the retention policy for memory entries.
    /// </summary>
    public required RetentionPolicy RetentionPolicy { get; init; }

    /// <summary>
    /// Gets a value indicating whether to automatically purge entries
    /// based on the retention policy.
    /// </summary>
    public required bool AutoPurge { get; init; }

    /// <summary>
    /// Gets the optional maximum file size in bytes for memory entries.
    /// If null, no size limit is enforced.
    /// </summary>
    public long? MaxFileSizeBytes { get; init; }

    /// <summary>
    /// Gets a value indicating whether to compress memory entries.
    /// </summary>
    public bool CompressionEnabled { get; init; }
}

/// <summary>
/// Defines retention policies for memory entries.
/// </summary>
public enum RetentionPolicy
{
    /// <summary>
    /// Retain entries indefinitely (never auto-purge).
    /// </summary>
    Indefinite,

    /// <summary>
    /// Retain entries for 30 days.
    /// </summary>
    Days30,

    /// <summary>
    /// Retain entries for 90 days.
    /// </summary>
    Days90,

    /// <summary>
    /// Retain entries for one year (365 days).
    /// </summary>
    OneYear,

    /// <summary>
    /// Retain entries for two years (730 days).
    /// </summary>
    TwoYears
}
