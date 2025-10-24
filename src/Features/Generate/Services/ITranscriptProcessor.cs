using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for token limit enforcement and transcript truncation.
/// Provides operations to estimate token counts, count words, and intelligently truncate transcripts.
/// </summary>
public interface ITranscriptProcessor
{
    /// <summary>
    /// Processes transcript to fit within token limits.
    /// Truncates intelligently at sentence boundaries if needed, preserving the beginning of the transcript.
    /// Applies safety factor to ensure prompt template content also fits within limits.
    /// </summary>
    /// <param name="transcriptContent">The full transcript content to process.</param>
    /// <param name="maxInputTokens">The maximum number of input tokens allowed.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing a <see cref="TruncatedTranscript"/> with the processed content and metadata,
    /// or an error message if processing fails.
    /// </returns>
    Task<Result<TruncatedTranscript>> ProcessTranscriptAsync(
        string transcriptContent,
        int maxInputTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates token count from text using a heuristic formula.
    /// Uses the formula: tokens ≈ words × 1.3 (based on typical English text tokenization).
    /// This is a conservative estimate suitable for token limit checks.
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>The estimated number of tokens.</returns>
    int EstimateTokenCount(string text);

    /// <summary>
    /// Counts the number of words in text.
    /// Words are defined as whitespace-separated tokens.
    /// </summary>
    /// <param name="text">The text to count words in.</param>
    /// <returns>The number of words in the text.</returns>
    int CountWords(string text);
}
