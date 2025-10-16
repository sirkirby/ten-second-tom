using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Auth.Commands;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth.Handlers;

/// <summary>
/// Handles the login command to authenticate users via SSH key.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, Result<UserSession>>
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<LoginCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommandHandler"/> class.
    /// </summary>
    /// <param name="authService">The authentication service.</param>
    /// <param name="logger">The logger instance.</param>
    public LoginCommandHandler(
        IAuthenticationService authService,
        ILogger<LoginCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(logger);

        _authService = authService;
        _logger = logger;
    }

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
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation("Attempting to authenticate user");

        try
        {
            var result = await _authService.AuthenticateAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "User authenticated successfully with session {SessionId}",
                    result.Value.SessionId);
                return result;
            }

            _logger.LogWarning("Authentication failed: {Error}", result.Error);
            return result;
        }
        catch (Exception ex)
        {
            const string errorMessage = "An unexpected error occurred during authentication.";
            _logger.LogError(ex, errorMessage);
            return Result<UserSession>.Failure(errorMessage);
        }
    }
}
