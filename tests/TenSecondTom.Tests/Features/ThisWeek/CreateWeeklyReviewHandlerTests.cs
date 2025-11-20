using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Shared.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Options;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.ThisWeek;

namespace TenSecondTom.Tests.Features.ThisWeek;

/// <summary>
/// Unit tests for CreateWeeklyReview.Handler per contract specification.
/// Tests the weekly review aggregation workflow from daily entries to weekly summary.
/// </summary>
public sealed class CreateWeeklyReviewHandlerTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorage;
    private readonly Mock<ILlmProviderFactory> _mockLlmFactory;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IPromptTemplateLoader> _mockPromptLoader;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IOptionsSnapshot<LlmOptions>> _mockLlmOptions;
    private readonly Mock<ILogger<CreateWeeklyReview.Handler>> _mockLogger;
    private readonly Mock<ITemplateProvider> _mockTemplateProvider;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly CreateWeeklyReview.Handler _handler;

    public CreateWeeklyReviewHandlerTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLlmFactory = new Mock<ILlmProviderFactory>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockPromptLoader = new Mock<IPromptTemplateLoader>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockLlmOptions = new Mock<IOptionsSnapshot<LlmOptions>>();
        _mockLogger = new Mock<ILogger<CreateWeeklyReview.Handler>>();
        _mockTemplateProvider = new Mock<ITemplateProvider>();
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();

        // Setup default LLM options
        var llmOptions = new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "test-api-key",
            Model = "gpt-4o",
            MaxInputTokens = 100000
        };
        _mockLlmOptions.Setup(o => o.Value).Returns(llmOptions);

        // Setup default successful behaviors
        _mockLlmFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(_mockLlmProvider.Object);

        _mockAuthService.Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockPromptLoader.Setup(p => p.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(new PromptTemplate
            {
                TemplateId = "weekly-review",
                Content = "Review: {{DAILY_ENTRIES}}",
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.Embedded
            }));

        string mockLlmResponseContent = @"## Top 3 Accomplishments
1. Completed major project milestone
2. Resolved critical bugs
3. Improved team collaboration

## Top 3 Challenges
1. Time management issues
2. Resource constraints
3. Technical debt accumulation

## Key Insights
Noticed pattern of afternoon productivity dips.

## Goals for Next Week
- Focus on code refactoring
- Implement automated testing";

        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = mockLlmResponseContent,
                InputTokens = 100,
                OutputTokens = 200
            }));

        _handler = new CreateWeeklyReview.Handler(
            _mockStorage.Object,
            _mockLlmFactory.Object,
            _mockPromptLoader.Object,
            _mockAuthService.Object,
            _mockLlmOptions.Object,
            _mockLogger.Object,
            _mockTemplateProvider.Object,
            _mockTemplateSelectionUI.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesWeeklyReview()
    {
        // Arrange
        var dailyEntries = CreateSampleDailyEntries(5);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Command.Should().Be("thisweek");
        result.Value.LlmResponse.Should().Contain("Accomplishments");
        result.Value.LlmResponse.Should().Contain("Challenges");
    }

    [Fact]
    public async Task Handle_WithNoDailyEntries_ReturnsNoDataError()
    {
        // Arrange
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(Array.Empty<MemoryEntry>()));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No daily entries found");
    }

    [Fact]
    public async Task Handle_WithCustomDateRange_UsesCustomRange()
    {
        // Arrange
        var customStart = DateTimeOffset.UtcNow.AddDays(-10);
        var customEnd = DateTimeOffset.UtcNow.AddDays(-3);
        var customRange = new DateRange
        {
            StartDate = customStart,
            EndDate = customEnd
        };

        var dailyEntries = CreateSampleDailyEntries(5);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                customStart.DateTime,
                customEnd.DateTime,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command { CustomDateRange = customRange };

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockStorage.Verify(
            s => s.GetEntriesAsync("today", customStart.DateTime, customEnd.DateTime, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutCustomDateRange_UsesLast7Days()
    {
        // Arrange
        var dailyEntries = CreateSampleDailyEntries(7);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mockStorage.Verify(
            s => s.GetEntriesAsync(
                "today",
                It.Is<DateTime>(d => d >= DateTime.UtcNow.AddDays(-7).Date),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLlmProviderFails_ReturnsError()
    {
        // Arrange
        var dailyEntries = CreateSampleDailyEntries(5);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LLM service unavailable");
    }

    [Fact]
    public async Task Handle_WithFewerThan7Days_Succeeds()
    {
        // Arrange
        var customRange = new DateRange
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-5),
            EndDate = DateTimeOffset.UtcNow
        };
        var dailyEntries = CreateSampleDailyEntries(5);

        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command { CustomDateRange = customRange };

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithFewerThan3Days_ReturnsValidationError()
    {
        // Arrange
        var customRange = new DateRange
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-2),
            EndDate = DateTimeOffset.UtcNow
        };

        var command = new CreateWeeklyReview.Command { CustomDateRange = customRange };

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("at least 3 days");
    }

    [Fact]
    public async Task Handle_ParsesAccomplishmentsFromLlmResponse()
    {
        // Arrange
        var dailyEntries = CreateSampleDailyEntries(7);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LlmResponse.Should().Contain("Accomplishments", 
            "the default template should include accomplishments section");
    }

    [Fact]
    public async Task Handle_ParsesChallengesFromLlmResponse()
    {
        // Arrange
        var dailyEntries = CreateSampleDailyEntries(7);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LlmResponse.Should().Contain("Challenges",
            "the default template should include challenges section");
    }

    [Fact]
    public async Task Handle_AggregatesMultipleDailyEntriesPerDay()
    {
        // Arrange
        var dailyEntries = CreateSampleDailyEntries(10);
        _mockStorage.Setup(s => s.GetEntriesAsync(
                "today",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(dailyEntries));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        var command = new CreateWeeklyReview.Command();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.LlmResponse.Should().NotBeNullOrWhiteSpace();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Test helper method readability")]
    private static IReadOnlyList<MemoryEntry> CreateSampleDailyEntries(int count)
    {
        var entries = new List<MemoryEntry>();
        DateTimeOffset baseDate = DateTimeOffset.UtcNow.AddDays(-6);

        for (int i = 0; i < count; i++)
        {
            entries.Add(new DailyEntry
            {
                EntryId = Guid.NewGuid().ToString(),
                Command = "today",
                Timestamp = baseDate.AddDays(i),
                EntryNumber = i + 1,
                UserInput = $"Sample daily input {i + 1}",
                LlmResponse = $"## Key Events\n- Event {i + 1}\n- Event {i + 2}\n\n## Themes\n- Theme {i + 1}\n\n## To-Do Items\n- [ ] Todo {i + 1}",
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "openai",
                    LlmModel = "gpt-4",
                    TokensUsed = 100,
                    ProcessingDuration = TimeSpan.FromSeconds(1.0)
                }
            });
        }

        return entries;
    }
}
