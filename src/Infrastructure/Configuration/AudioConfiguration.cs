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
    /// Configures optional silence removal and audio optimization.
    /// </summary>
    public PreprocessingConfiguration Preprocessing { get; init; } = new();

    /// <summary>
    /// Gets or sets per-command recording timeouts.
    /// </summary>
    public RecordingTimeoutsConfiguration Timeouts { get; init; } = new();
}

/// <summary>
/// FFmpeg recorder settings for audio capture.
/// Defaults optimized for laptop/omnidirectional microphones (MacBook Pro, etc).
/// </summary>
/// <remarks>
/// Microphone type recommendations:
/// - Laptop/Built-in mics: InputVolume=1.0, EnableNoiseReduction=true, EnableFrequencyFilters=true
/// - Dynamic mics (SM7B, Shure, etc): InputVolume=0.7-0.8, EnableNoiseReduction=false, EnableFrequencyFilters=true
/// - Condenser/USB mics: InputVolume=0.9, EnableNoiseReduction=false, EnableFrequencyFilters=true
/// - Professional studio setup: InputVolume=1.0, EnableNoiseReduction=false, EnableFrequencyFilters=false
/// </remarks>
public sealed class RecorderConfiguration
{
    /// <summary>
    /// Gets or sets the path to ffmpeg binary.
    /// Default: <see cref="AudioConstants.FfmpegBinaryName"/> (assumes ffmpeg is on PATH).
    /// </summary>
    public string FfmpegPath { get; init; } = AudioConstants.FfmpegBinaryName;

    /// <summary>
    /// Gets or sets the input volume multiplier (0.0 to 2.0).
    /// Default: 1.0 (100% volume - no adjustment).
    /// Adjust if you experience clipping (lower) or quiet audio (higher).
    /// Typical values: 0.7-0.8 for hot dynamic mics, 1.0-1.2 for laptop mics.
    /// </summary>
    public double InputVolume { get; init; } = 1.0;

    /// <summary>
    /// Gets or sets whether to enable noise reduction during recording.
    /// Default: true (uses FFmpeg's anlmdn adaptive noise filter).
    /// Recommended for laptop/built-in microphones. Disable for professional mics in treated rooms.
    /// </summary>
    public bool EnableNoiseReduction { get; init; } = true;

    /// <summary>
    /// Gets or sets whether to enable high-pass/low-pass filters during recording.
    /// Default: true (removes rumble below 80Hz and hiss above 8kHz).
    /// Recommended for voice recording to reduce environmental noise.
    /// Disable if you have a treated recording environment.
    /// </summary>
    public bool EnableFrequencyFilters { get; init; } = true;
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
/// Audio preprocessing configuration.
/// Settings for silence removal and audio optimization using FFmpeg filters.
/// Preprocessing happens after recording and before transcription.
/// </summary>
public sealed class PreprocessingConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable silence removal preprocessing.
    /// Default: false (disabled by default to preserve original audio).
    /// When enabled, uses FFmpeg's silenceremove filter to reduce silence from recordings.
    /// </summary>
    public bool RemoveSilence { get; init; }

    /// <summary>
    /// Gets or sets the silence detection threshold in decibels.
    /// Default: -50 dB (lower values = more aggressive silence removal).
    /// Typical values: -40dB (conservative), -50dB (balanced), -60dB (aggressive).
    /// Only used when RemoveSilence is true.
    /// </summary>
    public int SilenceThresholdDb { get; init; } = -50;

    /// <summary>
    /// Gets or sets the minimum silence duration to remove in milliseconds.
    /// Default: 500ms (don't remove pauses shorter than 0.5 seconds).
    /// Only used when RemoveSilence is true.
    /// </summary>
    public int MinimumSilenceDurationMs { get; init; } = 500;
}
