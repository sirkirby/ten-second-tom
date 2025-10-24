using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Queries;

/// <summary>
/// Query to retrieve the transcript content for a specific recording.
/// Used after user selects a recording to load full content.
/// </summary>
public sealed record GetRecordingTranscriptQuery : IRequest<Result<string>>
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
