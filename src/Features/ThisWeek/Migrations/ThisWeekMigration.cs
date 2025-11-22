using System.Globalization;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Bootstrapping;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.ThisWeek.Migrations;

/// <summary>
/// Migrates legacy weekly review files from the deprecated 'thisweek/' directory into the unified note directory.
/// </summary>
/// <remarks>
/// <para>
/// Previous releases stored weekly summaries under {root}/thisweek/ using the naming pattern
/// 'YYYY-WW-DayOfWeek-EntryNumber.md'. The new standard stores all LLM-generated content under
/// {root}/note/ using the pattern '{from_date}_{to_date}_{increment}_generated.md'.
/// </para>
/// <para>
/// This migration renames and moves existing weekly files to the new layout, then removes the legacy directory
/// once it is empty. The migration is idempotent and safe to run multiple times.
/// </para>
/// </remarks>
public sealed class ThisWeekMigration : IFeatureMigration
{
    /// <inheritdoc />
    public string FeatureName => "ThisWeek Storage";

    /// <inheritdoc />
    /// <remarks>
    /// Priority 55 ensures this migration runs immediately after NoteMigration (50) so the note directory exists
    /// before weekly files are moved. It also runs before migrations that rely on the unified storage layout.
    /// </remarks>
    public int Priority => 55;

    /// <inheritdoc />
    public async Task<bool> MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var logger = services.GetRequiredService<ILogger<ThisWeekMigration>>();
        var storageOptions = services.GetRequiredService<IOptions<StorageOptions>>();
        var fileSystem = services.GetRequiredService<IFileSystem>();

        string rootDirectory = storageOptions.Value.GetEffectiveStorageDirectory();
        string legacyDirectory = fileSystem.Path.Combine(rootDirectory, DirectoryNames.ThisWeek);
        string noteDirectory = fileSystem.Path.Combine(rootDirectory, DirectoryNames.Note);

        logger.LogDebug("Checking for legacy weekly entries in {LegacyDirectory}", legacyDirectory);

        if (!fileSystem.Directory.Exists(legacyDirectory))
        {
            logger.LogDebug("Legacy 'thisweek/' directory not found at {Path}, skipping migration", legacyDirectory);
            return false;
        }

        try
        {
            return await MigrateWeeklyFilesAsync(
                legacyDirectory,
                noteDirectory,
                fileSystem,
                logger,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ThisWeek migration failed, continuing startup without blocking");
            return false;
        }
    }

    private static Task<bool> MigrateWeeklyFilesAsync(
        string legacyDirectory,
        string noteDirectory,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string[] legacyFiles = fileSystem.Directory.GetFiles(legacyDirectory, "*.md", SearchOption.TopDirectoryOnly);
        if (legacyFiles.Length == 0)
        {
            logger.LogInformation("Legacy 'thisweek/' directory is empty, deleting it");
            fileSystem.Directory.Delete(legacyDirectory, recursive: false);
            return Task.FromResult(false);
        }

        if (!fileSystem.Directory.Exists(noteDirectory))
        {
            logger.LogInformation("Creating note directory at {NoteDirectory} for weekly entries", noteDirectory);
            fileSystem.Directory.CreateDirectory(noteDirectory);
        }

        int migratedCount = 0;
        int skippedCount = 0;

        foreach (string legacyFile in legacyFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fileName = fileSystem.Path.GetFileName(legacyFile);

            if (!TryParseLegacyFileName(fileName, out var legacyMetadata))
            {
                logger.LogWarning("Skipping weekly file {FileName} - unrecognized legacy format", fileName);
                skippedCount++;
                continue;
            }

            var (rangeStart, rangeEnd) = CalculateWeekRange(legacyMetadata.Year, legacyMetadata.WeekNumber);
            string newFileName = $"{rangeStart:MM-dd-yyyy}_{rangeEnd:MM-dd-yyyy}_{legacyMetadata.EntryNumber}_generated.md";
            string destinationPath = fileSystem.Path.Combine(noteDirectory, newFileName);

            if (fileSystem.File.Exists(destinationPath))
            {
                logger.LogWarning(
                    "Skipping weekly file {FileName} because destination {Destination} already exists",
                    fileName,
                    newFileName);
                skippedCount++;
                continue;
            }

            logger.LogDebug("Migrating weekly file {FileName} to {NewFileName}", fileName, newFileName);
            fileSystem.File.Move(legacyFile, destinationPath);
            migratedCount++;
        }

        TryDeleteLegacyDirectory(fileSystem, legacyDirectory, logger);

        if (migratedCount == 0)
        {
            logger.LogInformation(
                "No weekly files migrated from legacy 'thisweek/' directory (skipped {SkippedCount})",
                skippedCount);
            return Task.FromResult(false);
        }

        logger.LogInformation(
            "Migrated {MigratedCount} weekly files from legacy 'thisweek/' directory (skipped {SkippedCount})",
            migratedCount,
            skippedCount);
        return Task.FromResult(true);
    }

    private static void TryDeleteLegacyDirectory(IFileSystem fileSystem, string legacyDirectory, ILogger logger)
    {
        if (!fileSystem.Directory.Exists(legacyDirectory))
        {
            return;
        }

        bool hasEntries = fileSystem.Directory.EnumerateFileSystemEntries(legacyDirectory).Any();
        if (!hasEntries)
        {
            logger.LogInformation("Deleting empty legacy 'thisweek/' directory");
            fileSystem.Directory.Delete(legacyDirectory, recursive: false);
        }
        else
        {
            logger.LogDebug(
                "Legacy 'thisweek/' directory at {Path} still contains files after migration; leaving in place",
                legacyDirectory);
        }
    }

    private static bool TryParseLegacyFileName(string fileName, out LegacyWeeklyFile legacyMetadata)
    {
        legacyMetadata = default;

        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string nameWithoutExtension = fileName.Substring(0, fileName.Length - 3);
        string[] segments = nameWithoutExtension.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 4)
        {
            return false;
        }

        if (!int.TryParse(segments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int year))
        {
            return false;
        }

        if (!int.TryParse(segments[1], NumberStyles.None, CultureInfo.InvariantCulture, out int weekNumber))
        {
            return false;
        }

        if (!TryParseDayOfWeek(segments[2], out _))
        {
            return false;
        }

        if (!int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out int entryNumber))
        {
            return false;
        }

        legacyMetadata = new LegacyWeeklyFile(year, weekNumber, entryNumber);
        return true;
    }

    private static bool TryParseDayOfWeek(string value, out DayOfWeek dayOfWeek)
    {
        if (Enum.TryParse(value, ignoreCase: true, out dayOfWeek))
        {
            return true;
        }

        DayOfWeek? parsed = value.ToLowerInvariant() switch
        {
            "mon" => DayOfWeek.Monday,
            "tue" or "tues" => DayOfWeek.Tuesday,
            "wed" => DayOfWeek.Wednesday,
            "thu" or "thur" or "thurs" => DayOfWeek.Thursday,
            "fri" => DayOfWeek.Friday,
            "sat" => DayOfWeek.Saturday,
            "sun" => DayOfWeek.Sunday,
            _ => null
        };

        if (parsed.HasValue)
        {
            dayOfWeek = parsed.Value;
            return true;
        }

        dayOfWeek = default;
        return false;
    }

    private static (DateTime Start, DateTime End) CalculateWeekRange(int year, int weekNumber)
    {
        DateTime start = ISOWeek.ToDateTime(year, weekNumber, DayOfWeek.Monday);
        return (start, start.AddDays(6));
    }

    private readonly record struct LegacyWeeklyFile(int Year, int WeekNumber, int EntryNumber);
}
