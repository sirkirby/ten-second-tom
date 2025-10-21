using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Command to record audio from the system microphone using FFmpeg.
/// Recording continues until the user presses Enter, a configurable timeout is reached,
/// or cancellation is requested. On timeout, the UX prompts: "timeout reached — press any key to continue or Enter to stop".
/// </summary>
public sealed record RecordAudioCommand : IRequest<Result<AudioRecording>>
{
    /// <summary>
    /// Gets the output file path where the recording will be saved.
    /// Must be a valid file path in a writable directory.
    /// File will be created in WAV format (16kHz, mono, PCM s16le).
    /// </summary>
    public required string OutputPath { get; init; }
    
    /// <summary>
    /// Gets the optional callback invoked when recording starts successfully.
    /// Provides the file path being recorded to.
    /// </summary>
    public Action<string>? OnRecordingStarted { get; init; }
    
    /// <summary>
    /// Gets the optional callback for periodic recording progress updates.
    /// Provides the current recording duration.
    /// Called approximately every second during recording.
    /// </summary>
    public Action<TimeSpan>? OnProgressUpdate { get; init; }

    /// <summary>
    /// Optional maximum recording duration. When reached, the recorder prompts to continue or stop.
    /// If null, use per-command default from configuration.
    /// </summary>
    public TimeSpan? MaxDuration { get; init; }
}

/// <summary>
/// Represents metadata about a completed audio recording.
/// </summary>
public sealed record AudioRecording
{
    /// <summary>
    /// Gets the base filename of the recording (without path).
    /// Example: "note-20251020-143000.wav"
    /// </summary>
    public required string Filename { get; init; }
    
    /// <summary>
    /// Gets the full file system path to the audio file.
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// Gets the total duration of the recording.
    /// </summary>
    public required TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Gets the sample rate in Hz.
    /// Always 16000 for whisper.cpp compatibility.
    /// </summary>
    public required int SampleRate { get; init; }
    
    /// <summary>
    /// Gets the number of audio channels.
    /// Always 1 (mono) for whisper.cpp compatibility.
    /// </summary>
    public required int Channels { get; init; }
    
    /// <summary>
    /// Gets the audio format.
    /// </summary>
    public required AudioFormat Format { get; init; }
    
    /// <summary>
    /// Gets the audio encoding type.
    /// Example: "pcm_s16le" for PCM 16-bit little-endian.
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
}

/// <summary>
/// Supported audio formats.
/// </summary>
public enum AudioFormat
{
    /// <summary>
    /// WAV format (required for whisper.cpp).
    /// </summary>
    Wav,
    
    /// <summary>
    /// MP3 format (supported by OpenAI STT).
    /// </summary>
    Mp3,
    
    /// <summary>
    /// M4A format (supported by OpenAI STT).
    /// </summary>
    M4a
}

