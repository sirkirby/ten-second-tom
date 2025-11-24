using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Infrastructure.Notifications.Migrations;

/// <summary>
/// Auto-generates a random notification secret if one is not configured.
/// This enables interactive notifications to work out-of-the-box without manual configuration.
/// </summary>
public sealed class SecurityOptionsMigration : IFeatureMigration
{
    /// <inheritdoc/>
    public string FeatureName => "Security (Notifications)";

    /// <inheritdoc/>
    public int Priority => 10; // Low priority infrastructure migration

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<SecurityOptionsMigration>>();
        var sectionStore = services.GetRequiredService<IConfigurationSectionStore>();

        // Read current configuration from file (not IOptions which is cached at startup)
        var currentConfigResult = await sectionStore.ReadSectionAsync<SecurityOptions>(
            SecurityOptions.SectionPath,
            cancellationToken).ConfigureAwait(false);

        // Check if NotificationSecret is already configured in the config file
        if (currentConfigResult.IsSuccess &&
            currentConfigResult.Value != null &&
            !string.IsNullOrWhiteSpace(currentConfigResult.Value.NotificationSecret))
        {
            logger.LogDebug("NotificationSecret already configured, skipping auto-generation");
            return false;
        }

        logger.LogInformation("NotificationSecret not configured, generating random secret");

        try
        {
            // Generate a cryptographically secure random secret (32 bytes = 64 hex chars)
            var secretBytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(secretBytes);
            }

            var generatedSecret = Convert.ToHexString(secretBytes);

            // Create updated security options with generated secret
            // Preserve existing MaxTokenAgeSeconds if configured, otherwise use default
            var updatedSecurityOptions = new SecurityOptions
            {
                NotificationSecret = generatedSecret,
                MaxTokenAgeSeconds = currentConfigResult.IsSuccess && currentConfigResult.Value != null
                    ? currentConfigResult.Value.MaxTokenAgeSeconds
                    : 300 // Default value
            };

            // Save to configuration
            var saveResult = await sectionStore.WriteSectionAsync(
                SecurityOptions.SectionPath,
                updatedSecurityOptions,
                cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                logger.LogWarning("Failed to save auto-generated NotificationSecret: {Error}", saveResult.Error);
                return false;
            }

            logger.LogInformation("NotificationSecret auto-generated and saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            // Log but don't fail - this is a non-critical enhancement
            logger.LogWarning(ex, "Failed to auto-generate NotificationSecret, continuing without it");
            return false;
        }
    }
}
