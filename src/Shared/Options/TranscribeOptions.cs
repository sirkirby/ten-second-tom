using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Transcription (Speech-to-Text) configuration options.
/// Maps to the "TenSecondTom:Transcribe" configuration section.
/// </summary>
/// <remarks>
/// Provider-specific settings (Model, ApiKey, BinaryPath) are stored under
/// Providers/{providerName}. This allows switching between providers without losing config.
///
/// Configuration example (config.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Transcribe": {
///       "SttProvider": "built-in-local",
///       "Providers": {
///         "built-in-local": { "Model": "whisper-large-v3-turbo" },
///         "openai": { "ApiKey": "sk-...", "Model": "whisper-1" },
///         "whisper-cpp": { "BinaryPath": "/path/to/whisper", "Model": "base.en" }
///       },
///       "KeepFiles": true
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Transcribe__SttProvider
/// - TenSecondTom__Transcribe__Providers__built-in-local__Model
/// - TenSecondTom__Transcribe__Providers__openai__ApiKey
/// etc.
/// </remarks>
public sealed class TranscribeOptions
{
    /// <summary>
    /// Configuration section path for Transcribe options.
    /// </summary>
    public const string SectionPath = "TenSecondTom:Transcribe";

    /// <summary>
    /// Configuration section name for Transcribe settings.
    /// </summary>
    public const string SectionName = "TenSecondTom:Transcribe";

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
