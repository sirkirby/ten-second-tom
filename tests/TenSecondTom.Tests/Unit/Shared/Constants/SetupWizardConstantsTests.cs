using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.Shared.Constants;

/// <summary>
/// Unit tests for SetupWizardConstants.
/// Tests that all setup wizard UI constants are properly defined and consistent.
/// </summary>
public sealed class SetupWizardConstantsTests
{
    [Fact]
    public void ProviderDisplayNames_GetDisplayName_WithOpenAI_ReturnsProperCase()
    {
        // Act
        var result = SetupWizardConstants.ProviderDisplayNames.GetDisplayName(LlmProviders.OpenAI);

        // Assert
        result.Should().Be("OpenAI");
    }

    [Fact]
    public void ProviderDisplayNames_GetDisplayName_WithAnthropic_ReturnsProperCase()
    {
        // Act
        var result = SetupWizardConstants.ProviderDisplayNames.GetDisplayName(LlmProviders.Anthropic);

        // Assert
        result.Should().Be("Anthropic");
    }

    [Fact]
    public void ProviderDisplayNames_GetDisplayName_WithUnknownProvider_ReturnsSameValue()
    {
        // Act
        var result = SetupWizardConstants.ProviderDisplayNames.GetDisplayName("unknown");

        // Assert
        result.Should().Be("unknown");
    }

    [Fact]
    public void ProviderDisplayNames_GetDisplayName_WithUppercaseInput_ReturnsProperCase()
    {
        // Act
        var result = SetupWizardConstants.ProviderDisplayNames.GetDisplayName("OPENAI");

        // Assert - should normalize to lowercase first, then map
        result.Should().Be("OpenAI");
    }

    [Fact]
    public void ProviderDisplayNames_OpenAIChoice_ShouldContainOpenAI()
    {
        // Assert
        SetupWizardConstants.ProviderDisplayNames.GetDisplayNames().Should().Contain("OpenAI");
    }

    [Fact]
    public void ProviderDisplayNames_GetAvailableDisplayNames_ShouldOnlyContainOpenAI()
    {
        // Arrange & Act
        var availableProviders = SetupWizardConstants.ProviderDisplayNames.GetAvailableDisplayNames();

        // Assert - Only OpenAI should be available for configuration
        availableProviders.Should().ContainSingle();
        availableProviders.Should().Contain("OpenAI");
        availableProviders.Should().NotContain("Anthropic",
            "Anthropic is not available for configuration (code exists but not selectable)");
    }

    [Fact]
    public void ProviderDisplayNames_AnthropicDisplayName_ShouldStillExist()
    {
        // Assert - Anthropic data still exists, just not shown in UI
        SetupWizardConstants.ProviderDisplayNames.GetDisplayName(LlmProviders.Anthropic)
            .Should().Be("Anthropic", "Anthropic code and data should remain intact");
    }

    [Fact]
    public void LogLevelDisplayNames_Debug_ShouldBeCorrectValue()
    {
        // Assert
        SetupWizardConstants.LogLevelDisplayNames.Debug.Should().Be("Debug (verbose)");
    }

    [Fact]
    public void LogLevelDisplayNames_Information_ShouldBeCorrectValue()
    {
        // Assert
        SetupWizardConstants.LogLevelDisplayNames.Information.Should().Be("Information (recommended)");
    }

    [Fact]
    public void LogLevelDisplayNames_Warning_ShouldBeCorrectValue()
    {
        // Assert
        SetupWizardConstants.LogLevelDisplayNames.Warning.Should().Be("Warning (quiet)");
    }

    [Fact]
    public void LogLevelDisplayNames_Error_ShouldBeCorrectValue()
    {
        // Assert
        SetupWizardConstants.LogLevelDisplayNames.Error.Should().Be("Error (silent)");
    }

    [Fact]
    public void RetentionKeywords_Unlimited_ShouldBeLowercase()
    {
        // Assert
        SetupWizardConstants.RetentionKeywords.Unlimited.Should().Be("unlimited");
    }

    [Fact]
    public void RetentionKeywords_Forever_ShouldBeLowercase()
    {
        // Assert
        SetupWizardConstants.RetentionKeywords.Forever.Should().Be("forever");
    }

    [Fact]
    public void RetentionKeywords_Zero_ShouldBeStringZero()
    {
        // Assert
        SetupWizardConstants.RetentionKeywords.Zero.Should().Be("0");
    }

    [Fact]
    public void RetentionKeywords_UnlimitedDisplay_ShouldBeUserFriendly()
    {
        // Assert
        SetupWizardConstants.RetentionKeywords.UnlimitedDisplay.Should().Be("Unlimited (never delete)");
    }

    [Fact]
    public void DisplayStrings_NotSet_ShouldBeCorrectValue()
    {
        // Assert
        SetupWizardConstants.DisplayStrings.NotSet.Should().Be("Not set");
    }

    [Fact]
    public void DisplayStrings_Days_ShouldBeCorrectValue()
    {
        // Assert
        SetupWizardConstants.DisplayStrings.Days.Should().Be("days");
    }

    [Theory]
    [InlineData("unlimited")]
    [InlineData("forever")]
    [InlineData("0")]
    public void RetentionKeywords_AllUnlimitedVariants_ShouldBeDefined(string keyword)
    {
        // Arrange & Act
        var allKeywords = new[]
        {
            SetupWizardConstants.RetentionKeywords.Unlimited,
            SetupWizardConstants.RetentionKeywords.Forever,
            SetupWizardConstants.RetentionKeywords.Zero
        };

        // Assert
        allKeywords.Should().Contain(keyword);
    }

    [Fact]
    public void ProviderDisplayNames_AllChoices_ShouldBeUnique()
    {
        // Arrange
        var choices = new[]
        {
            SetupWizardConstants.ProviderDisplayNames.GetDisplayName(LlmProviders.OpenAI),
            SetupWizardConstants.ProviderDisplayNames.GetDisplayName(LlmProviders.Anthropic)
        };

        // Assert
        choices.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(LlmProviders.OpenAI, "OpenAI")]
    [InlineData(LlmProviders.Anthropic, "Anthropic")]
    public void ProviderDisplayNames_GetDisplayName_MapsCorrectly(string providerConstant, string expectedDisplay)
    {
        // Act
        var result = SetupWizardConstants.ProviderDisplayNames.GetDisplayName(providerConstant);

        // Assert
        result.Should().Be(expectedDisplay);
    }

    [Fact]
    public void LogLevelDisplayNames_AllChoices_ShouldBeUnique()
    {
        // Arrange
        var choices = new[]
        {
            SetupWizardConstants.LogLevelDisplayNames.Debug,
            SetupWizardConstants.LogLevelDisplayNames.Information,
            SetupWizardConstants.LogLevelDisplayNames.Warning,
            SetupWizardConstants.LogLevelDisplayNames.Error
        };

        // Assert
        choices.Should().OnlyHaveUniqueItems();
    }
}
