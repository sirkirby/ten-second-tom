using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth.Commands;

/// <summary>
/// Command to log out the current authenticated user.
/// Invalidates the active session and clears authentication state.
/// </summary>
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
