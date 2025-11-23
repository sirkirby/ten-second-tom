using Microsoft.Extensions.Options;

namespace TenSecondTom.Shared.Options.Validation;

/// <summary>
/// Validates <see cref="SecurityOptions"/> at application startup.
/// </summary>
/// <remarks>
/// This validator ensures that security configuration values are present and valid
/// before the application starts processing requests. Validation failures will prevent
/// the application from starting and display clear error messages.
/// Security validation is critical for preventing tampering with notification actions.
/// </remarks>
public sealed class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    private const int MinimumSecretLength = 16;

    /// <summary>
    /// Validates the specified <see cref="SecurityOptions"/> instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// A validation result indicating success or failure with an error message.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        // NotificationSecret is required
        if (string.IsNullOrWhiteSpace(options.NotificationSecret))
        {
            return ValidateOptionsResult.Fail(
                "NotificationSecret is required for interactive notifications. " +
                "Set the 'TenSecondTom:Security:NotificationSecret' configuration value " +
                "or the 'TenSecondTom__Security__NotificationSecret' environment variable. " +
                "Generate a random 32+ character string for this value. " +
                "NEVER commit this secret to source control - use user secrets or environment variables.");
        }

        // NotificationSecret must be sufficiently long for security
        if (options.NotificationSecret.Length < MinimumSecretLength)
        {
            return ValidateOptionsResult.Fail(
                $"NotificationSecret must be at least {MinimumSecretLength} characters long for security. " +
                $"Current length: {options.NotificationSecret.Length}. " +
                "Generate a longer random string for this value.");
        }

        // MaxTokenAgeSeconds must be positive
        if (options.MaxTokenAgeSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"MaxTokenAgeSeconds must be positive. Current value: {options.MaxTokenAgeSeconds}. " +
                "Set a valid value in the 'TenSecondTom:Security:MaxTokenAgeSeconds' configuration.");
        }

        return ValidateOptionsResult.Success;
    }
}
