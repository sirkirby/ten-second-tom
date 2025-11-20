using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Storage configuration settings (JSON configuration model).
/// Defines storage provider selection and settings for the config file.
/// The RootDirectory (where config and templates live) is at ConfigurationSettings root level.
/// </summary>
/// <remarks>
/// This is the configuration file model. For the runtime storage infrastructure model,
/// see <see cref="StorageConfiguration"/>.
/// </remarks>
public sealed record StorageSettings
{
    /// <summary>
    /// Gets the storage provider ID (e.g., "default", "obsidian").
    /// Default: "default"
    /// </summary>
    public string ProviderId { get; init; } = StorageProviderIds.Default;

    /// <summary>
    /// Gets the provider-specific storage path (e.g., Obsidian vault path).
    /// For default provider: not used (uses RootDirectory for both config and storage)
    /// For Obsidian provider: the vault root path where memory entries are stored
    /// Default: null (use RootDirectory)
    /// </summary>
    public string? ProviderPath { get; init; }

    /// <summary>
    /// Gets the optional subdirectory name for memory entries within the provider path.
    /// When set, memory entries are stored under {ProviderPath}/{MemorySubdirectory}/
    /// Default: null (store directly in provider path root)
    /// Primarily used by Obsidian provider to isolate TST entries in a vault subdirectory.
    /// </summary>
    public string? MemorySubdirectory { get; init; }

    /// <summary>
    /// Gets whether to create directories if they don't exist
    /// </summary>
    public bool CreateIfMissing { get; init; } = true;

    /// <summary>
    /// Gets the retention policy for memory entries.
    /// Default: Indefinite (never auto-purge)
    /// </summary>
    public RetentionPolicy RetentionPolicy { get; init; } = RetentionPolicy.Indefinite;

    /// <summary>
    /// Gets whether to automatically purge entries based on retention policy.
    /// Default: false (manual purging only)
    /// </summary>
    public bool AutoPurge { get; init; }

    /// <summary>
    /// Gets the optional maximum file size in bytes for memory entries.
    /// If null, no size limit is enforced.
    /// </summary>
    public long? MaxFileSizeBytes { get; init; }

    /// <summary>
    /// Gets whether to compress memory entries.
    /// Default: false (no compression)
    /// </summary>
    public bool CompressionEnabled { get; init; }

    /// <summary>
    /// Determines whether the storage configuration is complete and valid.
    /// </summary>
    /// <returns>True if storage provider is configured with necessary paths; otherwise false.</returns>
    public bool IsConfigured()
    {
        // For Obsidian provider, ProviderPath must be set
        if (ProviderId.Equals(StorageProviderIds.Obsidian, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(ProviderPath);
        }

        // For default provider, always configured (uses RootDirectory)
        return !string.IsNullOrWhiteSpace(ProviderId);
    }
}
