using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.Today;

/// <summary>
/// Unit tests for CreateDailyEntryHandler per contract specification.
/// Tests cover command validation, LLM provider interaction, storage operations,
/// authentication, and error handling scenarios.
/// </summary>
public sealed class CreateDailyEntryHandlerTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorage;
    private readonly Mock<ILlmProviderFactory> _mockLlmFactory;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IPromptTemplateLoader> _mockPromptLoader;
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<CreateDailyEntryHandler>> _mockLogger;
    private readonly TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler _listTemplatesHandler;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly CreateDailyEntryHandler _handler;

    public CreateDailyEntryHandlerTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLlmFactory = new Mock<ILlmProviderFactory>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockPromptLoader = new Mock<IPromptTemplateLoader>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<CreateDailyEntryHandler>>();
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
                TemplateId = "daily-summary",
                Content = "Summarize: {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded
            }));

        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Success("## Summary\nKey events: meeting"));

        _mockStorage.Setup(s => s.CountEntriesAsync(
                It.IsAny<string>(), 
                It.IsAny<DateTime>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        _handler = new CreateDailyEntryHandler(
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
    public async Task Handle_WithValidCommand_CreatesDailyEntry()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Had a productive meeting",
                ["Plans for tomorrow?"] = "Finish the design doc",
                ["How are you feeling?"] = "Energized and focused"
            }
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Command.Should().Be("today");
        result.Value.EntryNumber.Should().Be(1);
        result.Value.EntryId.Should().StartWith("today-");
        result.Value.UserInput.Should().Contain("Had a productive meeting");
        result.Value.Summary.Should().NotBeNull();

        _mockStorage.Verify(s => s.SaveAsync(
            It.Is<DailyEntry>(e => e.Command == "today"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyResponses_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>()
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Daily responses cannot be empty");

        _mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<DailyEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithFewerThan3Responses_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["Question 1?"] = "Answer 1",
                ["Question 2?"] = "Answer 2"
            }
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Daily reflection requires 3-5 responses");

        _mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<DailyEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMoreThan5Responses_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["Question 1?"] = "Answer 1",
                ["Question 2?"] = "Answer 2",
                ["Question 3?"] = "Answer 3",
                ["Question 4?"] = "Answer 4",
                ["Question 5?"] = "Answer 5",
                ["Question 6?"] = "Answer 6"
            }
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Daily reflection requires 3-5 responses");

        _mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<DailyEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenLlmProviderFails_SavesUserInputAndReturnsError()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Had a productive meeting",
                ["Plans for tomorrow?"] = "Finish the design doc",
                ["How are you feeling?"] = "Energized and focused"
            }
        };

        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(), 
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Failure("API rate limit exceeded"));

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("LLM provider error");
        result.Error.Should().Contain("User input saved for retry");

        // Verify partial entry was saved (user input only, no LLM response)
        _mockStorage.Verify(s => s.SaveAsync(
            It.Is<DailyEntry>(e => 
                e.UserInput.Contains("Had a productive meeting") && 
                string.IsNullOrEmpty(e.LlmResponse)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStorageFails_ReturnsStorageError()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Had a productive meeting",
                ["Plans for tomorrow?"] = "Finish the design doc",
                ["How are you feeling?"] = "Energized and focused"
            }
        };

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<MemoryEntry>.Failure("Disk full"));

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to save entry");
        result.Error.Should().Contain("Disk full");
    }

    [Fact]
    public async Task Handle_WithOpenAIProvider_UsesOpenAI()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Had a productive meeting",
                ["Plans for tomorrow?"] = "Finish the design doc",
                ["How are you feeling?"] = "Energized and focused"
            },
            LlmProviderOverride = "OpenAI"
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockLlmFactory.Verify(f => f.CreateProvider("OpenAI"), Times.Once);
        _mockLlmProvider.Verify(p => p.GenerateCompletionAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<int?>(),
            It.IsAny<double?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAnthropicProvider_UsesAnthropic()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Had a productive meeting",
                ["Plans for tomorrow?"] = "Finish the design doc",
                ["How are you feeling?"] = "Energized and focused"
            },
            LlmProviderOverride = "Anthropic"
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockLlmFactory.Verify(f => f.CreateProvider("Anthropic"), Times.Once);
        _mockLlmProvider.Verify(p => p.GenerateCompletionAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<int?>(),
            It.IsAny<double?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidProvider_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Had a productive meeting",
                ["Plans for tomorrow?"] = "Finish the design doc",
                ["How are you feeling?"] = "Energized and focused"
            },
            LlmProviderOverride = "InvalidProvider"
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid LLM provider");
        result.Error.Should().Contain("Use 'OpenAI' or 'Anthropic'");

        _mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<DailyEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MultipleCallsSameDay_IncrementsEntryNumber()
    {
        // Arrange
        var command1 = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Morning meeting",
                ["Plans for tomorrow?"] = "Design review",
                ["How are you feeling?"] = "Good"
            }
        };

        var command2 = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Afternoon coding session",
                ["Plans for tomorrow?"] = "Code review",
                ["How are you feeling?"] = "Productive"
            }
        };

        // Setup storage to simulate existing entry
        var callCount = 0;
        _mockStorage.Setup(s => s.CountEntriesAsync(
                It.Is<string>(cmd => cmd == "today"),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result<int>.Success(callCount++));

        // Act
        Result<DailyEntry> result1 = await _handler.Handle(command1, CancellationToken.None);
        Result<DailyEntry> result2 = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result1.Value.EntryNumber.Should().Be(1);
        result1.Value.EntryId.Should().Contain("-1");

        result2.IsSuccess.Should().BeTrue();
        result2.Value.EntryNumber.Should().Be(2);
        result2.Value.EntryId.Should().Contain("-2");

        _mockStorage.Verify(s => s.CountEntriesAsync(
            It.Is<string>(cmd => cmd == "today"),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
