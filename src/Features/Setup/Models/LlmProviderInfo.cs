namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Represents configuration information for an LLM provider
/// </summary>
public sealed record LlmProviderInfo
{
    /// <summary>
    /// Gets the LLM provider
    /// </summary>
    public required LlmProvider Provider { get; init; }

    /// <summary>
    /// Gets the display name for the provider
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the description of the provider
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the API key for this provider
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the regex pattern for validating API keys
    /// </summary>
    public required string ApiKeyPattern { get; init; }

    /// <summary>
    /// Gets the default model for this provider
    /// </summary>
    public string? DefaultModel { get; init; }

    /// <summary>
    /// Gets whether this provider is fully configured
    /// </summary>
    public bool IsConfigured { get; init; }

    /// <summary>
    /// Gets the timestamp of the last successful validation
    /// </summary>
    public DateTime? LastValidated { get; init; }

    /// <summary>
    /// Creates provider info for OpenAI
    /// </summary>
    public static LlmProviderInfo CreateOpenAI(string? apiKey = null) => new()
    {
        Provider = LlmProvider.OpenAI,
        DisplayName = "OpenAI",
        Description = "GPT-4, GPT-3.5",
        ApiKey = apiKey,
        ApiKeyPattern = @"^sk-[a-zA-Z0-9]{48,}$",
        DefaultModel = "gpt-4",
        IsConfigured = !string.IsNullOrEmpty(apiKey)
    };

    /// <summary>
    /// Creates provider info for Anthropic
    /// </summary>
    public static LlmProviderInfo CreateAnthropic(string? apiKey = null) => new()
    {
        Provider = LlmProvider.Anthropic,
        DisplayName = "Anthropic",
        Description = "Claude 3.5",
        ApiKey = apiKey,
        ApiKeyPattern = @"^sk-ant-[a-zA-Z0-9\-]{32,}$",
        DefaultModel = "claude-3-5-sonnet-20241022",
        IsConfigured = !string.IsNullOrEmpty(apiKey)
    };

    /// <summary>
    /// Validates if the API key matches the expected pattern
    /// </summary>
    public bool ValidateApiKeyFormat(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(apiKey, ApiKeyPattern);
    }

    /// <summary>
    /// Updates with a new API key
    /// </summary>
    public LlmProviderInfo WithApiKey(string apiKey) => this with
    {
        ApiKey = apiKey,
        IsConfigured = ValidateApiKeyFormat(apiKey),
        LastValidated = null // Reset validation timestamp
    };

    /// <summary>
    /// Marks as validated
    /// </summary>
    public LlmProviderInfo MarkAsValidated() => this with
    {
        LastValidated = DateTime.UtcNow
    };
}

/// <summary>
/// Supported LLM providers
/// </summary>
public enum LlmProvider
{
    /// <summary>
    /// OpenAI (GPT models)
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic (Claude models)
    /// </summary>
    Anthropic
}
