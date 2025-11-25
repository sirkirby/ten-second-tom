using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Audio feature configuration options.
/// Maps to the "TenSecondTom:Audio" configuration section.
/// </summary>
/// <remarks>
/// Provider-specific settings (Model, ApiKey, BinaryPath) are stored under
/// Providers/{providerName}. This allows switching between providers without losing config.
///
/// Configuration example (config.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Audio": {
///       "SttProvider": "built-in-local",
///       "Providers": {
///         "built-in-local": { "Model": "whisper-large-v3-turbo" },
///         "openai": { "ApiKey": "sk-...", "Model": "whisper-1" },
///         "whisper-cpp": { "BinaryPath": "/path/to/whisper", "Model": "base.en" }
///       },
///       "KeepFiles": true,
///       "Recorder": {
///         "FfmpegPath": "ffmpeg",
///         "InputVolume": 1.0,
///         "EnableNoiseReduction": true,
///         "EnableFrequencyFilters": true
///       },
///       "Preprocessing": {
///         "RemoveSilence": false,
///         "SilenceThresholdDb": -50,
///         "MinimumSilenceDurationMs": 500
///       },
///       "Timeouts": {
///         "TodaySeconds": 300,
///         "RecordSeconds": 1800
///       }
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Audio__SttProvider
/// - TenSecondTom__Audio__Providers__built-in-local__Model
/// - TenSecondTom__Audio__Providers__openai__ApiKey
/// - TenSecondTom__Audio__Recorder__InputVolume
/// etc.
/// </remarks>
public sealed class AudioOptions
{
    /// <summary>
    /// Configuration section path for Audio options.
    /// </summary>
    public const string SectionPath = "TenSecondTom:Audio";

    /// <summary>
    /// Configuration section name for Audio settings.
    /// </summary>
    public const string SectionName = "TenSecondTom:Audio";

    /// <summary>
    /// Gets or sets the speech-to-text provider.
    /// Valid values: "built-in-local", "whisper-cpp", "openai".
    /// Default: "built-in-local".
    /// </summary>
    public string SttProvider { get; set; } = SttProviders.BuiltInLocal;

    /// <summary>
    /// Gets provider-specific configuration.
    /// Key is the provider name (e.g., "built-in-local", "whisper-cpp", "openai").
    /// Value contains provider-specific settings (Model, ApiKey, BinaryPath).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Providers { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to keep audio files after transcription (for note entries).
    /// Default: true.
    /// Note: Recording command always keeps files regardless of this setting.
    /// </summary>
    public bool KeepFiles { get; init; } = true;

    /// <summary>
    /// Gets or sets the recorder configuration (FFmpeg settings).
    /// </summary>
    public RecorderOptions Recorder { get; init; } = new();

    /// <summary>
    /// Gets or sets the audio preprocessing configuration.
    /// Configures optional silence removal and audio optimization.
    /// </summary>
    public PreprocessingOptions Preprocessing { get; init; } = new();

    /// <summary>
    /// Gets or sets per-command recording timeouts.
    /// </summary>
    public RecordingTimeoutsOptions Timeouts { get; init; } = new();

    #region Provider Config Accessors

    /// <summary>
    /// Gets the model for a specific STT provider from the Providers dictionary.
    /// </summary>
    public string? GetSttModel(string? provider = null)
    {
        var targetProvider = provider ?? SttProvider;

        if (Providers.TryGetValue(targetProvider, out var config) &&
            config.TryGetValue("Model", out var model) &&
            !string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        return null;
    }

    /// <summary>
    /// Gets the API key for a specific STT provider from the Providers dictionary.
    /// </summary>
    public string? GetSttApiKey(string? provider = null)
    {
        var targetProvider = provider ?? SttProvider;

        if (Providers.TryGetValue(targetProvider, out var config) &&
            config.TryGetValue("ApiKey", out var apiKey) &&
            !string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        return null;
    }

    /// <summary>
    /// Gets the binary path for a specific STT provider from the Providers dictionary.
    /// </summary>
    public string? GetSttBinaryPath(string? provider = null)
    {
        var targetProvider = provider ?? SttProvider;

        if (Providers.TryGetValue(targetProvider, out var config) &&
            config.TryGetValue("BinaryPath", out var binaryPath) &&
            !string.IsNullOrWhiteSpace(binaryPath))
        {
            return binaryPath;
        }

        return null;
    }

    /// <summary>
    /// Sets a provider-specific configuration value.
    /// </summary>
    public void SetSttProviderConfig(string provider, string key, string? value)
    {
        if (!Providers.TryGetValue(provider, out var config))
        {
            config = new Dictionary<string, string>();
            Providers[provider] = config;
        }

        if (string.IsNullOrEmpty(value))
        {
            config.Remove(key);
        }
        else
        {
            config[key] = value;
        }
    }

    /// <summary>
    /// Determines whether the current provider's STT configuration is complete and valid.
    /// </summary>
    /// <returns>True if the STT provider is properly configured.</returns>
    public bool IsConfigured()
    {
        var model = GetSttModel();

        // Built-in local provider needs a model
        if (SttProvider == SttProviders.BuiltInLocal)
        {
            return !string.IsNullOrWhiteSpace(model);
        }

        // OpenAI needs both API key and model
        if (SttProvider == SttProviders.OpenAI)
        {
            var apiKey = GetSttApiKey();
            return !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(model);
        }

        // WhisperCpp (Whisper.NET) only needs model path - no external binary required
        if (SttProvider == SttProviders.WhisperCpp)
        {
            return !string.IsNullOrWhiteSpace(model);
        }

        return false;
    }

    #endregion
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
public sealed class RecorderOptions
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
public sealed class RecordingTimeoutsOptions
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
public sealed class PreprocessingOptions
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
