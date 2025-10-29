namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed storage provider identifier constants.
/// These constants ensure consistency when configuring and selecting storage providers.
/// </summary>
public static class StorageProviderIds
{
    /// <summary>
    /// Default file system storage provider.
    /// Stores entries in a flat directory structure.
    /// </summary>
    public const string Default = "default";

    /// <summary>
    /// Obsidian vault storage provider.
    /// Integrates with Obsidian daily notes format.
    /// </summary>
    public const string Obsidian = "obsidian";
}
