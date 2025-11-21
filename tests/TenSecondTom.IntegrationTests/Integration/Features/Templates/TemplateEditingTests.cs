using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static TenSecondTom.Features.Templates.ListTemplates;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Features.Templates;

namespace TenSecondTom.IntegrationTests.Integration.Features.Templates;

/// <summary>
/// Integration tests verifying that template edits are immediately recognized.
/// Tests that FileSystemTemplateLoader does not cache templates across command invocations.
/// </summary>
public sealed class TemplateEditingTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ServiceProvider _serviceProvider;
    private readonly IPromptTemplateLoader _templateLoader;

    public TemplateEditingTests()
    {
        // Create a temporary directory for test templates
        _testDirectory = Path.Combine(Path.GetTempPath(), $"tst-templates-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Setup DI container with real implementations
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<YamlFrontMatterParser>();
        services.AddSingleton<IPromptTemplateLoader>(sp =>
            new FileSystemTemplateLoader(
                _testDirectory,
                sp.GetRequiredService<YamlFrontMatterParser>(),
                sp.GetRequiredService<ILogger<FileSystemTemplateLoader>>()));

        _serviceProvider = services.BuildServiceProvider();
        _templateLoader = _serviceProvider.GetRequiredService<IPromptTemplateLoader>();
    }

    /// <summary>
    /// Tests that editing a template's content is immediately reflected in the next load.
    /// Verifies FR-012: Template changes take effect immediately without application restart.
    /// </summary>
    [Fact]
    public async Task LoadTemplateAsync_AfterContentEdit_ReturnsNewContent()
    {
        // Arrange - Create initial template
        const string templateId = "test-daily";
        var templatePath = Path.Combine(_testDirectory, $"{templateId}.md");
        var originalContent = """
            ---
            id: test-daily
            templateType: daily
            title: Test Daily Template
            description: Original version
            ---

            # Original Content
            This is the original template content.
            """;

        await File.WriteAllTextAsync(templatePath, originalContent);

        // Act 1 - Load original template
        var result1 = await _templateLoader.LoadTemplateAsync(templateId);

        // Assert 1 - Verify original content
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Content.Should().Contain("Original Content");
        result1.Value.Metadata!.Description.Should().Be("Original version");

        // Arrange - Edit template content
        var editedContent = """
            ---
            id: test-daily
            templateType: daily
            title: Test Daily Template
            description: Edited version
            ---

            # Edited Content
            This is the edited template content with new information.
            """;

        await File.WriteAllTextAsync(templatePath, editedContent);

        // Act 2 - Load template again (same instance of loader)
        var result2 = await _templateLoader.LoadTemplateAsync(templateId);

        // Assert 2 - Verify edited content is loaded (no caching)
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Content.Should().Contain("Edited Content");
        result2.Value.Content.Should().NotContain("Original Content");
        result2.Value.Metadata!.Description.Should().Be("Edited version");
    }

    /// <summary>
    /// Tests that changing a template's type moves it to the correct filter.
    /// Verifies that template type changes are immediately recognized.
    /// </summary>
    [Fact]
    public async Task LoadAllTemplatesAsync_AfterTypeChange_ReflectsNewType()
    {
        // Arrange - Create template with Daily type
        const string templateId = "test-template";
        var templatePath = Path.Combine(_testDirectory, $"{templateId}.md");
        var dailyTemplate = """
            ---
            id: test-template
            templateType: daily
            title: Test Template
            description: Initially a daily template
            ---

            # Daily Template Content
            """;

        await File.WriteAllTextAsync(templatePath, dailyTemplate);

        // Act 1 - Load as daily template
        var dailyResult = await _templateLoader.LoadTemplateAsync(templateId);

        // Assert 1 - Verify it's daily type
        dailyResult.IsSuccess.Should().BeTrue();
        dailyResult.Value.TemplateType.Should().Be(TemplateType.Daily);

        // Arrange - Change to Weekly type
        var weeklyTemplate = """
            ---
            id: test-template
            templateType: weekly
            title: Test Template
            description: Now a weekly template
            ---

            # Weekly Template Content
            """;

        await File.WriteAllTextAsync(templatePath, weeklyTemplate);

        // Act 2 - Load again
        var weeklyResult = await _templateLoader.LoadTemplateAsync(templateId);

        // Assert 2 - Verify type changed (no caching)
        weeklyResult.IsSuccess.Should().BeTrue();
        weeklyResult.Value.TemplateType.Should().Be(TemplateType.Weekly);
        weeklyResult.Value.Metadata!.Description.Should().Be("Now a weekly template");
    }

    /// <summary>
    /// Tests that editing template metadata updates display in selection.
    /// Verifies that title and description changes are immediately recognized.
    /// </summary>
    [Fact]
    public async Task LoadTemplateAsync_AfterMetadataEdit_ReturnsNewMetadata()
    {
        // Arrange - Create template with original metadata
        const string templateId = "test-metadata";
        var templatePath = Path.Combine(_testDirectory, $"{templateId}.md");
        var originalTemplate = """
            ---
            id: test-metadata
            templateType: daily
            title: Original Title
            description: Original description
            version: 1.0
            author: Original Author
            ---

            # Template Content
            """;

        await File.WriteAllTextAsync(templatePath, originalTemplate);

        // Act 1 - Load original
        var result1 = await _templateLoader.LoadTemplateAsync(templateId);

        // Assert 1 - Verify original metadata
        result1.IsSuccess.Should().BeTrue();
        var metadata1 = result1.Value.Metadata;
        metadata1.Should().NotBeNull();
        metadata1!.Title.Should().Be("Original Title");
        metadata1.Description.Should().Be("Original description");
        metadata1.Version.Should().Be("1.0");
        metadata1.Author.Should().Be("Original Author");

        // Arrange - Edit metadata
        var editedTemplate = """
            ---
            id: test-metadata
            templateType: daily
            title: Updated Title
            description: Updated description with more details
            version: 2.0
            author: Updated Author
            ---

            # Template Content
            """;

        await File.WriteAllTextAsync(templatePath, editedTemplate);

        // Act 2 - Load edited version
        var result2 = await _templateLoader.LoadTemplateAsync(templateId);

        // Assert 2 - Verify metadata updated (no caching)
        result2.IsSuccess.Should().BeTrue();
        var metadata2 = result2.Value.Metadata;
        metadata2.Should().NotBeNull();
        metadata2!.Title.Should().Be("Updated Title");
        metadata2.Description.Should().Be("Updated description with more details");
        metadata2.Version.Should().Be("2.0");
        metadata2.Author.Should().Be("Updated Author");
    }

    /// <summary>
    /// Tests that creating a new template file is immediately recognized.
    /// Verifies FR-012: New custom templates appear without application restart.
    /// This is the critical test for T063a - same process instance recognition.
    /// </summary>
    [Fact]
    public async Task LoadAllTemplatesAsync_AfterNewTemplateCreated_IncludesNewTemplate()
    {
        // Arrange - Start with one template
        var template1Path = Path.Combine(_testDirectory, "existing-template.md");
        var template1Content = """
            ---
            id: existing-template
            templateType: daily
            title: Existing Template
            ---

            # Existing Content
            """;

        await File.WriteAllTextAsync(template1Path, template1Content);

        // Act 1 - Load all templates (should find 1)
        var result1 = await _templateLoader.LoadAllTemplatesAsync();

        // Assert 1 - Verify only one template
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Should().HaveCount(1);
        result1.Value[0].TemplateId.Should().Be("existing-template");

        // Arrange - Create a new template file
        var template2Path = Path.Combine(_testDirectory, "new-custom-template.md");
        var template2Content = """
            ---
            id: new-custom-template
            templateType: daily
            title: New Custom Template
            description: This was just created
            ---

            # New Content
            This is a newly created custom template.
            """;

        await File.WriteAllTextAsync(template2Path, template2Content);

        // Act 2 - Load all templates again (same loader instance, same process)
        var result2 = await _templateLoader.LoadAllTemplatesAsync();

        // Assert 2 - Verify new template is discovered immediately (no caching, no restart needed)
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Should().HaveCount(2);
        result2.Value.Should().Contain(t => t.TemplateId == "existing-template");
        result2.Value.Should().Contain(t => t.TemplateId == "new-custom-template");

        var newTemplate = result2.Value.First(t => t.TemplateId == "new-custom-template");
        newTemplate.Metadata!.Title.Should().Be("New Custom Template");
        newTemplate.Content.Should().Contain("newly created custom template");
    }

    /// <summary>
    /// Tests that deleting a template file is immediately recognized.
    /// Verifies that template deletion is handled correctly without caching issues.
    /// </summary>
    [Fact]
    public async Task LoadAllTemplatesAsync_AfterTemplateDeleted_ExcludesDeletedTemplate()
    {
        // Arrange - Create two templates
        var template1Path = Path.Combine(_testDirectory, "template-1.md");
        var template2Path = Path.Combine(_testDirectory, "template-2.md");

        var template1Content = """
            ---
            id: template-1
            templateType: daily
            title: Test Template 1
            ---

            # Content
            """;

        var template2Content = """
            ---
            id: template-2
            templateType: daily
            title: Test Template 2
            ---

            # Content
            """;

        await File.WriteAllTextAsync(template1Path, template1Content);
        await File.WriteAllTextAsync(template2Path, template2Content);

        // Act 1 - Load all templates (should find 2)
        var result1 = await _templateLoader.LoadAllTemplatesAsync();

        // Assert 1 - Verify two templates
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Should().HaveCount(2);

        // Arrange - Delete one template
        File.Delete(template1Path);

        // Act 2 - Load all templates again
        var result2 = await _templateLoader.LoadAllTemplatesAsync();

        // Assert 2 - Verify only one template remains (no stale cache)
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Should().HaveCount(1);
        result2.Value.Should().Contain(t => t.TemplateId == "template-2");
        result2.Value.Should().NotContain(t => t.TemplateId == "template-1");
    }

    /// <summary>
    /// Tests that LoadAllTemplatesAsync with type filter reflects changes immediately.
    /// Verifies that filtered queries do not cache results.
    /// </summary>
    [Fact]
    public async Task LoadAllTemplatesAsync_WithTypeFilter_ReflectsImmediateChanges()
    {
        // Arrange - Create daily and weekly templates
        var dailyPath = Path.Combine(_testDirectory, "daily-template.md");
        var weeklyPath = Path.Combine(_testDirectory, "weekly-template.md");

        await File.WriteAllTextAsync(dailyPath, """
            ---
            id: daily-template
            templateType: daily
            title: Daily Template
            ---
            Content
            """);

        await File.WriteAllTextAsync(weeklyPath, """
            ---
            id: weekly-template
            templateType: weekly
            title: Weekly Template
            ---
            Content
            """);

        // Cast to access filtering method
        var fsLoader = (FileSystemTemplateLoader)_templateLoader;

        // Act 1 - Load daily templates only
        var dailyResult1 = await fsLoader.LoadAllTemplatesAsync(TemplateType.Daily);

        // Assert 1 - Only daily template
        dailyResult1.IsSuccess.Should().BeTrue();
        dailyResult1.Value.Should().HaveCount(1);
        dailyResult1.Value[0].TemplateType.Should().Be(TemplateType.Daily);

        // Arrange - Add another daily template
        var dailyPath2 = Path.Combine(_testDirectory, "daily-template-2.md");
        await File.WriteAllTextAsync(dailyPath2, """
            ---
            id: daily-template-2
            templateType: daily
            title: Daily Template 2
            ---
            Content
            """);

        // Act 2 - Load daily templates again (should include new one)
        var dailyResult2 = await fsLoader.LoadAllTemplatesAsync(TemplateType.Daily);

        // Assert 2 - Both daily templates present (no filter caching)
        dailyResult2.IsSuccess.Should().BeTrue();
        dailyResult2.Value.Should().HaveCount(2);
        dailyResult2.Value.Should().AllSatisfy(t => t.TemplateType.Should().Be(TemplateType.Daily));
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();

        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
