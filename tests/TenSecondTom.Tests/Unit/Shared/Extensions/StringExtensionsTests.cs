using FluentAssertions;
using TenSecondTom.Shared.Extensions;

namespace TenSecondTom.Tests.Unit.Shared.Extensions;

/// <summary>
/// Unit tests for StringExtensions utility methods.
/// Tests string manipulation and cleaning operations.
/// </summary>
public sealed class StringExtensionsTests
{
    [Fact]
    public void StripMarkdownCodeBlock_RemovesMarkdownCodeBlock()
    {
        // Arrange
        string input = "```markdown\n# Heading\n\nContent\n```";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Heading\n\nContent");
    }

    [Fact]
    public void StripMarkdownCodeBlock_RemovesPlainCodeBlock()
    {
        // Arrange
        string input = "```\n# Heading\n\nContent\n```";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Heading\n\nContent");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesWhitespaceAroundCodeBlock()
    {
        // Arrange
        string input = "  ```markdown\n# Heading\n```  ";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Heading");
    }

    [Fact]
    public void StripMarkdownCodeBlock_LeavesContentWithoutCodeBlockUnchanged()
    {
        // Arrange
        string input = "# Heading\n\nRegular content";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Heading\n\nRegular content");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesEmptyString()
    {
        // Arrange
        string input = "";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesNullString()
    {
        // Arrange
        string? input = null;

        // Act
        string? result = input!.StripMarkdownCodeBlock();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesWhitespaceOnlyString()
    {
        // Arrange
        string input = "   \n  \t  ";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("   \n  \t  ");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesCaseInsensitiveMarkdown()
    {
        // Arrange
        string input = "```MARKDOWN\n# Content\n```";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Content");
    }

    [Fact]
    public void StripMarkdownCodeBlock_DoesNotRemoveInlineCodeBlocks()
    {
        // Arrange
        string input = "Some text with `inline code` and more text";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("Some text with `inline code` and more text");
    }

    [Fact]
    public void StripMarkdownCodeBlock_DoesNotRemoveCodeBlocksInMiddleOfContent()
    {
        // Arrange
        string input = "Before\n```\ncode\n```\nAfter";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        // Should remain unchanged as the code block is not wrapping the entire content
        result.Should().Be("Before\n```\ncode\n```\nAfter");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesWindowsLineEndings()
    {
        // Arrange
        string input = "```markdown\r\n# Heading\r\nContent\r\n```";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Heading\r\nContent");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesMacLineEndings()
    {
        // Arrange
        string input = "```markdown\r# Heading\rContent\r```";

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Be("# Heading\rContent");
    }

    [Fact]
    public void StripMarkdownCodeBlock_HandlesMultilineContentWithNewlines()
    {
        // Arrange
        string input = """
            ```markdown
            # Daily Summary

            ## Key Events
            - Event 1
            - Event 2

            ## Themes
            - Theme 1
            ```
            """;

        // Act
        string result = input.StripMarkdownCodeBlock();

        // Assert
        result.Should().Contain("# Daily Summary");
        result.Should().Contain("## Key Events");
        result.Should().NotContain("```");
    }
}

