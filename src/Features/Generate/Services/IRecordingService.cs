using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for recording file discovery and transcript loading.
/// Provides operations to list recordings, load transcript content, and validate files.
/// </summary>
public interface IRecordingService
{
    /// <summary>
    /// Lists all available recordings sorted by date (newest first).
    /// Scans the recording directory for transcript files and builds list items with metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing a read-only list of recording items on success,
    /// or an error message if the directory cannot be accessed or no recordings are found.
    /// </returns>
    Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the transcript content for a specific recording.
    /// </summary>
    /// <param name="transcriptFilePath">The full path to the transcript file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing the transcript content as a string on success,
    /// or an error message if the file cannot be found or read.
    /// </returns>
    Task<Result<string>> GetTranscriptContentAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a transcript file exists and is readable.
    /// Performs basic checks without loading the full content.
    /// </summary>
    /// <param name="transcriptFilePath">The full path to the transcript file.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result indicating success if the file is valid and accessible,
    /// or a failure result with an error message describing the problem.
    /// </returns>
    Task<Result> ValidateTranscriptFileAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default);
}
