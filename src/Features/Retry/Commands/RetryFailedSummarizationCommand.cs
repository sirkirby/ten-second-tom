using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Retry.Commands;

/// <summary>
/// Command to retry failed LLM summarization for memory entries.
/// Can retry all failed entries or a specific entry by ID.
/// </summary>
public sealed record RetryFailedSummarizationCommand
{
    /// <summary>
    /// Gets the specific entry ID to retry.
    /// If null, all failed entries will be retried.
    /// </summary>
    public string? EntryId { get; init; }
}

/// <summary>
/// Result of retry operation containing statistics about the retry attempts.
/// </summary>
public sealed record RetryResult
{
    /// <summary>
    /// Gets the total number of entries attempted.
    /// </summary>
    public required int TotalAttempted { get; init; }

    /// <summary>
    /// Gets the number of successful retries.
    /// </summary>
    public required int SuccessCount { get; init; }

    /// <summary>
    /// Gets the number of failed retries.
    /// </summary>
    public required int FailureCount { get; init; }

    /// <summary>
    /// Gets the map of entry IDs to error messages for failed retries.
    /// </summary>
    public Dictionary<string, string> Errors { get; init; } = new();
}
