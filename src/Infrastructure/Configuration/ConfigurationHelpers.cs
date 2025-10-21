using Microsoft.Extensions.Configuration;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Helper methods for consistent configuration reading across the application.
/// Enforces configuration precedence: .env → user secrets → appsettings.json
/// </summary>
public static class ConfigurationHelpers
{
    /// <summary>
    /// Gets the memory directory from configuration with proper precedence.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="expandHomeDirectory">Whether to expand ~ to user's home directory.</param>
    /// <returns>The resolved memory directory path.</returns>
    /// <remarks>
    /// Configuration precedence follows standard .NET configuration:
    /// 1. Environment variables (TenSecondTom__MemoryDirectory)
    /// 2. User Secrets (TenSecondTom:MemoryDirectory)
    /// 3. appsettings.json (TenSecondTom:MemoryDirectory)
    /// 4. ./ten-second-tom (hardcoded fallback)
    /// 
    /// The key is always TenSecondTom:MemoryDirectory
    /// Environment variable name: TenSecondTom__MemoryDirectory
    /// </remarks>
    public static string GetMemoryDirectory(
        this IConfiguration configuration,
        bool expandHomeDirectory = true)
    {
        // Get TenSecondTom:MemoryDirectory with .NET's built-in precedence
        var memoryDir = configuration[ConfigurationKeys.MemoryDirectory] ??
                        Path.Combine(".", DirectoryNames.ApplicationRoot);

        if (expandHomeDirectory && memoryDir.Contains('~'))
        {
            memoryDir = memoryDir.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        return memoryDir;
    }

    /// <summary>
    /// Gets a configuration value with proper fallback pattern.
    /// </summary>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="primaryKey">The primary configuration key (from user config).</param>
    /// <param name="fallbackKey">The fallback configuration key (from defaults).</param>
    /// <param name="defaultValue">The default value if neither key exists.</param>
    /// <typeparam name="T">The type of the configuration value.</typeparam>
    /// <returns>The resolved configuration value.</returns>
    /// <remarks>
    /// This enforces the configuration precedence pattern:
    /// 1. Check primary key (from .env, user secrets, environment)
    /// 2. Check fallback key (from appsettings.json)
    /// 3. Use default value
    /// </remarks>
    public static T? GetValueWithFallback<T>(
        this IConfiguration configuration,
        string primaryKey,
        string fallbackKey,
        T? defaultValue = default)
    {
        // Try primary key first
        var primaryValue = configuration.GetValue<T?>(primaryKey);
        if (primaryValue != null)
        {
            return primaryValue;
        }

        // Try fallback key
        var fallbackValue = configuration.GetValue<T?>(fallbackKey);
        if (fallbackValue != null)
        {
            return fallbackValue;
        }

        // Return default
        return defaultValue;
    }
}

