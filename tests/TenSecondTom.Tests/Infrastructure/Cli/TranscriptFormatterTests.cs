using FluentAssertions;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Tests.Infrastructure.Cli;

/// <summary>
/// Tests for <see cref="TranscriptFormatter"/> implementation.
/// Validates transcript truncation and frontmatter stripping logic.
/// </summary>
public sealed class TranscriptFormatterTests
{
    [Fact]
    public void FormatForDisplay_WithShortTranscript_ReturnsUnmodified()
    {
        // Arrange
        var transcript = "This is a short transcript.";

        // Act
        var (formattedText, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(transcript);

        // Assert
        formattedText.Should().Be("This is a short transcript.");
        wasTruncated.Should().BeFalse();
        truncatedChars.Should().Be(0);
    }

    [Fact]
    public void FormatForDisplay_WithLongTranscript_TruncatesCorrectly()
    {
        // Arrange
        var longText = new string('A', 600) + new string('B', 600); // 1200 chars
        var transcript = longText;

        // Act
        var (formattedText, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(transcript);

        // Assert
        wasTruncated.Should().BeTrue();
        truncatedChars.Should().Be(400); // 1200 - (DefaultPreviewLength * 2)
        formattedText.Should().StartWith(new string('A', TranscriptFormatter.DefaultPreviewLength));
        formattedText.Should().EndWith(new string('B', TranscriptFormatter.DefaultPreviewLength));
        formattedText.Should().Contain("... [Transcript truncated - 400 more characters] ...");
    }

    [Fact]
    public void FormatForDisplay_WithExactMaxLength_DoesNotTruncate()
    {
        // Arrange
        var transcript = new string('A', TranscriptFormatter.DefaultMaxDisplayLength);

        // Act
        var (formattedText, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(transcript);

        // Assert
        wasTruncated.Should().BeFalse();
        truncatedChars.Should().Be(0);
        formattedText.Length.Should().Be(TranscriptFormatter.DefaultMaxDisplayLength);
    }

    [Fact]
    public void FormatForDisplay_WithCustomLimits_RespectsParameters()
    {
        // Arrange
        var transcript = new string('X', 300); // 300 chars
        int maxLength = 200;
        int previewLength = 80;

        // Act
        var (formattedText, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(
            transcript,
            maxLength,
            previewLength);

        // Assert
        wasTruncated.Should().BeTrue();
        truncatedChars.Should().Be(140); // 300 - (80 * 2)
        formattedText.Should().StartWith(new string('X', 80));
        formattedText.Should().EndWith(new string('X', 80));
    }

    [Fact]
    public void FormatForDisplay_WithNullTranscript_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*transcript*");
    }

    [Fact]
    public void FormatForDisplay_WithEmptyTranscript_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*transcript*");
    }

    [Fact]
    public void FormatForDisplay_WithWhitespaceTranscript_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*transcript*");
    }

    [Fact]
    public void FormatForDisplay_WithNegativeMaxLength_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay("test", maxDisplayLength: -1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Max display length must be positive*");
    }

    [Fact]
    public void FormatForDisplay_WithZeroMaxLength_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay("test", maxDisplayLength: 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Max display length must be positive*");
    }

    [Fact]
    public void FormatForDisplay_WithNegativePreviewLength_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay("test", previewLength: -1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Preview length must be positive*");
    }

    [Fact]
    public void FormatForDisplay_WithPreviewLengthTooLarge_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.FormatForDisplay("test", maxDisplayLength: 100, previewLength: 50);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Preview length * 2 must be less than max display length*");
    }

    [Fact]
    public void FormatForDisplay_TrimsWhitespace()
    {
        // Arrange
        var transcript = "   Hello World   ";

        // Act
        var (formattedText, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(transcript);

        // Assert
        formattedText.Should().Be("Hello World");
        wasTruncated.Should().BeFalse();
    }

    [Fact]
    public void StripFrontmatter_WithFrontmatter_RemovesCorrectly()
    {
        // Arrange
        var content = @"---
recording-id: 10-21-2025_1
timestamp: 2025-10-21T14:32:00Z
---

This is the transcript text.
It has multiple lines.";

        // Act
        var result = TranscriptFormatter.StripFrontmatter(content);

        // Assert
        result.Should().Be("This is the transcript text.\nIt has multiple lines.");
    }

    [Fact]
    public void StripFrontmatter_WithoutFrontmatter_ReturnsOriginalText()
    {
        // Arrange
        var content = "This is just plain text.\nWith multiple lines.";

        // Act
        var result = TranscriptFormatter.StripFrontmatter(content);

        // Assert
        result.Should().Be("This is just plain text.\nWith multiple lines.");
    }

    [Fact]
    public void StripFrontmatter_WithEmptyFrontmatter_RemovesCorrectly()
    {
        // Arrange
        var content = @"---
---

Transcript text here.";

        // Act
        var result = TranscriptFormatter.StripFrontmatter(content);

        // Assert
        result.Should().Be("Transcript text here.");
    }

    [Fact]
    public void StripFrontmatter_WithMultipleFrontmatterBlocks_HandlesCorrectly()
    {
        // Arrange
        var content = @"---
first: block
---

Some text.

---
second: block
---

More text.";

        // Act
        var result = TranscriptFormatter.StripFrontmatter(content);

        // Assert
        result.Should().Be("Some text.\nMore text.");
    }

    [Fact]
    public void StripFrontmatter_WithNullContent_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.StripFrontmatter(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*transcriptContent*");
    }

    [Fact]
    public void StripFrontmatter_WithEmptyContent_ThrowsArgumentException()
    {
        // Act
        var act = () => TranscriptFormatter.StripFrontmatter("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*transcriptContent*");
    }

    [Fact]
    public void StripFrontmatter_PreservesNewlines()
    {
        // Arrange
        var content = @"---
metadata: value
---

Line 1.

Line 2.


Line 3.";

        // Act
        var result = TranscriptFormatter.StripFrontmatter(content);

        // Assert
        result.Should().Contain("Line 1.\nLine 2.\nLine 3.");
    }

    [Fact]
    public void FormatForDisplay_WithVeryLongTranscript_ShowsCorrectTruncationCount()
    {
        // Arrange
        const int testLength = 50000;
        var transcript = new string('Z', testLength);

        // Act
        var (formattedText, wasTruncated, truncatedChars) = TranscriptFormatter.FormatForDisplay(transcript);

        // Assert
        var expectedTruncated = testLength - (TranscriptFormatter.DefaultPreviewLength * 2);
        wasTruncated.Should().BeTrue();
        truncatedChars.Should().Be(expectedTruncated); // 50,000 - (DefaultPreviewLength * 2)
        formattedText.Should().Contain($"{expectedTruncated:N0} more characters");
    }

    [Fact]
    public void FormatForDisplay_TruncationIndicatorFormat_IsCorrect()
    {
        // Arrange
        var transcript = new string('M', 2000);

        // Act
        var (formattedText, _, _) = TranscriptFormatter.FormatForDisplay(transcript);

        // Assert
        formattedText.Should().Contain("\n\n... [Transcript truncated - ");
        formattedText.Should().Contain(" more characters] ...\n\n");
    }
}
