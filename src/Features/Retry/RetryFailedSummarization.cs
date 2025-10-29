using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Retry;

/// <summary>
/// Command to retry failed LLM summarization for memory entries.
/// Can retry all failed entries or a specific entry by ID.
/// </summary>
public static class RetryFailedSummarization
{
    /// <summary>
    /// Command to retry failed LLM summarization for memory entries.
    /// Can retry all failed entries or a specific entry by ID.
    /// </summary>
    public sealed record Command
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
    public sealed record Result
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

    /// <summary>
    /// Handles retry operations for entries where LLM summarization failed.
    /// Resubmits user input to LLM provider and updates entry metadata.
    /// </summary>
    public sealed class Handler(
        IMemoryStorageProvider storageProvider,
        ILlmProvider llmProvider,
        ILogger<Handler> logger)
    {
        /// <summary>
        /// Handles the retry command to reprocess failed entries.
        /// </summary>
        /// <param name="command">Command specifying which entries to retry.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result containing retry statistics.</returns>
        public async Task<Shared.Results.Result<Result>> Handle(
            Command command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var result = new Result
            {
                TotalAttempted = 0,
                SuccessCount = 0,
                FailureCount = 0
            };

            // If specific entry ID provided, retry only that entry
            if (!string.IsNullOrEmpty(command.EntryId))
            {
                return await RetrySpecificEntryAsync(command.EntryId, cancellationToken).ConfigureAwait(false);
            }

            // Otherwise, find and retry all failed entries
            return await RetryAllFailedEntriesAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Retries a specific entry by ID.
        /// </summary>
        private async Task<Shared.Results.Result<Result>> RetrySpecificEntryAsync(
            string entryId,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Retrying specific entry: {EntryId}", entryId);

            Shared.Results.Result<MemoryEntry?> entryResult = await storageProvider
                .GetEntryByIdAsync(entryId, cancellationToken)
                .ConfigureAwait(false);

            if (!entryResult.IsSuccess)
            {
                return Shared.Results.Result<Result>.Failure($"Failed to retrieve entry: {entryResult.Error}");
            }

            if (entryResult.Value == null)
            {
                return Shared.Results.Result<Result>.Failure($"Entry {entryId} not found");
            }

            // Check if entry actually failed
            if (!HasSummarizationFailed(entryResult.Value))
            {
                return Shared.Results.Result<Result>.Failure($"Entry {entryId} did not fail summarization");
            }

            var result = new Result
            {
                TotalAttempted = 1,
                SuccessCount = 0,
                FailureCount = 0
            };

            bool success = await RetryEntryAsync(entryResult.Value, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                result = result with { SuccessCount = 1 };
                logger.LogInformation("Successfully retried entry {EntryId}", entryId);
            }
            else
            {
                result = result with { FailureCount = 1 };
                result.Errors[entryId] = "Retry failed";
                logger.LogWarning("Failed to retry entry {EntryId}", entryId);
            }

            return Shared.Results.Result<Result>.Success(result);
        }

        /// <summary>
        /// Retries all entries that have the summarization-failed flag.
        /// </summary>
        private async Task<Shared.Results.Result<Result>> RetryAllFailedEntriesAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Searching for all failed entries to retry");

            // Get all entries from the last 30 days (failed entries are likely recent)
            DateTime startDate = DateTime.UtcNow.AddDays(-30);
            DateTime endDate = DateTime.UtcNow;

            var todayResult = await storageProvider
                .GetEntriesAsync(CommandNames.Today, startDate, endDate, cancellationToken)
                .ConfigureAwait(false);

            if (!todayResult.IsSuccess)
            {
                return Shared.Results.Result<Result>.Failure($"Failed to retrieve entries: {todayResult.Error}");
            }

            // Filter for failed entries
            var failedEntries = todayResult.Value
                .Where(HasSummarizationFailed)
                .ToList();

            var result = new Result
            {
                TotalAttempted = failedEntries.Count,
                SuccessCount = 0,
                FailureCount = 0
            };

            if (failedEntries.Count == 0)
            {
                logger.LogInformation("No failed entries found to retry");
                return Shared.Results.Result<Result>.Success(result);
            }

            logger.LogInformation("Found {Count} failed entries to retry", failedEntries.Count);

            int successCount = 0;
            int failureCount = 0;

            foreach (MemoryEntry entry in failedEntries)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                bool success = await RetryEntryAsync(entry, cancellationToken).ConfigureAwait(false);

                if (success)
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                    result.Errors[entry.EntryId] = "Retry failed";
                }
            }

            result = result with
            {
                SuccessCount = successCount,
                FailureCount = failureCount
            };

            logger.LogInformation(
                "Retry complete: {Success} succeeded, {Failed} failed out of {Total}",
                successCount,
                failureCount,
                failedEntries.Count);

            return Shared.Results.Result<Result>.Success(result);
        }

        /// <summary>
        /// Attempts to retry summarization for a single entry.
        /// </summary>
        private async Task<bool> RetryEntryAsync(MemoryEntry entry, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug("Retrying entry {EntryId}", entry.EntryId);

                // Track processing time
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Call LLM provider with user input
                Shared.Results.Result<LlmResponse> llmResult = await llmProvider
                    .GenerateCompletionAsync(entry.UserInput, cancellationToken)
                    .ConfigureAwait(false);

                stopwatch.Stop();

                if (!llmResult.IsSuccess)
                {
                    logger.LogWarning(
                        "LLM retry failed for entry {EntryId}: {Error}",
                        entry.EntryId,
                        llmResult.Error);
                    return false;
                }

                // Update entry with new LLM response and remove failed flag
                // Create a new dictionary without the failed flag
                var updatedTags = new Dictionary<string, string>(entry.Metadata.CustomTags);
                updatedTags.Remove("summarization-failed");
                updatedTags.Remove("original-error");

                var updatedMetadata = entry.Metadata with
                {
                    TokensUsed = llmResult.Value.TotalTokens,
                    ProcessingDuration = stopwatch.Elapsed,
                    CustomTags = updatedTags
                };

                MemoryEntry updatedEntry = entry with
                {
                    LlmResponse = llmResult.Value.Content,
                    Metadata = updatedMetadata
                };

                // Save updated entry
                Shared.Results.Result<MemoryEntry> saveResult = await storageProvider
                    .SaveAsync(updatedEntry, cancellationToken)
                    .ConfigureAwait(false);

                if (!saveResult.IsSuccess)
                {
                    logger.LogWarning(
                        "Failed to save retried entry {EntryId}: {Error}",
                        entry.EntryId,
                        saveResult.Error);
                    return false;
                }

                logger.LogDebug("Successfully retried and saved entry {EntryId}", entry.EntryId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Exception while retrying entry {EntryId}", entry.EntryId);
                return false;
            }
        }

        /// <summary>
        /// Checks if an entry has the summarization-failed flag.
        /// </summary>
        private static bool HasSummarizationFailed(MemoryEntry entry)
        {
            return entry.Metadata.CustomTags.TryGetValue("summarization-failed", out string? value)
                && value == "true";
        }
    }
}
