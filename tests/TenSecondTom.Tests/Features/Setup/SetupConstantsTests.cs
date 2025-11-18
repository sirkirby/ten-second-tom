using FluentAssertions;
using TenSecondTom.Features.Setup.Constants;
using Xunit;

namespace TenSecondTom.Tests.Features.Setup;

/// <summary>
/// Unit tests for SetupConstants.
/// Tests that all setup wizard UI constants are properly defined and consistent.
/// </summary>
public sealed class SetupConstantsTests
{
    [Fact]
    public void LogLevelDisplayNames_Debug_ShouldBeCorrectValue()
    {
        // Assert
        SetupConstants.LogLevelDisplayNames.Debug.Should().Be("Debug (verbose)");
    }

    [Fact]
    public void LogLevelDisplayNames_Information_ShouldBeCorrectValue()
    {
        // Assert
        SetupConstants.LogLevelDisplayNames.Information.Should().Be("Information (recommended)");
    }

    [Fact]
    public void LogLevelDisplayNames_Warning_ShouldBeCorrectValue()
    {
        // Assert
        SetupConstants.LogLevelDisplayNames.Warning.Should().Be("Warning (quiet)");
    }

    [Fact]
    public void LogLevelDisplayNames_Error_ShouldBeCorrectValue()
    {
        // Assert
        SetupConstants.LogLevelDisplayNames.Error.Should().Be("Error (silent)");
    }

    [Fact]
    public void RetentionKeywords_Unlimited_ShouldBeLowercase()
    {
        // Assert
        SetupConstants.RetentionKeywords.Unlimited.Should().Be("unlimited");
    }

    [Fact]
    public void RetentionKeywords_Forever_ShouldBeLowercase()
    {
        // Assert
        SetupConstants.RetentionKeywords.Forever.Should().Be("forever");
    }

    [Fact]
    public void RetentionKeywords_Zero_ShouldBeStringZero()
    {
        // Assert
        SetupConstants.RetentionKeywords.Zero.Should().Be("0");
    }

    [Fact]
    public void RetentionKeywords_UnlimitedDisplay_ShouldBeUserFriendly()
    {
        // Assert
        SetupConstants.RetentionKeywords.UnlimitedDisplay.Should().Be("Unlimited (never delete)");
    }

    [Fact]
    public void DisplayStrings_NotSet_ShouldBeCorrectValue()
    {
        // Assert
        SetupConstants.DisplayStrings.NotSet.Should().Be("Not set");
    }

    [Fact]
    public void DisplayStrings_Days_ShouldBeCorrectValue()
    {
        // Assert
        SetupConstants.DisplayStrings.Days.Should().Be("days");
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
            SetupConstants.RetentionKeywords.Unlimited,
            SetupConstants.RetentionKeywords.Forever,
            SetupConstants.RetentionKeywords.Zero
        };

        // Assert
        allKeywords.Should().Contain(keyword);
    }

    [Fact]
    public void LogLevelDisplayNames_AllChoices_ShouldBeUnique()
    {
        // Arrange
        var choices = new[]
        {
            SetupConstants.LogLevelDisplayNames.Debug,
            SetupConstants.LogLevelDisplayNames.Information,
            SetupConstants.LogLevelDisplayNames.Warning,
            SetupConstants.LogLevelDisplayNames.Error
        };

        // Assert
        choices.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Timeouts_SshKeyDetectionSeconds_ShouldBePositive()
    {
        // Assert
        SetupConstants.Timeouts.SshKeyDetectionSeconds.Should().BePositive();
    }

    [Fact]
    public void Timeouts_ApiValidationSeconds_ShouldBePositive()
    {
        // Assert
        SetupConstants.Timeouts.ApiValidationSeconds.Should().BePositive();
    }

    [Fact]
    public void Timeouts_TotalSetupSeconds_ShouldBePositive()
    {
        // Assert
        SetupConstants.Timeouts.TotalSetupSeconds.Should().BePositive();
    }
}
