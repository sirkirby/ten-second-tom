using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Templates;
using TenSecondTom.Features.Templates.Models;
using TenSecondTom.Features.Today; // Add for CreateDailyEntry co-located use case
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.Integration.Cli;

/// <summary>
/// Integration tests for 'tom today' command with --no-edit flag.
/// Tests cover the quick entry creation flow where users provide notes via CLI arguments.
/// Focus on template selection behavior with --use-default-template and --template flags.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposableAnalyzers.Correctness", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposal is handled via IAsyncLifetime.DisposeAsync")]
public sealed class TodayCommandNoEditTests : IAsyncLifetime, IDisposable
{
    private readonly TemporaryTestDirectory _testDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly Mock<IPromptTemplateLoader> _mockTemplateLoader;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IMemoryStorageProvider> _mockStorage;

    public TodayCommandNoEditTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();
        _mockTemplateLoader = new Mock<IPromptTemplateLoader>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _serviceProvider = BuildTestServiceProvider();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        _testDirectory.Dispose();
    }

    void IDisposable.Dispose() => DisposeAsync().GetAwaiter().GetResult();

    [Fact]
    public async Task Handle_WithNoEditAndDefaultTemplate_CreatesEntryQuickly()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupSingleDailyTemplate(); // Setup default template

        var command = new CreateDailyEntry.Command
        {
            Content = "Productive day! Finished the feature and reviewed two PRs.",
            UseDefaultTemplate = true // Simulates --use-default-template flag
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with default template");
        result.Value.Should().NotBeNull();
        result.Value.UserInput.Should().Contain("Productive day");

        // Verify template selection UI was NOT invoked (bypassed with default template)
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "template selection UI should be bypassed when using default template");

        // Verify LLM was called
        _mockLlmProvider.Verify(
            p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()),
            Times.Once,
            "LLM should be called to generate summary");
    }

    [Fact]
    public async Task Handle_WithNoEditAndNamedTemplate_UsesCorrectTemplate()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupMultipleDailyTemplates();

        var command = new CreateDailyEntry.Command
        {
            Content = "Team standup at 9am. Sprint planning in the afternoon.",
            TemplateName = "daily-standup" // Simulates --template "daily-standup"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with named template");

        // Verify the specific template was loaded
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("daily-standup", It.IsAny<CancellationToken>()),
            Times.Once,
            "specified template should be loaded");

        // Verify template selection UI was NOT invoked
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "template selection UI should be bypassed when template name is specified");
    }

    [Fact]
    public async Task Handle_WithNoEditAndMissingTemplate_FallsBackToDefault()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupMultipleDailyTemplates();

        // Setup template loader to fail for nonexistent template, then succeed for default
        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync("nonexistent-template", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Failure("Template not found"));

        var command = new CreateDailyEntry.Command
        {
            Content = "My daily notes",
            TemplateName = "nonexistent-template" // Invalid template
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with fallback to default template");

        // Verify attempt to load specified template
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("nonexistent-template", It.IsAny<CancellationToken>()),
            Times.Once,
            "should attempt to load specified template first");

        // Verify fallback to default template
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("daily-summary", It.IsAny<CancellationToken>()),
            Times.Once,
            "should fall back to default template when specified template not found");
    }

    [Fact]
    public async Task Handle_WithEmptyContent_ReturnsValidationError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        var command = new CreateDailyEntry.Command
        {
            Content = "", // Empty content
            UseDefaultTemplate = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse("command should fail with empty content");
        result.Error.Should().Contain("content", "error should mention content validation");

        // Verify LLM was NOT called
        _mockLlmProvider.Verify(
            p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()),
            Times.Never,
            "LLM should not be called with invalid content");
    }

    [Fact]
    public async Task Handle_WithWhitespaceOnlyContent_ReturnsValidationError()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        var command = new CreateDailyEntry.Command
        {
            Content = "   \n\n  \t  ", // Whitespace only
            UseDefaultTemplate = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse("command should fail with whitespace-only content");
        result.Error.Should().Contain("content", "error should mention content validation");
    }

    [Fact]
    public async Task Handle_WithMultiLineNotes_PreservesLineBreaks()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupSingleDailyTemplate();

        var multiLineContent = "Morning:\n- Team standup\n- Code review\n\nAfternoon:\n- Feature implementation\n- Unit tests";

        var command = new CreateDailyEntry.Command
        {
            Content = multiLineContent,
            UseDefaultTemplate = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with multi-line content");
        result.Value.UserInput.Should().Contain("Morning:");
        result.Value.UserInput.Should().Contain("Afternoon:");
        result.Value.UserInput.Should().Contain("\n", "newlines should be preserved");
    }

    [Fact]
    public async Task Handle_WithTemplateAndUseDefaultFlag_PrefersTemplateFlag()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupMultipleDailyTemplates();

        var command = new CreateDailyEntry.Command
        {
            Content = "Daily notes",
            TemplateName = "daily-standup", // Specific template
            UseDefaultTemplate = true // Also set this flag
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that --template takes precedence over --use-default-template
        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("daily-standup", It.IsAny<CancellationToken>()),
            Times.Once,
            "--template flag should take precedence over --use-default-template");

        _mockTemplateLoader.Verify(
            l => l.LoadTemplateAsync("daily-summary", It.IsAny<CancellationToken>()),
            Times.Never,
            "default template should not be loaded when specific template is named");
    }

    [Fact]
    public async Task Handle_WithLongContent_ProcessesSuccessfully()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupSingleDailyTemplate();

        var longContent = string.Join("\n", Enumerable.Range(1, 50)
            .Select(i => $"Point {i}: This is a detailed note about what happened during the day."));

        var command = new CreateDailyEntry.Command
        {
            Content = longContent,
            UseDefaultTemplate = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should handle long content");
        result.Value.UserInput.Should().Contain("Point 1:");
        result.Value.UserInput.Should().Contain("Point 50:");
    }

    [Fact]
    public async Task Handle_WithSpecialCharacters_PreservesContent()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupSingleDailyTemplate();

        var specialContent = "Today's work: \"Fixed bug #123\"\n- Improved performance by 50%\n- Cost: $500\n- Email: test@example.com";

        var command = new CreateDailyEntry.Command
        {
            Content = specialContent,
            UseDefaultTemplate = true
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should handle special characters");
        result.Value.UserInput.Should().Contain("bug #123");
        result.Value.UserInput.Should().Contain("50%");
        result.Value.UserInput.Should().Contain("$500");
        result.Value.UserInput.Should().Contain("test@example.com");
    }

    [Fact]
    public async Task Handle_QuickEntryPerformance_CompletesWithoutTemplateSelection()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupSingleDailyTemplate();

        var command = new CreateDailyEntry.Command
        {
            Content = "Quick daily update",
            UseDefaultTemplate = true
        };

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        stopwatch.Stop();

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify no interactive UI calls that would slow down execution
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "quick entry mode should bypass interactive template selection");

        // Note: Actual performance assertion would depend on LLM response time
        // This test verifies the flow, not absolute timing
    }

    [Fact]
    public async Task Handle_WithNoTemplateFlags_UsesDefaultBehavior()
    {
        // Arrange
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        SetupSingleDailyTemplate(); // Only one template available

        var command = new CreateDailyEntry.Command
        {
            Content = "Daily notes without template flags",
            // No TemplateName, no UseDefaultTemplate
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with auto-selection");

        // With single template, it should auto-select without prompting
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "single template should auto-select without UI prompt");
    }

    // Helper methods for test setup

    private void SetupSingleDailyTemplate()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "daily-summary",
                Content = "Default daily template. User input:\n{{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Id = "daily-summary", Title = "Daily Summary", TemplateType = TemplateType.Daily }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        _mockTemplateLoader
            .Setup(l => l.LoadTemplateAsync("daily-summary", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(templates[0]));
    }

    private void SetupMultipleDailyTemplates()
    {
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "daily-summary",
                Content = "Default daily template. User input:\n{{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.Embedded,
                Metadata = new TemplateMetadata { Id = "daily-summary", Title = "Daily Summary", TemplateType = TemplateType.Daily }
            },
            new PromptTemplate
            {
                TemplateId = "daily-standup",
                Content = "Daily standup template. User input:\n{{USER_INPUT}}",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem,
                Metadata = new TemplateMetadata { Id = "daily-standup", Title = "Daily Standup", TemplateType = TemplateType.Daily }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        // Mock LoadTemplateAsync for all templates
        foreach (var template in templates)
        {
            _mockTemplateLoader
                .Setup(l => l.LoadTemplateAsync(template.TemplateId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<PromptTemplate>.Success(template));
        }
    }

    private ServiceProvider BuildTestServiceProvider()
    {
        var services = new ServiceCollection();

        // Add logging (minimal output for tests)
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Mock configuration
        var mockConfiguration = new Mock<IConfiguration>();
        mockConfiguration.Setup(c => c["TenSecondTom:Llm:Provider"]).Returns("OpenAI");
        mockConfiguration.Setup(c => c["TenSecondTom:Llm:Model"]).Returns("gpt-4o");
        services.AddSingleton(mockConfiguration.Object);

        // Mock storage - return success for all operations
        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));
        _mockStorage.Setup(s => s.CountEntriesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));
        services.AddSingleton(_mockStorage.Object);

        // Mock LLM provider - return successful response
        _mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse
            {
                Content = "## Summary\nTest summary of the day's events.",
                InputTokens = 50,
                OutputTokens = 100
            }));

        var mockLlmFactory = new Mock<ILlmProviderFactory>();
        mockLlmFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(_mockLlmProvider.Object);
        services.AddSingleton(mockLlmFactory.Object);

        // Mock auth - always authenticated
        var mockAuth = new Mock<IAuthenticationService>();
        mockAuth.Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        services.AddSingleton(mockAuth.Object);

        // Add template infrastructure
        services.AddSingleton(_mockTemplateLoader.Object);
        services.AddSingleton(_mockTemplateSelectionUI.Object);

        // Add TemplateProvider (required by CreateDailyEntry.Handler)
        services.AddSingleton<ITemplateProvider, TemplateProvider>();

        // Add handler under test
        services.AddSingleton<CreateDailyEntry.Handler>();

        return services.BuildServiceProvider();
    }
}
