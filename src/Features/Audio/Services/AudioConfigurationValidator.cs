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
        // Delegate to AudioOptions.IsConfigured() which knows provider-specific requirements
        return configuration.IsConfigured();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetMissingConfiguration(AudioOptions configuration)
    {
        var missing = new List<string>();

        // Check if STT provider is set
        if (string.IsNullOrWhiteSpace(configuration.SttProvider))
        {
            missing.Add("STT Provider (Speech-to-Text provider must be configured)");
            return missing.AsReadOnly();
        }

        // Delegate provider-specific validation to AudioOptions.IsConfigured()
        // and provide user-friendly messages based on provider type
        if (!configuration.IsConfigured())
        {
            missing.Add(configuration.SttProvider switch
            {
                SttProviders.BuiltInLocal => "STT Model (required for built-in local provider)",
                SttProviders.OpenAI => "STT API Key and/or Model (required for OpenAI provider)",
                SttProviders.WhisperCpp => "Binary Path and/or Model (required for whisper.cpp provider)",
                _ => $"Configuration incomplete for {configuration.SttProvider} provider"
            });
        }

        return missing.AsReadOnly();
    }
}
