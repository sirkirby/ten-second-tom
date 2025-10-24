using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Features.Generate.Queries;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Handlers;

/// <summary>
/// Handles listing of available recordings from the recording directory.
/// Scans filesystem, parses metadata, sorts by date.
/// </summary>
public sealed class ListRecordingsQueryHandler
    : IRequestHandler<ListRecordingsQuery, Result<IReadOnlyList<RecordingListItem>>>
{
    private readonly IRecordingService _recordingService;
    private readonly ILogger<ListRecordingsQueryHandler> _logger;

    public ListRecordingsQueryHandler(
        IRecordingService recordingService,
        ILogger<ListRecordingsQueryHandler> logger)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<IReadOnlyList<RecordingListItem>>> Handle(
        ListRecordingsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listing recordings from recording directory");

        var result = await _recordingService.ListRecordingsAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to list recordings: {Error}", result.Error);
            return result;
        }

        var recordings = result.Value;

        _logger.LogInformation("Found {Count} recordings", recordings.Count);

        return Result<IReadOnlyList<RecordingListItem>>.Success(recordings);
    }
}
