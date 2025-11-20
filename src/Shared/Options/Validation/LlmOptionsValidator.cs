using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options.Validation;

/// <summary>
/// Validates <see cref="LlmOptions"/> at application startup.
/// </summary>
/// <remarks>
/// This validator ensures that all required LLM configuration values are present
/// and valid before the application starts processing requests. Validation failures
/// will prevent the application from starting and display clear error messages.
/// </remarks>
public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    /// <summary>
    /// Validates the specified <see cref="LlmOptions"/> instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// A validation result indicating success or failure with an error message.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        // Allow unconfigured state - ConfigurationChecker.IsConfigured() handles detection
        // Only validate structure if Provider is configured (indicates intentional configuration)
        if (!Enum.IsDefined(options.Provider))
        {
            // Provider not set or invalid - allow (might be unconfigured or will be caught by IsConfigured)
            return ValidateOptionsResult.Success;
        }

        // If Provider is set, validate related fields are consistent
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail(
                "LLM API key is required when Provider is configured. Set the 'TenSecondTom:Llm:ApiKey' configuration value or the 'TenSecondTom__Llm__ApiKey' environment variable.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            return ValidateOptionsResult.Fail(
                "LLM model is required when Provider is configured. Set the 'TenSecondTom:Llm:Model' configuration value or the 'TenSecondTom__Llm__Model' environment variable.");
        }

        if (options.MaxInputTokens.HasValue && options.MaxInputTokens <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"MaxInputTokens must be a positive number. Current value: {options.MaxInputTokens}. Set a valid value in the 'TenSecondTom:Llm:MaxInputTokens' configuration.");
        }

        return ValidateOptionsResult.Success;
    }
}
