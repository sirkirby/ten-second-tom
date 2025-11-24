using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Migrations;

/// <summary>
/// Ensures the TenSecondTom:Notifications section exists in config.json so users can
/// toggle notification behavior without touching environment variables.
/// </summary>
public sealed class NotificationOptionsMigration : IFeatureMigration
{
    /// <inheritdoc/>
    public string FeatureName => "Notifications (Defaults)";

    /// <inheritdoc/>
    public int Priority => 11; // Runs after security secret generation

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<NotificationOptionsMigration>>();
        var sectionStore = services.GetRequiredService<IConfigurationSectionStore>();

        try
        {
            var sectionExistsResult = await NotificationsSectionExistsAsync(sectionStore, cancellationToken)
                .ConfigureAwait(false);

            if (sectionExistsResult.IsSuccess && sectionExistsResult.Value)
            {
                logger.LogDebug("Notifications section already exists. Skipping migration.");
                return false;
            }

            if (sectionExistsResult.IsFailure)
            {
                logger.LogWarning(
                    "Unable to inspect notifications configuration: {Error}. Assuming missing and attempting to create defaults.",
                    sectionExistsResult.Error);
            }

            var defaultOptions = new NotificationOptions();

            var writeResult = await sectionStore.WriteSectionAsync(
                    NotificationOptions.SectionPath,
                    defaultOptions,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!writeResult.IsSuccess)
            {
                logger.LogWarning("Failed to persist default notification options: {Error}", writeResult.Error);
                return false;
            }

            logger.LogInformation("Default notification options saved to {Path}", writeResult.Value);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Notification options migration failed.");
            return false;
        }
    }

    private static async Task<Result<bool>> NotificationsSectionExistsAsync(
        IConfigurationSectionStore sectionStore,
        CancellationToken cancellationToken)
    {
        var configResult = await sectionStore.ReadFullConfigAsync(cancellationToken).ConfigureAwait(false);
        if (!configResult.IsSuccess)
        {
            return Result<bool>.Failure(configResult.Error ?? "Unknown configuration error");
        }

        using var document = configResult.Value;
        if (!document.RootElement.TryGetProperty("TenSecondTom", out var tenSecondTom))
        {
            return Result<bool>.Success(false);
        }

        var exists = tenSecondTom.TryGetProperty("Notifications", out _);
        return Result<bool>.Success(exists);
    }
}
