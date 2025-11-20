namespace TenSecondTom.Shared.Models;

/// <summary>
/// Audio configuration validation result.
/// Used as a cross-feature contract for audio validation queries.
/// </summary>
public sealed record AudioValidationResult
{
    /// <summary>
    /// Gets whether audio is fully configured.
    /// </summary>
    public required bool IsConfigured { get; init; }

    /// <summary>
    /// Gets the list of missing configuration items (empty if IsConfigured is true).
    /// </summary>
    public required IReadOnlyList<string> MissingItems { get; init; }
}
