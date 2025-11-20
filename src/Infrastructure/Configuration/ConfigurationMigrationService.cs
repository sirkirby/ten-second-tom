using Microsoft.Extensions.Logging;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Service for detecting and cleaning up legacy user secrets configuration.
/// Helps users migrate from the old UserSecretsStorageService to the new appsettings.json-based configuration.
/// </summary>
public sealed class ConfigurationMigrationService
{
    private readonly ILogger<ConfigurationMigrationService> _logger;
    private readonly string _userSecretsDirectory;
    private readonly string _secretsFilePath;

    /// <summary>
    /// Creates a new instance of ConfigurationMigrationService.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="userSecretsDirectory">Optional override for user secrets directory (for testing)</param>
    public ConfigurationMigrationService(
        ILogger<ConfigurationMigrationService> logger,
        string? userSecretsDirectory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(userSecretsDirectory))
        {
            // Use the default system path for user secrets
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (OperatingSystem.IsWindows())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _userSecretsDirectory = Path.Combine(appData, "Microsoft", "UserSecrets", "ten-second-tom-secrets");
            }
            else
            {
                // macOS and Linux
                _userSecretsDirectory = Path.Combine(userProfile, ".microsoft", "usersecrets", "ten-second-tom-secrets");
            }
        }
        else
        {
            _userSecretsDirectory = userSecretsDirectory;
        }

        _secretsFilePath = Path.Combine(_userSecretsDirectory, "secrets.json");
    }

    /// <summary>
    /// Checks if legacy user secrets configuration exists.
    /// </summary>
    /// <returns>True if legacy configuration file exists, false otherwise</returns>
    public bool HasLegacyConfiguration()
    {
        return File.Exists(_secretsFilePath);
    }

    /// <summary>
    /// Gets the full path to the legacy configuration file.
    /// </summary>
    /// <returns>Path to secrets.json file</returns>
    public string GetLegacyConfigurationPath()
    {
        return _secretsFilePath;
    }

    /// <summary>
    /// Removes the legacy user secrets configuration file and directory.
    /// Safe to call even if the file doesn't exist.
    /// </summary>
    public void CleanupLegacyConfiguration()
    {
        try
        {
            if (!File.Exists(_secretsFilePath))
            {
                _logger.LogDebug("No legacy configuration file found at {Path}", _secretsFilePath);
                return;
            }

            _logger.LogInformation("Removing legacy user secrets configuration from {Path}", _secretsFilePath);

            // Delete the secrets file
            File.Delete(_secretsFilePath);

            // Try to remove the directory if it's now empty
            if (Directory.Exists(_userSecretsDirectory))
            {
                try
                {
                    // This will only succeed if directory is empty
                    Directory.Delete(_userSecretsDirectory, recursive: true);
                    _logger.LogInformation("Removed legacy user secrets directory {Directory}", _userSecretsDirectory);
                }
                catch (IOException)
                {
                    // Directory not empty or in use - that's okay
                    _logger.LogDebug("Could not remove directory {Directory} - may contain other files", _userSecretsDirectory);
                }
            }

            _logger.LogInformation("Legacy configuration cleanup completed successfully");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied while cleaning up legacy configuration at {Path}. You may need to manually delete this file.", _secretsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup legacy configuration at {Path}. This is not critical - you can manually delete this file if needed.", _secretsFilePath);
        }
    }
}
