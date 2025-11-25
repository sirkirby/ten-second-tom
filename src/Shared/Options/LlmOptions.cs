using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for Large Language Model (LLM) integration.
/// Maps to the "TenSecondTom:Llm" configuration section (VSA-compliant flat structure).
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;LlmOptions&gt; or IOptionsSnapshot&lt;LlmOptions&gt; in services.
///
/// Provider-specific settings (Model, ApiKey, MaxInputTokens, BaseUrl) are stored under
/// Providers/{providerName}. This allows switching between providers without losing config.
///
/// Configuration example (config.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Llm": {
///       "Provider": "OpenAI",
///       "Providers": {
///         "OpenAI": { "ApiKey": "sk-...", "Model": "gpt-4o", "MaxInputTokens": "50000" },
///         "Anthropic": { "ApiKey": "sk-ant-...", "Model": "claude-3-5-sonnet-20241022" },
///         "LocalOpenAiCompatible": { "BaseUrl": "http://127.0.0.1:8080/v1", "Model": "qwen2.5" }
///       }
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Llm__Provider
/// - TenSecondTom__Llm__Providers__OpenAI__ApiKey
/// - TenSecondTom__Llm__Providers__OpenAI__Model
/// </remarks>
public sealed class LlmOptions
{
    /// <summary>
    /// The configuration section name for LLM settings.
    /// </summary>
    public const string SectionName = "TenSecondTom:Llm";

    /// <summary>
    /// Configuration section path for LLM feature settings (alias for SectionName).
    /// </summary>
    public const string SectionPath = "TenSecondTom:Llm";

    /// <summary>
    /// Gets the selected LLM provider.
    /// </summary>
    public LlmProvider Provider { get; set; }

    /// <summary>
    /// Gets the ordered list of providers to attempt for fallback.
    /// Defaults to just the configured Provider if not specified.
    /// </summary>
    public List<string> FallbackOrder { get; set; } = new();

    /// <summary>
    /// Gets provider-specific configuration.
    /// Key is the provider name (e.g., "OpenAI", "Anthropic", "LocalOpenAiCompatible", "BuiltInLocal").
    /// Value contains provider-specific settings (Model, ApiKey, MaxInputTokens, BaseUrl).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Providers { get; set; } = new();

    #region Provider Config Accessors

    /// <summary>
    /// Gets the API key for a provider from the Providers dictionary.
    /// </summary>
    public string? GetApiKey(LlmProvider? provider = null)
    {
        var targetProvider = provider ?? Provider;
        var providerName = targetProvider.ToString();

        if (Providers.TryGetValue(providerName, out var config) &&
            config.TryGetValue("ApiKey", out var apiKey) &&
            !string.IsNullOrWhiteSpace(apiKey))
        {
            return apiKey;
        }

        return null;
    }

    /// <summary>
    /// Gets the model for a provider from the Providers dictionary.
    /// </summary>
    public string? GetModel(LlmProvider? provider = null)
    {
        var targetProvider = provider ?? Provider;
        var providerName = targetProvider.ToString();

        if (Providers.TryGetValue(providerName, out var config) &&
            config.TryGetValue("Model", out var model) &&
            !string.IsNullOrWhiteSpace(model))
        {
            return model;
        }

        return null;
    }

    /// <summary>
    /// Gets the max input tokens for a provider from the Providers dictionary.
    /// </summary>
    public int? GetMaxInputTokens(LlmProvider? provider = null)
    {
        var targetProvider = provider ?? Provider;
        var providerName = targetProvider.ToString();

        if (Providers.TryGetValue(providerName, out var config) &&
            config.TryGetValue("MaxInputTokens", out var maxTokensStr) &&
            int.TryParse(maxTokensStr, out var maxTokens))
        {
            return maxTokens;
        }

        return null;
    }

    /// <summary>
    /// Gets the base URL for a provider (primarily for LocalOpenAiCompatible).
    /// </summary>
    public string? GetBaseUrl(LlmProvider? provider = null)
    {
        var targetProvider = provider ?? Provider;
        var providerName = targetProvider.ToString();

        if (Providers.TryGetValue(providerName, out var config) &&
            config.TryGetValue("BaseUrl", out var baseUrl) &&
            !string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        return null;
    }

    /// <summary>
    /// Sets a provider-specific configuration value.
    /// </summary>
    public void SetProviderConfig(LlmProvider provider, string key, string? value)
    {
        var providerName = provider.ToString();

        if (!Providers.TryGetValue(providerName, out var config))
        {
            config = new Dictionary<string, string>();
            Providers[providerName] = config;
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

    #endregion

    /// <summary>
    /// Determines whether the current provider's LLM configuration is complete and valid.
    /// </summary>
    /// <returns>True if Model is configured; ApiKey required for cloud providers.</returns>
    public bool IsConfigured()
    {
        var model = GetModel();
        var apiKey = GetApiKey();

        // Local providers don't need API keys
        if (Provider == LlmProvider.LocalOpenAiCompatible || Provider == LlmProvider.BuiltInLocal)
        {
            return !string.IsNullOrWhiteSpace(model);
        }

        // Cloud providers need both API key and model
        return !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(model);
    }
}
