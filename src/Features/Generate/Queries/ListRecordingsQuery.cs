using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Queries;

/// <summary>
/// Query to list all available recordings sorted by date (newest first).
/// Used for interactive selection UI.
/// </summary>
public sealed record ListRecordingsQuery : IRequest<Result<IReadOnlyList<RecordingListItem>>>
{
    /// <summary>
    /// Gets optional cancellation token for async operations.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}
