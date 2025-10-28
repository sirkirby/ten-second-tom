using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for Large Language Model (LLM) integration.
/// Maps to the "TenSecondTom:Llm" configuration section.
/// </summary>
/// <remarks>
/// This class follows the .NET Options Pattern for strongly-typed configuration.
/// Use with IOptions&lt;LlmOptions&gt; or IOptionsSnapshot&lt;LlmOptions&gt; in services.
///
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "Llm": {
///       "Provider": "Anthropic",
///       "ApiKey": "sk-ant-...",
///       "Model": "claude-3-5-sonnet-20241022",
///       "MaxInputTokens": 100000
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
    /// Gets or sets the LLM provider.
    /// </summary>
    /// <remarks>
    /// Valid values: <see cref="LlmProvider.OpenAI"/>, <see cref="LlmProvider.Anthropic"/>.
    /// This is a required configuration value.
    /// </remarks>
    public required LlmProvider Provider { get; init; }

    /// <summary>
    /// Gets or sets the API key for the LLM provider.
    /// </summary>
    /// <remarks>
    /// This is a sensitive value and should be stored securely using environment variables
    /// or other secure configuration providers. Never commit API keys to source control.
    /// This is a required configuration value.
    /// </remarks>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets or sets the model identifier for the LLM provider.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - OpenAI: "gpt-4", "gpt-4-turbo", "gpt-3.5-turbo"
    /// - Anthropic: "claude-3-5-sonnet-20241022", "claude-3-opus-20240229"
    /// This is a required configuration value.
    /// </remarks>
    public required string Model { get; init; }

    /// <summary>
    /// Gets or sets the maximum number of input tokens for LLM processing.
    /// </summary>
    /// <remarks>
    /// Controls how much context can be sent to the LLM in a single request.
    /// Default: 100000 tokens (approximately 75,000 words).
    /// Adjust based on your model's capabilities and cost considerations.
    /// </remarks>
    public int MaxInputTokens { get; init; } = 100000;
}
