using System.Globalization;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Provides discovery helpers for note and recording audio libraries.
/// </summary>
public sealed class AudioLibraryService : IAudioLibraryService
{
    private static readonly string[] TranscriptExtensions = [".md", ".txt"];
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<AudioLibraryService> _logger;
    private readonly string _recordingDirectory;
    private readonly string _noteDirectory;
    private readonly string _todayDirectory;

    public AudioLibraryService(
        IFileSystem fileSystem,
        IOptions<StorageOptions> storageOptions,
        ILogger<AudioLibraryService> logger)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ArgumentNullException.ThrowIfNull(storageOptions);
        var storageRoot = storageOptions.Value.GetEffectiveStorageDirectory();

        _recordingDirectory = _fileSystem.Path.Combine(storageRoot, DirectoryNames.Recording);
        _noteDirectory = _fileSystem.Path.Combine(storageRoot, DirectoryNames.Note);
        _todayDirectory = _fileSystem.Path.Combine(storageRoot, DirectoryNames.Today);
    }

    public Task<Result<IReadOnlyList<AudioLibraryItem>>> ListAudioFilesAsync(
        AudioLibraryScope scope,
        CancellationToken cancellationToken = default)
    {
        var directory = GetDirectoryForScope(scope);
        if (!_fileSystem.Directory.Exists(directory))
        {
            return Task.FromResult(Result<IReadOnlyList<AudioLibraryItem>>.Failure(
                $"Audio directory not found: {directory}"));
        }

        // Search for both .mp3 (new format) and .wav (legacy format) files
        var mp3Files = _fileSystem.Directory.GetFiles(directory, "*.mp3", SearchOption.TopDirectoryOnly);
        var wavFiles = _fileSystem.Directory.GetFiles(directory, "*.wav", SearchOption.TopDirectoryOnly);
        var files = mp3Files.Concat(wavFiles).ToArray();

        if (files.Length == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<AudioLibraryItem>>.Failure(
                $"No audio files found in {directory}. Create a {scope.ToString().ToLowerInvariant()} entry first."));
        }

        var items = new List<AudioLibraryItem>(files.Length);
        foreach (var filePath in files)
        {
            if (!TryCreateItem(scope, filePath, out var item))
            {
                _logger.LogDebug("Skipping unreadable audio file: {FilePath}", filePath);
                continue;
            }

            items.Add(item);
        }

        if (items.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyList<AudioLibraryItem>>.Failure(
                $"No valid audio files found in {directory}."));
        }

        var ordered = items
            .OrderByDescending(i => i.RecordedAt)
            .ThenByDescending(i => i.BaseName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<AudioLibraryItem>>.Success(ordered));
    }

    public Result<AudioLibraryItem> GetAudioFile(AudioLibraryScope scope, string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return Result<AudioLibraryItem>.Failure("Base filename is required.");
        }

        var directory = GetDirectoryForScope(scope);

        // Try .mp3 first (new format), then fall back to .wav (legacy format)
        var mp3Path = _fileSystem.Path.Combine(directory, $"{baseName}.mp3");
        var wavPath = _fileSystem.Path.Combine(directory, $"{baseName}.wav");

        var audioPath = _fileSystem.File.Exists(mp3Path) ? mp3Path :
                        _fileSystem.File.Exists(wavPath) ? wavPath : null;

        if (audioPath is null)
        {
            return Result<AudioLibraryItem>.Failure(
                $"Audio file '{baseName}' not found under {directory}.");
        }

        if (!TryCreateItem(scope, audioPath, out var item))
        {
            return Result<AudioLibraryItem>.Failure(
                $"Unable to read metadata for '{baseName}'.");
        }

        return Result<AudioLibraryItem>.Success(item);
    }

    private string GetDirectoryForScope(AudioLibraryScope scope)
    {
        return scope switch
        {
            AudioLibraryScope.Recording => _recordingDirectory,
            AudioLibraryScope.Note => _noteDirectory,
            AudioLibraryScope.Today => _todayDirectory,
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "External scope does not map to a library directory.")
        };
    }

    private bool TryCreateItem(AudioLibraryScope scope, string filePath, out AudioLibraryItem item)
    {
        item = null!;
        try
        {
            var fileInfo = _fileSystem.FileInfo.New(filePath);
            var baseName = _fileSystem.Path.GetFileNameWithoutExtension(fileInfo.Name);
            var (metadata, transcriptExists) = TryReadFrontMatterMetadata(fileInfo.FullName);
            var recordedAt = metadata?.Timestamp
                ?? TryParseTimestamp(baseName)
                ?? new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToLocalTime();

            item = new AudioLibraryItem
            {
                BaseName = baseName,
                AudioFilePath = fileInfo.FullName,
                Scope = scope,
                RecordedAt = recordedAt,
                FileSizeBytes = fileInfo.Length,
                DurationSeconds = metadata?.DurationSeconds,
                TranscriptExists = transcriptExists
            };

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read audio file metadata: {FilePath}", filePath);
            return false;
        }
    }

    private (AudioFrontMatterMetadata? Metadata, bool TranscriptExists) TryReadFrontMatterMetadata(string audioFilePath)
    {
        AudioFrontMatterMetadata? metadata = null;
        var transcriptExists = false;

        foreach (var extension in TranscriptExtensions)
        {
            var transcriptPath = _fileSystem.Path.ChangeExtension(audioFilePath, extension);
            if (transcriptPath is null || !_fileSystem.File.Exists(transcriptPath))
            {
                continue;
            }

            transcriptExists = true;
            metadata ??= ReadFrontMatterMetadata(transcriptPath);

            if (metadata is not null)
            {
                break;
            }
        }

        return (metadata, transcriptExists);
    }

    private AudioFrontMatterMetadata? ReadFrontMatterMetadata(string transcriptPath)
    {
        try
        {
            using var stream = _fileSystem.File.OpenRead(transcriptPath);
            using var reader = new StreamReader(stream);

            var inFrontMatter = false;
            var metadata = new AudioFrontMatterMetadata();
            for (var i = 0; i < 40 && !reader.EndOfStream; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    if (inFrontMatter)
                    {
                        continue;
                    }

                    break;
                }

                if (trimmed == "---")
                {
                    if (!inFrontMatter)
                    {
                        inFrontMatter = true;
                        continue;
                    }

                    break;
                }

                if (!inFrontMatter)
                {
                    break;
                }

                if (TryParseFrontMatterDate(trimmed, out var parsed))
                {
                    metadata.Timestamp = parsed;
                    continue;
                }

                if (TryParseFrontMatterDuration(trimmed, out var duration))
                {
                    metadata.DurationSeconds = duration;
                }
            }

            if (metadata.Timestamp.HasValue || metadata.DurationSeconds.HasValue)
            {
                return metadata;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to read transcript metadata at {TranscriptPath}", transcriptPath);
        }

        return null;
    }

    private static bool TryParseFrontMatterDate(string line, out DateTimeOffset result)
    {
        result = default;
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var key = line[..separatorIndex].Trim();
        if (!key.Equals("date", StringComparison.OrdinalIgnoreCase) &&
            !key.Equals("timestamp", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = line[(separatorIndex + 1)..].Trim();
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            result = parsed.ToLocalTime();
            return true;
        }

        return false;
    }

    private static bool TryParseFrontMatterDuration(string line, out double durationSeconds)
    {
        durationSeconds = 0;
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var key = line[..separatorIndex].Trim();
        if (!key.Equals("duration", StringComparison.OrdinalIgnoreCase) &&
            !key.Equals("audio-duration-seconds", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var value = line[(separatorIndex + 1)..].Trim();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out durationSeconds);
    }

    private static DateTimeOffset? TryParseTimestamp(string baseName)
    {
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return null;
        }

        var dashIndex = baseName.IndexOf('_');
        var datePart = dashIndex >= 0 ? baseName[..dashIndex] : baseName;
        var segments = datePart.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3)
        {
            return null;
        }

        if (int.TryParse(segments[0], out var month) &&
            int.TryParse(segments[1], out var day) &&
            int.TryParse(segments[2], out var year))
        {
            try
            {
                return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}

internal sealed class AudioFrontMatterMetadata
{
    public DateTimeOffset? Timestamp { get; set; }
    public double? DurationSeconds { get; set; }
}

/// <summary>
/// Contract for discovering note/recording audio files.
/// </summary>
public interface IAudioLibraryService
{
    Task<Result<IReadOnlyList<AudioLibraryItem>>> ListAudioFilesAsync(
        AudioLibraryScope scope,
        CancellationToken cancellationToken = default);

    Result<AudioLibraryItem> GetAudioFile(AudioLibraryScope scope, string baseName);
}
