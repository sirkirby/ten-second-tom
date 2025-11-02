using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Shared.Extensions;

/// <summary>
/// Extension methods for StorageOptions to simplify storage path resolution.
/// </summary>
public static class StorageOptionsExtensions
{
    /// <summary>
    /// Resolves the effective storage directory path based on provider configuration.
    /// </summary>
    /// <param name="options">Storage options containing provider configuration.</param>
    /// <returns>The resolved storage directory path.</returns>
    /// <remarks>
    /// Resolution priority:
    /// 1. ProviderPath (if set) - for Obsidian or other external storage providers
    /// 2. RootDirectory (if set) - for default provider
    /// 3. Fallback to "./ten-second-tom"
    ///
    /// Additional processing:
    /// - Tilde (~) is expanded to user's home directory
    /// - MemorySubdirectory (if specified) is appended to the resolved path
    ///
    /// Examples:
    /// - Default provider: ~/ten-second-tom → /Users/chris/ten-second-tom
    /// - Obsidian with subdir: ~/Documents/MyVault + "tst" → /Users/chris/Documents/MyVault/tst
    /// </remarks>
    public static string GetEffectiveStorageDirectory(this StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Priority: ProviderPath (Obsidian) > RootDirectory (Default) > fallback
        string? baseDirectory = options.ProviderPath;

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            baseDirectory = options.RootDirectory
                ?? Path.Combine(".", DirectoryNames.ApplicationRoot);
        }

        // Expand home directory if needed
        baseDirectory = baseDirectory.Replace("~",
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        // If MemorySubdirectory is specified (e.g., for Obsidian isolation), append it
        if (!string.IsNullOrWhiteSpace(options.MemorySubdirectory))
        {
            baseDirectory = Path.Combine(baseDirectory, options.MemorySubdirectory);
        }

        return baseDirectory;
    }
}
