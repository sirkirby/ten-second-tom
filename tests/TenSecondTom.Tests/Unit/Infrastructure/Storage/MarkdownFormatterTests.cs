using FluentAssertions;
using TenSecondTom.Infrastructure.Storage;

namespace TenSecondTom.Tests.Unit.Infrastructure.Storage;

/// <summary>
/// Unit tests for MarkdownFormatter utility class.
/// Tests YAML front matter formatting and standardization.
/// </summary>
public sealed class MarkdownFormatterTests
{
    [Fact]
    public void FormatWithYamlFrontMatter_CreatesCorrectStructure()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object>
        {
            ["entry-id"] = "test-entry-1",
            ["command"] = "today",
            ["timestamp"] = "2025-10-24T12:00:00.0000000+00:00"
        };
        string content = "# Test Content\n\nThis is the body.";

        // Act
        string result = MarkdownFormatter.FormatWithYamlFrontMatter(frontmatter, content);

        // Assert
        result.Should().StartWith("---\n");
        result.Should().Contain("entry-id: test-entry-1");
        result.Should().Contain("command: today");
        result.Should().Contain("timestamp: 2025-10-24T12:00:00.0000000+00:00");
        result.Should().Contain("---\n\n# Test Content");
    }

    [Fact]
    public void FormatWithYamlFrontMatter_HandlesEmptyContent()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object>
        {
            ["entry-id"] = "test-1"
        };
        string content = "";

        // Act
        string result = MarkdownFormatter.FormatWithYamlFrontMatter(frontmatter, content);

        // Assert
        result.Should().StartWith("---\n");
        result.Should().Contain("entry-id: test-1");
        result.Should().Contain("---\n\n");
    }

    [Fact]
    public void FormatWithYamlFrontMatter_HandlesNumericValues()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object>
        {
            ["tokens-used"] = 100,
            ["processing-duration"] = 2.5
        };
        string content = "Test";

        // Act
        string result = MarkdownFormatter.FormatWithYamlFrontMatter(frontmatter, content);

        // Assert
        result.Should().Contain("tokens-used: 100");
        result.Should().Contain("processing-duration: 2.5");
    }

    [Fact]
    public void FormatWithYamlFrontMatter_HandlesBooleanValues()
    {
        // Arrange
        var frontmatter = new Dictionary<string, object>
        {
            ["truncated"] = true,
            ["has-errors"] = false
        };
        string content = "Test";

        // Act
        string result = MarkdownFormatter.FormatWithYamlFrontMatter(frontmatter, content);

        // Assert
        result.Should().Contain("truncated: true");
        result.Should().Contain("has-errors: false");
    }

    [Fact]
    public void CreateEntryId_FormatsTodayCommand()
    {
        // Arrange
        string dateIdentifier = "10-24-2025";

        // Act
        string entryId = MarkdownFormatter.CreateEntryId("today", dateIdentifier, 1);

        // Assert
        entryId.Should().Be("today-10-24-2025-1");
    }

    [Fact]
    public void CreateEntryId_FormatsThisWeekCommand()
    {
        // Arrange
        string dateIdentifier = "10-24-2025";

        // Act
        string entryId = MarkdownFormatter.CreateEntryId("thisweek", dateIdentifier, 2);

        // Assert
        entryId.Should().Be("thisweek-10-24-2025-2");
    }

    [Fact]
    public void CreateGenerateEntryId_FormatsCorrectly()
    {
        // Arrange
        string recordingBaseName = "10-24-2025_1";
        string templateId = "business-meeting";

        // Act
        string entryId = MarkdownFormatter.CreateGenerateEntryId(recordingBaseName, templateId);

        // Assert
        entryId.Should().Be("generate-10-24-2025_1-business-meeting");
    }

    [Fact]
    public void FormatTimestamp_ReturnsIso8601Format()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 24, 14, 30, 45, 123, TimeSpan.FromHours(-5));

        // Act
        string formatted = MarkdownFormatter.FormatTimestamp(timestamp);

        // Assert
        formatted.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d+[+-]\d{2}:\d{2}$");
        formatted.Should().Contain("2025-10-24");
    }

    [Fact]
    public void FormatDuration_ReturnsTotalSeconds()
    {
        // Arrange
        var duration = TimeSpan.FromSeconds(2.5);

        // Act
        double formatted = MarkdownFormatter.FormatDuration(duration);

        // Assert
        formatted.Should().Be(2.5);
    }

    [Fact]
    public void FormatDuration_HandlesMilliseconds()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(1500);

        // Act
        double formatted = MarkdownFormatter.FormatDuration(duration);

        // Assert
        formatted.Should().Be(1.5);
    }

    [Fact]
    public void FormatDuration_HandlesMinutes()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(2);

        // Act
        double formatted = MarkdownFormatter.FormatDuration(duration);

        // Assert
        formatted.Should().Be(120.0);
    }
}

