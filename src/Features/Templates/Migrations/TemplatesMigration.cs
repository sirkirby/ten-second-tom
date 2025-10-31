using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static TenSecondTom.Features.Templates.InstallDefaultTemplates;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Shared.Constants;
using MediatR;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates.Migrations;

/// <summary>
/// Templates feature migration that installs default templates for existing users.
/// Runs automatically during application bootstrap if templates are missing.
/// </summary>
public sealed class TemplatesMigration : IFeatureMigration
{
    /// <inheritdoc/>
    public string FeatureName => "Templates";

    /// <inheritdoc/>
    public int Priority => 100; // Standard feature priority

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<TemplatesMigration>>();
        var storageOptions = services.GetRequiredService<IOptions<StorageOptions>>();
        var fileSystem = services.GetRequiredService<IFileSystem>();
        var sender = services.GetRequiredService<ISender>();

        // Get root directory from storage options
        string? rootDirectory = storageOptions.Value.RootDirectory;
        logger.LogDebug("Root directory from configuration: {RootDirectory}", rootDirectory);

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            logger.LogDebug("No root directory configured, skipping template migration");
            return false;
        }

        try
        {
            var migrationResult = await ValidateAndMigrateTemplatesAsync(
                rootDirectory,
                fileSystem,
                sender,
                logger,
                cancellationToken).ConfigureAwait(false);

            if (migrationResult.IsSuccess && migrationResult.Value)
            {
                logger.LogInformation("Template migration completed successfully");
                return true;
            }

            return false; // Migration was skipped (templates already exist)
        }
        catch (Exception ex)
        {
            // Log but don't fail - templates migration is non-critical
            logger.LogWarning(ex, "Template migration failed, but continuing execution");
            return false;
        }
    }

    /// <summary>
    /// Validates that required templates exist and installs them if missing.
    /// This is a silent migration that runs automatically for existing users.
    /// </summary>
    /// <param name="rootDirectory">The configured root directory (e.g., ~/ten-second-tom or ./.memory)</param>
    /// <param name="fileSystem">File system abstraction for testing</param>
    /// <param name="sender">MediatR sender for dispatching commands</param>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating whether migration was needed and successful (true if migrated, false if not needed)</returns>
    private static async Task<Result<bool>> ValidateAndMigrateTemplatesAsync(
        string rootDirectory,
        IFileSystem fileSystem,
        ISender sender,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return Result<bool>.Failure("Root directory cannot be null or empty");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Templates are under root directory: {root}/templates
        string templatesDirectory = fileSystem.Path.Combine(rootDirectory, DirectoryNames.Templates);

        // Check if templates directory exists
        bool directoryExists = fileSystem.Directory.Exists(templatesDirectory);

        // Check if required templates exist
        bool dailySummaryExists = directoryExists &&
            fileSystem.File.Exists(fileSystem.Path.Combine(templatesDirectory, "daily-summary.md"));
        bool dailyStandupExists = directoryExists &&
            fileSystem.File.Exists(fileSystem.Path.Combine(templatesDirectory, "daily-standup.md"));
        bool weeklyReviewExists = directoryExists &&
            fileSystem.File.Exists(fileSystem.Path.Combine(templatesDirectory, "weekly-review.md"));
        bool businessMeetingExists = directoryExists &&
            fileSystem.File.Exists(fileSystem.Path.Combine(templatesDirectory, "business-meeting.md"));

        // If all required templates exist, no migration needed
        if (dailySummaryExists && dailyStandupExists && weeklyReviewExists && businessMeetingExists)
        {
            logger.LogDebug("Templates already configured, no migration needed");
            return Result<bool>.Success(false);
        }

        // Templates are missing, need to install them
        if (!directoryExists)
        {
            logger.LogInformation(
                "Templates directory missing at {TemplatesDirectory}, installing default templates",
                templatesDirectory);
        }
        else
        {
            logger.LogInformation(
                "Default templates missing (DailySummary={DailySummary}, DailyStandup={DailyStandup}, WeeklyReview={WeeklyReview}, BusinessMeeting={BusinessMeeting}), installing",
                dailySummaryExists,
                dailyStandupExists,
                weeklyReviewExists,
                businessMeetingExists);
        }

        // Install templates (OverwriteExisting=false to preserve user customizations)
        var installCommand = new Command
        {
            TargetDirectory = templatesDirectory,
            OverwriteExisting = false
        };

        var installResult = await sender.Send(installCommand, cancellationToken);

        if (!installResult.IsSuccess)
        {
            logger.LogWarning(
                "Failed to install default templates: {Error}",
                installResult.Error);
            return Result<bool>.Success(false); // Return success but false value - migration failed but app can continue
        }

        logger.LogInformation(
            "Successfully installed {Count} templates ({TemplateIds})",
            installResult.Value.TemplatesInstalled,
            string.Join(", ", installResult.Value.InstalledTemplateIds));

        return Result<bool>.Success(true); // Migration completed successfully
    }
}
