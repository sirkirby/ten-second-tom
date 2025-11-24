using System.Text;
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
        // NotificationSecret is optional (graceful degradation)
        // If not set, interactive notifications will be disabled with a warning
        // If set, it must meet minimum security requirements
        if (!string.IsNullOrWhiteSpace(options.NotificationSecret))
        {
            // NotificationSecret must be sufficiently long for security
            // Check byte length (not character count) since HMAC works with bytes
            // Unicode characters provide more entropy, so byte length is the correct measure
            var secretBytes = Encoding.UTF8.GetByteCount(options.NotificationSecret);
            if (secretBytes < MinimumSecretLength)
            {
                return ValidateOptionsResult.Fail(
                    $"NotificationSecret must be at least {MinimumSecretLength} bytes long for security. " +
                    $"Current byte length: {secretBytes}. " +
                    "Generate a longer random string (32+ characters recommended). " +
                    "Set 'TenSecondTom:Security:NotificationSecret' in user secrets or environment variables. " +
                    "NEVER commit secrets to source control.");
            }
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
