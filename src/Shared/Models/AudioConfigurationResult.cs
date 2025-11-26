using TenSecondTom.Shared.Options;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Result of audio configuration operation.
/// Contains AudioOptions (recording and preprocessing settings).
/// </summary>
/// <remarks>
/// STT/transcription configuration is handled separately via /transcribe config.
/// </remarks>
public sealed record AudioConfigurationResult
{
    /// <summary>
    /// Audio recording and preprocessing configuration.
    /// </summary>
    public required AudioOptions Audio { get; init; }
}
