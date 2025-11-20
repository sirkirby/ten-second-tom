using FluentAssertions;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Infrastructure.Prompts;

/// <summary>
/// Unit tests for EmbeddedPromptTemplateLoader implementation.
/// Tests loading from embedded resources and file system overrides.
/// </summary>
public sealed class EmbeddedPromptTemplateLoaderTests
{
    private static YamlFrontMatterParser CreateYamlParser()
    {
        using var loggerFactory = LoggerFactory.Create(builder => { });
        var logger = loggerFactory.CreateLogger<YamlFrontMatterParser>();
        return new YamlFrontMatterParser(logger);
    }

    [Fact]
    public async Task LoadTemplateAsync_WithEmbeddedTemplate_ReturnsTemplate()
    {
        // Arrange
        const string templateId = "daily-summary";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplateId.Should().Be(templateId);
        result.Value.Content.Should().NotBeEmpty();
        result.Value.TemplateType.Should().Be(TemplateType.Daily);
    }

    [Fact]
    public async Task LoadTemplateAsync_WithWeeklyTemplate_ReturnsTemplate()
    {
        // Arrange
        const string templateId = "weekly-review";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplateId.Should().Be(templateId);
        result.Value.Content.Should().NotBeEmpty();
        result.Value.TemplateType.Should().Be(TemplateType.Weekly);
    }

    [Fact]
    public async Task LoadTemplateAsync_WithVariables_ExtractsVariablePlaceholders()
    {
        // Arrange
        const string templateId = "daily-summary";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Contain("{{USER_INPUT}}");
        result.Value.Content.Should().Contain("{{DATE}}");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithStructuredOutput_ContainsExpectedSections()
    {
        // Arrange
        const string templateId = "daily-summary";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Contain("Key Events");
        result.Value.Content.Should().Contain("Themes");
        result.Value.Content.Should().Contain("To-Do Items");
        result.Value.Content.Should().Contain("Important People");
        result.Value.Content.Should().Contain("Notable Tasks");
    }

    [Fact]
    public async Task LoadTemplateAsync_WeeklyTemplate_ContainsTop3Sections()
    {
        // Arrange
        const string templateId = "weekly-review";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Contain("Top 3 Accomplishments");
        result.Value.Content.Should().Contain("Top 3 Challenges");
        result.Value.Content.Should().Contain("Recurring Themes");
        result.Value.Content.Should().Contain("{{DAILY_ENTRIES}}");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithMissingTemplate_ReturnsFailure()
    {
        // Arrange
        const string templateId = "non-existent-template";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(templateId);
        result.Error.Should().Contain("not found");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadTemplateAsync_WithInvalidTemplateId_ReturnsFailure(string? invalidId)
    {
        // Arrange
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            invalidId!,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Template ID");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithUserOverride_PreferUserFile()
    {
        // Arrange
        const string templateId = "daily-summary";
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string templatesDir = Path.Combine(tempDir, "templates");
        Directory.CreateDirectory(templatesDir);

        try
        {
            // Create user override file
            string overrideContent = """
                # User Override Template

                This is a custom template with {{CUSTOM_VAR}}.
                """;
            string overridePath = Path.Combine(templatesDir, $"{templateId}.md");
            await File.WriteAllTextAsync(overridePath, overrideContent);

            var yamlParser = CreateYamlParser();
            EmbeddedPromptTemplateLoader loader = new(baseDirectory: tempDir, yamlParser: yamlParser);

            // Act
            Result<PromptTemplate> result = await loader.LoadTemplateAsync(
                templateId,
                CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Content.Should().Contain("User Override Template");
            result.Value.Content.Should().Contain("{{CUSTOM_VAR}}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadTemplateAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        const string templateId = "daily-summary";
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () => await loader.LoadTemplateAsync(
            templateId,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task LoadTemplateAsync_WithIOError_ReturnsFailure()
    {
        // Arrange
        const string templateId = "daily-summary";
        string invalidDir = Path.Combine(Path.GetTempPath(), new string('x', 300)); // Invalid path
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: invalidDir, yamlParser: yamlParser);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert - Should fallback to embedded resource
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplateId.Should().Be(templateId);
    }

    [Fact]
    public async Task LoadAllTemplatesAsync_ReturnsAllEmbeddedTemplates()
    {
        // Arrange
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<List<PromptTemplate>> result = await loader.LoadAllTemplatesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(4); // daily-summary, daily-standup, weekly-review, and business-meeting
        result.Value.Should().Contain(t => t.TemplateId == "daily-summary");
        result.Value.Should().Contain(t => t.TemplateId == "daily-standup");
        result.Value.Should().Contain(t => t.TemplateId == "weekly-review");
        result.Value.Should().Contain(t => t.TemplateId == "business-meeting");
        result.Value.Should().OnlyContain(t => t.Source == TemplateSource.Embedded);
    }

    [Fact]
    public async Task LoadAllTemplatesAsync_ParsesYamlFrontMatter()
    {
        // Arrange
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);

        // Act
        Result<List<PromptTemplate>> result = await loader.LoadAllTemplatesAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        PromptTemplate? dailyTemplate = result.Value.FirstOrDefault(t => t.TemplateId == "daily-summary");
        dailyTemplate.Should().NotBeNull();
        dailyTemplate!.Metadata.Should().NotBeNull();
        dailyTemplate.Metadata!.TemplateType.Should().Be(TemplateType.Daily);
        dailyTemplate.Metadata.Title.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadAllTemplatesAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var yamlParser = CreateYamlParser();
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null, yamlParser: yamlParser);
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Act
        Func<Task> act = async () => await loader.LoadAllTemplatesAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // NOTE: These tests are commented out because TemplatesDirectoryExistsAsync method
    // does not exist in the actual EmbeddedPromptTemplateLoader implementation.
    // These were written for a planned interface that differs from the actual implementation.

    // [Fact]
    // public async Task TemplatesDirectoryExistsAsync_WithNullBaseDirectory_ReturnsFalse()
    // {
    //     // Arrange
    //     EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);
    //
    //     // Act
    //     bool result = await loader.TemplatesDirectoryExistsAsync(CancellationToken.None);
    //
    //     // Assert
    //     result.Should().BeFalse();
    // }
    //
    // [Fact]
    // public async Task TemplatesDirectoryExistsAsync_WithExistingDirectory_ReturnsTrue()
    // {
    //     // Arrange
    //     string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    //     string templatesDir = Path.Combine(tempDir, "templates");
    //     Directory.CreateDirectory(templatesDir);
    //
    //     try
    //     {
    //         EmbeddedPromptTemplateLoader loader = new(baseDirectory: tempDir);
    //
    //         // Act
    //         bool result = await loader.TemplatesDirectoryExistsAsync(CancellationToken.None);
    //
    //         // Assert
    //         result.Should().BeTrue();
    //     }
    //     finally
    //     {
    //         if (Directory.Exists(tempDir))
    //         {
    //             Directory.Delete(tempDir, recursive: true);
    //         }
    //     }
    // }
    //
    // [Fact]
    // public async Task TemplatesDirectoryExistsAsync_WithNonExistingDirectory_ReturnsFalse()
    // {
    //     // Arrange
    //     string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    //     EmbeddedPromptTemplateLoader loader = new(baseDirectory: tempDir);
    //
    //     // Act
    //     bool result = await loader.TemplatesDirectoryExistsAsync(CancellationToken.None);
    //
    //     // Assert
    //     result.Should().BeFalse();
    // }
}
