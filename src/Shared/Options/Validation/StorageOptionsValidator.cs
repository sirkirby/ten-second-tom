using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options.Validation;

/// <summary>
/// Validates <see cref="StorageOptions"/> at application startup.
/// </summary>
/// <remarks>
/// This validator ensures that storage configuration is valid:
/// - MemoryDirectory must be specified and have a valid path format
/// - RetentionPolicy must be a defined enum value
/// - MaxFileSizeBytes (if specified) must be positive
/// Validation failures will prevent the application from starting with clear error messages.
/// </remarks>
public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    /// <summary>
    /// Validates the specified <see cref="StorageOptions"/> instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// A validation result indicating success or failure with an error message.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MemoryDirectory))
        {
            return ValidateOptionsResult.Fail(
                "MemoryDirectory is required. Set the 'TenSecondTom:MemoryDirectory' configuration value or the 'TenSecondTom__MemoryDirectory' environment variable.");
        }

        // Validate path format
        try
        {
            // This will throw if the path contains invalid characters
            _ = Path.GetFullPath(options.MemoryDirectory);
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(
                $"MemoryDirectory contains an invalid path format: '{options.MemoryDirectory}'. Error: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return ValidateOptionsResult.Fail(
                $"MemoryDirectory path format is not supported: '{options.MemoryDirectory}'. Error: {ex.Message}");
        }

        if (!Enum.IsDefined(options.RetentionPolicy))
        {
            return ValidateOptionsResult.Fail(
                $"Invalid retention policy '{options.RetentionPolicy}'. Valid values are: {string.Join(", ", Enum.GetNames<RetentionPolicy>())}.");
        }

        // Validate MaxFileSizeBytes if specified
        if (options.MaxFileSizeBytes.HasValue)
        {
            if (options.MaxFileSizeBytes.Value <= 0)
            {
                return ValidateOptionsResult.Fail(
                    $"MaxFileSizeBytes must be a positive number when specified. Current value: {options.MaxFileSizeBytes.Value}. Set a valid value in the 'TenSecondTom:Storage:MaxFileSizeBytes' configuration.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
