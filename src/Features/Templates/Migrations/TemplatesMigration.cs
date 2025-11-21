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
        bool journalExists = directoryExists &&
            fileSystem.File.Exists(fileSystem.Path.Combine(templatesDirectory, "journal.md"));
        bool organizeExists = directoryExists &&
            fileSystem.File.Exists(fileSystem.Path.Combine(templatesDirectory, "organize.md"));

        // If all required templates exist, check if they have IDs
        if (dailySummaryExists && dailyStandupExists && weeklyReviewExists && businessMeetingExists && journalExists && organizeExists)
        {
            logger.LogDebug("Templates already configured, checking for missing IDs");
            await EnsureTemplateIdsAsync(templatesDirectory, fileSystem, logger, cancellationToken);
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
                "Default templates missing (DailySummary={DailySummary}, DailyStandup={DailyStandup}, WeeklyReview={WeeklyReview}, BusinessMeeting={BusinessMeeting}, Journal={Journal}, Organize={Organize}), installing",
                dailySummaryExists,
                dailyStandupExists,
                weeklyReviewExists,
                businessMeetingExists,
                journalExists,
                organizeExists);
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

        // Ensure IDs are present even after installation (just in case embedded templates were missing them)
        await EnsureTemplateIdsAsync(templatesDirectory, fileSystem, logger, cancellationToken);

        return Result<bool>.Success(true); // Migration completed successfully
    }

    /// <summary>
    /// Scans existing default templates and injects the 'id' field into front matter if missing.
    /// </summary>
    private static async Task EnsureTemplateIdsAsync(
        string templatesDirectory,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var defaultTemplates = new Dictionary<string, string>
        {
            { "daily-summary.md", TemplateConstants.DailySummaryTemplateId },
            { "daily-standup.md", "daily-standup" }, // No constant for this one yet?
            { "weekly-review.md", TemplateConstants.WeeklyReviewTemplateId },
            { "business-meeting.md", TemplateConstants.BusinessMeetingTemplateId },
            { "journal.md", TemplateConstants.JournalTemplateId },
            { "organize.md", TemplateConstants.OrganizeTemplateId }
        };

        foreach (var (filename, id) in defaultTemplates)
        {
            var filePath = fileSystem.Path.Combine(templatesDirectory, filename);
            if (!fileSystem.File.Exists(filePath)) continue;

            try
            {
                var content = await fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);

                // Check if ID is already present in front matter
                if (content.Contains($"id: {id}") || content.Contains($"id: \"{id}\""))
                {
                    continue;
                }

                logger.LogInformation("Migrating template {Filename}: Adding id: {Id}", filename, id);

                // Inject ID into front matter
                // Assumes front matter starts with ---
                if (content.StartsWith("---"))
                {
                    var lines = content.Split('\n').ToList();
                    // Insert after the first ---
                    lines.Insert(1, $"id: {id}");
                    var newContent = string.Join('\n', lines);
                    await fileSystem.File.WriteAllTextAsync(filePath, newContent, cancellationToken);
                }
                else
                {
                    // No front matter? Prepend it.
                    var newContent = $"---\nid: {id}\n---\n\n{content}";
                    await fileSystem.File.WriteAllTextAsync(filePath, newContent, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to migrate template {Filename}", filename);
            }
        }
    }
}
