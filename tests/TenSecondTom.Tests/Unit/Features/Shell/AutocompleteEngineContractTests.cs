using FluentAssertions;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Features.Shell.Models;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Shell;

/// <summary>
/// Contract tests for the Autocomplete Engine component.
/// Tests verify the interface contract defined in contracts/autocomplete.md
/// </summary>
public sealed class AutocompleteEngineContractTests
{
    [Fact]
    public void GetSuggestions_WithValidPrefix_ReturnsSuggestions()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("/to");
        
        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.CommandName == "/today");
    }

    [Fact]
    public void GetSuggestions_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("");
        
        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void GetSuggestions_WithoutSlashPrefix_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("today");
        
        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void GetSuggestions_WithNoMatches_ReturnsEmptyList()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("/xyz");
        
        // Assert
        suggestions.Should().BeEmpty();
    }

    [Fact]
    public void GetSuggestions_WithMultipleMatches_ReturnsRankedList()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("/");
        
        // Assert
        suggestions.Should().NotBeEmpty();
        suggestions.Should().BeInDescendingOrder(s => s.MatchScore);
    }

    [Fact]
    public void GetSuggestions_WithExactMatch_ReturnsSingleSuggestion()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("/today");
        
        // Assert
        suggestions.Should().HaveCount(1);
        suggestions[0].CommandName.Should().Be("/today");
        suggestions[0].MatchScore.Should().Be(100);
    }

    [Fact]
    public void GetSuggestions_LimitsToTenResults()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("/");
        
        // Assert
        suggestions.Count.Should().BeLessThanOrEqualTo(10);
    }

    [Fact]
    public void GetSuggestions_IncludesAliases()
    {
        // Arrange
        var engine = new AutocompleteEngine();
        
        // Act
        var suggestions = engine.GetSuggestions("/ex");
        
        // Assert
        suggestions.Should().Contain(s => s.CommandName == "/exit");
    }
}
