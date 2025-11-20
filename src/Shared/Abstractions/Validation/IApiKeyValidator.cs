namespace TenSecondTom.Shared.Abstractions.Validation;

/// <summary>
/// Interface for validating API keys
/// Supports both format and network validation
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validates the format of an API key using regex pattern
    /// </summary>
    Task<Models.ApiValidationResult> ValidateFormatAsync(string apiKey);

    /// <summary>
    /// Validates the API key by making a network call with retry logic
    /// </summary>
    Task<Models.ApiValidationResult> ValidateNetworkAsync(
        string apiKey,
        int maxRetries,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the LLM provider this validator is for
    /// </summary>
    Models.LlmProvider Provider { get; }
}
