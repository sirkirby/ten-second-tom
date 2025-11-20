using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Interface for SSH key detector factory
/// Enables mocking for unit tests
/// </summary>
public interface ISshKeyDetectorFactory
{
    /// <summary>
    /// Detects SSH keys from all available sources with priority ordering
    /// Priority: SSH agents first, then file system
    /// </summary>
    /// <param name="timeout">Maximum time to spend detecting keys</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detection result with found keys, duration, and sources checked</returns>
    Task<SshDetectionResult> DetectKeysAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
