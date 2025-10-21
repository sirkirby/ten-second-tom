using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Today.Models;

/// <summary>
/// Represents a daily entry created from voice input.
/// Extends <see cref="DailyEntry"/> with voice-specific metadata.
/// </summary>
public sealed record VoiceNoteEntry : DailyEntry
{
    /// <summary>
    /// Gets or sets the name of the audio file (e.g., "note-20251020-143000.wav").
    /// This can be updated if the file is renamed after initial creation.
    /// </summary>
    public required string AudioFilename { get; set; }

    /// <summary>
    /// Gets the duration of the audio recording.
    /// Should match the duration from the <see cref="AudioRecording"/>.
    /// </summary>
    public required TimeSpan AudioDuration { get; init; }

    /// <summary>
    /// Gets the full transcript text from speech-to-text.
    /// Should match the <see cref="MemoryEntry.UserInput"/> property.
    /// </summary>
    public required string TranscriptText { get; init; }

    /// <summary>
    /// Gets the STT engine used for transcription.
    /// </summary>
    public required SttEngine SttEngine { get; init; }

    /// <summary>
    /// Gets the STT model identifier (e.g., "ggml-base.en", "whisper-1").
    /// </summary>
    public string? SttModel { get; init; }

    /// <summary>
    /// Validates that the voice note entry is internally consistent.
    /// </summary>
    /// <returns>True if valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(AudioFilename)
               && AudioDuration.TotalSeconds > 0
               && !string.IsNullOrWhiteSpace(TranscriptText)
               && TranscriptText == UserInput; // Transcript must match user input
    }
}
