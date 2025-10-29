using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Generate.Services;
using MediatR;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// Retrieves the transcript content for a specific recording.
/// Used after user selects a recording to load full content.
/// </summary>
public static class GetRecordingTranscript
{
    /// <summary>
    /// Query to retrieve the transcript content for a specific recording.
    /// Used after user selects a recording to load full content.
    /// </summary>
    public sealed record Query : IRequest<Result<string>>
    {
        /// <summary>
        /// Gets the full path to the transcript file.
        /// </summary>
        public required string TranscriptFilePath { get; init; }

        /// <summary>
        /// Gets optional cancellation token for async operations.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }
    }

    /// <summary>
    /// Handles loading transcript content from filesystem.
    /// Validates file existence and readability.
    /// </summary>
    public sealed class Handler(
        IRecordingService recordingService,
        ILogger<Handler> logger)
        : IRequestHandler<Query, Result<string>>
    {
        public async Task<Result<string>> Handle(
            Query request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.TranscriptFilePath))
            {
                return Result<string>.Failure("TranscriptFilePath is required");
            }

            logger.LogDebug("Loading transcript from {Path}", request.TranscriptFilePath);

            var result = await recordingService.GetTranscriptContentAsync(
                request.TranscriptFilePath,
                cancellationToken);

            if (!result.IsSuccess)
            {
                logger.LogError(
                    "Failed to load transcript {Path}: {Error}",
                    request.TranscriptFilePath,
                    result.Error);
                return result;
            }

            logger.LogDebug(
                "Loaded transcript: {Length} characters",
                result.Value.Length);

            return result;
        }
    }
}
