using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Provides reusable authentication orchestration logic for CLI commands.
/// Handles authentication checks, user prompts, and error formatting.
/// </summary>
public static class AuthenticationHelper
{
    /// <summary>
    /// Ensures the user is authenticated, prompting for authentication if needed.
    /// Handles both JSON and text output modes.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="commandName">Name of the command requiring authentication (for error messages).</param>
    /// <param name="jsonOutput">Whether to output errors in JSON format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success result if authenticated, failure result with error message if not.</returns>
    public static async Task<Result<bool>> EnsureAuthenticatedAsync(
        IAuthenticationService authService,
        string commandName,
        bool jsonOutput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(commandName);

        try
        {
            bool isAuthenticated = await authService.IsAuthenticatedAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!isAuthenticated)
            {
                Result<UserSession> authResult = await authService.AuthenticateAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (!authResult.IsSuccess)
                {
                    HandleAuthenticationError(authResult.Error ?? "Authentication failed", commandName, jsonOutput);
                    return Result<bool>.Failure(authResult.Error ?? "Authentication failed");
                }
            }

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            HandleAuthenticationError(ex.Message, commandName, jsonOutput);
            return Result<bool>.Failure($"Authentication error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles authentication error display in JSON or text format.
    /// </summary>
    private static void HandleAuthenticationError(string errorMessage, string commandName, bool jsonOutput)
    {
        if (jsonOutput)
        {
            string json = JsonOutputFormatter.FormatFailure(commandName, errorMessage, DateTimeOffset.UtcNow);
            Console.WriteLine(json);
        }
        else
        {
            AuthenticationErrorFormatter.DisplayAuthenticationError(errorMessage);
        }
    }
}
