using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Shared.Extensions;

/// <summary>
/// Extension members for StorageOptions to simplify storage path resolution.
/// </summary>
/// <remarks>
/// Uses C# 14 extension member syntax for cleaner extension properties.
/// </remarks>
public static class StorageOptionsExtensions
{
    extension(StorageOptions options)
    {
        /// <summary>
        /// Gets the effective storage directory path based on configuration.
        /// </summary>
        /// <remarks>
        /// Resolution priority:
        /// 1. ProviderPath - for Obsidian or other external storage providers
        /// 2. RootDirectory - for default storage location
        /// 3. Fallback to "./ten-second-tom"
        ///
        /// Additional processing:
        /// - Tilde (~) is expanded to user's home directory
        /// - MemorySubdirectory (if specified) is appended to the resolved path for isolation
        ///
        /// Examples:
        /// - Default provider: ~/ten-second-tom → /Users/chris/ten-second-tom
        /// - Obsidian with subdir: ~/Documents/MyVault + "tst" → /Users/chris/Documents/MyVault/tst
        /// </remarks>
        public string EffectiveStorageDirectory
        {
            get
            {
                ArgumentNullException.ThrowIfNull(options);

                // Priority: ProviderPath > RootDirectory > fallback
                string? baseDirectory = options.ProviderPath;

                if (string.IsNullOrWhiteSpace(baseDirectory))
                {
                    baseDirectory = options.RootDirectory
                        ?? Path.Combine(".", DirectoryNames.ApplicationRoot);
                }

                // Expand tilde to home directory
                baseDirectory = baseDirectory.Replace("~",
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                // Append isolation subdirectory if specified
                if (!string.IsNullOrWhiteSpace(options.MemorySubdirectory))
                {
                    baseDirectory = Path.Combine(baseDirectory, options.MemorySubdirectory);
                }

                return baseDirectory;
            }
        }
    }
}
