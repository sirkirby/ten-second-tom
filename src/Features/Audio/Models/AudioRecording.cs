namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents a captured voice recording with metadata.
/// Contains all information about an audio file created by the recorder.
/// </summary>
public sealed class AudioRecording
{
    /// <summary>
    /// Gets the base filename of the audio file (e.g., "note-20251020-143000.wav").
    /// </summary>
    public required string Filename { get; init; }

    /// <summary>
    /// Gets the full path to the audio file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the duration of the recording.
    /// Must be greater than 0.5 seconds.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the sample rate in Hz.
    /// Default: 16000 Hz (required for whisper.cpp compatibility).
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Gets the number of audio channels.
    /// Default: 1 (mono, required for whisper.cpp).
    /// </summary>
    public required int Channels { get; init; }

    /// <summary>
    /// Gets the audio format.
    /// </summary>
    public required AudioFormat Format { get; init; }

    /// <summary>
    /// Gets the audio encoding (e.g., "pcm_s16le" for WAV).
    /// </summary>
    public required string Encoding { get; init; }

    /// <summary>
    /// Gets the timestamp when the recording was created.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets the size of the audio file in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// Validates the audio recording meets requirements for whisper.cpp.
    /// </summary>
    /// <returns>True if valid for whisper.cpp; otherwise, false.</returns>
    public bool IsValidForWhisperCpp()
    {
        return Duration.TotalSeconds > 0.5
               && SampleRate == 16000
               && Channels == 1
               && Format == AudioFormat.Wav;
    }

    /// <summary>
    /// Validates the audio recording meets basic requirements.
    /// </summary>
    /// <returns>True if the recording is valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Filename)
               && !string.IsNullOrWhiteSpace(FilePath)
               && Duration.TotalSeconds > 0.5
               && SampleRate > 0
               && Channels > 0
               && FileSizeBytes > 0
               && !string.IsNullOrWhiteSpace(Encoding);
    }
}
