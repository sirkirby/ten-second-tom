using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Root configuration for audio recording and transcription features.
/// Supports both local (whisper.cpp) and remote (OpenAI) speech-to-text engines.
/// </summary>
/// <remarks>
/// DEPRECATED: This class is obsolete and will be removed in a future version.
/// Use <see cref="TenSecondTom.Shared.Options.AudioOptions"/> instead.
/// Access audio configuration through CQRS queries: <see cref="TenSecondTom.Features.Audio.GetAudioConfiguration"/>.
/// </remarks>
public sealed class AudioConfiguration
{
    /// <summary>
    /// Gets or sets the speech-to-text provider.
    /// Valid values: "whisper-cpp" (local, free), "openai" (cloud, requires API key).
    /// Default: "whisper-cpp" (local, free).
    /// </summary>
    public string SttProvider { get; init; } = "whisper-cpp";

    /// <summary>
    /// Gets or sets the API key for the STT provider.
    /// Required for "openai", optional for "whisper-cpp" (fallback only).
    /// Default: null (no API key).
    /// </summary>
    public string? SttApiKey { get; init; }

    /// <summary>
    /// Gets or sets whether to enable fallback to a secondary STT provider.
    /// When true, falls back to the configured fallback provider if the primary STT provider fails.
    /// Default: false (no fallback).
    /// </summary>
    public bool SttFallbackEnabled { get; init; }

    /// <summary>
    /// Gets or sets the fallback STT provider (e.g., "openai").
    /// Only used when <see cref="SttFallbackEnabled"/> is true.
    /// Default: null (no fallback provider configured).
    /// </summary>
    public string? SttFallbackProvider { get; init; }

    /// <summary>
    /// Gets or sets the API key for the fallback STT provider.
    /// Only used when <see cref="SttFallbackEnabled"/> is true.
    /// Default: null (no API key for fallback provider).
    /// </summary>
    public string? SttFallbackApiKey { get; init; }

    /// <summary>
    /// Gets or sets the binary path for the primary STT provider.
    /// Only used for local providers (e.g., whisper-cpp).
    /// Default: "whisper-cli" (Homebrew installs whisper-cpp package as 'whisper-cli' binary).
    /// </summary>
    public string SttBinaryPath { get; init; } = "whisper-cli";

    /// <summary>
    /// Gets or sets the model for the primary STT provider.
    /// For local providers: path to model file (e.g., "~/.cache/whisper/ggml-base.en.bin").
    /// For cloud providers: model name (e.g., "whisper-1").
    /// Default: "~/.cache/whisper/ggml-base.en.bin" (Base.en model: English-only, 142 MB, balanced speed/accuracy).
    /// </summary>
    public string SttModel { get; init; } = "~/.cache/whisper/ggml-base.en.bin";

    /// <summary>
    /// Gets or sets the binary path for the fallback STT provider.
    /// Only used for local fallback providers (e.g., whisper-cpp).
    /// Default: null.
    /// </summary>
    public string? SttFallbackBinaryPath { get; init; }

    /// <summary>
    /// Gets or sets the model for the fallback STT provider.
    /// For local providers: path to model file.
    /// For cloud providers: model name.
    /// Default: null.
    /// </summary>
    public string? SttFallbackModel { get; init; }

    /// <summary>
    /// Gets or sets the recorder configuration (FFmpeg settings).
    /// </summary>
    public RecorderConfiguration Recorder { get; init; } = new();

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
    /// Default: "ffmpeg" (assumes ffmpeg is available on the system PATH).
    /// </summary>
    public string FfmpegPath { get; init; } = "ffmpeg";

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
    /// Default: 300 (5 minutes).
    /// </summary>
    public int TodaySeconds { get; init; } = 300;

    /// <summary>
    /// Gets or sets the maximum duration (seconds) for open-ended 'record' command before prompting.
    /// Default: 1800 (30 minutes).
    /// </summary>
    public int RecordSeconds { get; init; } = 1800;
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
