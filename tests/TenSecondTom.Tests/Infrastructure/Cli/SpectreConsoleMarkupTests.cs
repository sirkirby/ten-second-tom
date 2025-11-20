using FluentAssertions;
using Spectre.Console;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Cli;

/// <summary>
/// Unit tests to verify Spectre.Console markup escaping is handled correctly.
/// These tests ensure that user input or dynamic content doesn't cause markup parsing errors.
/// 
/// Bug context: When dynamic values containing special characters like square brackets
/// are used in Spectre.Console markup without escaping, it throws InvalidOperationException.
/// Example: "[Balanced]" is interpreted as a style directive instead of literal text.
/// </summary>
public sealed class SpectreConsoleMarkupTests
{
    [Theory]
    [InlineData("--from-date")]
    [InlineData("--to-date")]
    [InlineData("[Balanced]")]
    [InlineData("[Premium]")]
    [InlineData("[red]dangerous[/]")]
    [InlineData("text with [markup] inside")]
    public void EscapeMarkup_ShouldPreventMarkupInterpretation(string input)
    {
        // This test verifies that potentially problematic strings are properly escaped
        // Bug fix: CommandRegistry search error handler was using invalidToken directly
        // in markup, causing "Could not find color or style '--from-date'" errors
        
        // Act
        var escaped = input.EscapeMarkup();
        
        // Assert - The escaped string should not equal the input if it contains markup characters
        if (input?.Contains('[', StringComparison.CurrentCulture) == true)
        {
            escaped.Should().NotBe(input, "Strings with square brackets should be escaped");
            escaped.Should().Contain("[[", "Square brackets should be doubled for escaping");
        }
    }

    [Fact]
    public void EscapeMarkup_ShouldHandleSquareBracketsInErrorMessages()
    {
        // Arrange - Simulate the search command error scenario
        var invalidToken = "--from-date";
        var errorMessage = $"Invalid argument: '{invalidToken}' cannot appear in query text.";
        
        // Act - Construct markup with escaped token (the fix)
        var escaped = invalidToken.EscapeMarkup();
        var markup = $"[red]Invalid argument:[/] '{escaped}' cannot appear in query text.";
        
        // Assert - Should not throw when parsed by Spectre.Console
        var action = () => new Markup(markup);
        action.Should().NotThrow<InvalidOperationException>(
            "Properly escaped markup should not cause parsing errors");
    }

    [Theory]
    [InlineData("llm.provider", "OpenAI")]
    [InlineData("llm.model-id", "gpt-4o")]
    [InlineData("memory.directory", "/path/to/[memory]")]
    public void EscapeMarkup_ShouldHandleConfigSettingNames(string settingName, string value)
    {
        // This test verifies config command success messages handle setting names safely
        // Bug fix: Config command was using settingName directly in markup
        
        // Act - Construct markup with escaped setting name (the fix)
        var escapedName = settingName.EscapeMarkup();
        var markup = $"[green]✓[/] Updated [yellow]{escapedName}[/] successfully";

        var escapedValue = value.EscapeMarkup();
        markup += $" to [cyan]{escapedValue}[/].";
        
        // Assert - Should not throw when parsed by Spectre.Console
        var action = () => new Markup(markup);
        action.Should().NotThrow<InvalidOperationException>(
            "Setting names should be properly escaped in success messages");
    }

    [Theory]
    [InlineData("Could not find color or style 'Balanced'.")]
    [InlineData("API key validation failed: [error] Invalid key")]
    [InlineData("Configuration error: missing [required] field")]
    [InlineData("Editor error: terminal [not available]")]
    public void EscapeMarkup_ShouldHandleErrorMessagesWithBrackets(string errorMessage)
    {
        // This test verifies that error messages containing brackets are properly escaped
        // Bug fix: Multiple handlers were using error messages directly in markup
        
        // Act - Construct markup with escaped error message (the fix)
        var escaped = errorMessage.EscapeMarkup();
        var markup = $"[red]✗[/] {escaped}";
        
        // Assert - Should not throw when parsed by Spectre.Console
        var action = () => new Markup(markup);
        action.Should().NotThrow<InvalidOperationException>(
            "Error messages with brackets should be properly escaped");
        
        // Verify brackets are escaped
        if (errorMessage?.Contains('[', StringComparison.CurrentCulture) == true)
        {
            escaped.Should().Contain("[[", "Error messages with brackets should have them escaped");
        }
    }

