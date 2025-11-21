using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Generate.Services;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// Lists all available recordings sorted by date (newest first).
/// Used for interactive selection UI.
/// </summary>
public static class ListRecordings
{
    /// <summary>
    /// Query to list all available recordings sorted by date (newest first).
    /// Used for interactive selection UI.
    /// </summary>
    public sealed record Query : IRequest<Result<IReadOnlyList<RecordingListItem>>>
    {
        /// <summary>
        /// Gets optional cancellation token for async operations.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }
    }

    /// <summary>
    /// Handles listing of available recordings from the recording directory.
    /// Scans filesystem, parses metadata, sorts by date.
    /// </summary>
    public sealed class Handler(
        IRecordingService recordingService,
        ILogger<Handler> logger)
        : IRequestHandler<Query, Result<IReadOnlyList<RecordingListItem>>>
    {
        public async Task<Result<IReadOnlyList<RecordingListItem>>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            logger.LogDebug("Listing recordings from recording directory");

            var result = await recordingService.ListRecordingsAsync(cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogError("Failed to list recordings: {Error}", result.Error);
                return result;
            }

            var recordings = result.Value;

            logger.LogInformation("Found {Count} recordings", recordings.Count);

            return Result<IReadOnlyList<RecordingListItem>>.Success(recordings);
        }
    }
}
