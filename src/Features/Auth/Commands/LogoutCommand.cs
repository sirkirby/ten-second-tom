using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth.Commands;

/// <summary>
/// Marker interface for request/response pattern.
/// Indicates this command returns a specific response type.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker interface for CQRS pattern")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Marker interface for command handlers.
/// Handles a request and returns a response.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the specified request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The response from handling the request.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Command to log out the current authenticated user.
/// Invalidates the active session and clears authentication state.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public sealed record LogoutCommand : IRequest<Result<bool>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LogoutCommand"/> class.
    /// </summary>
    /// <remarks>
    /// This command requires no parameters as it operates on the current session.
    /// </remarks>
    public LogoutCommand()
    {
        // No parameters needed for logout
    }
}
