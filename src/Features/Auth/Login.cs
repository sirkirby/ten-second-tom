using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Auth;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Authenticate the user using SSH key-based authentication.
/// Creates an active session if authentication succeeds.
/// </summary>
public static class Login
{
    /// <summary>
    /// Command to authenticate the user.
    /// </summary>
    /// <remarks>
    /// This command requires no parameters as authentication uses SSH keys from the user's system.
    /// </remarks>
    public sealed record Command : IRequest<Result<UserSession>>;

    /// <summary>
    /// Handles the login command to authenticate users via SSH key.
    /// Auto-discovered by MediatR assembly scanning.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
    public sealed class Handler(
        IAuthenticationService authService,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<UserSession>>
    {
        /// <summary>
        /// Handles the login command by authenticating the user via SSH key.
        /// </summary>
        /// <param name="request">The login command.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing the authenticated <see cref="UserSession"/> on success,
        /// or an error message if authentication fails.
        /// </returns>
        public async Task<Result<UserSession>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            logger.LogInformation("Attempting to authenticate user");

            try
            {
                var result = await authService.AuthenticateAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    logger.LogInformation(
                        "User authenticated successfully with session {SessionId}",
                        result.Value.SessionId);
                    return result;
                }

                logger.LogWarning("Authentication failed: {Error}", result.Error);
                return result;
            }
            catch (Exception ex)
            {
                const string errorMessage = "An unexpected error occurred during authentication.";
                logger.LogError(ex, errorMessage);
                return Result<UserSession>.Failure(errorMessage);
            }
        }
    }
}
