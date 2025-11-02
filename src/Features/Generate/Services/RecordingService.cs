using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for recording file operations including discovery and transcript loading.
/// Abstracts filesystem operations for testability.
/// </summary>
public sealed partial class RecordingService : IRecordingService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _recordingDirectory;
    private readonly ILogger<RecordingService> _logger;

    /// <summary>
    /// Regex pattern for M-D-Y_Increment filename format (e.g., "10-21-2025_1.txt").
    /// Groups: Month, Day, Year, Increment
    /// </summary>
    [GeneratedRegex(@"^(\d{1,2})-(\d{1,2})-(\d{4})_(\d+)\.txt$", RegexOptions.Compiled)]
    private static partial Regex RecordingFilenamePattern();

    public RecordingService(
        IFileSystem fileSystem,
        IOptions<StorageOptions> storageOptions,
        ILogger<RecordingService> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(storageOptions);
        var options = storageOptions.Value;

        // Get the effective storage directory using extension method
        var storageBaseDir = options.GetEffectiveStorageDirectory();
        _recordingDirectory = Path.Combine(storageBaseDir, DirectoryNames.Recording);
    }

    public async Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Listing recordings from {Directory}", _recordingDirectory);

        if (!_fileSystem.Directory.Exists(_recordingDirectory))
        {
            return Result<IReadOnlyList<RecordingListItem>>.Failure(
                $"Recording directory not found: {_recordingDirectory}");
        }

        var files = _fileSystem.Directory.GetFiles(
            _recordingDirectory,
            "*.txt",
            SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            return Result<IReadOnlyList<RecordingListItem>>.Failure(
                $"No recordings found in {_recordingDirectory}. Use 'tom record' to create a recording first.");
        }

        var recordings = new List<RecordingListItem>();

        foreach (var filePath in files)
        {
            try
            {
                var fileInfo = _fileSystem.FileInfo.New(filePath);
                var filename = fileInfo.Name;
                var baseName = Path.GetFileNameWithoutExtension(filename);

                // Parse timestamp from filename
                var timestampResult = ParseRecordingTimestamp(filename);
                if (!timestampResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "Skipping file with invalid name format: {Filename}",
                        filename);
                    continue;
                }

                // Check file size for potential corruption
                if (fileInfo.Length > LlmConstants.MaxTranscriptFileSizeBytes)
                {
                    _logger.LogWarning(
                        "Skipping file exceeding maximum size ({Size} bytes): {Filename}",
                        fileInfo.Length,
                        filename);
                    continue;
                }

                // Load content to count words (may throw for corrupted files)
                var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);

                // Skip empty files
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning(
                        "Skipping empty file: {Filename}",
                        filename);
                    continue;
                }

                var wordCount = CountWords(content);

                // Create recording list item
                var recordedAt = timestampResult.Value;
                recordings.Add(new RecordingListItem
                {
                    RecordingBaseName = baseName,
                    TranscriptFilePath = filePath,
                    RecordedAt = recordedAt,
                    FormattedDate = recordedAt.ToString("MMM dd, yyyy h:mm tt", CultureInfo.InvariantCulture),
                    WordCount = wordCount,
                    FileSizeBytes = fileInfo.Length
                });
            }
            catch (IOException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping file due to I/O error (possibly corrupted or in use): {FilePath}",
                    filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping file due to access denied: {FilePath}",
                    filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Skipping file due to unexpected error: {FilePath}",
                    filePath);
            }
        }

        if (recordings.Count == 0)
        {
            return Result<IReadOnlyList<RecordingListItem>>.Failure(
                "No valid recordings found. Recordings must follow M-D-Y_Increment.txt naming pattern.");
        }

        // Sort by date descending (newest first)
        var sorted = recordings
            .OrderByDescending(r => r.RecordedAt)
            .ToList();

        var duration = DateTimeOffset.UtcNow - startTime;
        _logger.LogInformation(
            "Found {Count} recordings in {Duration}ms",
            sorted.Count,
            duration.TotalMilliseconds);

        return Result<IReadOnlyList<RecordingListItem>>.Success(sorted);
    }

    public async Task<Result<string>> GetTranscriptContentAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptFilePath))
        {
            return Result<string>.Failure("Transcript file path is required");
        }

        if (!_fileSystem.File.Exists(transcriptFilePath))
        {
            return Result<string>.Failure($"Transcript file not found: {transcriptFilePath}");
        }

        try
        {
            var content = await _fileSystem.File.ReadAllTextAsync(transcriptFilePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return Result<string>.Failure($"Transcript file is empty: {transcriptFilePath}");
            }

            _logger.LogDebug(
                "Loaded transcript from {Path}: {Length} characters",
                transcriptFilePath,
                content.Length);

            return Result<string>.Success(content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read transcript file: {Path}", transcriptFilePath);
            return Result<string>.Failure($"Unable to read transcript: {ex.Message}");
        }
    }

    public async Task<Result> ValidateTranscriptFileAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptFilePath))
        {
            return Result.Failure("Transcript file path is required");
        }

        if (!_fileSystem.File.Exists(transcriptFilePath))
        {
            return Result.Failure($"Transcript file not found: {transcriptFilePath}");
        }

        try
        {
            // Check file size before attempting to read
            var fileInfo = _fileSystem.FileInfo.New(transcriptFilePath);
            if (fileInfo.Length > LlmConstants.MaxTranscriptFileSizeBytes)
            {
                var maxSizeMb = LlmConstants.MaxTranscriptFileSizeBytes / (1024 * 1024);
                var actualSizeMb = fileInfo.Length / (1024.0 * 1024.0);

                _logger.LogWarning(
                    "Transcript file exceeds maximum size: {ActualSize:F2} MB > {MaxSize} MB for {Path}",
                    actualSizeMb,
                    maxSizeMb,
                    transcriptFilePath);

                return Result.Failure(
                    $"Transcript file is too large ({actualSizeMb:F2} MB). Maximum allowed size is {maxSizeMb} MB.");
            }

            var content = await _fileSystem.File.ReadAllTextAsync(transcriptFilePath, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return Result.Failure($"Transcript file is empty: {transcriptFilePath}");
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate transcript file: {Path}", transcriptFilePath);
            return Result.Failure($"Unable to read transcript: {ex.Message}");
        }
    }

    public Result<DateTimeOffset> ParseRecordingTimestamp(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return Result<DateTimeOffset>.Failure("Filename is required");
        }

        var match = RecordingFilenamePattern().Match(filename);

        if (!match.Success)
        {
            return Result<DateTimeOffset>.Failure(
                $"Invalid filename format. Expected M-D-Y_Increment.txt, got: {filename}");
        }

        try
        {
            var month = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            // var increment = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);

            // Create DateTimeOffset (assuming local time)
            var recordedAt = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);

            return Result<DateTimeOffset>.Success(recordedAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse timestamp from filename: {Filename}", filename);
            return Result<DateTimeOffset>.Failure($"Invalid date components in filename: {filename}");
        }
    }

    private static int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
