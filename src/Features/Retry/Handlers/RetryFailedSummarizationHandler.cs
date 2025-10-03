using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Retry.Commands;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Retry.Handlers;

/// <summary>
/// Handles retry operations for entries where LLM summarization failed.
/// Resubmits user input to LLM provider and updates entry metadata.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Retry operations need to catch all exceptions")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging for now")]
public sealed class RetryFailedSummarizationHandler
{
    private readonly IMemoryStorageProvider _storageProvider;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<RetryFailedSummarizationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryFailedSummarizationHandler"/> class.
    /// </summary>
    /// <param name="storageProvider">Storage provider for reading and updating entries.</param>
    /// <param name="llmProvider">LLM provider for generating summaries.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public RetryFailedSummarizationHandler(
        IMemoryStorageProvider storageProvider,
        ILlmProvider llmProvider,
        ILogger<RetryFailedSummarizationHandler> logger)
    {
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the retry command to reprocess failed entries.
    /// </summary>
    /// <param name="command">Command specifying which entries to retry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing retry statistics.</returns>
    public async Task<Result<RetryResult>> Handle(
        RetryFailedSummarizationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = new RetryResult
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
    private async Task<Result<RetryResult>> RetrySpecificEntryAsync(
        string entryId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrying specific entry: {EntryId}", entryId);

        Result<MemoryEntry?> entryResult = await _storageProvider
            .GetEntryByIdAsync(entryId, cancellationToken)
            .ConfigureAwait(false);

        if (!entryResult.IsSuccess)
        {
            return Result<RetryResult>.Failure($"Failed to retrieve entry: {entryResult.Error}");
        }

        if (entryResult.Value == null)
        {
            return Result<RetryResult>.Failure($"Entry {entryId} not found");
        }

        // Check if entry actually failed
        if (!HasSummarizationFailed(entryResult.Value))
        {
            return Result<RetryResult>.Failure($"Entry {entryId} did not fail summarization");
        }

        var result = new RetryResult
        {
            TotalAttempted = 1,
            SuccessCount = 0,
            FailureCount = 0
        };

        bool success = await RetryEntryAsync(entryResult.Value, cancellationToken).ConfigureAwait(false);

        if (success)
        {
            result = result with { SuccessCount = 1 };
            _logger.LogInformation("Successfully retried entry {EntryId}", entryId);
        }
        else
        {
            result = result with { FailureCount = 1 };
            result.Errors[entryId] = "Retry failed";
            _logger.LogWarning("Failed to retry entry {EntryId}", entryId);
        }

        return Result<RetryResult>.Success(result);
    }

    /// <summary>
    /// Retries all entries that have the summarization-failed flag.
    /// </summary>
    private async Task<Result<RetryResult>> RetryAllFailedEntriesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Searching for all failed entries to retry");

        // Get all entries from the last 30 days (failed entries are likely recent)
        DateTime startDate = DateTime.UtcNow.AddDays(-30);
        DateTime endDate = DateTime.UtcNow;

        var todayResult = await _storageProvider
            .GetEntriesAsync("today", startDate, endDate, cancellationToken)
            .ConfigureAwait(false);

        if (!todayResult.IsSuccess)
        {
            return Result<RetryResult>.Failure($"Failed to retrieve entries: {todayResult.Error}");
        }

        // Filter for failed entries
        var failedEntries = todayResult.Value
            .Where(HasSummarizationFailed)
            .ToList();

        var result = new RetryResult
        {
            TotalAttempted = failedEntries.Count,
            SuccessCount = 0,
            FailureCount = 0
        };

        if (failedEntries.Count == 0)
        {
            _logger.LogInformation("No failed entries found to retry");
            return Result<RetryResult>.Success(result);
        }

        _logger.LogInformation("Found {Count} failed entries to retry", failedEntries.Count);

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

        _logger.LogInformation(
            "Retry complete: {Success} succeeded, {Failed} failed out of {Total}",
            successCount,
            failureCount,
            failedEntries.Count);

        return Result<RetryResult>.Success(result);
    }

    /// <summary>
    /// Attempts to retry summarization for a single entry.
    /// </summary>
    private async Task<bool> RetryEntryAsync(MemoryEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Retrying entry {EntryId}", entry.EntryId);

            // Call LLM provider with user input
            Result<string> llmResult = await _llmProvider
                .GenerateCompletionAsync(entry.UserInput, cancellationToken)
                .ConfigureAwait(false);

            if (!llmResult.IsSuccess)
            {
                _logger.LogWarning(
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
                CustomTags = updatedTags
            };

            MemoryEntry updatedEntry = entry with
            {
                LlmResponse = llmResult.Value,
                Metadata = updatedMetadata
            };

            // Save updated entry
            Result<MemoryEntry> saveResult = await _storageProvider
                .SaveAsync(updatedEntry, cancellationToken)
                .ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Failed to save retried entry {EntryId}: {Error}",
                    entry.EntryId,
                    saveResult.Error);
                return false;
            }

            _logger.LogDebug("Successfully retried and saved entry {EntryId}", entry.EntryId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while retrying entry {EntryId}", entry.EntryId);
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
