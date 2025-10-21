using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth.Commands;

/// <summary>
/// Command to authenticate the user using SSH key-based authentication.
/// Creates an active session if authentication succeeds.
/// </summary>
public sealed record LoginCommand : IRequest<Result<UserSession>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoginCommand"/> class.
    /// </summary>
    /// <remarks>
    /// This command requires no parameters as authentication uses SSH keys from the user's system.
    /// </remarks>
    public LoginCommand()
    {
        // No parameters needed - authentication uses SSH keys
    }
}
