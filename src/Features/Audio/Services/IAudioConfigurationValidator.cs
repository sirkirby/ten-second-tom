using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Validates transcription (STT) configuration completeness for features requiring transcription setup.
/// </summary>
public interface IAudioConfigurationValidator
{
    /// <summary>
    /// Checks if transcription configuration is complete and valid for use.
    /// </summary>
    /// <param name="configuration">The transcription configuration to validate.</param>
    /// <returns>True if configuration is complete; otherwise false.</returns>
    /// <remarks>
    /// Validates:
    /// - STT provider is set
    /// - If using cloud provider, API key is configured
    /// - If fallback is enabled, fallback provider and key are configured
    /// </remarks>
    bool IsAudioConfigured(TranscribeOptions configuration);

    /// <summary>
    /// Gets a list of missing configuration items.
    /// </summary>
    /// <param name="configuration">The transcription configuration to validate.</param>
    /// <returns>List of missing configuration items, or empty if complete.</returns>
    IReadOnlyList<string> GetMissingConfiguration(TranscribeOptions configuration);
}
