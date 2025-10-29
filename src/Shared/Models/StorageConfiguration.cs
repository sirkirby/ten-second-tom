namespace TenSecondTom.Shared.Models;

/// <summary>
/// Configuration settings for memory storage.
/// </summary>
public record StorageConfiguration
{
    /// <summary>
    /// Gets the root directory path for all Ten Second Tom data.
    /// This is the top-level directory containing all subdirectories.
    /// Default: "./ten-second-tom" or "~/ten-second-tom"
    /// </summary>
    public required string RootDirectory { get; init; }

    /// <summary>
    /// Gets the storage provider ID (e.g., "default", "obsidian").
    /// Default: "default"
    /// </summary>
    public required string ProviderId { get; init; }

    /// <summary>
    /// Gets the optional subdirectory name for memory entries within the root directory.
    /// When set, memory entries are stored under {RootDirectory}/{MemorySubdirectory}/
    /// Default: null (store directly in root)
    /// </summary>
    public string? MemorySubdirectory { get; init; }

    /// <summary>
    /// Gets the legacy memory directory path (backward compatibility only).
    /// Use <see cref="RootDirectory"/> for new configurations.
    /// </summary>
    [Obsolete("Use RootDirectory instead. This property is for backward compatibility only.", false)]
    public string? MemoryDirectory { get; init; }

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
