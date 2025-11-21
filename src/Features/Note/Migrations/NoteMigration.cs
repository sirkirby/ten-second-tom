using System.IO.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Note.Migrations;

/// <summary>
/// Note storage migration that moves files from the 'today/' directory to the new 'note/' directory.
/// This migration runs automatically during application bootstrap if the today directory exists with files.
/// </summary>
/// <remarks>
/// <para>
/// Background: The application originally stored daily entries in a 'today/' directory.
/// With the introduction of the /note command for raw note capture, both commands now share
/// the same storage location in 'note/' directory to maintain unified entry numbering.
/// </para>
/// <para>
/// This migration ensures existing users' data is automatically moved to the new structure.
/// The migration is idempotent and safe to run multiple times.
/// </para>
/// </remarks>
public sealed class NoteMigration : IFeatureMigration
{
    /// <inheritdoc/>
    public string FeatureName => "Note Storage";

    /// <inheritdoc/>
    /// <remarks>
    /// Priority 50 ensures this migration runs before TemplatesMigration (100),
    /// establishing the correct directory structure early in the bootstrap process.
    /// </remarks>
    public int Priority => 50;

    /// <inheritdoc/>
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<NoteMigration>>();
        var storageOptions = services.GetRequiredService<IOptions<StorageOptions>>();
        var fileSystem = services.GetRequiredService<IFileSystem>();

        // Get effective storage directory using centralized resolution logic
        string rootDirectory = storageOptions.Value.GetEffectiveStorageDirectory();
        logger.LogDebug("Effective storage directory resolved: {RootDirectory}", rootDirectory);

        try
        {
            return await MigrateNotesDirectoryAsync(
                rootDirectory,
                fileSystem,
                logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log but don't fail - note migration is non-critical
            logger.LogWarning(ex, "Note migration failed, but continuing execution");
            return false;
        }
    }

    /// <summary>
    /// Migrates files from the 'today/' directory to the new 'note/' directory.
    /// All .md files are renamed with a '_generated' suffix to indicate LLM-generated content.
    /// Audio files (.wav) are moved without modification.
    /// </summary>
    /// <param name="rootDirectory">The effective storage directory (e.g., ~/ten-second-tom or ~/Documents/MyVault/tst)</param>
    /// <param name="fileSystem">File system abstraction for testing</param>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if migration was performed, false if it was skipped (directory doesn't exist or is empty)</returns>
    /// <remarks>
    /// All files in 'today/' directory are LLM-generated (from /today command), so .md files
    /// receive the '_generated' suffix to distinguish them from user-created notes (from /note command).
    /// Example: '01-21-2025_1.md' becomes '01-21-2025_1_generated.md'
    /// </remarks>
    private static async Task<bool> MigrateNotesDirectoryAsync(
        string rootDirectory,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Paths for legacy and new directories
        string legacyTodayDirectory = fileSystem.Path.Combine(rootDirectory, DirectoryNames.Today);
        string newNoteDirectory = fileSystem.Path.Combine(rootDirectory, DirectoryNames.Note);

        // Check if legacy directory exists
        if (!fileSystem.Directory.Exists(legacyTodayDirectory))
        {
            logger.LogDebug("Legacy 'today/' directory does not exist at {Path}, no migration needed", legacyTodayDirectory);
            return false;
        }

        // Get all files in legacy directory (both .md and .wav files)
        string[] allFiles = fileSystem.Directory.GetFiles(legacyTodayDirectory, "*.*", System.IO.SearchOption.TopDirectoryOnly);

        if (allFiles.Length == 0)
        {
            logger.LogInformation("Legacy 'today/' directory is empty, deleting it");
            fileSystem.Directory.Delete(legacyTodayDirectory, recursive: false);
            return false;
        }

        // Migration is needed - ensure target directory exists
        if (!fileSystem.Directory.Exists(newNoteDirectory))
        {
            logger.LogInformation("Creating new 'note/' directory at {Path}", newNoteDirectory);
            fileSystem.Directory.CreateDirectory(newNoteDirectory);
        }

        // Move all files from today/ to note/, adding _generated suffix to .md files
        int filesMoved = 0;
        foreach (string sourceFilePath in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = fileSystem.Path.GetFileName(sourceFilePath);
            string newFileName = fileName;

            // Add _generated suffix to .md files (all are LLM-generated from /today command)
            if (fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                // Replace .md extension with _generated.md
                newFileName = string.Concat(fileName.AsSpan(0, fileName.Length - 3), "_generated.md");
                logger.LogDebug("Transforming {OldName} to {NewName}", fileName, newFileName);
            }
            // .wav files keep original names

            string destinationFilePath = fileSystem.Path.Combine(newNoteDirectory, newFileName);

            // Skip if file already exists in destination (safety check)
            if (fileSystem.File.Exists(destinationFilePath))
            {
                logger.LogWarning(
                    "File {FileName} already exists in note/ directory, skipping move from today/",
                    newFileName);
                continue;
            }

            logger.LogDebug("Moving {FileName} from today/ to note/ as {NewFileName}", fileName, newFileName);
            fileSystem.File.Move(sourceFilePath, destinationFilePath);
            filesMoved++;
        }

        // Delete the now-empty legacy directory
        if (filesMoved > 0)
        {
            logger.LogInformation("Deleting empty legacy 'today/' directory");
            fileSystem.Directory.Delete(legacyTodayDirectory, recursive: false);
            logger.LogInformation("Migrated {Count} files from today/ to note/ (added _generated suffix to .md files)", filesMoved);
            return true;
        }

        logger.LogWarning("No files were moved from today/ to note/, migration skipped");
        return false;
    }
}
