using FluentAssertions;
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.Tests.Shared.TextEditing.Services;

public sealed class InputSanitizerTests
{
    private readonly InputSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_StripsAnsiEscapeSequences()
    {
        // Arrange
        var inputWithAnsi = "\x1B[31mRed text\x1B[0m and \x1B[1mbold\x1B[0m";
        var expectedClean = "Red text and bold";

        // Act
        var result = _sanitizer.Sanitize(inputWithAnsi);

        // Assert
        result.Content.Should().Be(expectedClean);
        result.WasSanitized.Should().BeTrue();
        result.OriginalLength.Should().Be(inputWithAnsi.Length);
        result.RemovedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sanitize_PreservesEmoji()
    {
        // Arrange
        var inputWithEmoji = "Hello 👋 World 🌍 with emoji 😊";

        // Act
        var result = _sanitizer.Sanitize(inputWithEmoji);

        // Assert
        result.Content.Should().Be(inputWithEmoji);
        result.WasSanitized.Should().BeFalse();
        result.OriginalLength.Should().Be(inputWithEmoji.Length);
        result.RemovedCount.Should().Be(0);
    }

    [Fact]
    public void Sanitize_PreservesAccentedCharacters()
    {
        // Arrange
        var inputWithAccents = "Café, naïve, résumé, Zürich, São Paulo";

        // Act
        var result = _sanitizer.Sanitize(inputWithAccents);

        // Assert
        result.Content.Should().Be(inputWithAccents);
        result.WasSanitized.Should().BeFalse();
        result.OriginalLength.Should().Be(inputWithAccents.Length);
        result.RemovedCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Sanitize_HandlesEmptyOrNullInput(string? input)
    {
        // Act
        var result = _sanitizer.Sanitize(input);

        // Assert
        result.Content.Should().BeEmpty();
        result.WasSanitized.Should().BeFalse();
        result.OriginalLength.Should().Be(0);
        result.RemovedCount.Should().Be(0);
    }

    [Fact]
    public void Sanitize_PreservesNewlinesAndTabs()
    {
        // Arrange
        var inputWithWhitespace = "Line 1\nLine 2\n\tIndented line\n\nBlank line above";

        // Act
        var result = _sanitizer.Sanitize(inputWithWhitespace);

        // Assert
        result.Content.Should().Be(inputWithWhitespace);
        result.WasSanitized.Should().BeFalse();
        result.OriginalLength.Should().Be(inputWithWhitespace.Length);
    }

    [Fact]
    public void Sanitize_StripsCursorMovementCodes()
    {
        // Arrange
        var inputWithCursorCodes = "Text\x1B[2Jclear\x1B[Hscreen";
        var expectedClean = "Textclearscreen";

        // Act
        var result = _sanitizer.Sanitize(inputWithCursorCodes);

        // Assert
        result.Content.Should().Be(expectedClean);
        result.WasSanitized.Should().BeTrue();
        result.RemovedCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Sanitize_HandlesMixedContent()
    {
        // Arrange
        var mixedInput = "Normal text \x1B[31mwith color\x1B[0m and emoji 🎉 and accents café";
        var expectedClean = "Normal text with color and emoji 🎉 and accents café";

        // Act
        var result = _sanitizer.Sanitize(mixedInput);

        // Assert
        result.Content.Should().Be(expectedClean);
        result.WasSanitized.Should().BeTrue();
    }
}
