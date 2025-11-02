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
        // Validate RootDirectory (with backward compatibility for MemoryDirectory)
        string? rootDirectory = options.RootDirectory;

        // Backward compatibility: fall back to MemoryDirectory if RootDirectory not set
#pragma warning disable CS0618 // Type or member is obsolete
        if (string.IsNullOrWhiteSpace(rootDirectory) && !string.IsNullOrWhiteSpace(options.MemoryDirectory))
        {
            rootDirectory = options.MemoryDirectory;
        }
#pragma warning restore CS0618

        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            return ValidateOptionsResult.Fail(
                "RootDirectory is required. Set the 'TenSecondTom:RootDirectory' configuration value or the 'TenSecondTom__RootDirectory' environment variable. " +
                "(Legacy: 'TenSecondTom:MemoryDirectory' is also supported for backward compatibility)");
        }

        // Validate path format
        try
        {
            // This will throw if the path contains invalid characters
            _ = Path.GetFullPath(rootDirectory);
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail(
                $"RootDirectory contains an invalid path format: '{rootDirectory}'. Error: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return ValidateOptionsResult.Fail(
                $"RootDirectory path format is not supported: '{rootDirectory}'. Error: {ex.Message}");
        }

        // Validate ProviderId
        if (string.IsNullOrWhiteSpace(options.ProviderId))
        {
            return ValidateOptionsResult.Fail(
                "ProviderId cannot be empty. Set the 'TenSecondTom:Storage:ProviderId' configuration value.");
        }

        // Validate MemorySubdirectory if specified
        if (!string.IsNullOrWhiteSpace(options.MemorySubdirectory))
        {
            try
            {
                // Validate that it's a valid directory name (not a full path)
                if (Path.IsPathRooted(options.MemorySubdirectory))
                {
                    return ValidateOptionsResult.Fail(
                        $"MemorySubdirectory must be a relative directory name, not an absolute path: '{options.MemorySubdirectory}'");
                }

                // Check for invalid path characters
                if (options.MemorySubdirectory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                {
                    return ValidateOptionsResult.Fail(
                        $"MemorySubdirectory contains invalid characters: '{options.MemorySubdirectory}'");
                }
            }
            catch (ArgumentException ex)
            {
                return ValidateOptionsResult.Fail(
                    $"MemorySubdirectory is invalid: '{options.MemorySubdirectory}'. Error: {ex.Message}");
            }
        }

        // Validate RetentionPolicy
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
