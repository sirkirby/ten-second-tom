using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Shared.Options.Validation;

/// <summary>
/// Validates <see cref="TranscribeOptions"/> at application startup.
/// </summary>
/// <remarks>
/// This validator ensures that all required transcription/STT configuration values are present
/// and valid before the application starts processing requests. Validation failures
/// will prevent the application from starting and display clear error messages.
/// </remarks>
public sealed class TranscribeOptionsValidator : IValidateOptions<TranscribeOptions>
{
    /// <summary>
    /// Validates the specified <see cref="TranscribeOptions"/> instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// A validation result indicating success or failure with an error message.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, TranscribeOptions options)
    {
        // Allow unconfigured state - ConfigurationChecker.IsConfigured() handles detection
        // Only validate structure if SttProvider is a recognized value
        if (!SttProviders.All.Contains(options.SttProvider))
        {
            // Provider not set or invalid - allow (might be unconfigured or will be caught by IsConfigured)
            return ValidateOptionsResult.Success;
        }

        // Delegate to TranscribeOptions.IsConfigured() which knows provider-specific requirements
        // (e.g., local providers don't need API keys, cloud providers do)
        if (!options.IsConfigured())
        {
            return ValidateOptionsResult.Fail(
                $"Transcription configuration incomplete for {options.SttProvider}. Run 'tom transcribe config' to configure.");
        }

        return ValidateOptionsResult.Success;
    }
}
