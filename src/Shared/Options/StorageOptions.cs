using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for memory storage and data persistence.
/// Maps to the "TenSecondTom:Storage" configuration section, with RootDirectory at the root level.
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;StorageOptions&gt; or IOptionsSnapshot&lt;StorageOptions&gt; in services.
///
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "RootDirectory": "~/ten-second-tom",
///     "Storage": {
///       "ProviderId": "default",
///       "MemorySubdirectory": null,
///       "RetentionPolicy": "Days90",
///       "AutoPurge": false,
///       "MaxFileSizeBytes": 10485760,
///       "CompressionEnabled": false
///     }
///   }
/// }
/// </code>
///
/// Legacy configuration (backward compatible):
/// <code>
/// {
///   "TenSecondTom": {
///     "MemoryDirectory": ".memory",
///     "Storage": { ... }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__RootDirectory (or legacy: TenSecondTom__MemoryDirectory)
/// - TenSecondTom__Storage__ProviderId
/// - TenSecondTom__Storage__MemorySubdirectory
/// - TenSecondTom__Storage__RetentionPolicy
/// - TenSecondTom__Storage__AutoPurge
/// - TenSecondTom__Storage__MaxFileSizeBytes
/// - TenSecondTom__Storage__CompressionEnabled
/// </remarks>
public sealed class StorageOptions
{
    /// <summary>
    /// The configuration section name for storage settings (excluding RootDirectory).
    /// </summary>
    public const string SectionName = "TenSecondTom:Storage";

    /// <summary>
    /// The configuration key for the root directory (at root level).
    /// </summary>
    public const string RootDirectoryKey = "TenSecondTom:RootDirectory";

    /// <summary>
    /// The configuration key for the memory directory (legacy, at root level).
    /// </summary>
    public const string MemoryDirectoryKey = "TenSecondTom:MemoryDirectory";

    /// <summary>
    /// Gets or sets the root directory for all Ten Second Tom data.
    /// </summary>
    /// <remarks>
    /// This is the top-level directory containing all subdirectories (memory, templates, etc.).
    /// Supports both absolute and relative paths.
    /// Default: Varies by provider. For default provider: "./ten-second-tom"
    /// When both RootDirectory and MemoryDirectory (legacy) are set, RootDirectory takes precedence.
    /// </remarks>
    public string? RootDirectory { get; set; }

    /// <summary>
    /// Gets or sets the storage provider ID to use (e.g., "default", "obsidian").
    /// </summary>
    /// <remarks>
    /// Provider IDs are defined by IStorageProvider implementations and discovered via assembly scanning.
    /// Use constants from <see cref="StorageProviderIds"/> class.
    /// Default: "default" (file system provider).
    /// </remarks>
    public string ProviderId { get; set; } = StorageProviderIds.Default;

    /// <summary>
    /// Gets or sets the provider-specific storage path.
    /// </summary>
    /// <remarks>
    /// This path specifies where the storage provider should store memory entries.
    /// - For default provider: null (uses RootDirectory for both config and storage)
    /// - For Obsidian provider: The vault root path where memory entries are stored
    /// When null, the provider uses RootDirectory as the storage location.
    /// Default: null (use RootDirectory).
    /// Example: "/Users/chris/Documents/MyVault" for Obsidian
    /// </remarks>
    public string? ProviderPath { get; set; }

    /// <summary>
    /// Gets or sets the optional subdirectory name for memory entries within the provider path.
    /// </summary>
    /// <remarks>
    /// When set, memory entries are stored under {ProviderPath or RootDirectory}/{MemorySubdirectory}/
    /// When null/empty, memory entries are stored directly under {ProviderPath or RootDirectory}/
    /// Example: "memory" results in ~/ten-second-tom/memory/today/ (default provider)
    ///          or /Users/chris/Documents/MyVault/memory/today/ (Obsidian provider)
    /// Default: null (store directly in storage root).
    /// Primarily used by Obsidian provider to isolate TST entries in a vault subdirectory.
    /// </remarks>
    public string? MemorySubdirectory { get; set; }

    /// <summary>
    /// Gets or sets the legacy memory directory path (backward compatibility only).
    /// </summary>
    /// <remarks>
    /// This property exists for backward compatibility with v0.5.x and earlier.
    /// New configurations should use <see cref="RootDirectory"/> instead.
    /// When both are set, RootDirectory takes precedence.
    /// </remarks>
    [Obsolete("Use RootDirectory instead. This property is for backward compatibility only.", false)]
    public string? MemoryDirectory { get; set; }

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
