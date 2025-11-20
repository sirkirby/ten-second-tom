namespace TenSecondTom.Shared.Models;

/// <summary>
/// LLM provider configuration model.
/// Returned as a DTO from ConfigureLlm and GetSetupConfiguration queries.
/// </summary>
public sealed record LlmConfiguration
{
    /// <summary>
    /// Gets the selected LLM provider
    /// </summary>
    public LlmProvider Provider { get; init; }

    /// <summary>
    /// Gets the API key for the provider
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the model to use for chat/text generation
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the maximum number of input tokens to send to the LLM.
    /// If null, uses provider-specific defaults (50K for OpenAI, 80K for Anthropic).
    /// This limit helps control costs and ensures inputs fit within context windows.
    /// </summary>
    public int? MaxInputTokens { get; init; }
}
