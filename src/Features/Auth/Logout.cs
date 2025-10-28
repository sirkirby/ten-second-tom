using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Auth;
using MediatR;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Log out the current authenticated user.
/// Invalidates the active session and clears authentication state.
/// </summary>
public static class Logout
{
    /// <summary>
    /// Command to log out the current user.
    /// </summary>
    /// <remarks>
    /// This command requires no parameters as it operates on the current session.
    /// </remarks>
    public sealed record Command : IRequest<Result<bool>>;

    /// <summary>
    /// Handles the logout command to invalidate the current user session.
    /// Auto-discovered by MediatR assembly scanning.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
    public sealed class Handler(
        IAuthenticationService authService,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<bool>>
    {
        /// <summary>
        /// Handles the logout command by invalidating the current user session.
        /// </summary>
        /// <param name="request">The logout command.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Result{T}"/> containing <c>true</c> if logout was successful,
        /// or an error message if no active session exists or logout failed.
        /// </returns>
        public async Task<Result<bool>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            logger.LogInformation("Logging out user");

            try
            {
                var result = await authService.LogoutAsync(cancellationToken).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    logger.LogInformation("User logged out successfully");
                    return result;
                }

                logger.LogWarning("Logout failed: {Error}", result.Error);
                return result;
            }
            catch (Exception ex)
            {
                const string errorMessage = "An unexpected error occurred during logout.";
                logger.LogError(ex, errorMessage);
                return Result<bool>.Failure(errorMessage);
            }
        }
    }
}
