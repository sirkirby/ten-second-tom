using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Templates;
using static TenSecondTom.Features.Templates.ListTemplates;
using TenSecondTom.Features.Today;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.IntegrationTests.TestHelpers;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.IntegrationTests.Integration.Features.Templates;

/// <summary>
/// Integration tests for custom template workflow (T055).
/// Tests end-to-end flow: creating custom template file -> running command -> selecting template -> verifying it works.
/// </summary>
public sealed class TemplateWorkflowTests : IDisposable
{
    private readonly TemporaryTestDirectory _testDirectory;
    private readonly string _templatesDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly Mock<ITemplateSelectionUI> _mockTemplateSelectionUI;
    private readonly Mock<ILlmProvider> _mockLlmProvider;
    private readonly Mock<IMemoryStorageProvider> _mockStorage;

    public TemplateWorkflowTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _templatesDirectory = Path.Combine(_testDirectory.BasePath, "templates");
        Directory.CreateDirectory(_templatesDirectory);

        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();
        _mockLlmProvider = new Mock<ILlmProvider>();
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _serviceProvider = BuildTestServiceProvider();
    }

    [Fact]
    public async Task EndToEnd_CreateCustomTemplate_SelectInCommand_GeneratesOutput()
    {
        // Arrange - Create multiple custom template files to trigger selection UI (T055)
        var customTemplateContent = @"---
templateType: daily
title: My Custom Daily Template
description: A custom template for daily reflections
version: 1.0
---
# Custom Daily Reflection

## What happened today?
{{USER_INPUT}}

## Key Insights
Focus on learnings and growth.

## Tomorrow's Plan
What will you tackle next?
";

        var customTemplatePath = Path.Combine(_templatesDirectory, "my-custom-daily.md");
        await File.WriteAllTextAsync(customTemplatePath, customTemplateContent);

        // Create a second template to ensure SelectTemplateAsync is called
        var secondTemplateContent = @"---
templateType: daily
title: Alternative Daily Template
description: Another daily template
version: 1.0
---
# Alternative Daily Reflection

{{USER_INPUT}}
";

        var secondTemplatePath = Path.Combine(_templatesDirectory, "alternative-daily.md");
        await File.WriteAllTextAsync(secondTemplatePath, secondTemplateContent);

        // Setup template selection to choose the custom template
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("my-custom-daily");

        // Create handler with the real filesystem loader
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();

        var command = new CreateDailyEntry.Command
        {
            Content = "Implemented custom templates feature\nAdd comprehensive tests\nAccomplished"
        };

        // Act - Run command which should discover and use custom template
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with custom template");

        // Verify custom template was presented for selection
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TemplateInfo>>(
                    templates => templates.Any(t => t.TemplateId == "my-custom-daily")),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "custom template should appear in selection list");

        // Verify the daily entry was saved
        _mockStorage.Verify(
            s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "daily entry should be saved");
    }

    [Fact]
    public async Task EndToEnd_ListTemplates_IncludesCustomTemplate()
    {
        // Arrange - Create custom template
        var customTemplateContent = @"---
templateType: weekly
title: Custom Weekly Review
description: My personalized weekly review template
---
# Custom Weekly Review
Weekly reflection content here.
";

        await File.WriteAllTextAsync(
            Path.Combine(_templatesDirectory, "custom-weekly.md"),
            customTemplateContent);

        var queryHandler = _serviceProvider.GetRequiredService<ListTemplates.Handler>();
        var query = new ListTemplates.Query(FilterByType: TemplateType.Weekly);

        // Act
        var result = await queryHandler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("query should succeed");
        result.Value.Templates.Should().Contain(t => t.TemplateId == "custom-weekly",
            "custom template should be in the list");

        var customTemplate = result.Value.Templates.First(t => t.TemplateId == "custom-weekly");
        customTemplate.Title.Should().Be("Custom Weekly Review");
        customTemplate.Description.Should().Be("My personalized weekly review template");
        customTemplate.Source.Should().Be(TemplateSource.FileSystem);
    }

    [Fact]
    public async Task EndToEnd_MultipleCustomTemplates_AllAppearInSelection()
    {
        // Arrange - Create multiple custom templates (T055)
        var template1 = @"---
templateType: daily
title: Morning Reflection
---
# Morning Reflection
Start your day right.
";

        var template2 = @"---
templateType: daily
title: Evening Review
---
# Evening Review
End your day with reflection.
";

        var template3 = @"---
templateType: daily
title: Quick Check-in
---
# Quick Check-in
A brief daily check-in.
";

        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "morning-reflection.md"), template1);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "evening-review.md"), template2);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "quick-checkin.md"), template3);

        // Setup selection to return one of them
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("morning-reflection");

        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        var command = new CreateDailyEntry.Command
        {
            Content = "Created templates\nTest them\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all three custom templates were offered in selection
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TemplateInfo>>(
                    templates => templates.Count(t => !t.IsDefault) >= 3),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "all custom templates should be available for selection");
    }

    [Fact]
    public async Task EndToEnd_CustomTemplateWithInvalidYAML_SkippedGracefully()
    {
        // Arrange - Create two valid and one invalid custom template (T055)
        var validTemplate = @"---
templateType: daily
title: Valid Custom Template
---
# Valid Content
This template is valid.
{{USER_INPUT}}
";

        var validTemplate2 = @"---
templateType: daily
title: Another Valid Template
---
# Another Valid Template
{{USER_INPUT}}
";

        var invalidTemplate = @"---
templateType: daily
title: [broken yaml structure
invalid: {unclosed
---
# Content that won't be reached
";

        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "valid-custom.md"), validTemplate);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "valid-custom-2.md"), validTemplate2);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "invalid-custom.md"), invalidTemplate);

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-custom");

        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        var command = new CreateDailyEntry.Command
        {
            Content = "Worked on templates\nTest validation\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue($"command should succeed despite invalid template being present, error: {result.Error}");

        // Verify invalid template was skipped, valid template was included
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TemplateInfo>>(
                    templates => templates.Any(t => t.TemplateId == "valid-custom") &&
                                !templates.Any(t => t.TemplateId == "invalid-custom")),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "invalid template should be skipped, valid template should be available");
    }

    [Fact]
    public async Task EndToEnd_CustomTemplateSelection_UsesCorrectPromptContent()
    {
        // Arrange - Create multiple templates to trigger selection UI
        var customTemplateContent = @"---
templateType: daily
title: Test Marker Template
---
# CUSTOM_MARKER_START
User responses will appear here:
{{USER_INPUT}}
# CUSTOM_MARKER_END
This is custom template content.
";

        await File.WriteAllTextAsync(
            Path.Combine(_templatesDirectory, "marker-template.md"),
            customTemplateContent);

        // Create a second template to ensure SelectTemplateAsync is called
        var secondTemplateContent = @"---
templateType: daily
title: Another Template
---
{{USER_INPUT}}
";

        await File.WriteAllTextAsync(
            Path.Combine(_templatesDirectory, "another-template.md"),
            secondTemplateContent);

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TemplateInfo>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("marker-template");

        // Reconfigure the LLM provider mock to capture the prompt that was sent
        string? capturedPrompt = null;
        _mockLlmProvider.Reset();
        _mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .Callback<string, CancellationToken, int?, double?>((prompt, _, _, _) => capturedPrompt = prompt)
            .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse 
            { 
                Content = "## Generated Summary",
                InputTokens = 10,
                OutputTokens = 20
            }));

        var handler = _serviceProvider.GetRequiredService<CreateDailyEntry.Handler>();
        var command = new CreateDailyEntry.Command
        {
            Content = "Test input\nTest plans\nGood"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedPrompt.Should().NotBeNull("LLM should be called with a prompt");
        capturedPrompt.Should().Contain("CUSTOM_MARKER_START",
            "custom template content should be used in the prompt");
        capturedPrompt.Should().Contain("CUSTOM_MARKER_END",
            "custom template markers should be preserved");
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
        _mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));
        _mockStorage.Setup(s => s.CountEntriesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));
        services.AddSingleton(_mockStorage.Object);

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

        // Real template infrastructure with filesystem
        var yamlLogger = new Mock<ILogger<YamlFrontMatterParser>>();
        var yamlParser = new YamlFrontMatterParser(yamlLogger.Object);

        var loaderLogger = new Mock<ILogger<FileSystemTemplateLoader>>();
        var templateLoader = new FileSystemTemplateLoader(
            _templatesDirectory,
            yamlParser,
            loaderLogger.Object);

        services.AddSingleton<IPromptTemplateLoader>(templateLoader);
        services.AddSingleton(_mockTemplateSelectionUI.Object);

        // Add TemplateProvider and ListTemplates.Handler (both needed for different tests)
        services.AddSingleton<ITemplateProvider, TemplateProvider>();
        services.AddSingleton<ListTemplates.Handler>();

        // Add handlers
        services.AddSingleton<CreateDailyEntry.Handler>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _testDirectory?.Dispose();
    }
}
