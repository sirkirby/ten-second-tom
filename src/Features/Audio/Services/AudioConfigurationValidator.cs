using TenSecondTom.Features.Audio.Constants;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Validates audio configuration completeness for features requiring audio setup.
/// </summary>
public sealed class AudioConfigurationValidator : IAudioConfigurationValidator
{
    /// <inheritdoc/>
    public bool IsAudioConfigured(AudioOptions configuration)
    {
        return GetMissingConfiguration(configuration).Count == 0;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetMissingConfiguration(AudioOptions configuration)
    {
        var missing = new List<string>();

        // Check if STT provider is set (should always have a default, but validate anyway)
        if (string.IsNullOrWhiteSpace(configuration.SttProvider))
        {
            missing.Add("STT Provider (Speech-to-Text provider must be configured)");
        }
        else
        {
            // If using cloud provider, API key is required
            if (configuration.SttProvider.Equals(SttProviders.OpenAI, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(configuration.SttApiKey))
                {
                    missing.Add($"STT API Key (required for {SttProviders.OpenAI} provider)");
                }
            }
        }

        // If fallback is enabled, validate fallback configuration
        if (configuration.SttFallbackEnabled)
        {
            if (string.IsNullOrWhiteSpace(configuration.SttFallbackProvider))
            {
                missing.Add("STT Fallback Provider (required when fallback is enabled)");
            }
            else
            {
                // If fallback provider is cloud, API key is required
                if (configuration.SttFallbackProvider.Equals(SttProviders.OpenAI, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(configuration.SttFallbackApiKey))
                    {
                        missing.Add($"STT Fallback API Key (required for {SttProviders.OpenAI} fallback provider)");
                    }
                }
            }
        }

        return missing.AsReadOnly();
    }
}
