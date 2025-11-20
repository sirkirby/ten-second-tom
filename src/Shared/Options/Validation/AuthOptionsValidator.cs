using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Options.Validation;

/// <summary>
/// Validates <see cref="AuthOptions"/> at application startup.
/// </summary>
/// <remarks>
/// This validator ensures that authentication configuration is valid based on the selected
/// <see cref="SshKeySource"/>. Different key sources have different validation requirements:
/// - FileSystem/ManualPath sources require a valid KeyPath
/// - Agent-based sources require an AgentSocketPath
/// Validation failures will prevent the application from starting with clear error messages.
/// </remarks>
public sealed class AuthOptionsValidator : IValidateOptions<AuthOptions>
{
    /// <summary>
    /// Validates the specified <see cref="AuthOptions"/> instance.
    /// </summary>
    /// <param name="name">The name of the options instance being validated.</param>
    /// <param name="options">The options instance to validate.</param>
    /// <returns>
    /// A validation result indicating success or failure with an error message.
    /// </returns>
    public ValidateOptionsResult Validate(string? name, AuthOptions options)
    {
        // KeySource is now nullable - check if it has a value
        if (!options.KeySource.HasValue)
        {
            // Allow null for unconfigured state - will be validated during setup
            return ValidateOptionsResult.Success;
        }

        if (!Enum.IsDefined(options.KeySource.Value))
        {
            return ValidateOptionsResult.Fail(
                $"Invalid SSH key source '{options.KeySource}'. Valid values are: {string.Join(", ", Enum.GetNames<SshKeySource>())}.");
        }

        // Validate KeyPath for file-based sources
        if (options.KeySource is SshKeySource.FileSystem or SshKeySource.ManualPath)
        {
            if (string.IsNullOrWhiteSpace(options.KeyPath))
            {
                return ValidateOptionsResult.Fail(
                    $"KeyPath is required when KeySource is '{options.KeySource}'. Set the 'TenSecondTom:Auth:KeyPath' configuration value or the 'TenSecondTom__Auth__KeyPath' environment variable.");
            }

            // Basic path format validation
            if (options.KeyPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return ValidateOptionsResult.Fail(
                    $"KeyPath contains invalid characters: '{options.KeyPath}'. Provide a valid file system path.");
            }
        }

        // Validate AgentSocketPath for agent-based sources
        if (options.KeySource is SshKeySource.SystemAgent or SshKeySource.OnePasswordAgent or SshKeySource.SecretiveAgent)
        {
            if (string.IsNullOrWhiteSpace(options.AgentSocketPath))
            {
                return ValidateOptionsResult.Fail(
                    $"AgentSocketPath is required when KeySource is '{options.KeySource}'. Set the 'TenSecondTom:Auth:AgentSocketPath' configuration value or use the SSH_AUTH_SOCK environment variable.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
