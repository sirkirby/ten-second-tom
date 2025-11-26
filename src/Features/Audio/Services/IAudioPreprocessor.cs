using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Provides audio preprocessing capabilities such as silence removal and optimization.
/// </summary>
public interface IAudioPreprocessor
{
    /// <summary>
    /// Checks if audio preprocessing is available on the current system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if preprocessing is available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Preprocesses an audio file by applying configured filters (e.g., silence removal).
    /// Creates a new preprocessed file and optionally replaces the original.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file to preprocess.</param>
    /// <param name="replaceOriginal">If true, replaces the original file with the preprocessed version. Default: true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result containing the path to the preprocessed file (may be the same as input if replaceOriginal is true)
    /// and preprocessing statistics.
    /// </returns>
    Task<Result<PreprocessingResult>> PreprocessAsync(
        string audioFilePath,
        bool replaceOriginal = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preprocesses an audio file by applying filters with optional setting overrides.
    /// Creates a new preprocessed file and optionally replaces the original.
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file to preprocess.</param>
    /// <param name="replaceOriginal">If true, replaces the original file with the preprocessed version. Default: true.</param>
    /// <param name="overrides">Optional preprocessing settings overrides for this operation only.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result containing the path to the preprocessed file (may be the same as input if replaceOriginal is true)
    /// and preprocessing statistics.
    /// </returns>
    Task<Result<PreprocessingResult>> PreprocessAsync(
        string audioFilePath,
        bool replaceOriginal,
        PreprocessingOverrides? overrides,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contains the results and statistics from audio preprocessing.
/// </summary>
public sealed record PreprocessingResult
{
    /// <summary>
    /// Gets the path to the preprocessed audio file.
    /// </summary>
    public required string ProcessedFilePath { get; init; }

    /// <summary>
    /// Gets the original file size in bytes.
    /// </summary>
    public required long OriginalSizeBytes { get; init; }

    /// <summary>
    /// Gets the processed file size in bytes.
    /// </summary>
    public required long ProcessedSizeBytes { get; init; }

    /// <summary>
    /// Gets the original duration.
    /// </summary>
    public required TimeSpan OriginalDuration { get; init; }

    /// <summary>
    /// Gets the processed duration.
    /// </summary>
    public required TimeSpan ProcessedDuration { get; init; }

    /// <summary>
    /// Gets the time taken to perform preprocessing.
    /// </summary>
    public required TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Gets the percentage of the original duration that was removed.
    /// </summary>
    public double DurationReductionPercent =>
        OriginalDuration.TotalSeconds > 0
            ? ((OriginalDuration.TotalSeconds - ProcessedDuration.TotalSeconds) / OriginalDuration.TotalSeconds) * 100
            : 0;

    /// <summary>
    /// Gets the percentage of the original file size that was reduced.
    /// </summary>
    public double SizeReductionPercent =>
        OriginalSizeBytes > 0
            ? ((OriginalSizeBytes - ProcessedSizeBytes) / (double)OriginalSizeBytes) * 100
            : 0;
}

