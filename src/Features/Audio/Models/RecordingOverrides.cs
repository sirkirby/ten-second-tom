namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Contains optional overrides for audio recording settings.
/// When specified, these values override the configured defaults for a single recording session.
/// </summary>
public sealed record RecordingOverrides
{
    /// <summary>
    /// Gets the input volume multiplier override (0.0 to 2.0).
    /// When null, uses the configured default.
    /// </summary>
    public double? InputVolume { get; init; }

    /// <summary>
    /// Gets the noise reduction override.
    /// When null, uses the configured default.
    /// </summary>
    public bool? EnableNoiseReduction { get; init; }

    /// <summary>
    /// Gets the frequency filters override.
    /// When null, uses the configured default.
    /// </summary>
    public bool? EnableFrequencyFilters { get; init; }

    /// <summary>
    /// Gets whether any recording overrides are specified.
    /// </summary>
    public bool HasOverrides =>
        InputVolume.HasValue ||
        EnableNoiseReduction.HasValue ||
        EnableFrequencyFilters.HasValue;
}

/// <summary>
/// Contains optional overrides for audio preprocessing settings.
/// When specified, these values override the configured defaults for a single preprocessing operation.
/// </summary>
public sealed record PreprocessingOverrides
{
    /// <summary>
    /// Gets the silence removal override.
    /// When null, uses the configured default.
    /// </summary>
    public bool? RemoveSilence { get; init; }

    /// <summary>
    /// Gets the silence threshold in dB override (-60 to -40).
    /// When null, uses the configured default.
    /// </summary>
    public int? SilenceThresholdDb { get; init; }

    /// <summary>
    /// Gets the minimum silence duration in milliseconds override (100 to 2000).
    /// When null, uses the configured default.
    /// </summary>
    public int? MinSilenceDurationMs { get; init; }

    /// <summary>
    /// Gets whether any preprocessing overrides are specified.
    /// </summary>
    public bool HasOverrides =>
        RemoveSilence.HasValue ||
        SilenceThresholdDb.HasValue ||
        MinSilenceDurationMs.HasValue;
}
