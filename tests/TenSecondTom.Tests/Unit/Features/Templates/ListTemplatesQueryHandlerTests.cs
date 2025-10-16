using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Features.Templates.Models;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.Templates;

/// <summary>
/// Unit tests for ListTemplatesQueryHandler (T030).
/// Tests querying and filtering templates for selection UI.
/// Tests cover:
/// - Filtering by template type (daily vs weekly)
/// - Sorting (default templates first, then alphabetical)
/// - Handling invalid templates (skip them)
/// - Empty results
/// </summary>
public sealed class ListTemplatesQueryHandlerTests
{
    private readonly Mock<IPromptTemplateLoader> _mockTemplateLoader;
    private readonly Mock<ILogger<ListTemplatesQueryHandler>> _mockLogger;
    private readonly ListTemplatesQueryHandler _handler;

    public ListTemplatesQueryHandlerTests()
    {
        _mockTemplateLoader = new Mock<IPromptTemplateLoader>();
        _mockLogger = new Mock<ILogger<ListTemplatesQueryHandler>>();

        // This will fail until ListTemplatesQueryHandler is implemented
        _handler = new ListTemplatesQueryHandler(
            _mockTemplateLoader.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithDailyTypeFilter_ReturnsOnlyDailyTemplates()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary", isDefault: true),
            CreateTemplate("daily-custom", TemplateType.Daily, "Custom Daily"),
            CreateTemplate("weekly-review", TemplateType.Weekly, "Weekly Review", isDefault: true)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("query should succeed");
        result.Value.Templates.Should().HaveCount(2, "should return only daily templates");
        result.Value.Templates.Should().OnlyContain(t => t.TemplateType == TemplateType.Daily);
    }

    [Fact]
    public async Task Handle_WithWeeklyTypeFilter_ReturnsOnlyWeeklyTemplates()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary", isDefault: true),
            CreateTemplate("weekly-review", TemplateType.Weekly, "Weekly Review", isDefault: true),
            CreateTemplate("weekly-custom", TemplateType.Weekly, "Custom Weekly")
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Weekly);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("query should succeed");
        result.Value.Templates.Should().HaveCount(2, "should return only weekly templates");
        result.Value.Templates.Should().OnlyContain(t => t.TemplateType == TemplateType.Weekly);
    }

    [Fact]
    public async Task Handle_WithMultipleTemplates_SortsDefaultsFirst()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("custom-daily", TemplateType.Daily, "Custom Daily", isDefault: false),
            CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary", isDefault: true),
            CreateTemplate("another-custom", TemplateType.Daily, "Another Custom", isDefault: false)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(3);
        result.Value.Templates[0].IsDefault.Should().BeTrue("default template should be first");
        result.Value.Templates[0].TemplateId.Should().Be("daily-summary");
    }

    [Fact]
    public async Task Handle_WithMultipleCustomTemplates_SortsAlphabetically()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("zebra-daily", TemplateType.Daily, "Zebra Daily", isDefault: false),
            CreateTemplate("apple-daily", TemplateType.Daily, "Apple Daily", isDefault: false),
            CreateTemplate("banana-daily", TemplateType.Daily, "Banana Daily", isDefault: false)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(3);
        result.Value.Templates[0].Title.Should().Be("Apple Daily");
        result.Value.Templates[1].Title.Should().Be("Banana Daily");
        result.Value.Templates[2].Title.Should().Be("Zebra Daily");
    }

    [Fact]
    public async Task Handle_WithDefaultAndCustomTemplates_SortsDefaultsFirstThenAlphabetical()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("zebra-daily", TemplateType.Daily, "Zebra Daily", isDefault: false),
            CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary", isDefault: true),
            CreateTemplate("apple-daily", TemplateType.Daily, "Apple Daily", isDefault: false),
            CreateTemplate("weekly-review", TemplateType.Weekly, "Weekly Review", isDefault: true)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(3);
        result.Value.Templates[0].IsDefault.Should().BeTrue();
        result.Value.Templates[0].Title.Should().Be("Daily Summary");
        result.Value.Templates[1].Title.Should().Be("Apple Daily");
        result.Value.Templates[2].Title.Should().Be("Zebra Daily");
    }

    [Fact]
    public async Task Handle_WithInvalidTemplates_SkipsInvalidAndReturnsValid()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("valid-daily", TemplateType.Daily, "Valid Daily"),
            // Invalid template missing title
            new PromptTemplate
            {
                TemplateId = "invalid-no-title",
                Content = "Content",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem,
                Metadata = new TemplateMetadata
                {
                    TemplateType = TemplateType.Daily,
                    Title = null // Invalid: missing title
                }
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("handler should skip invalid and return valid");
        result.Value.Templates.Should().HaveCount(1, "should skip invalid template");
        result.Value.Templates[0].TemplateId.Should().Be("valid-daily");
        result.Value.InvalidCount.Should().Be(1, "should track skipped invalid templates");
    }

    [Fact]
    public async Task Handle_WithNoTemplates_ReturnsEmptyList()
    {
        // Arrange
        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(new List<PromptTemplate>()));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue("empty result should still succeed");
        result.Value.Templates.Should().BeEmpty("should return empty list");
        result.Value.InvalidCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenLoaderFails_ReturnsFailure()
    {
        // Arrange
        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Failure("Failed to load templates from disk"));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse("should propagate loader failure");
        result.Error.Should().Contain("Failed to load templates");
    }

    [Fact]
    public async Task Handle_WithTemplatesMissingMetadata_UsesTemplateIdAsTitle()
    {
        // Arrange - Template without metadata
        var templates = new List<PromptTemplate>
        {
            new PromptTemplate
            {
                TemplateId = "no-metadata-daily",
                Content = "Content",
                TemplateType = TemplateType.Daily,
                Source = TemplateSource.FileSystem,
                Metadata = null
            }
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(1);
        result.Value.Templates[0].Title.Should().Be("no-metadata-daily", "should use template ID as fallback title");
    }

    [Fact]
    public async Task Handle_WithMixedSources_IncludesBothEmbeddedAndFilesystem()
    {
        // Arrange
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary",
                isDefault: true, source: TemplateSource.Embedded),
            CreateTemplate("custom-daily", TemplateType.Daily, "Custom Daily",
                isDefault: false, source: TemplateSource.FileSystem)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(2, "should include templates from both sources");
        result.Value.Templates.Should().Contain(t => t.Source == TemplateSource.Embedded);
        result.Value.Templates.Should().Contain(t => t.Source == TemplateSource.FileSystem);
    }

    [Fact]
    public async Task Handle_MapsTemplateToListItem_CorrectlyPopulatesAllFields()
    {
        // Arrange
        var template = CreateTemplate(
            "test-daily",
            TemplateType.Daily,
            "Test Title",
            description: "Test description",
            isDefault: false);

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(new List<PromptTemplate> { template }));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var listItem = result.Value.Templates[0];
        listItem.TemplateId.Should().Be("test-daily");
        listItem.Title.Should().Be("Test Title");
        listItem.Description.Should().Be("Test description");
        listItem.Source.Should().Be(TemplateSource.FileSystem);
        listItem.IsDefault.Should().BeFalse();
        listItem.TemplateType.Should().Be(TemplateType.Daily);
    }

    [Fact]
    public async Task Handle_WithCustomTemplatesAlongsideDefaults_ReturnsAll()
    {
        // Arrange - Custom templates (T053)
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary", isDefault: true, source: TemplateSource.Embedded),
            CreateTemplate("my-custom-daily", TemplateType.Daily, "My Custom Daily", isDefault: false, source: TemplateSource.FileSystem),
            CreateTemplate("another-custom", TemplateType.Daily, "Another Custom", isDefault: false, source: TemplateSource.FileSystem)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(3, "should return default and custom templates");
        result.Value.Templates.Should().Contain(t => t.Source == TemplateSource.Embedded, "should include embedded default");
        result.Value.Templates.Should().Contain(t => t.Source == TemplateSource.FileSystem, "should include filesystem custom templates");
    }

    [Fact]
    public async Task Handle_WithCustomTemplates_SortedAlphabetically()
    {
        // Arrange - Custom templates sorted (T053)
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("zebra-daily", TemplateType.Daily, "Zebra Daily", isDefault: false),
            CreateTemplate("alpha-daily", TemplateType.Daily, "Alpha Daily", isDefault: false),
            CreateTemplate("beta-daily", TemplateType.Daily, "Beta Daily", isDefault: false),
            CreateTemplate("charlie-daily", TemplateType.Daily, "Charlie Daily", isDefault: false)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(4);
        result.Value.Templates[0].Title.Should().Be("Alpha Daily", "custom templates should be sorted alphabetically");
        result.Value.Templates[1].Title.Should().Be("Beta Daily");
        result.Value.Templates[2].Title.Should().Be("Charlie Daily");
        result.Value.Templates[3].Title.Should().Be("Zebra Daily");
    }

    [Fact]
    public async Task Handle_WithMultipleCustomTemplates_HandlesLargeCollections()
    {
        // Arrange - Multiple custom templates (T053)
        var templates = new List<PromptTemplate>();

        // Add default template
        templates.Add(CreateTemplate("daily-summary", TemplateType.Daily, "Daily Summary", isDefault: true, source: TemplateSource.Embedded));

        // Add 20 custom templates with various names
        for (int i = 1; i <= 20; i++)
        {
            templates.Add(CreateTemplate(
                $"custom-{i:D2}",
                TemplateType.Daily,
                $"Custom Template {i:D2}",
                isDefault: false,
                source: TemplateSource.FileSystem));
        }

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(21, "should handle large collections");
        result.Value.Templates[0].IsDefault.Should().BeTrue("default template should still be first");
        result.Value.Templates[0].Title.Should().Be("Daily Summary");

        // Verify custom templates are sorted alphabetically after default
        for (int i = 1; i < result.Value.Templates.Count; i++)
        {
            result.Value.Templates[i].IsDefault.Should().BeFalse("remaining should be custom templates");
            result.Value.Templates[i].Source.Should().Be(TemplateSource.FileSystem);
        }
    }

    [Fact]
    public async Task Handle_WithOnlyCustomTemplates_NoDefaults_ReturnsCustomOnly()
    {
        // Arrange - Only custom templates, no defaults (T053)
        var templates = new List<PromptTemplate>
        {
            CreateTemplate("custom-one", TemplateType.Daily, "Custom One", isDefault: false),
            CreateTemplate("custom-two", TemplateType.Daily, "Custom Two", isDefault: false)
        };

        _mockTemplateLoader
            .Setup(l => l.LoadAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<List<PromptTemplate>>.Success(templates));

        var query = new ListTemplatesQuery(FilterByType: TemplateType.Daily);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Templates.Should().HaveCount(2);
        result.Value.Templates.Should().OnlyContain(t => !t.IsDefault, "should only contain custom templates");
        result.Value.Templates.Should().OnlyContain(t => t.Source == TemplateSource.FileSystem);
    }

    private static PromptTemplate CreateTemplate(
        string templateId,
        TemplateType templateType,
        string title,
        string? description = null,
        bool isDefault = false,
        TemplateSource source = TemplateSource.FileSystem)
    {
        return new PromptTemplate
        {
            TemplateId = templateId,
            Content = "# Template Content",
            TemplateType = templateType,
            Source = source,
            Metadata = new TemplateMetadata
            {
                TemplateType = templateType,
                Title = title,
                Description = description
            }
        };
    }
}
