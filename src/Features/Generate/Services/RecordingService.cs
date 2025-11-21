using System.Globalization;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Models;
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
    private readonly YamlFrontMatterParser _yamlParser;

    /// <summary>
    /// Regex pattern for M-D-Y_Increment filename format (e.g., "10-21-2025_1.md").
    /// Groups: Month, Day, Year, Increment
    /// </summary>
    [GeneratedRegex(@"^(\d{1,2})-(\d{1,2})-(\d{4})_(\d+)\.md$", RegexOptions.Compiled)]
    private static partial Regex RecordingFilenamePattern();

    public RecordingService(
        IFileSystem fileSystem,
        IOptions<StorageOptions> storageOptions,
        ILogger<RecordingService> logger,
        YamlFrontMatterParser yamlParser)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlParser = yamlParser ?? throw new ArgumentNullException(nameof(yamlParser));

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
            "*.md",
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

                // Skip generated files (output of generate command)
                if (filename.EndsWith("_generated.md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var baseName = Path.GetFileNameWithoutExtension(filename);

                // Parse timestamp from filename to validate format
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

                // Load content to count words and parse date from front matter
                var content = await _fileSystem.File.ReadAllTextAsync(filePath, cancellationToken);

                // Skip empty files
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning(
                        "Skipping empty file: {Filename}",
                        filename);
                    continue;
                }

                // Try to parse date from YAML front matter, fall back to file LastWriteTime
                DateTimeOffset recordedAt;
                if (TryParseDateFromFrontMatter(content, out var parsedDate))
                {
                    recordedAt = parsedDate;
                }
                else
                {
                    _logger.LogDebug(
                        "Could not parse date from YAML front matter for {Filename}, using file LastWriteTime",
                        filename);
                    recordedAt = new DateTimeOffset(fileInfo.LastWriteTime);
                }

                // Strip front matter for word count
                var contentWithoutFrontMatter = StripFrontMatter(content);
                var wordCount = CountWords(contentWithoutFrontMatter);

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
                "No valid recordings found. Recordings must follow M-D-Y_Increment.md naming pattern.");
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

            var contentWithoutFrontMatter = StripFrontMatter(content);

            _logger.LogDebug(
                "Loaded transcript from {Path}: {Length} characters",
                transcriptFilePath,
                contentWithoutFrontMatter.Length);

            return Result<string>.Success(contentWithoutFrontMatter);
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
                $"Invalid filename format. Expected M-D-Y_Increment.md, got: {filename}");
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

    private static string StripFrontMatter(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        // Simple check for front matter delimiters
        if (!content.StartsWith("---")) return content;

        var parts = content.Split("---", StringSplitOptions.RemoveEmptyEntries);
        // If we have at least 2 parts, the first one was the front matter (empty string before first --- is removed),
        // so the second part is the content.
        // Wait, Split with RemoveEmptyEntries might be tricky if front matter is the first thing.
        // content = "---\nkey: value\n---\nreal content"
        // Split("---") -> ["\nkey: value\n", "\nreal content"]

        if (parts.Length >= 2)
        {
            // Return everything after the second delimiter (which is the start of the second part in the array)
            // Actually, let's be more robust.
            // Regex is safer.
            var match = Regex.Match(content, @"^---\s*[\s\S]*?---\s*", RegexOptions.Multiline);
            if (match.Success)
            {
                return content.Substring(match.Length).Trim();
            }
        }

        return content;
    }

    /// <summary>
    /// Tries to parse the date from the YAML front matter of a recording file.
    /// </summary>
    /// <param name="content">The file content with YAML front matter.</param>
    /// <param name="date">The parsed date if successful.</param>
    /// <returns>True if the date was successfully parsed, false otherwise.</returns>
    private bool TryParseDateFromFrontMatter(string content, out DateTimeOffset date)
    {
        date = default;

        try
        {
            // Check if content starts with front matter
            if (!content.TrimStart().StartsWith("---", StringComparison.Ordinal))
            {
                return false;
            }

            // Split to extract front matter block
            var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            // Find first and second --- delimiters
            int firstDelim = -1, secondDelim = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == "---")
                {
                    if (firstDelim == -1)
                        firstDelim = i;
                    else if (secondDelim == -1)
                    {
                        secondDelim = i;
                        break;
                    }
                }
            }

            if (firstDelim == -1 || secondDelim == -1)
            {
                return false;
            }

            // Extract YAML content between delimiters
            var yamlLines = lines.Skip(firstDelim + 1).Take(secondDelim - firstDelim - 1);
            var yamlContent = string.Join("\n", yamlLines);

            // Use YamlDotNet to deserialize the front matter
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.HyphenatedNamingConvention.Instance)
                .Build();

            var frontMatter = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);

            // Look for either 'timestamp' or 'date' field (recordings use 'timestamp', notes use 'date')
            object? dateValue = null;
            if (frontMatter != null)
            {
                if (!frontMatter.TryGetValue("timestamp", out dateValue))
                {
                    frontMatter.TryGetValue("date", out dateValue);
                }
            }

            if (dateValue != null)
            {
                // Try to parse the date string
                if (dateValue is string dateStr)
                {
                    // Try ISO 8601 format first (used by recordings: 2025-10-27T17:39:44.5350960+00:00)
                    if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    {
                        return true;
                    }

                    // Try yyyy-MM-dd HH:mm:ss format (legacy/notes format)
                    if (DateTimeOffset.TryParseExact(
                        dateStr,
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out date))
                    {
                        return true;
                    }
                }
                else if (dateValue is DateTime dateTime)
                {
                    date = new DateTimeOffset(dateTime);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse date from YAML front matter");
            return false;
        }
    }
}
