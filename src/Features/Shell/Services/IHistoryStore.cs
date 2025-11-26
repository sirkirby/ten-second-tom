using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Provides persistent storage for command history across sessions.
/// History is stored as a JSON file in the application data directory.
/// </summary>
public interface IHistoryStore
{
    /// <summary>
    /// Loads command history from persistent storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of history entries, or empty list if no history exists.</returns>
    Task<Result<List<CommandHistoryEntry>>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves command history to persistent storage.
    /// </summary>
    /// <param name="entries">History entries to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success with file path, or failure with error message.</returns>
    Task<Result<string>> SaveAsync(IReadOnlyList<CommandHistoryEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the history file.
    /// </summary>
    string GetHistoryPath();
}
