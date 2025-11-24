using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace TenSecondTom.Shared.Options.Validation;

/// <summary>
/// Validates <see cref="NotificationOptions"/> at application startup.
/// </summary>
/// <remarks>
/// This validator ensures that notification configuration values are valid
/// before the application starts processing requests. Validation failures
/// will prevent the application from starting and display clear error messages.
/// </remarks>
public sealed class NotificationOptionsValidator : IValidateOptions<NotificationOptions>
{
    /// <summary>
    /// Validates the specified <see cref="NotificationOptions"/> instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// A validation result indicating success or failure with an error message.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, NotificationOptions options)
    {
        // DefaultTimeoutSeconds must be non-negative
        if (options.DefaultTimeoutSeconds < 0)
        {
            return ValidateOptionsResult.Fail(
                $"DefaultTimeoutSeconds must be non-negative. Current value: {options.DefaultTimeoutSeconds}. " +
                "Set a valid value in the 'TenSecondTom:Notifications:DefaultTimeoutSeconds' configuration.");
        }

        // DefaultPriority must be a valid enum value
        if (!Enum.IsDefined(options.DefaultPriority))
        {
            return ValidateOptionsResult.Fail(
                $"DefaultPriority must be a valid NotificationPriority value (Low, Normal, High, Critical). " +
                $"Current value: {options.DefaultPriority}. " +
                "Set a valid value in the 'TenSecondTom:Notifications:DefaultPriority' configuration.");
        }

        if (!string.IsNullOrWhiteSpace(options.ExtensionDirectory))
        {
            string extensionDir;
            try
            {
                extensionDir = Path.GetFullPath(options.ExtensionDirectory);
            }
            catch (Exception ex)
            {
                return ValidateOptionsResult.Fail(
                    "ExtensionDirectory is not a valid path. " +
                    $"Value: '{options.ExtensionDirectory}'. Error: {ex.Message}. " +
                    "Update the 'TenSecondTom:Notifications:ExtensionDirectory' configuration.");
            }

            if (!Directory.Exists(extensionDir))
            {
                return ValidateOptionsResult.Fail(
                    "ExtensionDirectory does not exist. " +
                    $"Resolved path: '{extensionDir}'. " +
                    "Ensure the macOS extension bundle exists or remove the override 'TenSecondTom:Notifications:ExtensionDirectory'.");
            }

            var notifierPath = Path.Combine(extensionDir, "Contents", "MacOS", "notifier");
            if (!File.Exists(notifierPath))
            {
                return ValidateOptionsResult.Fail(
                    "ExtensionDirectory must contain the native notifier binary at 'Contents/MacOS/notifier'. " +
                    $"Expected path: '{notifierPath}'. Rebuild the extension with 'make extensions'.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
