using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates for improved performance", Justification = "Migration logic, performance not critical")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Non-critical migration operation, must not fail application startup")]
    public async Task RunAutomaticMigrationAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        string? memoryDir = configuration["Storage:MemoryDirectory"];
        _logger.LogDebug("Memory directory from configuration: {MemoryDir}", memoryDir);

        if (string.IsNullOrWhiteSpace(memoryDir))
        {
            _logger.LogDebug("No memory directory configured, skipping template migration");
            return;
        }

        try
        {
            var migrationResult = await ValidateAndMigrateTemplatesAsync(memoryDir, cancellationToken)
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
    /// <param name="memoryDirectory">The memory directory path where templates should exist</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating whether migration was needed and successful (true if migrated, false if not needed)</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates for improved performance", Justification = "Migration logic, performance not critical")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Console application, no synchronization context")]
    private async Task<Result<bool>> ValidateAndMigrateTemplatesAsync(
        string memoryDirectory,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(memoryDirectory))
        {
            return Result<bool>.Failure("Memory directory cannot be null or empty");
        }

        cancellationToken.ThrowIfCancellationRequested();

        string templatesDirectory = _fileSystem.Path.Combine(memoryDirectory, "templates");

        // Check if templates directory exists
        bool directoryExists = _fileSystem.Directory.Exists(templatesDirectory);

        // Check if required templates exist
        bool dailySummaryExists = directoryExists &&
            _fileSystem.File.Exists(_fileSystem.Path.Combine(templatesDirectory, "daily-summary.md"));
        bool weeklyReviewExists = directoryExists &&
            _fileSystem.File.Exists(_fileSystem.Path.Combine(templatesDirectory, "weekly-review.md"));

        // If both templates exist, no migration needed
        if (dailySummaryExists && weeklyReviewExists)
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
                "Default templates missing (DailySummary={DailySummary}, WeeklyReview={WeeklyReview}), installing",
                dailySummaryExists,
                weeklyReviewExists);
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
