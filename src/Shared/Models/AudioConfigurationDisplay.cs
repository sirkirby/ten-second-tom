namespace TenSecondTom.Shared.Models;

/// <summary>
/// Audio recording and preprocessing configuration (display model for config show).
/// STT-related settings are now in <see cref="TranscribeConfigurationDisplay"/>.
/// </summary>
public sealed record AudioConfigurationDisplay
{
    /// <summary>
    /// Gets the audio recorder configuration
    /// </summary>
    public RecorderConfigurationDisplay Recorder { get; init; } = new();

    /// <summary>
    /// Gets the audio preprocessing configuration
    /// </summary>
    public PreprocessingConfigurationDisplay Preprocessing { get; init; } = new();
}

/// <summary>
/// Transcription (Speech-to-Text) configuration (display model for config show).
/// </summary>
public sealed record TranscribeConfigurationDisplay
{
    /// <summary>
    /// Gets the speech-to-text provider.
    /// Default: "built-in-local" (local AI via Whisper.NET).
    /// </summary>
    public string SttProvider { get; init; } = "built-in-local";

    /// <summary>
    /// Gets the API key for the STT provider (masked for display)
    /// </summary>
    public string? SttApiKey { get; init; }

    /// <summary>
    /// Gets the STT model name/path
    /// </summary>
    public string? SttModel { get; init; }

    /// <summary>
    /// Gets whether to keep audio files after transcription
    /// </summary>
    public bool KeepFiles { get; init; } = true;
}

/// <summary>
/// Audio recorder configuration (display model for config show)
/// </summary>
public sealed record RecorderConfigurationDisplay
{
    /// <summary>
    /// Gets the input volume multiplier (0.0 to 2.0)
    /// </summary>
    public double InputVolume { get; init; } = 1.0;

    /// <summary>
    /// Gets whether noise reduction is enabled during recording
    /// </summary>
    public bool EnableNoiseReduction { get; init; } = true;

    /// <summary>
    /// Gets whether frequency filters are enabled during recording
    /// </summary>
    public bool EnableFrequencyFilters { get; init; } = true;
}

/// <summary>
/// Audio preprocessing configuration (display model for config show)
/// </summary>
public sealed record PreprocessingConfigurationDisplay
{
    /// <summary>
    /// Gets whether silence removal is enabled
    /// </summary>
    public bool RemoveSilence { get; init; } = true;

    /// <summary>
    /// Gets the silence detection threshold in decibels
    /// </summary>
    public int SilenceThresholdDb { get; init; } = -50;

    /// <summary>
    /// Gets the minimum silence duration to remove in milliseconds
    /// </summary>
    public int MinimumSilenceDurationMs { get; init; } = 500;
}
