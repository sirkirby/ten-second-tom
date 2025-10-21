namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents the output of speech-to-text processing.
/// Contains the transcript and metadata about the transcription process.
/// </summary>
/// <remarks>
/// This model represents the result of processing voice input, a fundamental
/// input method alongside text. Stored in Shared because transcription results
/// are used across multiple features (Today, ThisWeek, future voice commands)
/// and represent a core application capability.
/// </remarks>
public sealed class TranscriptionResult
{
    /// <summary>
    /// Gets the reference to the source audio file (path or ID).
    /// </summary>
    public required string AudioReference { get; init; }

    /// <summary>
    /// Gets the full transcript text.
    /// Must not be null or empty.
    /// </summary>
    public required string TranscriptText { get; init; }

    /// <summary>
    /// Gets the STT engine used for transcription.
    /// </summary>
    public required SttEngine SttEngine { get; init; }

    /// <summary>
    /// Gets the model identifier (e.g., "ggml-base.en" for whisper.cpp, "whisper-1" for OpenAI).
    /// </summary>
    public string? SttModel { get; init; }

    /// <summary>
    /// Gets the confidence score if available (0.0-1.0).
    /// May not be available for all STT engines (e.g., whisper.cpp doesn't provide confidence).
    /// </summary>
    public float? ConfidenceScore { get; init; }

    /// <summary>
    /// Gets the time taken to transcribe the audio.
    /// </summary>
    public required TimeSpan ProcessingDuration { get; init; }

    /// <summary>
    /// Gets the timestamp when the transcription was completed.
    /// </summary>
    public required DateTimeOffset TranscribedAt { get; init; }

    /// <summary>
    /// Gets the number of words in the transcript.
    /// </summary>
    public required int WordCount { get; init; }

    /// <summary>
    /// Gets the detected language (if available).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transcript is empty (no words).
    /// </summary>
    public bool IsEmpty => WordCount == 0;

    /// <summary>
    /// Calculates the processing speed ratio.
    /// Higher values indicate faster processing (e.g., 10.0 means 10x real-time).
    /// </summary>
    /// <param name="audioDuration">The duration of the audio file.</param>
    /// <returns>Processing speed ratio (audio duration / processing duration).</returns>
    public double GetProcessingSpeed(TimeSpan audioDuration)
    {
        if (ProcessingDuration.TotalSeconds <= 0)
            return 0;

        return audioDuration.TotalSeconds / ProcessingDuration.TotalSeconds;
    }

    /// <summary>
    /// Validates the transcription result.
    /// </summary>
    /// <returns>True if valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(AudioReference)
               && !string.IsNullOrWhiteSpace(TranscriptText)
               && ProcessingDuration.TotalSeconds > 0
               && WordCount >= 0
               && (!ConfidenceScore.HasValue || (ConfidenceScore.Value >= 0.0f && ConfidenceScore.Value <= 1.0f));
    }
}

