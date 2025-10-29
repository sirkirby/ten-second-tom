using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Features.Generate;

namespace TenSecondTom.Tests.Features.Generate.Services;

/// <summary>
/// Tests for <see cref="TranscriptProcessor"/> implementation.
/// Validates token estimation, word counting, truncation logic, and transcript processing.
/// </summary>
public sealed class TranscriptProcessorTests
{
    private readonly Mock<ILogger<TranscriptProcessor>> _mockLogger;

    public TranscriptProcessorTests()
    {
        _mockLogger = new Mock<ILogger<TranscriptProcessor>>();
    }

    #region CountWords Tests

    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("word", 1)]
    [InlineData("one two three", 3)]
    [InlineData("one  two   three", 3)] // Multiple spaces
    [InlineData("one\ttwo\nthree\rfour", 4)] // Mixed whitespace
    [InlineData("Hello, world! How are you?", 5)]
    public void CountWords_WithVariousInputs_ReturnsCorrectCount(string input, int expectedCount)
    {
        // Arrange
        var processor = CreateProcessor();

        // Act
        var actual = processor.CountWords(input);

        // Assert
        actual.Should().Be(expectedCount);
    }

    #endregion

    #region EstimateTokenCount Tests

    [Theory]
    [InlineData("", 0)]
    [InlineData("one two three", 3)] // 3 words * 1.3 = 3.9 → 3
    [InlineData("one two three four five", 6)] // 5 words * 1.3 = 6.5 → 6
    public void EstimateTokenCount_UsesCorrectMultiplier(string text, int expectedMinimum)
    {
        // Arrange
        var processor = CreateProcessor();

        // Act
        var actual = processor.EstimateTokenCount(text);

        // Assert
        actual.Should().BeGreaterThanOrEqualTo(expectedMinimum);
        // Verify it's using ~1.3 multiplier (allow some rounding variance)
        var words = processor.CountWords(text);
        if (words > 0)
        {
            var ratio = (double)actual / words;
            ratio.Should().BeApproximately(1.3, 0.5);
        }
    }

    [Fact]
    public void EstimateTokenCount_WithEmptyString_ReturnsZero()
    {
        // Arrange
        var processor = CreateProcessor();

        // Act
        var result = processor.EstimateTokenCount(string.Empty);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region TruncateToWordCount Tests

    [Fact]
    public void TruncateToWordCount_WhenContentFitsWithinLimit_ReturnsOriginalContent()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "This is a short transcript.";
        var targetWordCount = 10;

        // Act
        var result = TranscriptProcessor.TruncateToWordCount(content, targetWordCount);

        // Assert
        result.Should().Be(content);
    }

    [Fact]
    public void TruncateToWordCount_WhenTruncationNeeded_PreservesSentenceBoundary()
    {
        // Arrange
        var processor = CreateProcessor();
        // Sentence boundary is found when period is in last 10% of truncated text
        // Take 5 words: ["The", "quick", "brown", "fox", "jumps."]
        // Result: "The quick brown fox jumps." (26 chars)
        // Period at index 25, 90% = 23.4, so 25 > 23.4 → truncate to period
        var content = "The quick brown fox jumps. This is a longer second sentence that will be truncated.";
        var targetWordCount = 5;

        // Act
        var result = TranscriptProcessor.TruncateToWordCount(content, targetWordCount);

        // Assert
        result.Should().EndWith(".");
        result.Should().Be("The quick brown fox jumps.");
        var wordCount = processor.CountWords(result);
        wordCount.Should().Be(5);
    }

    [Fact]
    public void TruncateToWordCount_WhenNoPeriodNearEnd_UsesHardWordBoundary()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "This is content without periods at all just continuous text going on and on";
        var targetWordCount = 5;

        // Act
        var result = TranscriptProcessor.TruncateToWordCount(content, targetWordCount);

        // Assert
        var wordCount = processor.CountWords(result);
        wordCount.Should().Be(targetWordCount);
    }

    [Fact]
    public void TruncateToWordCount_WithZeroTarget_ReturnsEmptyString()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "Some content here";

        // Act
        var result = TranscriptProcessor.TruncateToWordCount(content, 0);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ProcessTranscriptAsync Tests

    [Fact]
    public async Task ProcessTranscriptAsync_WhenContentFitsWithinLimit_ReturnsUntruncated()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "This is a short transcript with just a few words.";
        var maxInputTokens = 10000; // Large limit

        // Act
        var result = await processor.ProcessTranscriptAsync(content, maxInputTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var processed = result.Value;
        processed.WasTruncated.Should().BeFalse();
        processed.Content.Should().Be(content);
        processed.OriginalWordCount.Should().Be(processor.CountWords(content));
        processed.FinalWordCount.Should().Be(processed.OriginalWordCount);
    }

    [Fact]
    public async Task ProcessTranscriptAsync_WhenContentExceedsLimit_ReturnsTruncated()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = string.Join(" ", Enumerable.Repeat("word", 1000)); // 1000 words
        var maxInputTokens = 100; // Small limit that will force truncation

        // Act
        var result = await processor.ProcessTranscriptAsync(content, maxInputTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var processed = result.Value;
        processed.WasTruncated.Should().BeTrue();
        processed.OriginalWordCount.Should().Be(1000);
        processed.FinalWordCount.Should().BeLessThan(processed.OriginalWordCount);
        processed.EstimatedTokenCount.Should().BeLessThanOrEqualTo(maxInputTokens);
    }

    [Fact]
    public async Task ProcessTranscriptAsync_AppliesSafetyFactor()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = string.Join(" ", Enumerable.Repeat("word", 1000)); // 1000 words
        var maxInputTokens = 1000;

        // Act
        var result = await processor.ProcessTranscriptAsync(content, maxInputTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var processed = result.Value;
        // Safety factor is 0.8, so estimated tokens should be <= 800 (80% of 1000)
        processed.EstimatedTokenCount.Should().BeLessThanOrEqualTo((int)(maxInputTokens * 0.8) + 10); // +10 for rounding
    }

    [Fact]
    public async Task ProcessTranscriptAsync_WithEmptyContent_ReturnsFailure()
    {
        // Arrange
        var processor = CreateProcessor();
        var maxInputTokens = 1000;

        // Act
        var result = await processor.ProcessTranscriptAsync(string.Empty, maxInputTokens);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task ProcessTranscriptAsync_WithNullContent_ReturnsFailure()
    {
        // Arrange
        var processor = CreateProcessor();
        var maxInputTokens = 1000;

        // Act
        var result = await processor.ProcessTranscriptAsync(null!, maxInputTokens);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessTranscriptAsync_WithZeroMaxTokens_ReturnsFailure()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "Some content";

        // Act
        var result = await processor.ProcessTranscriptAsync(content, 0);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("positive");
    }

    [Fact]
    public async Task ProcessTranscriptAsync_WithNegativeMaxTokens_ReturnsFailure()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "Some content";

        // Act
        var result = await processor.ProcessTranscriptAsync(content, -100);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("positive");
    }

    [Fact]
    public async Task ProcessTranscriptAsync_SetsCorrectEstimatedTokenCount()
    {
        // Arrange
        var processor = CreateProcessor();
        var content = "one two three four five"; // 5 words
        var maxInputTokens = 10000;

        // Act
        var result = await processor.ProcessTranscriptAsync(content, maxInputTokens);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var processed = result.Value;
        var expectedTokens = processor.EstimateTokenCount(content);
        processed.EstimatedTokenCount.Should().Be(expectedTokens);
    }

    #endregion

    #region Helper Methods

    private TranscriptProcessor CreateProcessor()
    {
        return new TranscriptProcessor(_mockLogger.Object);
    }

    #endregion
}
