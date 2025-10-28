using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for memory storage and data persistence.
/// Maps to the "TenSecondTom:Storage" configuration section, with MemoryDirectory at the root level.
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;StorageOptions&gt; or IOptionsSnapshot&lt;StorageOptions&gt; in services.
///
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "MemoryDirectory": ".memory",
///     "Storage": {
///       "RetentionPolicy": "Days90",
///       "AutoPurge": false,
///       "MaxFileSizeBytes": 10485760,
///       "CompressionEnabled": false
///     }
///   }
/// }
/// </code>
///
/// Note: MemoryDirectory is at root level ("TenSecondTom:MemoryDirectory"), not in Storage section.
///
/// Environment variables:
/// - TenSecondTom__MemoryDirectory
/// - TenSecondTom__Storage__RetentionPolicy
/// - TenSecondTom__Storage__AutoPurge
/// - TenSecondTom__Storage__MaxFileSizeBytes
/// - TenSecondTom__Storage__CompressionEnabled
/// </remarks>
public sealed class StorageOptions
{
    /// <summary>
    /// The configuration section name for storage settings (excluding MemoryDirectory).
    /// </summary>
    public const string SectionName = "TenSecondTom:Storage";

    /// <summary>
    /// The configuration key for the memory directory (at root level).
    /// </summary>
    public const string MemoryDirectoryKey = "TenSecondTom:MemoryDirectory";

    /// <summary>
    /// Gets or sets the directory path where memory entries are stored.
    /// </summary>
    /// <remarks>
    /// This is the root directory for all memory storage operations.
    /// Supports both absolute and relative paths.
    /// Default: ".memory" (relative to working directory).
    /// This is a required configuration value.
    /// </remarks>
    public string MemoryDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the retention policy for memory entries.
    /// </summary>
    /// <remarks>
    /// Determines how long memory entries are kept before being eligible for purging.
    /// Default: <see cref="RetentionPolicy.Indefinite"/> (never auto-purge).
    /// </remarks>
    public RetentionPolicy RetentionPolicy { get; set; } = RetentionPolicy.Indefinite;

    /// <summary>
    /// Gets or sets a value indicating whether to automatically purge entries
    /// based on the retention policy.
    /// </summary>
    /// <remarks>
    /// When true, entries older than the retention policy will be automatically deleted.
    /// When false, entries are kept indefinitely regardless of retention policy.
    /// Default: false (manual purging only).
    /// </remarks>
    public bool AutoPurge { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum file size in bytes for memory entries.
    /// </summary>
    /// <remarks>
    /// If set, memory entries larger than this size will be rejected.
    /// If null, no size limit is enforced.
    /// Default: null (no limit).
    /// Example: 10485760 (10 MB).
    /// </remarks>
    public long? MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to compress memory entries.
    /// </summary>
    /// <remarks>
    /// When true, memory entries are compressed to save disk space.
    /// Compression uses standard .NET compression algorithms.
    /// Default: false (no compression).
    /// </remarks>
    public bool CompressionEnabled { get; set; }
}
