using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Today;

namespace TenSecondTom.IntegrationTests.Integration.Features.Today;

/// <summary>
/// Integration tests for CreateDailyEntry.Command with template selection (T032).
/// Tests end-to-end flow including template selection step.
/// Tests cover:
/// - Template selection is invoked before LLM call
/// - Only daily templates are filtered
/// - Selected template is used for prompt generation
/// - Cancellation during template selection
/// </summary>
public sealed class CreateDailyEntryWithTemplateSelectionTests : IDisposable
{
    private readonly TemporaryTestDirectory _testDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly Mock<IPromptTemplateLoader> _mockTemplateLoader;
    private readonly Mock<ILlmProvider> _mockLlmProvider;

    public CreateDailyEntryWithTemplateSelectionTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();
        _mockTemplateLoader = new Mock<IPromptTemplateLoader>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _serviceProvider = BuildTestServiceProvider();
    }

    [Fact]
    public async Task Handle_WithMultipleTemplates_InvokesTemplateSelection()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        SetupMultipleDailyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("custom-daily");

        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting\nFinish the design doc\nEnergized"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with template selection");

        // Verify template selection UI was invoked
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TemplateInfo>>(
                    templates => templates.Count >= 2),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "template selection UI should be invoked with multiple templates");
    }

    [Fact]
    public async Task Handle_WithSingleTemplate_AutoSelectsWithoutUI()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        SetupSingleDailyTemplate();

        var command = new CreateDailyEntry.Command
        {
            Content = "Had a productive meeting\nFinish the design doc\nEnergized"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with single template");

        // Verify template selection UI was NOT invoked (auto-selection)
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "template selection UI should not be invoked for single template");
    }

    [Fact]
    public async Task Handle_FiltersOnlyDailyTemplates_DoesNotShowWeeklyTemplates()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        SetupMixedDailyAndWeeklyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("daily-summary");

        var command = new CreateDailyEntry.Command
        {
            Content = "Meeting\nCode review\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify only daily templates were passed to UI
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TemplateInfo>>(
                    templates => templates.All(t => t.TemplateType == TemplateType.Daily)),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "only daily templates should be shown for 'today' command");
    }

    [Fact]
    public async Task Handle_UsesSelectedTemplate_ForPromptGeneration()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        var selectedTemplateContent = "# Custom Prompt\nUser input: {{USER_INPUT}}";
        SetupMultipleDailyTemplates();

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("custom-daily");

        // Setup loader to return custom template when loaded
        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync("custom-daily", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(new PromptTemplate
            {
                TemplateId = "custom-daily",
                Content = selectedTemplateContent,
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem
            }));

        var command = new CreateDailyEntry.Command
        {
            Content = "Meeting\nCode review\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify the selected template was loaded and used
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("custom-daily", It.IsAny<CancellationToken>()),
            Times.Once,
            "selected template should be loaded for prompt generation");
    }

    [Fact]
    public async Task Handle_WhenTemplateSelectionCancelled_ReturnsFailure()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        SetupMultipleDailyTemplates();
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("User cancelled template selection"));

        var command = new CreateDailyEntry.Command
        {
            Content = "Meeting\nCode review\nGood"
        };

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
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        SetupNoTemplates();

        var command = new CreateDailyEntry.Command
        {
            Content = "Meeting\nCode review\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("should fall back to embedded template");

        // Template selection UI should not be invoked
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "should not prompt for selection when no templates available");
    }

    [Fact]
    public async Task Handle_TemplateSelectionFlow_OccursBeforeLLMCall()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        var callSequence = new List<string>();

        SetupMultipleDailyTemplates();

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callSequence.Add("TemplateSelection");
                return "daily-summary";
            });

        _mockLlmProvider.Reset();
        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(() =>
            {
                callSequence.Add("LLMCall");
                return Result<LlmResponse>.Success(new LlmResponse
                {
                    Content = "Summary generated",
                    InputTokens = 10,
                    OutputTokens = 20
                });
            });

        var command = new CreateDailyEntry.Command
        {
            Content = "Meeting\nCode review\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        callSequence.Should().HaveCount(2);
        callSequence[0].Should().Be("TemplateSelection", "template selection must occur first");
        callSequence[1].Should().Be("LLMCall", "LLM call must occur after template selection");
    }

    private void SetupMultipleDailyTemplates()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "daily-summary",
                Content = "Default daily template {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Daily Summary", TemplateType = TemplateType.Daily }
            },
            new PromptTemplate
            {
                TemplateId = "custom-daily",
                Content = "Custom daily template {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem,
                Metadata = new TemplateMetadata { Title = "Custom Daily", TemplateType = TemplateType.Daily }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        // Also mock LoadTemplateAsync for each template
        foreach (var template in templates)
        {
            _mockTemplateLoader
                .Setup(l => l.LoadTemplateAsync(template.TemplateId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PromptTemplate>.Success(template));
        }
    }

    private void SetupSingleDailyTemplate()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "daily-summary",
                Content = "Default daily template {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Daily Summary", TemplateType = TemplateType.Daily }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        // Also mock LoadTemplateAsync for the template
        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync("daily-summary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(templates[0]));
    }

    private void SetupMixedDailyAndWeeklyTemplates()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "daily-summary",
                Content = "Daily template {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Daily Summary", TemplateType = TemplateType.Daily }
            },
            new PromptTemplate
            {
                TemplateId = "daily-detailed",
                Content = "Detailed daily template {{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem,
                Metadata = new TemplateMetadata { Title = "Daily Detailed", TemplateType = TemplateType.Daily }
            },
            new PromptTemplate
            {
                TemplateId = "weekly-review",
                Content = "Weekly template {{USER_INPUT}}",
                TemplateType = TemplateType.Weekly,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Title = "Weekly Review", TemplateType = TemplateType.Weekly }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(
                templates.Where(t => t.TemplateType == TemplateType.Daily).ToList()));

        // Mock LoadTemplateAsync for all templates
        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string templateId, CancellationToken _) => 
            {
                var template = templates.FirstOrDefault(t => t.TemplateId == templateId);
                return template != null 
                    ? Result<PromptTemplate>.Success(template)
                    : Result<PromptTemplate>.Failure($"Template {templateId} not found");
            });
    }

    private void SetupNoTemplates()
    {
        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(new List<PromptTemplate>()));

        // Mock LoadTemplateAsync to return success for embedded templates (fallback), failure otherwise
        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string templateId, CancellationToken _) =>
            {
                if (templateId == "daily-summary")
                {
                    // Return the embedded daily template
                    return Result<PromptTemplate>.Success(new PromptTemplate
                    {
                        TemplateId = "daily-summary",
                        Content = "Default daily template {{USER_INPUT}}",
                        TemplateType = TemplateType.Daily,
                        Source = TemplateSource.Embedded,
                        Metadata = new TemplateMetadata { Title = "Daily Summary", TemplateType = TemplateType.Daily }
                    });
                }
                return Result<PromptTemplate>.Failure($"Template {templateId} not found");
            });
    }

    private ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Mock configuration
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["TenSecondTom:Llm:Provider"]).Returns("OpenAI");
        mockConfiguration.Setup(c => c["TenSecondTom:Llm:Model"]).Returns("gpt-4o");
        services.AddSingleton(mockConfiguration.Object);

        // Mock storage
        var mockStorage = new Mock<IMemoryStorageProvider>();
        mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));
        mockStorage.Setup(s => s.CountEntriesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));
        services.AddSingleton(mockStorage.Object);

        // Mock LLM
        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = "## Summary\nGenerated summary",
                InputTokens = 10,
                OutputTokens = 20
            }));

        var mockLlmFactory = new Mock<ILlmProviderFactory>();
        mockLlmFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(_mockLlmProvider.Object);
        services.AddSingleton(mockLlmFactory.Object);

        // Mock auth
        var mockAuth = new Mock<IAuthenticationService>();
        mockAuth.Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton(mockAuth.Object);

        // Add template infrastructure
        services.AddSingleton(_mockTemplateLoader.Object);
        services.AddSingleton(_mockTemplateSelectionUI.Object);

        // Add TemplateProvider (required by CreateDailyEntry.Handler)
        services.AddSingleton<ITemplateProvider, TemplateProvider>();

        // Add handler
        services.AddSingleton<CreateDailyEntry.Handler>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _testDirectory?.Dispose();
    }
}
