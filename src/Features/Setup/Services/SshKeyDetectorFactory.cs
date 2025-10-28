using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Services;

/// <summary>
/// Factory for creating and orchestrating SSH key detectors
/// Implements priority ordering and timeout enforcement
/// </summary>
public class SshKeyDetectorFactory : ISshKeyDetectorFactory
{
    private readonly IEnumerable<ISshKeyDetector> _detectors;
    private readonly ILogger<SshKeyDetectorFactory> _logger;

    public SshKeyDetectorFactory(
        IEnumerable<ISshKeyDetector> detectors,
        ILogger<SshKeyDetectorFactory> logger)
    {
        _detectors = detectors ?? throw new ArgumentNullException(nameof(detectors));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects SSH keys from all available sources with priority ordering
    /// Priority: SSH agents first, then file system
    /// </summary>
    public async Task<SshDetectionResult> DetectKeysAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var allKeys = new List<SshKeyInfo>();
        var sourcesChecked = new List<SshKeySource>();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);

            _logger.LogInformation("Starting SSH key detection with {Timeout}s timeout", timeout.TotalSeconds);

            // Priority order: Agents first (more secure, frequently updated), then file system
            var orderedDetectors = _detectors.OrderBy(d => GetPriority(d.Source)).ToList();

            foreach (var detector in orderedDetectors)
            {
                if (cts.Token.IsCancellationRequested)
                {
                    _logger.LogWarning("SSH key detection cancelled");
                    break;
                }

                var remainingTime = timeout - (DateTime.UtcNow - startTime);
                if (remainingTime <= TimeSpan.Zero)
                {
                    _logger.LogWarning("SSH key detection timeout reached");
                    break;
                }

                sourcesChecked.Add(detector.Source);

                try
                {
                    _logger.LogDebug("Checking {Source} for SSH keys", detector.Source);
                    var keys = await detector.DetectKeysAsync(remainingTime, cts.Token);
                    
                    if (keys.Any())
                    {
                        allKeys.AddRange(keys);
                        _logger.LogInformation("Found {Count} keys from {Source}", keys.Count, detector.Source);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Detection from {Source} was cancelled", detector.Source);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting keys from {Source}", detector.Source);
                }
            }

            var duration = DateTime.UtcNow - startTime;

            // Deduplicate keys by public key content (same key from multiple sources)
            // When duplicates exist, prefer more specific sources:
            // - 1Password/Secretive/dedicated agents are more specific than generic SystemAgent
            // - FileSystem is least specific (just a file on disk)
            var deduplicatedKeys = allKeys
                .GroupBy(k => k.PublicKey) // Group by actual key content
                .Select(g => g.OrderBy(k => GetDeduplicationPriority(k.Source)).First())
                .ToList();

            var duplicatesRemoved = allKeys.Count - deduplicatedKeys.Count;
            if (duplicatesRemoved > 0)
            {
                _logger.LogInformation(
                    "Removed {DuplicateCount} duplicate keys (same key detected from multiple sources)",
                    duplicatesRemoved);
            }

            // Sort keys to prefer ED25519 keys first (recommended), then other key types
            // This ensures ED25519 keys are presented first to users during selection
            var sortedKeys = deduplicatedKeys
                .OrderByDescending(k => k.IsEd25519)  // ED25519 first
                .ThenBy(k => GetDeduplicationPriority(k.Source))  // Then by source priority
                .ToList();

            var ed25519Count = sortedKeys.Count(k => k.IsEd25519);

            _logger.LogInformation(
                "SSH key detection completed in {Duration}ms. Found {TotalKeys} keys ({Ed25519Keys} ED25519, {OtherKeys} other types) from {Sources} sources",
                duration.TotalMilliseconds,
                sortedKeys.Count,
                ed25519Count,
                sortedKeys.Count - ed25519Count,
                sourcesChecked.Count);

            return SshDetectionResult.Success(
                sortedKeys,
                duration,
                sourcesChecked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH key detection failed");
            var duration = DateTime.UtcNow - startTime;
            return SshDetectionResult.Empty(duration, sourcesChecked);
        }
    }

    /// <summary>
    /// Gets the priority for detection order (lower = check first)
    /// Agents have highest priority, then file system
    /// </summary>
    private static int GetPriority(SshKeySource source)
    {
        return source switch
        {
            // Agents have highest priority (most secure, frequently updated)
            SshKeySource.SystemAgent => 1,
            SshKeySource.OnePasswordAgent => 2,
            SshKeySource.SecretiveAgent => 3,
            // File system is fallback
            SshKeySource.FileSystem => 4,
            // Manual path is last resort
            SshKeySource.ManualPath => 5,
            _ => 999
        };
    }

    /// <summary>
    /// Gets the priority for deduplication (lower = prefer this source)
    /// When the same key is found from multiple sources, prefer more specific sources
    /// </summary>
    private static int GetDeduplicationPriority(SshKeySource source)
    {
        return source switch
        {
            // Prefer specific agent sources over generic SystemAgent
            SshKeySource.OnePasswordAgent => 1,
            SshKeySource.SecretiveAgent => 2,
            SshKeySource.SystemAgent => 3, // Generic, could be any agent
            SshKeySource.FileSystem => 4,
            SshKeySource.ManualPath => 5,
            _ => 999
        };
    }
}
