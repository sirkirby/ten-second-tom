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
/// Configuration example (config.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Llm": {
///       "Provider": "OpenAI",
///       "ApiKey": "sk-...",
///       "Model": "gpt-4o",
///       "MaxInputTokens": 50000
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__Llm__Provider
/// - TenSecondTom__Llm__ApiKey
/// - TenSecondTom__Llm__Model
/// - TenSecondTom__Llm__MaxInputTokens
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
    /// Gets the API key for the provider.
    /// </summary>
    /// <remarks>
    /// This is a sensitive value and should be stored securely using environment variables
    /// or other secure configuration providers. Never commit API keys to source control.
    /// </remarks>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets the model to use for chat/text generation.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - OpenAI: "gpt-4", "gpt-4-turbo", "gpt-3.5-turbo"
    /// - Anthropic: "claude-3-5-sonnet-20241022", "claude-3-opus-20240229"
    /// </remarks>
    public string? Model { get; set; }

    /// <summary>
    /// Gets the maximum number of input tokens to send to the LLM.
    /// If null, uses provider-specific defaults (50K for OpenAI, 80K for Anthropic).
    /// This limit helps control costs and ensures inputs fit within context windows.
    /// </summary>
    public int? MaxInputTokens { get; set; }

    /// <summary>
    /// Gets the ordered list of providers to attempt for fallback.
    /// Defaults to just the configured Provider if not specified.
    /// </summary>
    public List<string> FallbackOrder { get; set; } = new();

    /// <summary>
    /// Gets provider-specific configuration overrides.
    /// Key is the provider name (e.g., "LocalOpenAiCompatible").
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Providers { get; set; } = new();

    /// <summary>
    /// Determines whether the LLM configuration is complete and valid.
    /// </summary>
    /// <returns>True if ApiKey and Model are both configured; otherwise false.</returns>
    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(Model);
    }
}
