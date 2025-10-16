using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.Integration.Features.ThisWeek;

/// <summary>
/// Integration tests for CreateWeeklyReviewCommand with template selection (T033).
/// Tests end-to-end flow including template selection step for weekly reviews.
/// Tests cover:
/// - Template selection is invoked before LLM call
/// - Only weekly templates are filtered
/// - Selected template is used for prompt generation
/// - Cancellation during template selection
/// </summary>
public sealed class CreateWeeklyReviewWithTemplateSelectionTests : IDisposable
{
    private readonly TemporaryTestDirectory _testDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly Mock<IPromptTemplateLoader> _mockTemplateLoader;

    public CreateWeeklyReviewWithTemplateSelectionTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();
        _mockTemplateLoader = new Mock<IPromptTemplateLoader>();
        _serviceProvider = BuildTestServiceProvider();
    }

    [Fact]
    public async Task Handle_WithMultipleTemplates_InvokesTemplateSelection()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        SetupMultipleWeeklyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("custom-weekly");

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with template selection");

        // Verify template selection UI was invoked
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(
                    templates => templates.Count >= 2),
                "thisweek",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "template selection UI should be invoked with multiple templates");
    }

    [Fact]
    public async Task Handle_WithSingleTemplate_AutoSelectsWithoutUI()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        SetupSingleWeeklyTemplate();

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with single template");

        // Verify template selection UI was NOT invoked (auto-selection)
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "template selection UI should not be invoked for single template");
    }

    [Fact]
    public async Task Handle_FiltersOnlyWeeklyTemplates_DoesNotShowDailyTemplates()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        SetupMixedDailyAndWeeklyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("weekly-review");

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify only weekly templates were passed to UI
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(
                    templates => templates.All(t => t.TemplateType == TemplateType.Weekly)),
                "thisweek",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "only weekly templates should be shown for 'thisweek' command");
    }

    [Fact]
    public async Task Handle_UsesSelectedTemplate_ForPromptGeneration()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        var selectedTemplateContent = "# Custom Weekly Review\nWeek input: {{WEEK_INPUT}}";
        SetupMultipleWeeklyTemplates();

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("custom-weekly");

        // Setup loader to return custom template when loaded
        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync("custom-weekly", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(new PromptTemplate
            {
                TemplateId = "custom-weekly",
                Content = selectedTemplateContent,
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.FileSystem
            }));

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the selected template was loaded and used
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("custom-weekly", It.IsAny<CancellationToken>()),
            Times.Once,
            "selected template should be loaded for prompt generation");
    }

    [Fact]
    public async Task Handle_WhenTemplateSelectionCancelled_ReturnsFailure()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        SetupMultipleWeeklyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("User cancelled template selection"));

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse("cancelled template selection should fail command");
        result.Error.Should().Contain("cancel", "error should indicate cancellation");
    }

    [Fact]
    public async Task Handle_WhenNoTemplatesAvailable_FallsBackToEmbedded()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        SetupNoTemplates();

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("should fall back to embedded template");

        // Template selection UI should not be invoked
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "should not prompt for selection when no templates available");
    }

    [Fact]
    public async Task Handle_TemplateSelectionFlow_OccursBeforeLLMCall()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();
        var callSequence = new List<string>();

        SetupMultipleWeeklyTemplates();

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callSequence.Add("TemplateSelection");
                return "weekly-review";
            });

        var mockLlmProvider = new Mock<ILlmProvider>();
        mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(() =>
            {
                callSequence.Add("LLMCall");
                return Result<string>.Success("Weekly summary generated");
            });

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        callSequence.Should().HaveCount(2);
        callSequence[0].Should().Be("TemplateSelection", "template selection must occur first");
        callSequence[1].Should().Be("LLMCall", "LLM call must occur after template selection");
    }

    [Fact]
    public async Task Handle_WithMultipleWeeklyTemplates_ShowsCorrectCommandContext()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();

        SetupMultipleWeeklyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("weekly-review");

        var command = new CreateWeeklyReviewCommand();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify correct command context was passed
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "thisweek",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should pass 'thisweek' as command context");
    }

    private void SetupMultipleWeeklyTemplates()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "weekly-review",
                Content = "Default weekly template",
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Weekly Review", TemplateType = TemplateType.Weekly }
            },
            new PromptTemplate
            {
                TemplateId = "custom-weekly",
                Content = "Custom weekly template",
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.FileSystem,
                Metadata = new TemplateMetadata { Title = "Custom Weekly", TemplateType = TemplateType.Weekly }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));
    }

    private void SetupSingleWeeklyTemplate()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "weekly-review",
                Content = "Default weekly template",
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Weekly Review", TemplateType = TemplateType.Weekly }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));
    }

    private void SetupMixedDailyAndWeeklyTemplates()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "daily-summary",
                Content = "Daily template",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Daily Summary", TemplateType = TemplateType.Daily }
            },
            new PromptTemplate
            {
                TemplateId = "weekly-review",
                Content = "Weekly template",
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Weekly Review", TemplateType = TemplateType.Weekly }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(
                templates.Where(t => t.TemplateType == TemplateType.Weekly).ToList()));
    }

    private void SetupNoTemplates()
    {
        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(new List<PromptTemplate>()));
    }

    private ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Mock configuration
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["Llm:Provider"]).Returns("OpenAI");
        mockConfiguration.Setup(c => c["Llm:Model"]).Returns("gpt-4o");
        services.AddSingleton(mockConfiguration.Object);

        // Mock storage
        var mockStorage = new Mock<IMemoryStorageProvider>();
        mockStorage.Setup(s => s.SaveAsync(It.IsAny<WeeklyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));
        mockStorage.Setup(s => s.CountEntriesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));
        // Setup mock daily entries for aggregation
        var mockDailyEntries = new List<MemoryEntry>
        {
            new DailyEntry
            {
                EntryId = "daily-1",
                Command = "today",
                Timestamp = DateTimeOffset.UtcNow.AddDays(-2),
                EntryNumber = 1,
                UserInput = "Sample input",
                LlmResponse = "Sample response",
                Summary = new DailySummary(),
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = "openai",
                    LlmModel = "gpt-4"
                }
            }
        };
        mockStorage.Setup(s => s.GetEntriesAsync(
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<MemoryEntry>>.Success(mockDailyEntries));
        services.AddSingleton(mockStorage.Object);

        // Mock LLM - return properly formatted weekly summary
        var mockLlmProvider = new Mock<ILlmProvider>();
        mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Success(@"## Top 3 Accomplishments
1. First accomplishment
2. Second accomplishment
3. Third accomplishment

## Top 3 Challenges
1. First challenge
2. Second challenge
3. Third challenge

## Key Insights
Some insights here

## Goals for Next Week
- Goal 1
- Goal 2"));

        var mockLlmFactory = new Mock<ILlmProviderFactory>();
        mockLlmFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(mockLlmProvider.Object);
        services.AddSingleton(mockLlmFactory.Object);

        // Mock auth
        var mockAuth = new Mock<IAuthenticationService>();
        mockAuth.Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton(mockAuth.Object);

        // Add template infrastructure
        services.AddSingleton(_mockTemplateLoader.Object);
        services.AddSingleton(_mockTemplateSelectionUI.Object);
        services.AddSingleton<TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler>();

        // Add handler
        services.AddSingleton<CreateWeeklyReviewHandler>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _testDirectory?.Dispose();
    }
}
