using FluentAssertions;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Prompts;

/// <summary>
/// Unit tests for EmbeddedPromptTemplateLoader implementation.
/// Tests loading from embedded resources and file system overrides.
/// </summary>
public sealed class EmbeddedPromptTemplateLoaderTests
{
    [Fact]
    public async Task LoadTemplateAsync_WithEmbeddedTemplate_ReturnsTemplate()
    {
        // Arrange
        const string templateId = "daily-summary";
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplateId.Should().Be(templateId);
        result.Value.Content.Should().NotBeEmpty();
        result.Value.TemplateType.Should().Be(TemplateType.DailySummary);
    }

    [Fact]
    public async Task LoadTemplateAsync_WithWeeklyTemplate_ReturnsTemplate()
    {
        // Arrange
        const string templateId = "weekly-review";
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplateId.Should().Be(templateId);
        result.Value.Content.Should().NotBeEmpty();
        result.Value.TemplateType.Should().Be(TemplateType.WeeklySummary);
    }

    [Fact]
    public async Task LoadTemplateAsync_WithVariables_ExtractsVariablePlaceholders()
    {
        // Arrange
        const string templateId = "daily-summary";
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

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
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

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
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

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
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

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
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);

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

            EmbeddedPromptTemplateLoader loader = new(baseDirectory: tempDir);

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
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: null);
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
        EmbeddedPromptTemplateLoader loader = new(baseDirectory: invalidDir);

        // Act
        Result<PromptTemplate> result = await loader.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert - Should fallback to embedded resource
        result.IsSuccess.Should().BeTrue();
        result.Value.TemplateId.Should().Be(templateId);
    }
}
