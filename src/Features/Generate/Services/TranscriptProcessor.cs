using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for processing transcripts to fit within LLM token limits.
/// Handles token estimation and intelligent truncation.
/// </summary>
public sealed class TranscriptProcessor : ITranscriptProcessor
{
    private readonly ILogger<TranscriptProcessor> _logger;

    public TranscriptProcessor(ILogger<TranscriptProcessor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<TruncatedTranscript>> ProcessTranscriptAsync(
        string transcriptContent,
        int maxInputTokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptContent))
        {
            return Result<TruncatedTranscript>.Failure("Transcript content is empty");
        }

        if (maxInputTokens <= 0)
        {
            return Result<TruncatedTranscript>.Failure("MaxInputTokens must be positive");
        }

        var originalWordCount = CountWords(transcriptContent);
        var estimatedTokens = EstimateTokenCount(transcriptContent);

        // Apply safety factor (keep at 80% of limit to leave room for template)
        var safeTokenLimit = (int)(maxInputTokens * LlmConstants.TruncationSafetyFactor);

        _logger.LogDebug(
            "Processing transcript: {OriginalWords} words, {EstimatedTokens} tokens, limit: {SafeLimit}",
            originalWordCount,
            estimatedTokens,
            safeTokenLimit);

        if (estimatedTokens <= safeTokenLimit)
        {
            // No truncation needed
            return await Task.FromResult(Result<TruncatedTranscript>.Success(new TruncatedTranscript
            {
                Content = transcriptContent,
                WasTruncated = false,
                OriginalWordCount = originalWordCount,
                FinalWordCount = originalWordCount,
                EstimatedTokenCount = estimatedTokens
            }));
        }

        // Truncation needed
        _logger.LogWarning(
            "Transcript exceeds token limit: {Estimated} > {Limit}. Truncating...",
            estimatedTokens,
            safeTokenLimit);

        // Calculate target word count
        var targetWordCount = (int)(safeTokenLimit / LlmConstants.TokensPerWord);
        var truncatedContent = TruncateToWordCount(transcriptContent, targetWordCount);
        var finalWordCount = CountWords(truncatedContent);
        var finalTokens = EstimateTokenCount(truncatedContent);

        _logger.LogInformation(
            "Truncated transcript from {OriginalWords} to {FinalWords} words ({FinalTokens} tokens)",
            originalWordCount,
            finalWordCount,
            finalTokens);

        return await Task.FromResult(Result<TruncatedTranscript>.Success(new TruncatedTranscript
        {
            Content = truncatedContent,
            WasTruncated = true,
            OriginalWordCount = originalWordCount,
            FinalWordCount = finalWordCount,
            EstimatedTokenCount = finalTokens
        }));
    }

    public int EstimateTokenCount(string text)
    {
        var words = CountWords(text);
        return (int)(words * LlmConstants.TokensPerWord);
    }

    public int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public static string TruncateToWordCount(string text, int targetWordCount)
    {
        if (targetWordCount <= 0)
        {
            return string.Empty;
        }

        var words = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= targetWordCount)
        {
            return text;
        }

        // Take first N words
        var truncated = string.Join(" ", words.Take(targetWordCount));

        // Try to end on sentence boundary (look for period in last 10% of content)
        var lastPeriodIndex = truncated.LastIndexOf('.');
        var ninetyPercentIndex = (int)(truncated.Length * 0.9);

        if (lastPeriodIndex > ninetyPercentIndex)
        {
            // Found a period near the end, truncate there
            truncated = truncated[..(lastPeriodIndex + 1)];
        }

        return truncated;
    }
}
