using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Validates transcription (STT) configuration completeness for features requiring transcription setup.
/// </summary>
public sealed class AudioConfigurationValidator : IAudioConfigurationValidator
{
    /// <inheritdoc/>
    public bool IsAudioConfigured(TranscribeOptions configuration)
    {
        // Delegate to TranscribeOptions.IsConfigured() which knows provider-specific requirements
        return configuration.IsConfigured();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetMissingConfiguration(TranscribeOptions configuration)
    {
        var missing = new List<string>();

        // Check if STT provider is set
        if (string.IsNullOrWhiteSpace(configuration.SttProvider))
        {
            missing.Add("STT Provider (Speech-to-Text provider must be configured)");
            return missing.AsReadOnly();
        }

        // Delegate provider-specific validation to TranscribeOptions.IsConfigured()
        // and provide user-friendly messages based on provider type
        if (!configuration.IsConfigured())
        {
            missing.Add(configuration.SttProvider switch
            {
                SttProviders.BuiltInLocal => "STT Model (required for built-in local provider)",
                SttProviders.OpenAI => "STT API Key and/or Model (required for OpenAI provider)",
                SttProviders.WhisperCpp => "Model path (required for Whisper.NET provider)",
                _ => $"Configuration incomplete for {configuration.SttProvider} provider"
            });
        }

        return missing.AsReadOnly();
    }
}