    [Fact]
    public void EscapeMarkup_ShouldPreserveNonMarkupText()
    {
        // Arrange
        var safeText = "This is perfectly safe text with no markup";
        
        // Act
        var escaped = safeText.EscapeMarkup();
        
        // Assert - Safe text should remain unchanged
        escaped.Should().Be(safeText, "Text without markup characters should not be modified");
    }

    [Theory]
    [InlineData("Text with [green]color[/] markup", "Text with [[green]]color[[/]] markup")]
    [InlineData("[bold]Important[/]", "[[bold]]Important[[/]]")]
    [InlineData("Already [[escaped]]", "Already [[[[escaped]]]]")]
    public void EscapeMarkup_ShouldDoubleSquareBrackets(string input, string expected)
    {
        // Act
        var escaped = input.EscapeMarkup();
        
        // Assert - Verify brackets are doubled
        escaped.Should().Be(expected, "Square brackets should be doubled for Spectre.Console escaping");
    }

    [Fact]
    public void ModelChoiceString_WithCostTierInBrackets_ShouldBeProperlyEscaped()
    {
        // This test verifies the model selection fix for cost tiers in brackets
        // Bug fix: SpectreConsoleSetupWizard was constructing "[Balanced]" without escaping
        
        // Arrange
        var displayName = "Claude Sonnet 4.5";
        var costTier = "Balanced";
        var description = "Best model for complex tasks";
        
        // Act - Build the choice string and escape it (the fix)
        var choice = $"{displayName} [{costTier}] - {description}".EscapeMarkup();
        var markup = choice; // This would be added to SelectionPrompt choices
        
        // Assert - Verify the string is safe for Spectre.Console
        var action = () => new Markup(markup);
        action.Should().NotThrow<InvalidOperationException>(
            "Model choice strings with cost tiers in brackets should be properly escaped");
        
        choice.Should().Contain("[[Balanced]]", "Cost tier should be escaped to prevent markup interpretation");
    }

    [Fact]
    public void MarkupWithUnescapedBrackets_ShouldThrowInvalidOperationException()
    {
        // This test demonstrates the bug we're fixing
        // When a string like "[Balanced]" is used in markup without escaping,
        // Spectre.Console tries to interpret it as a style and fails
        
        // Arrange
        var unescapedMarkup = "Select model: [Balanced] option";
        
        // Act & Assert - Should throw because "Balanced" is not a valid style
        var action = () => new Markup(unescapedMarkup);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not find color or style 'Balanced'*");
    }

    [Fact]
    public void MarkupWithEscapedBrackets_ShouldNotThrow()
    {
        // This test demonstrates the fix
        // When properly escaped, brackets are treated as literal characters
        
        // Arrange
        var properlyEscapedMarkup = "Select model: [[Balanced]] option";
        
        // Act & Assert - Should not throw
        var action = () => new Markup(properlyEscapedMarkup);
        action.Should().NotThrow("Properly escaped brackets should be treated as literal text");
    }

    [Fact]
    public void UsageMessage_WithCommandLineOptions_ShouldEscapeBrackets()
    {
        // This test verifies that usage messages showing optional command-line arguments
        // properly escape square brackets that are used to indicate optionality
        // Bug fix: Usage messages like "[--from-date YYYY-MM-DD]" were being parsed as markup
        
        // Arrange - Usage message showing optional arguments (common CLI convention)
        var usageMessage = "[dim]Usage: search [[--from-date YYYY-MM-DD]] [[--to-date YYYY-MM-DD]] <query words>[/]";
        
        // Act & Assert - Should not throw because brackets are properly escaped
        var action = () => new Markup(usageMessage);
        action.Should().NotThrow(
            "Usage messages with escaped optional argument brackets should not cause parsing errors");
    }

    [Fact]
    public void UsageMessage_WithUnescapedBrackets_ShouldThrow()
    {
        // This test demonstrates the bug we're fixing
        // Usage messages showing optional arguments in brackets cause parse errors
        
        // Arrange - Unescaped usage message (the bug)
        var unescapedUsage = "[dim]Usage: search [--from-date YYYY-MM-DD] <query>[/]";
        
        // Act & Assert - Should throw because "--from-date" is not a valid style
        var action = () => new Markup(unescapedUsage);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Could not find color or style '--from-date'*");
    }
}
