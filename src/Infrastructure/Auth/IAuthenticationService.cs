using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Provides authentication and session management services using SSH key-based authentication.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates the user using their SSH key and creates an active session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the authentication operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing the authenticated <see cref="UserSession"/> on success,
    /// or an error message if authentication fails.
    /// </returns>
    /// <remarks>
    /// This method:
    /// <list type="bullet">
    /// <item>Discovers SSH keys from ~/.ssh/ (id_ed25519 preferred, id_rsa fallback)</item>
    /// <item>Prompts for passphrase if the key is encrypted (max 3 attempts)</item>
    /// <item>Creates a session with SSH key fingerprint</item>
    /// <item>Persists the session until explicit logout</item>
    /// </list>
    /// </remarks>
    Task<Result<UserSession>> AuthenticateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the user has an active authenticated session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// <c>true</c> if an active session exists; otherwise, <c>false</c>.
    /// </returns>
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs out the current user by invalidating their active session.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing <c>true</c> if logout was successful,
    /// or an error message if there was no active session to logout.
    /// </returns>
    Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken = default);
}
