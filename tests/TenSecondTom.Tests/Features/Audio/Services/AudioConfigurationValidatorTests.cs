using FluentAssertions;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Features.Audio.Constants;
using Xunit;
using TenSecondTom.Features.Audio;


namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class AudioConfigurationValidatorTests
{
    private readonly AudioConfigurationValidator _validator = new();

    [Fact]
    public void IsAudioConfigured_WithLocalProviderAndNoFallback_ReturnsTrue()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = false
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithCloudProviderAndApiKey_ReturnsTrue()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.OpenAI,
            SttApiKey = "sk-test-key",
            SttFallbackEnabled = false
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithCloudProviderAndNoApiKey_ReturnsFalse()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.OpenAI,
            SttApiKey = null,
            SttFallbackEnabled = false
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAudioConfigured_WithFallbackEnabledAndValidFallback_ReturnsTrue()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = true,
            SttFallbackProvider = SttProviders.OpenAI,
            SttFallbackApiKey = "sk-fallback-key"
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithFallbackEnabledButNoProvider_ReturnsFalse()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = true,
            SttFallbackProvider = null,
            SttFallbackApiKey = "sk-fallback-key"
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAudioConfigured_WithFallbackEnabledButNoApiKey_ReturnsFalse()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = true,
            SttFallbackProvider = SttProviders.OpenAI,
            SttFallbackApiKey = null
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetMissingConfiguration_WithValidConfig_ReturnsEmpty()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = false
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void GetMissingConfiguration_WithMissingCloudApiKey_ReturnsCorrectItem()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.OpenAI,
            SttApiKey = null,
            SttFallbackEnabled = false
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(1);
        missing[0].Should().Contain("STT API Key");
        missing[0].Should().Contain(SttProviders.OpenAI);
    }

    [Fact]
    public void GetMissingConfiguration_WithMissingFallbackProvider_ReturnsCorrectItem()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = true,
            SttFallbackProvider = null
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(1);
        missing[0].Should().Contain("STT Fallback Provider");
    }

    [Fact]
    public void GetMissingConfiguration_WithMissingFallbackApiKey_ReturnsCorrectItem()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = true,
            SttFallbackProvider = SttProviders.OpenAI,
            SttFallbackApiKey = null
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(1);
        missing[0].Should().Contain("STT Fallback API Key");
        missing[0].Should().Contain(SttProviders.OpenAI);
    }

    [Fact]
    public void GetMissingConfiguration_WithMultipleMissingItems_ReturnsAllItems()
    {
        // Arrange
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.OpenAI,
            SttApiKey = null,  // Missing primary API key
            SttFallbackEnabled = true,
            SttFallbackProvider = null,  // Missing fallback provider
            SttFallbackApiKey = null
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(2);
        missing.Should().Contain(item => item.Contains("STT API Key"));
        missing.Should().Contain(item => item.Contains("STT Fallback Provider"));
    }

    [Fact]
    public void IsAudioConfigured_WithLocalFallbackToLocal_ReturnsTrue()
    {
        // Arrange - local primary, local fallback (no API keys needed)
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = true,
            SttFallbackProvider = SttProviders.WhisperCpp,
            SttFallbackApiKey = null
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithCloudPrimaryCloudFallback_RequiresBothKeys()
    {
        // Arrange - both cloud providers, both need keys
        var config = new AudioConfiguration
        {
            SttProvider = SttProviders.OpenAI,
            SttApiKey = "sk-primary-key",
            SttFallbackEnabled = true,
            SttFallbackProvider = SttProviders.OpenAI,
            SttFallbackApiKey = "sk-fallback-key"
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }
}
