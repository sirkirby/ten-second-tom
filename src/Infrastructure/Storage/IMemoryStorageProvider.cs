using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Defines the contract for memory storage operations.
/// Abstracts the storage mechanism to support different backends (file system, database, blob storage).
/// </summary>
public interface IMemoryStorageProvider
{
    /// <summary>
    /// Saves a memory entry to storage.
    /// </summary>
    /// <param name="entry">The memory entry to save.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the saved entry on success, or error message on failure.</returns>
    Task<Result<MemoryEntry>> SaveAsync(MemoryEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves memory entries filtered by command and date range.
    /// </summary>
    /// <param name="command">The command type (e.g., "today", "thisweek").</param>
    /// <param name="startDate">Start date of the range (inclusive).</param>
    /// <param name="endDate">End date of the range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing a read-only list of matching entries on success.</returns>
    Task<Result<IReadOnlyList<MemoryEntry>>> GetEntriesAsync(
        string command,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all generated entries (files ending with _generated.md) across supported directories
    /// within the specified date range.
    /// </summary>
    /// <param name="startDate">Start date of the range (inclusive).</param>
    /// <param name="endDate">End date of the range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing generated entries ordered chronologically on success.</returns>
    Task<Result<IReadOnlyList<MemoryEntry>>> GetGeneratedEntriesAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Counts the number of entries for a specific command and date.
    /// Used to determine the next entry number for the day.
    /// </summary>
    /// <param name="command">The command type.</param>
    /// <param name="targetDate">The date to count entries for.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the count of entries on success.</returns>
    Task<Result<int>> CountEntriesAsync(string command, DateTime targetDate, CancellationToken cancellationToken);

    /// <summary>
    /// Searches memory entries by text query across user input and LLM responses.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing a read-only list of matching entries on success.</returns>
    Task<Result<IReadOnlyList<MemoryEntry>>> SearchEntriesAsync(
        string query,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes memory entries within the specified date range.
    /// </summary>
    /// <param name="startDate">Start date of the range (inclusive).</param>
    /// <param name="endDate">End date of the range (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the count of deleted entries on success.</returns>
    Task<Result<int>> DeleteEntriesAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken);

    /// <summary>
    /// Purges expired entries based on the specified retention policy.
    /// </summary>
    /// <param name="retentionPolicy">The retention policy to apply.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the count of purged entries on success.</returns>
    Task<Result<int>> PurgeExpiredEntriesAsync(RetentionPolicy retentionPolicy, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves a specific memory entry by its unique identifier.
    /// </summary>
    /// <param name="entryId">The unique entry identifier (e.g., "today-10-02-2025-1").</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>Result containing the entry if found, or null if not found.</returns>
    Task<Result<MemoryEntry?>> GetEntryByIdAsync(string entryId, CancellationToken cancellationToken);
}
