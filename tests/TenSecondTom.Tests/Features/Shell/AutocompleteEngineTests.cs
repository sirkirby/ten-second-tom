using FluentAssertions;
using TenSecondTom.Features.Shell.Services;
using Xunit;

namespace TenSecondTom.Tests.Features.Shell;

/// <summary>
/// Unit tests for AutocompleteEngine edge cases and error handling.
/// </summary>
public sealed class AutocompleteEngineTests
{
    [Fact]
    public void GetSuggestions_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        Action act = () => engine.GetSuggestions(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("input");
    }

    [Fact]
    public void GetSuggestions_WithInputOver50Chars_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        string longInput = "/" + new string('a', 60); // 61 characters total

        // Act
        var suggestions = engine.GetSuggestions(longInput);

        // Assert
        suggestions.Should().BeEmpty("excessively long input should not match any commands");
    }

    [Fact]
    public void GetSuggestions_CaseInsensitiveMatching_ReturnsMatches()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var upperCase = engine.GetSuggestions("/NOTE").ToList();
        var lowerCase = engine.GetSuggestions("/note").ToList();
        var mixedCase = engine.GetSuggestions("/NoTe").ToList();

        // Assert
        upperCase.Should().NotBeEmpty("uppercase should match");
        lowerCase.Should().NotBeEmpty("lowercase should match");
        mixedCase.Should().NotBeEmpty("mixed case should match");

        // All should find the same command
        upperCase.Should().Contain(s => s.CommandName == "/note");
        lowerCase.Should().Contain(s => s.CommandName == "/note");
        mixedCase.Should().Contain(s => s.CommandName == "/note");
    }

    [Fact]
    public void GetSuggestions_WithAliasCommand_AppearsInResults()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var exitSuggestions = engine.GetSuggestions("/exit").ToList();
        var quitSuggestions = engine.GetSuggestions("/quit").ToList();

        // Assert
        exitSuggestions.Should().NotBeEmpty("/exit should match as alias");
        quitSuggestions.Should().NotBeEmpty("/quit should match as primary command");
        
        // Both should suggest the quit command functionality
        exitSuggestions.Should().Contain(s => s.CommandName.Contains("exit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_WithPartialMatch_RanksByRelevance()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions("/").ToList();

        // Assert
        suggestions.Should().NotBeEmpty("slash alone should return all commands");
        suggestions.Should().HaveCountLessThanOrEqualTo(10, "should limit to top 10 results");
        
        // Verify suggestions are ranked (most relevant first)
        for (int i = 0; i < suggestions.Count - 1; i++)
        {
            suggestions[i].MatchScore.Should().BeGreaterThanOrEqualTo(suggestions[i + 1].MatchScore,
                "suggestions should be in descending order of match score");
        }
    }

    [Fact]
    public void GetSuggestions_WithExactPrefix_ReturnsHighScores()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions("/not").ToList();

        // Assert
        suggestions.Should().NotBeEmpty();
        var noteSuggestion = suggestions.First(s => s.CommandName == "/note");
        noteSuggestion.MatchScore.Should().BeGreaterThan(80, "exact prefix match should have high score");
    }

    [Fact]
    public void GetSuggestions_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions("/xyz123nonexistent");

        // Assert
        suggestions.Should().BeEmpty("non-matching input should return no suggestions");
    }

    [Fact]
    public void GetSuggestions_WithWhitespaceOnly_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions("   ");

        // Assert
        suggestions.Should().BeEmpty("whitespace-only input should return no suggestions");
    }

    [Fact]
    public void GetSuggestions_WithEmptyString_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions(string.Empty);

        // Assert
        suggestions.Should().BeEmpty("empty string should return no suggestions");
    }

    [Fact]
    public void GetSuggestions_LimitsToMaxTenResults()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act - Use broad prefix that might match many commands
        var suggestions = engine.GetSuggestions("/").ToList();

        // Assert
        suggestions.Should().HaveCountLessThanOrEqualTo(10, 
            "should never return more than 10 suggestions regardless of match count");
    }

    [Fact]
    public void GetSuggestions_IncludesHelpTextInSuggestion()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions("/help").ToList();

        // Assert
        suggestions.Should().NotBeEmpty();
        var helpSuggestion = suggestions.First(s => s.CommandName == "/help");
        helpSuggestion.HelpText.Should().NotBeNullOrWhiteSpace("each suggestion should include help text");
        helpSuggestion.HelpText.Length.Should().BeGreaterThan(10, "help text should be descriptive");
    }

    [Fact]
    public void GetSuggestions_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        var engine = new AutocompleteEngine();

        // Act
        var suggestions = engine.GetSuggestions("/@#$%");

        // Assert
        suggestions.Should().BeEmpty("special characters should not cause exceptions");
    }

    [Fact]
    public void GetSuggestions_ConsistentResults_ForSameInput()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        string input = "/to";

        // Act
        var firstCall = engine.GetSuggestions(input).ToList();
        var secondCall = engine.GetSuggestions(input).ToList();

        // Assert
        firstCall.Should().Equal(secondCall, (a, b) => 
            a.CommandName == b.CommandName && 
            a.HelpText == b.HelpText && 
            a.MatchScore == b.MatchScore,
            "repeated calls with same input should return identical results");
    }
}
