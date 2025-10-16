using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.ThisWeek.Commands;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.ThisWeek;

/// <summary>
/// Unit tests for CreateWeeklyReviewHandler per contract specification.
/// Tests the weekly review aggregation workflow from daily entries to weekly summary.
/// </summary>
public sealed class CreateWeeklyReviewHandlerTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorage;
    private readonly Mock<ILlmProviderFactory> _mockLlmFactory;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IPromptTemplateLoader> _mockPromptLoader;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CreateWeeklyReviewHandler>> _mockLogger;
    private readonly TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler _listTemplatesHandler;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly CreateWeeklyReviewHandler _handler;

    public CreateWeeklyReviewHandlerTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLlmFactory = new Mock<ILlmProviderFactory>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockPromptLoader = new Mock<IPromptTemplateLoader>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<CreateWeeklyReviewHandler>>();
        var mockListTemplatesLogger = new Mock<ILogger<TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler>>();
        _listTemplatesHandler = new TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler(
            _mockPromptLoader.Object,
            mockListTemplatesLogger.Object);
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();

        // Setup default configuration values
        _mockConfiguration.Setup(c => c["Llm:Provider"]).Returns("OpenAI");
        _mockConfiguration.Setup(c => c["Llm:Model"]).Returns("gpt-4o");

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

        string mockLlmResponse = @"## Top 3 Accomplishments
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
            .ReturnsAsync(mockLlmResponse);

        _handler = new CreateWeeklyReviewHandler(
            _mockStorage.Object,
            _mockLlmFactory.Object,
            _mockPromptLoader.Object,
            _mockAuthService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _listTemplatesHandler,
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

        var command = new CreateWeeklyReviewCommand();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Command.Should().Be("thisweek");
        result.Value.Summary.Should().NotBeNull();
        result.Value.Summary.TopAccomplishments.Should().HaveCount(3);
        result.Value.Summary.TopChallenges.Should().HaveCount(3);
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

        var command = new CreateWeeklyReviewCommand();

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

        var command = new CreateWeeklyReviewCommand { CustomDateRange = customRange };

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

        var command = new CreateWeeklyReviewCommand();

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

        var command = new CreateWeeklyReviewCommand();

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

        var command = new CreateWeeklyReviewCommand { CustomDateRange = customRange };

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

        var command = new CreateWeeklyReviewCommand { CustomDateRange = customRange };

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("at least 3 days");
    }

    [Fact]
    public async Task Handle_EnsuresExactly3Accomplishments()
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

        var command = new CreateWeeklyReviewCommand();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.TopAccomplishments.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_EnsuresExactly3Challenges()
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

        var command = new CreateWeeklyReviewCommand();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Summary.TopChallenges.Should().HaveCount(3);
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

        var command = new CreateWeeklyReviewCommand();

        // Act
        Result<WeeklyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Summary.Should().NotBeNull();
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
                LlmResponse = $"Sample LLM response {i + 1}",
                Summary = new DailySummary
                {
                    KeyEvents = new List<string> { $"Event {i + 1}", $"Event {i + 2}" },
                    Themes = new List<string> { $"Theme {i + 1}" },
                    TodoItems = new List<TodoItem>
                    {
                        new() { Description = $"Todo {i + 1}", IsCompleted = false }
                    },
                    ImportantPeople = new List<string> { $"Person {i + 1}" },
                    NotableTasks = new List<string> { $"Task {i + 1}" }
                },
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "openai",
                    LlmModel = "gpt-4"
                }
            });
        }

        return entries;
    }
}
