using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Retry.Commands;
using TenSecondTom.Features.Retry.Handlers;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.Retry;

/// <summary>
/// Unit tests for RetryFailedSummarizationHandler.
/// Tests the retry mechanism for entries where LLM summarization failed.
/// </summary>
public sealed class RetryFailedSummarizationHandlerTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorage;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<ILogger<RetryFailedSummarizationHandler>> _mockLogger;

    public RetryFailedSummarizationHandlerTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockLogger = new Mock<ILogger<RetryFailedSummarizationHandler>>();
    }

    [Fact]
    public async Task Handle_WithNoEntryId_RetriesAllFailedEntries()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        var failedEntry = CreateFailedEntry("today-10-02-2025-1");
        
        _mockStorage
            .Setup(s => s.GetEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(new[] { failedEntry }));

        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = "New summary content",
                InputTokens = 10,
                OutputTokens = 20
            }));

        _mockStorage
            .Setup(s => s.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken _) => Result<MemoryEntry>.Success(e));

        var command = new RetryFailedSummarizationCommand();

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAttempted.Should().Be(1);
        result.Value.SuccessCount.Should().Be(1);
        result.Value.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithSpecificEntryId_RetriesOnlyThatEntry()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        var failedEntry = CreateFailedEntry("today-10-02-2025-1");
        
        _mockStorage
            .Setup(s => s.GetEntryByIdAsync("today-10-02-2025-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(failedEntry));

        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = "New summary content",
                InputTokens = 10,
                OutputTokens = 20
            }));

        _mockStorage
            .Setup(s => s.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry e, CancellationToken _) => Result<MemoryEntry>.Success(e));

        var command = new RetryFailedSummarizationCommand
        {
            EntryId = "today-10-02-2025-1"
        };

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAttempted.Should().Be(1);
        result.Value.SuccessCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenEntryNotFound_ReturnsError()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        _mockStorage
            .Setup(s => s.GetEntryByIdAsync("nonexistent-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(null));

        var command = new RetryFailedSummarizationCommand
        {
            EntryId = "nonexistent-id"
        };

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_WhenEntryNotFailed_SkipsEntry()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        var successfulEntry = CreateSuccessfulEntry("today-10-02-2025-1");
        
        _mockStorage
            .Setup(s => s.GetEntryByIdAsync("today-10-02-2025-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(successfulEntry));

        var command = new RetryFailedSummarizationCommand
        {
            EntryId = "today-10-02-2025-1"
        };

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("did not fail");
    }

    [Fact]
    public async Task Handle_WhenLlmRetryFails_RecordsFailure()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        var failedEntry = CreateFailedEntry("today-10-02-2025-1");
        
        _mockStorage
            .Setup(s => s.GetEntryByIdAsync("today-10-02-2025-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(failedEntry));

        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Failure("LLM API error"));

        var command = new RetryFailedSummarizationCommand
        {
            EntryId = "today-10-02-2025-1"
        };

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAttempted.Should().Be(1);
        result.Value.SuccessCount.Should().Be(0);
        result.Value.FailureCount.Should().Be(1);
        result.Value.Errors.Should().ContainKey("today-10-02-2025-1");
    }

    [Fact]
    public async Task Handle_OnSuccess_RemovesFailedFlag()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        var failedEntry = CreateFailedEntry("today-10-02-2025-1");
        
        _mockStorage
            .Setup(s => s.GetEntryByIdAsync("today-10-02-2025-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry?>.Success(failedEntry));

        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = "New summary content",
                InputTokens = 10,
                OutputTokens = 20
            }));

        MemoryEntry? savedEntry = null;
        _mockStorage
            .Setup(s => s.SaveAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .Callback<MemoryEntry, CancellationToken>((e, _) => savedEntry = e)
            .ReturnsAsync((MemoryEntry e, CancellationToken _) => Result<MemoryEntry>.Success(e));

        var command = new RetryFailedSummarizationCommand
        {
            EntryId = "today-10-02-2025-1"
        };

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        savedEntry.Should().NotBeNull();
        savedEntry!.Metadata.CustomTags.Should().NotContainKey("summarization-failed");
    }

    [Fact]
    public async Task Handle_WithNoFailedEntries_ReturnsZeroAttempts()
    {
        // Arrange
        var handler = new RetryFailedSummarizationHandler(
            _mockStorage.Object,
            _mockLlmProvider.Object,
            _mockLogger.Object);

        _mockStorage
            .Setup(s => s.GetEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(Array.Empty<MemoryEntry>()));

        var command = new RetryFailedSummarizationCommand();

        // Act
        Result<RetryResult> result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalAttempted.Should().Be(0);
        result.Value.SuccessCount.Should().Be(0);
        result.Value.FailureCount.Should().Be(0);
    }

    /// <summary>
    /// Creates a test entry with summarization-failed flag set to true.
    /// </summary>
    private static DailyEntry CreateFailedEntry(string entryId)
    {
        return new DailyEntry
        {
            EntryId = entryId,
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test user input that needs summarization",
            LlmResponse = string.Empty, // Empty because summarization failed
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4",
                CustomTags = new Dictionary<string, string>
                {
                    ["summarization-failed"] = "true",
                    ["original-error"] = "LLM API timeout"
                }
            }
        };
    }

    /// <summary>
    /// Creates a test entry that was successfully summarized.
    /// </summary>
    private static DailyEntry CreateSuccessfulEntry(string entryId)
    {
        return new DailyEntry
        {
            EntryId = entryId,
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test user input",
            LlmResponse = "Test summary",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };
    }
}
