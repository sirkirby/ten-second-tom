using System.IO.Abstractions;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Migrations;

/// <summary>
/// Migrates legacy .txt recording files to .md format with YAML front matter.
/// </summary>
public sealed class RecordingMigration(
    IFileSystem fileSystem,
    ILogger<RecordingMigration> logger) : IFeatureMigration
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly ILogger<RecordingMigration> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string FeatureName => "Recording Migration";
    public int Priority => 110; // Run after Templates (100)

    // Matches M-D-Y_Increment.txt (e.g. 10-31-2025_1.txt)
    private static readonly Regex LegacyFilenamePattern = new(@"^(\d{1,2}-\d{1,2}-\d{4})_(\d+)\.txt$");

    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Resolve services from the service provider (not from constructor)
        var logger = services.GetRequiredService<ILogger<RecordingMigration>>() ?? _logger;
        var storageOptions = services.GetRequiredService<IOptions<StorageOptions>>();
        var fileSystem = services.GetRequiredService<IFileSystem>() ?? _fileSystem;

        // Get effective storage directory using centralized resolution logic
        var storageDirectory = storageOptions.Value.GetEffectiveStorageDirectory();
        var recordingDirectory = fileSystem.Path.Combine(storageDirectory, "recording");

        logger.LogDebug("Checking for legacy .txt recording files in: {RecordingDirectory}", recordingDirectory);

        if (!fileSystem.Directory.Exists(recordingDirectory))
        {
            logger.LogDebug("Recording directory not found at {Path}, skipping migration", recordingDirectory);
            return true; // Migration not needed, considered successful
        }

        var txtFiles = fileSystem.Directory.GetFiles(recordingDirectory, "*.txt", SearchOption.TopDirectoryOnly);
        if (txtFiles.Length == 0)
        {
            logger.LogDebug("No legacy .txt recordings found, migration not needed");
            return true; // Migration not needed, considered successful
        }

        logger.LogInformation("Found {Count} legacy .txt recordings to migrate", txtFiles.Length);

        int migratedCount = 0;
        int skippedCount = 0;

        foreach (var txtFile in txtFiles)
        {
            try
            {
                var filename = fileSystem.Path.GetFileName(txtFile);
                var match = LegacyFilenamePattern.Match(filename);

                if (!match.Success)
                {
                    logger.LogWarning("Skipping file {Filename} - does not match legacy pattern", filename);
                    skippedCount++;
                    continue;
                }

                var content = await fileSystem.File.ReadAllTextAsync(txtFile, cancellationToken);

                var mdFilename = fileSystem.Path.ChangeExtension(filename, ".md");
                var mdPath = fileSystem.Path.Combine(recordingDirectory, mdFilename);

                // If .md already exists, skip to avoid data loss
                if (fileSystem.File.Exists(mdPath))
                {
                    logger.LogWarning("Target .md file {Filename} already exists, skipping migration for {TxtFile}", mdFilename, filename);
                    skippedCount++;
                    continue;
                }

                // Inject recording-id into front matter
                var newContent = InjectRecordingId(content);

                // Write to .md
                await fileSystem.File.WriteAllTextAsync(mdPath, newContent, cancellationToken);

                // Delete .txt
                fileSystem.File.Delete(txtFile);

                logger.LogInformation("Migrated {TxtFile} to {MdFile}", filename, mdFilename);
                migratedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to migrate recording {Filename}", txtFile);
                skippedCount++;
            }
        }

        if (migratedCount > 0)
        {
            logger.LogInformation(
                "Recording migration completed: {MigratedCount} files migrated, {SkippedCount} files skipped",
                migratedCount,
                skippedCount);
            return true;
        }

        logger.LogDebug("No recordings were migrated");
        return true; // Considered successful even if nothing was migrated
    }

    private static string InjectRecordingId(string content)
    {
        // Check if front matter exists
        if (!content.StartsWith("---"))
        {
            // No front matter, just prepend it with ID
            return $"""
                    ---
                    recording-id: {Guid.NewGuid()}
                    ---
                    
                    {content}
                    """;
        }

        // Find the end of the first front matter block
        // We assume it starts with ---
        var firstLineEnd = content.IndexOf('\n');
        if (firstLineEnd == -1) return content; // Should not happen if StartsWith("---")

        // Insert recording-id after the first ---
        // We want to insert "recording-id: <guid>\n" after "---" (and newline)

        // Handle CRLF or LF
        var newline = content.Substring(0, firstLineEnd).Contains('\r') ? "\r\n" : "\n";
        // Actually, just use Environment.NewLine or \n, but better to match file.
        // Let's just insert after "---"

        var insertionPoint = 3; // Length of "---"
        // Check if there is a newline after ---
        if (content.Length > 3 && (content[3] == '\r' || content[3] == '\n'))
        {
            // Find where the newline ends
            if (content[3] == '\r' && content.Length > 4 && content[4] == '\n')
            {
                insertionPoint = 5;
            }
            else
            {
                insertionPoint = 4;
            }
        }

        var idLine = $"recording-id: {Guid.NewGuid()}{Environment.NewLine}";
        return content.Insert(insertionPoint, idLine);
    }
}
