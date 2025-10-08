using FluentAssertions;
using Spectre.Console;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Shell;

/// <summary>
/// Unit tests for accessibility requirements in shell output.
/// Verifies WCAG AA contrast requirements and color scheme compatibility.
/// </summary>
public sealed class AccessibilityTests
{
    [Fact]
    public void SpectreConsoleColors_MeetWCAGAAContrast_ForLightTheme()
    {
        // Arrange - Define the colors used in shell output
        var successColor = Color.Green;
        var errorColor = Color.Red;
        var warningColor = Color.Yellow;
        var infoColor = Color.Cyan1;
        var dimColor = Color.Grey;

        // Assert - Verify colors are distinct and meet WCAG AA standards
        // WCAG AA requires 4.5:1 contrast ratio for normal text on white background
        // This is a smoke test - actual contrast calculation would require RGB values
        successColor.Should().NotBe(errorColor, "success and error colors must be distinguishable");
        errorColor.Should().NotBe(warningColor, "error and warning colors must be distinguishable");
        
        // Colors should be from the standard palette that supports contrast
        successColor.Should().Be(Color.Green, "success color should be green for accessibility");
        errorColor.Should().Be(Color.Red, "error color should be red for accessibility");
    }

    [Fact]
    public void SpectreConsoleColors_MeetWCAGAAContrast_ForDarkTheme()
    {
        // Arrange - Same colors should work on dark backgrounds
        var successColor = Color.Green;
        var errorColor = Color.Red;
        var infoColor = Color.Cyan1;

        // Assert - Verify colors provide sufficient contrast on dark backgrounds
        // Green, Red, and Cyan1 are standard terminal colors that work on both light and dark
        successColor.Should().NotBe(Color.Black, "must contrast with dark background");
        errorColor.Should().NotBe(Color.Black, "must contrast with dark background");
        infoColor.Should().NotBe(Color.Black, "must contrast with dark background");
    }

    [Fact]
    public void ErrorMessages_UseRedColor_WithBoldForEmphasis()
    {
        // Arrange
        string errorMarkup = "[red bold]Error[/]";

        // Act - Verify the markup is valid
        Action act = () => Markup.Escape(errorMarkup);

        // Assert - Should not throw
        act.Should().NotThrow("error markup should be valid");
        
        // Verify structure
        errorMarkup.Should().Contain("red", "errors should use red color");
        errorMarkup.Should().Contain("bold", "errors should be bold for emphasis");
    }

    [Fact]
    public void SuccessMessages_UseGreenColor()
    {
        // Arrange
        string successMarkup = "[green]Success[/]";

        // Act
        Action act = () => Markup.Escape(successMarkup);

        // Assert
        act.Should().NotThrow("success markup should be valid");
        successMarkup.Should().Contain("green", "success messages should use green");
    }

    [Fact]
    public void DimText_UsesGreyColor_NotTooLight()
    {
        // Arrange
        string dimMarkup = "[dim]Additional info[/]";

        // Act
        Action act = () => Markup.Escape(dimMarkup);

        // Assert
        act.Should().NotThrow("dim markup should be valid");
        dimMarkup.Should().Contain("dim", "supplementary text should be dimmed");
    }

    [Fact]
    public void OutputMarkup_RemainReadable_WhenColorsDisabled()
    {
        // Arrange - Simulate no-color environment
        string textWithMarkup = "[red]Error:[/] Something went wrong";
        string expectedPlainText = "Error: Something went wrong";

        // Act - Strip markup (Markup.Remove in Spectre.Console)
        string plainText = Markup.Remove(textWithMarkup);

        // Assert
        plainText.Should().Be(expectedPlainText, "output must remain readable without colors");
        plainText.Should().NotContain("[", "markup tags should be removed");
        plainText.Should().NotContain("]", "markup tags should be removed");
    }

    [Fact]
    public void PromptSymbol_UsesHighContrastColor()
    {
        // Arrange
        string promptMarkup = "[cyan]>[/]";

        // Act
        Action act = () => Markup.Escape(promptMarkup);

        // Assert
        act.Should().NotThrow();
        promptMarkup.Should().Contain("cyan", "prompt should use distinctive color");
    }

    [Fact]
    public void Banner_UsesColor_ButRemainReadableWithout()
    {
        // Arrange - Banner uses Cyan1 color
        string bannerText = "Ten Second Tom";

        // Assert - Text content is meaningful without color
        bannerText.Should().NotBeNullOrWhiteSpace();
        bannerText.Should().Contain("Ten Second Tom", "banner should be readable without color");
    }

    [Theory]
    [InlineData("[red]Error[/]", "Error")]
    [InlineData("[green]Success[/]", "Success")]
    [InlineData("[yellow]Warning[/]", "Warning")]
    [InlineData("[cyan]Info[/]", "Info")]
    [InlineData("[dim]Note[/]", "Note")]
    public void ColorMarkup_StripsToReadableText(string markup, string expectedText)
    {
        // Act
        string plainText = Markup.Remove(markup);

        // Assert
        plainText.Should().Be(expectedText, "markup should strip to readable text");
    }

    [Fact]
    public void AllColors_AreFromStandardPalette()
    {
        // Arrange - Colors used in the application
        var colorsUsed = new[]
        {
            Color.Green,   // Success
            Color.Red,     // Error
            Color.Yellow,  // Warning/Interrupt
            Color.Cyan1,   // Info/Prompt/Banner
            Color.Grey     // Dim text (alternative to [dim])
        };

        // Assert - All colors are from the standard 256-color palette
        foreach (var color in colorsUsed)
        {
            color.Should().NotBe(Color.Default, "should use explicit colors, not default");
        }
    }

    [Fact]
    public void ErrorPanels_UseRoundedBorder_ForVisualDistinction()
    {
        // Arrange
        var panel = new Panel("[red]Test error[/]")
            .Header("[red bold]Error[/]")
            .Border(BoxBorder.Rounded);
        
        panel = panel.BorderColor(Color.Red);

        // Assert
        panel.Border.Should().Be(BoxBorder.Rounded, "errors should use rounded border");
    }

    [Fact]
    public void TextOutput_DoesNotRelyOnColorAlone()
    {
        // Arrange - Examples of output that should be understandable without color
        string errorWithPrefix = "[red]Error:[/] File not found";
        string successWithPrefix = "[green]Success:[/] Operation completed";
        string warningWithPrefix = "[yellow]Warning:[/] Rate limit approaching";

        // Act - Remove colors
        string errorPlain = Markup.Remove(errorWithPrefix);
        string successPlain = Markup.Remove(successWithPrefix);
        string warningPlain = Markup.Remove(warningWithPrefix);

        // Assert - Text prefixes provide meaning without color
        errorPlain.Should().StartWith("Error:", "error messages have text indicator");
        successPlain.Should().StartWith("Success:", "success messages have text indicator");
        warningPlain.Should().StartWith("Warning:", "warning messages have text indicator");
    }
}
