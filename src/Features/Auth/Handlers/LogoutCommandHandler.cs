using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Auth.Commands;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth.Handlers;

/// <summary>
/// Handles the logout command to invalidate the current user session.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result<bool>>
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutCommandHandler"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="logger">The logger instance.</param>
    public LogoutCommandHandler(
        IAuthenticationService authService,
        ILogger<LogoutCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(logger);

        _authService = authService;
        _logger = logger;
    }

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
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Logging out user");

        try
        {
            var result = await _authService.LogoutAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation("User logged out successfully");
                return result;
            }

            _logger.LogWarning("Logout failed: {Error}", result.Error);
            return result;
        }
        catch (Exception ex)
        {
            const string errorMessage = "An unexpected error occurred during logout.";
            _logger.LogError(ex, errorMessage);
            return Result<bool>.Failure(errorMessage);
        }
    }
}
