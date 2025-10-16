using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.IntegrationTests.TestHelpers;
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

    public TemplateWorkflowTests()
    {
        _testDirectory = new TemporaryTestDirectory();
        _templatesDirectory = Path.Combine(_testDirectory.BasePath, "templates");
        Directory.CreateDirectory(_templatesDirectory);

        _mockTemplateSelectionUI = new Mock<ITemplateSelectionUI>();
        _serviceProvider = BuildTestServiceProvider();
    }

    [Fact]
    public async Task EndToEnd_CreateCustomTemplate_SelectInCommand_GeneratesOutput()
    {
        // Arrange - Create a custom template file in filesystem (T055)
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

        // Setup template selection to choose the custom template
        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("my-custom-daily");

        // Create handler with the real filesystem loader
        var handler = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();

        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened today?"] = "Implemented custom templates feature",
                ["Plans for tomorrow?"] = "Add comprehensive tests",
                ["How are you feeling?"] = "Accomplished"
            }
        };

        // Act - Run command which should discover and use custom template
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed with custom template");

        // Verify custom template was presented for selection
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(
                    templates => templates.Any(t => t.TemplateId == "my-custom-daily")),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "custom template should appear in selection list");

        // Verify the daily entry was created
        var dailyEntries = _testDirectory.GetDailyEntries();
        dailyEntries.Should().NotBeEmpty("daily entry should be created");
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

        var queryHandler = _serviceProvider.GetRequiredService<ListTemplatesQueryHandler>();
        var query = new ListTemplatesQuery(FilterByType: TemplateType.Weekly);

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
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("morning-reflection");

        var handler = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened?"] = "Created templates",
                ["Plans?"] = "Test them",
                ["Feeling?"] = "Good"
            }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify all three custom templates were offered in selection
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(
                    templates => templates.Count(t => !t.IsDefault) >= 3),
                "today",
                It.IsAny<CancellationToken>()),
            Times.Once,
            "all custom templates should be available for selection");
    }

    [Fact]
    public async Task EndToEnd_CustomTemplateWithInvalidYAML_SkippedGracefully()
    {
        // Arrange - Create one valid and one invalid custom template (T055)
        var validTemplate = @"---
templateType: daily
title: Valid Custom Template
---
# Valid Content
This template is valid.
";

        var invalidTemplate = @"---
templateType: daily
title: [broken yaml structure
invalid: {unclosed
---
# Content that won't be reached
";

        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "valid-custom.md"), validTemplate);
        await File.WriteAllTextAsync(Path.Combine(_templatesDirectory, "invalid-custom.md"), invalidTemplate);

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-custom");

        var handler = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string> { ["Test"] = "Test" }
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("command should succeed despite invalid template being present");

        // Verify invalid template was skipped, valid template was included
        _mockTemplateSelectionUI.Verify(
            ui => ui.SelectTemplateAsync(
                It.Is<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(
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
        // Arrange - Custom template with specific content markers (T055)
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

        _mockTemplateSelectionUI
            .Setup(ui => ui.SelectTemplateAsync(
                It.IsAny<IReadOnlyList<TenSecondTom.Features.Templates.Models.TemplateListItem>>(),
                "today",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("marker-template");

        // Mock LLM to capture the prompt that was sent
        string? capturedPrompt = null;
        var mockLlmProvider = new Mock<ILlmProvider>();
        mockLlmProvider
            .Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .Callback<string, CancellationToken, int?, double?>((prompt, _, _, _) => capturedPrompt = prompt)
            .ReturnsAsync(Result<string>.Success("## Generated Summary"));

        var handler = _serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
        var command = new CreateDailyEntryCommand
        {
            Responses = new Dictionary<string, string>
            {
                ["What happened?"] = "Test input",
                ["Plans?"] = "Test plans",
                ["Feeling?"] = "Good"
            }
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
        mockConfiguration.Setup(c => c["Llm:Provider"]).Returns("OpenAI");
        mockConfiguration.Setup(c => c["Llm:Model"]).Returns("gpt-4o");
        services.AddSingleton(mockConfiguration.Object);

        // Mock storage
        var mockStorage = new Mock<IMemoryStorageProvider>();
        mockStorage.Setup(s => s.SaveAsync(It.IsAny<DailyEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyEntry entry, CancellationToken _) => Result<MemoryEntry>.Success(entry));
        mockStorage.Setup(s => s.CountEntriesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));
        services.AddSingleton(mockStorage.Object);

        // Mock LLM
        var mockLlmProvider = new Mock<ILlmProvider>();
        mockLlmProvider.Setup(p => p.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(Result<string>.Success("## Summary\nGenerated summary"));

        var mockLlmFactory = new Mock<ILlmProviderFactory>();
        mockLlmFactory.Setup(f => f.CreateProvider(It.IsAny<string>()))
            .Returns(mockLlmProvider.Object);
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
        services.AddSingleton<ListTemplatesQueryHandler>();

        // Add handlers
        services.AddSingleton<CreateDailyEntryHandler>();

        return services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _testDirectory?.Dispose();
    }
}
