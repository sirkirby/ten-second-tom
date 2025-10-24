using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Generate.Queries;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Handlers;

/// <summary>
/// Handles loading transcript content from filesystem.
/// Validates file existence and readability.
/// </summary>
public sealed class GetRecordingTranscriptQueryHandler
    : IRequestHandler<GetRecordingTranscriptQuery, Result<string>>
{
    private readonly IRecordingService _recordingService;
    private readonly ILogger<GetRecordingTranscriptQueryHandler> _logger;

    public GetRecordingTranscriptQueryHandler(
        IRecordingService recordingService,
        ILogger<GetRecordingTranscriptQueryHandler> logger)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<string>> Handle(
        GetRecordingTranscriptQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TranscriptFilePath))
        {
            return Result<string>.Failure("TranscriptFilePath is required");
        }

        _logger.LogDebug("Loading transcript from {Path}", request.TranscriptFilePath);

        var result = await _recordingService.GetTranscriptContentAsync(
            request.TranscriptFilePath,
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "Failed to load transcript {Path}: {Error}",
                request.TranscriptFilePath,
                result.Error);
            return result;
        }

        _logger.LogDebug(
            "Loaded transcript: {Length} characters",
            result.Value.Length);

        return result;
    }
}
