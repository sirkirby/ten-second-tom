namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents an archived recording in the recording/ directory for future processing.
/// Created by 'tom record' command and persisted permanently.
/// </summary>
public sealed class StoredRecording
{
    /// <summary>
    /// Gets the full path to the audio file.
    /// Must be in the recording/ subdirectory.
    /// </summary>
    public required string AudioFilePath { get; init; }

    /// <summary>
    /// Gets the full path to the transcription text file.
    /// Must be in the recording/ subdirectory.
    /// </summary>
    public required string TranscriptionFilePath { get; init; }

    /// <summary>
    /// Gets the original recording timestamp.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets the audio duration.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the audio file size in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets the number of words in the transcription.
    /// </summary>
    public required int TranscriptionWordCount { get; init; }

    /// <summary>
    /// Gets the STT engine used for transcription.
    /// </summary>
    public required SttEngine SttEngine { get; init; }

    /// <summary>
    /// Gets the model used for transcription (if available).
    /// </summary>
    public string? SttModel { get; init; }

    /// <summary>
    /// Validates the stored recording.
    /// </summary>
    /// <returns>True if valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(AudioFilePath)
               && !string.IsNullOrWhiteSpace(TranscriptionFilePath)
               && AudioFilePath.Contains("/recording/")
               && TranscriptionFilePath.Contains("/recording/")
               && Duration.TotalSeconds > 0
               && FileSizeBytes > 0
               && TranscriptionWordCount >= 0;
    }

    /// <summary>
    /// Checks if the stored recording files follow the expected naming pattern.
    /// Expected pattern: recording-YYYYMMdd-HHmmss.*
    /// </summary>
    /// <returns>True if naming is valid; otherwise, false.</returns>
    public bool HasValidNaming()
    {
        var audioFileName = Path.GetFileName(AudioFilePath);
        var transcriptionFileName = Path.GetFileName(TranscriptionFilePath);

        return audioFileName.StartsWith("recording-", StringComparison.OrdinalIgnoreCase)
               && transcriptionFileName.StartsWith("recording-", StringComparison.OrdinalIgnoreCase);
    }
}
