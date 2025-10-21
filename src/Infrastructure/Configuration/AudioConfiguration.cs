using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Root configuration for audio recording and transcription features.
/// Supports both local (whisper.cpp) and remote (OpenAI) speech-to-text engines.
/// </summary>
public sealed class AudioConfiguration
{
    /// <summary>
    /// Gets or sets the preferred STT engine selection strategy.
    /// Valid values: "auto", "local", "openai".
    /// Default: "auto" (try local first, fallback to OpenAI).
    /// </summary>
    public string PreferredStt { get; init; } = "auto";

    /// <summary>
    /// Gets or sets the recorder configuration (FFmpeg settings).
    /// </summary>
    public RecorderConfiguration Recorder { get; init; } = new();

    /// <summary>
    /// Gets or sets the local whisper.cpp configuration.
    /// </summary>
    public LocalWhisperConfiguration LocalWhisper { get; init; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to keep audio files after transcription (for note entries).
    /// Default: true.
    /// Note: Recording command always keeps files regardless of this setting.
    /// </summary>
    public bool KeepFiles { get; init; } = true;

    /// <summary>
    /// Gets or sets the audio preprocessing configuration.
    /// Currently forward-compatible only (not implemented in MVP).
    /// </summary>
    public PreprocessingConfiguration Preprocessing { get; init; } = new();

    /// <summary>
    /// Gets or sets per-command recording timeouts.
    /// </summary>
    public RecordingTimeoutsConfiguration Timeouts { get; init; } = new();
}

/// <summary>
/// FFmpeg recorder settings for audio capture.
/// </summary>
public sealed class RecorderConfiguration
{
    /// <summary>
    /// Gets or sets the path to ffmpeg binary.
    /// Default: <see cref="AudioConstants.FfmpegBinaryName"/> (assumes ffmpeg is on PATH).
    /// </summary>
    public string FfmpegPath { get; init; } = AudioConstants.FfmpegBinaryName;
}

/// <summary>
/// Per-command recording timeout configuration.
/// Defines maximum duration before prompting user to continue recording.
/// </summary>
public sealed class RecordingTimeoutsConfiguration
{
    /// <summary>
    /// Gets or sets the maximum duration (seconds) for 'today --voice' before prompting to continue.
    /// Default: 180 (3 minutes).
    /// </summary>
    public int TodaySeconds { get; init; } = 180;

    /// <summary>
    /// Gets or sets the maximum duration (seconds) for open-ended 'record' command before prompting.
    /// Default: 900 (15 minutes).
    /// </summary>
    public int RecordSeconds { get; init; } = 900;
}

/// <summary>
/// Local whisper.cpp configuration for offline speech-to-text.
/// </summary>
public sealed class LocalWhisperConfiguration
{
    /// <summary>
    /// Gets or sets the path to whisper-cli binary.
    /// Default: <see cref="AudioConstants.WhisperCliBinaryName"/> (assumes whisper-cli is on PATH via Homebrew).
    /// Note: Homebrew installs the binary as 'whisper-cli', not 'whisper-cpp'.
    /// </summary>
    public string BinaryPath { get; init; } = AudioConstants.WhisperCliBinaryName;

    /// <summary>
    /// Gets or sets the path to GGML model file (e.g., ggml-base.en.bin).
    /// Default: <see cref="AudioConstants.DefaultWhisperModelPath"/> (~/.cache/whisper/ggml-base.en.bin, base.en model, 142 MB)
    /// Download from: https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin
    /// Users just need to download the model to the default location - no configuration needed.
    /// </summary>
    public string ModelPath { get; init; } = AudioConstants.DefaultWhisperModelPath;
}

/// <summary>
/// Audio preprocessing configuration (future enhancement).
/// Settings for silence removal and audio optimization.
/// Note: Not implemented in MVP - forward-compatible placeholder.
/// </summary>
public sealed class PreprocessingConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable silence removal preprocessing.
    /// Default: false (disabled for MVP).
    /// </summary>
    public bool RemoveSilence { get; init; }

    /// <summary>
    /// Gets or sets the silence detection threshold in decibels.
    /// Default: -40 dB (lower values = more aggressive silence removal).
    /// Only used when RemoveSilence is true.
    /// </summary>
    public int SilenceThresholdDb { get; init; } = -40;

    /// <summary>
    /// Gets or sets the minimum silence duration to remove in milliseconds.
    /// Default: 500ms (don't remove pauses shorter than 0.5 seconds).
    /// Only used when RemoveSilence is true.
    /// </summary>
    public int MinimumSilenceDurationMs { get; init; } = 500;
}
