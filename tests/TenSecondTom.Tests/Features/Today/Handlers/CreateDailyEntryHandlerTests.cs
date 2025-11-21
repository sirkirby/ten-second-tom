using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Shared.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Today;

namespace TenSecondTom.Tests.Features.Today;

/// <summary>
/// Unit tests for CreateDailyEntry.Handler per contract specification.
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
    private readonly Mock<IOptionsSnapshot<LlmOptions>> _mockLlmOptions;
    private readonly Mock<ILogger<CreateDailyEntry.Handler>> _mockLogger;
    private readonly Mock<ITemplateProvider> _mockTemplateProvider;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly CreateDailyEntry.Handler _handler;

    public CreateDailyEntryHandlerTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLlmFactory = new Mock<ILlmProviderFactory>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockPromptLoader = new Mock<IPromptTemplateLoader>();
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockLlmOptions = new Mock<IOptionsSnapshot<LlmOptions>>();
        _mockLogger = new Mock<ILogger<CreateDailyEntry.Handler>>();
        _mockTemplateProvider = new Mock<ITemplateProvider>();
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();

        // Setup default LLM options
        _mockLlmOptions.Setup(o => o.Value).Returns(new LlmOptions
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = "test-key",
            Model = "gpt-4o",
            MaxInputTokens = 100000
        });

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
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = "## Summary\nKey events: meeting",
                InputTokens = 10,
                OutputTokens = 20
            }));

        _mockStorage.Setup(s => s.CountEntriesAsync(
                It.IsAny<string>(), 
                It.IsAny<DateTime>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));

        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));

        _handler = new CreateDailyEntry.Handler(
            _mockStorage.Object,
            _mockLlmFactory.Object,
            _mockPromptLoader.Object,
            _mockLlmOptions.Object,
            _mockAuthService.Object,
            _mockLogger.Object,
            _mockTemplateProvider.Object,
            _mockTemplateSelectionUI.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesDailyEntry()
    {
        // Arrange
        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting today.\nPlanning to finish the design doc tomorrow.\nFeeling energized and focused."
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
        result.Value.LlmResponse.Should().NotBeNullOrEmpty();

        _mockStorage.Verify(s => s.SaveAsync(
            It.Is<DailyEntry>(e => e.Command == "today"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyContent_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateDailyEntry.Command
        {
            Content = string.Empty
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("content");

        _mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<DailyEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWhitespaceOnlyContent_ReturnsValidationError()
    {
        // Arrange
        var command = new CreateDailyEntry.Command
        {
            Content = "   \t\n   "
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("content");

        _mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<DailyEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithMultiLineContent_PreservesFormatting()
    {
        // Arrange
        var multiLineContent = "First line of my day\nSecond line with details\n\nThird line after blank";
        var command = new CreateDailyEntry.Command
        {
            Content = multiLineContent
        };

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserInput.Should().Be(multiLineContent);
        result.Value.UserInput.Should().Contain("\n");

        _mockStorage.Verify(s => s.SaveAsync(
            It.Is<DailyEntry>(e => e.UserInput == multiLineContent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLlmProviderFails_SavesUserInputAndReturnsError()
    {
        // Arrange
        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting today.\nPlanning to finish the design doc tomorrow.\nFeeling energized and focused."
        };

        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Failure("API rate limit exceeded"));

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
        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting today.\nPlanning to finish the design doc tomorrow.\nFeeling energized and focused."
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
        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting today.\nPlanning to finish the design doc tomorrow.\nFeeling energized and focused.",
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
        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting today.\nPlanning to finish the design doc tomorrow.\nFeeling energized and focused.",
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
        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting today.\nPlanning to finish the design doc tomorrow.\nFeeling energized and focused.",
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
        var command1 = new CreateDailyEntry.Command
        {
            Content = "Morning meeting notes and reflections."
        };

        var command2 = new CreateDailyEntry.Command
        {
            Content = "Afternoon coding session recap."
        };

        // Setup storage to simulate existing entry (now counts in note directory)
        var callCount = 0;
        _mockStorage.Setup(s => s.CountEntriesAsync(
                It.Is<string>(cmd => cmd == "note"),
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
            It.Is<string>(cmd => cmd == "note"),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_WithTemplateName_UsesSpecifiedTemplate()
    {
        // Arrange
        var customTemplateId = "custom-daily-template";
        var command = new CreateDailyEntry.Command
        {
            Content = "Daily content for custom template",
            TemplateName = customTemplateId
        };

        _mockPromptLoader.Setup(p => p.LoadTemplateAsync(customTemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(new PromptTemplate
            {
                TemplateId = customTemplateId,
                Content = "Custom template: {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem
            }));

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        _mockPromptLoader.Verify(p => p.LoadTemplateAsync(customTemplateId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidTemplateName_FallsBackToDefaultAndLogsWarning()
    {
        // Arrange
        var invalidTemplateId = "non-existent-template";
        var command = new CreateDailyEntry.Command
        {
            Content = "Daily content with invalid template",
            TemplateName = invalidTemplateId
        };

        // Setup: LoadTemplateAsync returns failure for invalid template
        _mockPromptLoader.Setup(p => p.LoadTemplateAsync(invalidTemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Failure($"Template '{invalidTemplateId}' not found"));

        // Setup: LoadTemplateAsync succeeds for default template
        _mockPromptLoader.Setup(p => p.LoadTemplateAsync(TemplateConstants.DailySummaryTemplateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(new PromptTemplate
            {
                TemplateId = TemplateConstants.DailySummaryTemplateId,
                Content = "Default: {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded
            }));

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the invalid template was attempted
        _mockPromptLoader.Verify(p => p.LoadTemplateAsync(invalidTemplateId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify fallback to default template
        _mockPromptLoader.Verify(p => p.LoadTemplateAsync(TemplateConstants.DailySummaryTemplateId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(invalidTemplateId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithUseDefaultTemplate_SkipsTemplateSelection()
    {
        // Arrange
        var command = new CreateDailyEntry.Command
        {
            Content = "Daily content for default template",
            UseDefaultTemplate = true
        };

        // Setup multiple templates to ensure selection is bypassed
        _mockPromptLoader.Setup(p => p.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success([
                new PromptTemplate { TemplateId = "template1", Content = "Template 1", TemplateType = TemplateType.Daily, Source = TemplateSource.FileSystem },
                new PromptTemplate { TemplateId = "template2", Content = "Template 2", TemplateType = TemplateType.Daily, Source = TemplateSource.FileSystem }
            ]));

        // Act
        Result<DailyEntry> result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that the default template was loaded directly
        _mockPromptLoader.Verify(p => p.LoadTemplateAsync(TemplateConstants.DailySummaryTemplateId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify template selection UI was never shown
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(It.IsAny<IReadOnlyList<TenSecondTom.Infrastructure.Prompts.TemplateInfo>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
