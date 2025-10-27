using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Validates audio configuration completeness for features requiring audio setup.
/// </summary>
public interface IAudioConfigurationValidator
{
    /// <summary>
    /// Checks if audio configuration is complete and valid for use.
    /// </summary>
    /// <param name="configuration">The audio configuration to validate.</param>
    /// <returns>True if configuration is complete; otherwise false.</returns>
    /// <remarks>
    /// Validates:
    /// - STT provider is set
    /// - If using cloud provider, API key is configured
    /// - If fallback is enabled, fallback provider and key are configured
    /// </remarks>
    bool IsAudioConfigured(AudioConfiguration configuration);

    /// <summary>
    /// Gets a list of missing configuration items.
    /// </summary>
    /// <param name="configuration">The audio configuration to validate.</param>
    /// <returns>List of missing configuration items, or empty if complete.</returns>
    IReadOnlyList<string> GetMissingConfiguration(AudioConfiguration configuration);
}
