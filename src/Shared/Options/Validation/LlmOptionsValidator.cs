using Microsoft.Extensions.Options;

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

        // Delegate to LlmOptions.IsConfigured() which knows provider-specific requirements
        // (e.g., local providers don't need API keys, cloud providers do)
        if (!options.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                $"LLM configuration incomplete for {options.Provider}. Run 'tom llm' to configure.");
        }

        // Validate MaxInputTokens if set (use accessor for provider-specific config)
        var maxTokens = options.GetMaxInputTokens();
        if (maxTokens.HasValue && maxTokens <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"MaxInputTokens must be a positive number. Current value: {maxTokens}.");
        }

        return ValidateOptionsResult.Success;
    }
}
