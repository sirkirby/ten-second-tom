namespace TenSecondTom.Features.Generate.Models;

/// <summary>
/// Transcript processed for token limit compliance.
/// Contains the possibly-truncated content along with metadata about the truncation.
/// </summary>
public sealed record TruncatedTranscript
{
    /// <summary>
    /// Gets the (possibly truncated) transcript content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets whether truncation occurred.
    /// </summary>
    public required bool WasTruncated { get; init; }

    /// <summary>
    /// Gets the original word count before truncation.
    /// </summary>
    public required int OriginalWordCount { get; init; }

    /// <summary>
    /// Gets the final word count after truncation.
    /// </summary>
    public required int FinalWordCount { get; init; }

    /// <summary>
    /// Gets the estimated token count of the content.
    /// </summary>
    public required int EstimatedTokenCount { get; init; }

    /// <summary>
    /// Creates a warning message if truncation occurred.
    /// Returns null if no truncation happened.
    /// </summary>
    /// <returns>A warning message if truncated, otherwise null.</returns>
    public string? GetTruncationWarning() =>
        WasTruncated
            ? $"⚠️  Transcript truncated from {OriginalWordCount} to {FinalWordCount} words to fit within token limit"
            : null;
}
