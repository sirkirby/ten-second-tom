using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Unit.Shared.Constants;

/// <summary>
/// Unit tests for SttProviders constants.
/// Tests that all STT provider constants are properly defined and helper methods work correctly.
/// </summary>
public sealed class SttProvidersTests
{
    [Fact]
    public void WhisperCpp_ShouldBeLowercase()
    {
        // Assert
        SttProviders.WhisperCpp.Should().Be("whisper-cpp");
    }

    [Fact]
    public void OpenAI_ShouldBeLowercase()
    {
        // Assert
        SttProviders.OpenAI.Should().Be("openai");
    }

    [Fact]
    public void All_ShouldContainWhisperCpp()
    {
        // Assert
        SttProviders.All.Should().Contain(SttProviders.WhisperCpp);
    }

    [Fact]
    public void All_ShouldContainOpenAI()
    {
        // Assert
        SttProviders.All.Should().Contain(SttProviders.OpenAI);
    }

    [Fact]
    public void All_ShouldHaveExactlyTwoProviders()
    {
        // Assert
        SttProviders.All.Should().HaveCount(2);
    }

    [Fact]
    public void All_ShouldHaveUniqueValues()
    {
        // Assert
        SttProviders.All.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("whisper-cpp", false)]
    [InlineData("openai", true)]
    [InlineData("OPENAI", true)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void RequiresApiKey_WithVariousProviders_ReturnsCorrectValue(string? provider, bool expected)
    {
        // Act
        var result = SttProviders.RequiresApiKey(provider!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RequiresApiKey_WithWhisperCpp_ReturnsFalse()
    {
        // Act
        var result = SttProviders.RequiresApiKey(SttProviders.WhisperCpp);

        // Assert
        result.Should().BeFalse("whisper.cpp is local and does not require an API key");
    }

    [Fact]
    public void RequiresApiKey_WithOpenAI_ReturnsTrue()
    {
        // Act
        var result = SttProviders.RequiresApiKey(SttProviders.OpenAI);

        // Assert
        result.Should().BeTrue("OpenAI Whisper API requires an API key");
    }

    [Theory]
    [InlineData("whisper-cpp", true)]
    [InlineData("openai", false)]
    [InlineData("WHISPER-CPP", true)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void SupportsFallback_WithVariousProviders_ReturnsCorrectValue(string? provider, bool expected)
    {
        // Act
        var result = SttProviders.SupportsFallback(provider!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SupportsFallback_WithWhisperCpp_ReturnsTrue()
    {
        // Act
        var result = SttProviders.SupportsFallback(SttProviders.WhisperCpp);

        // Assert
        result.Should().BeTrue("whisper.cpp can fallback to OpenAI cloud service");
    }

    [Fact]
    public void SupportsFallback_WithOpenAI_ReturnsFalse()
    {
        // Act
        var result = SttProviders.SupportsFallback(SttProviders.OpenAI);

        // Assert
        result.Should().BeFalse("OpenAI is already cloud-based");
    }

    [Fact]
    public void All_ShouldBeReadOnly()
    {
        // Arrange & Act
        var all = SttProviders.All;

        // Assert
        all.Should().BeAssignableTo<IReadOnlyList<string>>("All should be read-only");
    }

    [Fact]
    public void ProviderConstants_ShouldMatchAllCollection()
    {
        // Arrange
        var expectedProviders = new[]
        {
            SttProviders.WhisperCpp,
            SttProviders.OpenAI
        };

        // Assert
        SttProviders.All.Should().BeEquivalentTo(expectedProviders);
    }
}
