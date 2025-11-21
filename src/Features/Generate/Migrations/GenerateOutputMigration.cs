using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Generate.Migrations;

/// <summary>
/// Migrates existing generate output files to use the _generated suffix instead of template names.
/// </summary>
/// <remarks>
/// <para>
/// Background: The /generate command previously created files with template-specific suffixes like
/// '01-21-2025_1_business-meeting.md' or '01-21-2025_2_daily-summary.md'. This made file management
/// complex and tightly coupled to template names.
/// </para>
/// <para>
/// The new standard uses a generic '_generated' suffix for all LLM-generated outputs:
/// '01-21-2025_1_generated.md', '01-21-2025_2_generated.md', etc.
/// </para>
/// <para>
/// This migration automatically renames existing files to the new standard.
/// The migration is idempotent and safe to run multiple times.
/// </para>
/// </remarks>
public sealed class GenerateOutputMigration : IFeatureMigration
{
    /// <inheritdoc/>
    public string FeatureName => "Generate Output";

    /// <inheritdoc/>
    /// <remarks>
    /// Priority 60 ensures this migration runs after NoteMigration (50) but before
    /// TemplatesMigration (100), establishing the correct naming convention for generated
    /// files after the directory structure is migrated.
    /// </remarks>
    public int Priority => 60;

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<GenerateOutputMigration>>();
        var storageOptions = services.GetRequiredService<IOptions<StorageOptions>>();
        var fileSystem = services.GetRequiredService<IFileSystem>();

        // Get effective storage directory using centralized resolution logic
        string storageDirectory = storageOptions.Value.GetEffectiveStorageDirectory();
        string recordingDirectory = fileSystem.Path.Combine(storageDirectory, DirectoryNames.Recording);

        logger.LogDebug("Checking for generate output files to migrate in: {RecordingDirectory}", recordingDirectory);

        // Check if directory exists
        if (!fileSystem.Directory.Exists(recordingDirectory))
        {
            logger.LogDebug("Recording directory does not exist at {Path}, no migration needed", recordingDirectory);
            return false;
        }

        try
        {
            return await MigrateGenerateFilesAsync(
                recordingDirectory,
                fileSystem,
                logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't fail - generate output migration is non-critical
            logger.LogWarning(ex, "Generate output migration failed, but continuing execution");
            return false;
        }
    }

    /// <summary>
    /// Migrates generate output files from template-suffixed names to the standardized _generated suffix.
    /// </summary>
    /// <param name="recordingDirectory">The recording directory path (e.g., ~/ten-second-tom/recording)</param>
    /// <param name="fileSystem">File system abstraction for testing</param>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if migration was performed, false if it was skipped (no files to migrate)</returns>
    /// <remarks>
    /// <para>
    /// Files to migrate (any template name):
    /// - *_business-meeting.md
    /// - *_daily-summary.md
    /// - *_daily-standup.md
    /// - *_weekly-review.md
    /// - *_journal.md
    /// - *_organize.md
    /// - Any other *_{template}.md pattern
    /// </para>
    /// <para>
    /// Files to ignore:
    /// - *_generated.md (already in new format)
    /// - *_stt.txt (transcript files)
    /// - *.wav (audio files)
    /// </para>
    /// <para>
    /// Example: '01-21-2025_1_business-meeting.md' becomes '01-21-2025_1_generated.md'
    /// </para>
    /// </remarks>
    private static async Task<bool> MigrateGenerateFilesAsync(
        string recordingDirectory,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Get all .md files in recording directory
        string[] allMarkdownFiles = fileSystem.Directory.GetFiles(
            recordingDirectory,
            "*.md",
            System.IO.SearchOption.TopDirectoryOnly);

        if (allMarkdownFiles.Length == 0)
        {
            logger.LogDebug("No markdown files found in recording directory, no migration needed");
            return false;
        }

        int migratedCount = 0;
        int skippedCount = 0;

        foreach (string filePath in allMarkdownFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = fileSystem.Path.GetFileName(filePath);

            // Skip if already in new format
            if (fileName.EndsWith("_generated.md", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogTrace("Skipping {File}, already uses _generated suffix", fileName);
                continue;
            }

            // Skip transcripts (shouldn't be .md, but be defensive)
            if (fileName.EndsWith("_stt.txt", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogTrace("Skipping {File}, transcript file", fileName);
                continue;
            }

            // Extract base name (everything before last underscore before .md)
            // Example: "01-21-2025_1_business-meeting.md" -> baseName: "01-21-2025_1", template: "business-meeting"
            int lastUnderscoreIndex = fileName.LastIndexOf('_');
            if (lastUnderscoreIndex == -1)
            {
                logger.LogTrace("Skipping {File}, no underscore found (unexpected format)", fileName);
                continue;
            }

            // Extract base name
            string baseName = fileName.Substring(0, lastUnderscoreIndex);

            // Build new filename with _generated suffix
            string newFileName = $"{baseName}_generated.md";
            string newFilePath = fileSystem.Path.Combine(recordingDirectory, newFileName);

            // Handle conflicts by appending a counter (_2, _3, etc.)
            if (fileSystem.File.Exists(newFilePath))
            {
                logger.LogDebug(
                    "Target {Target} already exists for {Source}, finding available counter",
                    newFileName,
                    fileName);

                int counter = 2;
                string conflictFileName;
                string conflictFilePath;

                do
                {
                    conflictFileName = $"{baseName}_generated_{counter}.md";
                    conflictFilePath = fileSystem.Path.Combine(recordingDirectory, conflictFileName);
                    counter++;
                } while (fileSystem.File.Exists(conflictFilePath) && counter < 100);

                if (counter >= 100)
                {
                    logger.LogWarning(
                        "Skipping {File}, too many conflicts (counter reached {Counter})",
                        fileName,
                        counter);
                    skippedCount++;
                    continue;
                }

                newFileName = conflictFileName;
                newFilePath = conflictFilePath;

                logger.LogInformation(
                    "Conflict resolved: {OldFile} will be renamed to {NewFile} (multiple templates applied to same recording)",
                    fileName,
                    newFileName);
            }

            // Rename file
            logger.LogDebug("Migrating {OldFile} to {NewFile}", fileName, newFileName);
            fileSystem.File.Move(filePath, newFilePath);
            migratedCount++;
        }

        // Log summary
        if (migratedCount > 0)
        {
            logger.LogInformation(
                "Migrated {MigratedCount} generate output files to use _generated suffix (skipped {SkippedCount})",
                migratedCount,
                skippedCount);
            return true;
        }

        if (skippedCount > 0)
        {
            logger.LogDebug(
                "No files migrated, {SkippedCount} files already in new format or had conflicts",
                skippedCount);
        }
        else
        {
            logger.LogDebug("No generate output files found that need migration");
        }

        return false;
    }
}
