namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Result of SSH key detection operation
/// </summary>
public sealed record SshDetectionResult
{
    /// <summary>
    /// Gets the list of detected SSH keys
    /// </summary>
    public required IReadOnlyList<SshKeyInfo> DetectedKeys { get; init; }

    /// <summary>
    /// Gets how long the detection took
    /// </summary>
    public required TimeSpan DetectionDuration { get; init; }

    /// <summary>
    /// Gets which sources were checked during detection
    /// </summary>
    public required IReadOnlyList<SshKeySource> SourcesChecked { get; init; }

    /// <summary>
    /// Gets the timestamp when detection was performed
    /// </summary>
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets whether any keys were found
    /// </summary>
    public bool HasKeys => DetectedKeys.Any();

    /// <summary>
    /// Gets whether detection completed within the timeout
    /// </summary>
    public bool CompletedWithinTimeout(TimeSpan timeout) => DetectionDuration <= timeout;

    /// <summary>
    /// Creates an empty detection result
    /// </summary>
    public static SshDetectionResult Empty(TimeSpan duration, IReadOnlyList<SshKeySource> sourcesChecked) => new()
    {
        DetectedKeys = Array.Empty<SshKeyInfo>(),
        DetectionDuration = duration,
        SourcesChecked = sourcesChecked
    };

    /// <summary>
    /// Creates a successful detection result
    /// </summary>
    public static SshDetectionResult Success(
        IReadOnlyList<SshKeyInfo> keys,
        TimeSpan duration,
        IReadOnlyList<SshKeySource> sourcesChecked) => new()
    {
        DetectedKeys = keys,
        DetectionDuration = duration,
        SourcesChecked = sourcesChecked
    };

    /// <summary>
    /// Filters keys to only include ED25519 keys
    /// </summary>
    public SshDetectionResult FilterEd25519Only() => this with
    {
        DetectedKeys = DetectedKeys.Where(k => k.IsEd25519).ToList()
    };
}
