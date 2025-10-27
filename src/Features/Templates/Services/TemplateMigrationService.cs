using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates.Services;

/// <summary>
/// Service responsible for automatic template migration for existing users.
/// Part of the Templates feature vertical slice.
/// </summary>
public sealed class TemplateMigrationService
{
    private readonly IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>> _templateHandler;
    private readonly ILogger<TemplateMigrationService> _logger;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the TemplateMigrationService class.
    /// </summary>
    public TemplateMigrationService(
        IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>> templateHandler,
        ILogger<TemplateMigrationService> logger,
        IFileSystem fileSystem)
    {
        _templateHandler = templateHandler;
        _logger = logger;
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Runs automatic template migration for existing users if configured.
    /// Extracts memory directory from configuration and performs silent migration.
    /// Non-critical operation - failures are logged but don't stop application execution.
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task RunAutomaticMigrationAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Get memory directory using standard .NET configuration
        // TenSecondTom:MemoryDirectory is the root containing templates/, today/, thisweek/, etc.
            string? rootDirectory = configuration[ConfigurationKeys.MemoryDirectoryKey];
        _logger.LogDebug("Root directory from configuration: {RootDirectory}", rootDirectory);

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            _logger.LogDebug("No root directory configured, skipping template migration");
            return;
        }

        try
        {
            var migrationResult = await ValidateAndMigrateTemplatesAsync(rootDirectory, cancellationToken)
                .ConfigureAwait(false);

            if (migrationResult.IsSuccess && migrationResult.Value)
            {
                _logger.LogInformation("Template migration completed successfully");
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - templates migration is non-critical
            _logger.LogWarning(ex, "Template migration failed, but continuing execution");
        }
    }

    /// <summary>
    /// Validates that required templates exist and installs them if missing.
    /// This is a silent migration that runs automatically for existing users.
    /// </summary>
    /// <param name="rootDirectory">The configured root directory (e.g., ~/ten-second-tom or ./.memory)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating whether migration was needed and successful (true if migrated, false if not needed)</returns>
    private async Task<Result<bool>> ValidateAndMigrateTemplatesAsync(
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return Result<bool>.Failure("Root directory cannot be null or empty");
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Templates are under root directory: {root}/templates
        string templatesDirectory = _fileSystem.Path.Combine(rootDirectory, DirectoryNames.Templates);

        // Check if templates directory exists
        bool directoryExists = _fileSystem.Directory.Exists(templatesDirectory);

        // Check if required templates exist
        bool dailySummaryExists = directoryExists &&
            _fileSystem.File.Exists(_fileSystem.Path.Combine(templatesDirectory, "daily-summary.md"));
        bool dailyStandupExists = directoryExists &&
            _fileSystem.File.Exists(_fileSystem.Path.Combine(templatesDirectory, "daily-standup.md"));
        bool weeklyReviewExists = directoryExists &&
            _fileSystem.File.Exists(_fileSystem.Path.Combine(templatesDirectory, "weekly-review.md"));
        bool businessMeetingExists = directoryExists &&
            _fileSystem.File.Exists(_fileSystem.Path.Combine(templatesDirectory, "business-meeting.md"));

        // If all required templates exist, no migration needed
        if (dailySummaryExists && dailyStandupExists && weeklyReviewExists && businessMeetingExists)
        {
            _logger.LogDebug("Templates already configured, no migration needed");
            return Result<bool>.Success(false);
        }

        // Templates are missing, need to install them
        if (!directoryExists)
        {
            _logger.LogInformation(
                "Templates directory missing at {TemplatesDirectory}, installing default templates",
                templatesDirectory);
        }
        else
        {
            _logger.LogInformation(
                "Default templates missing (DailySummary={DailySummary}, DailyStandup={DailyStandup}, WeeklyReview={WeeklyReview}, BusinessMeeting={BusinessMeeting}), installing",
                dailySummaryExists,
                dailyStandupExists,
                weeklyReviewExists,
                businessMeetingExists);
        }

        // Install templates (OverwriteExisting=false to preserve user customizations)
        var installCommand = new InstallDefaultTemplatesCommand
        {
            TargetDirectory = templatesDirectory,
            OverwriteExisting = false
        };

        var installResult = await _templateHandler.Handle(installCommand, cancellationToken);

        if (!installResult.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to install default templates: {Error}",
                installResult.Error);
            return Result<bool>.Success(false); // Return success but false value - migration failed but app can continue
        }

        _logger.LogInformation(
            "Successfully installed {Count} templates ({TemplateIds})",
            installResult.Value.TemplatesInstalled,
            string.Join(", ", installResult.Value.InstalledTemplateIds));

        return Result<bool>.Success(true); // Migration completed successfully
    }
}
