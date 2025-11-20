using TenSecondTom.Shared.Models;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Interface for detecting SSH keys from various sources
/// </summary>
public interface ISshKeyDetector
{
    /// <summary>
    /// Detects available SSH keys from this detector's source
    /// </summary>
    /// <param name="timeout">Maximum time to spend on detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of detected SSH keys</returns>
    Task<IReadOnlyList<SshKeyInfo>> DetectKeysAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the source type for this detector
    /// </summary>
    SshKeySource Source { get; }
}
