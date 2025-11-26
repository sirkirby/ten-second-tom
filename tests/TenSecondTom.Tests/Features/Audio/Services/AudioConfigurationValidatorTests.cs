using FluentAssertions;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class AudioConfigurationValidatorTests
{
    private readonly AudioConfigurationValidator _validator = new();

    [Fact]
    public void IsAudioConfigured_WithBuiltInLocalProviderAndModel_ReturnsTrue()
    {
        // Arrange - built-in local requires a model
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.BuiltInLocal,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.BuiltInLocal] = new Dictionary<string, string>
                {
                    ["Model"] = "whisper-large-v3-turbo"
                }
            }
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithBuiltInLocalProviderWithoutModel_ReturnsFalse()
    {
        // Arrange - built-in local without model should fail
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.BuiltInLocal
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAudioConfigured_WithWhisperCppProviderAndBinaryAndModel_ReturnsTrue()
    {
        // Arrange - whisper.cpp requires binary path and model
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.WhisperCpp] = new Dictionary<string, string>
                {
                    ["BinaryPath"] = "/usr/local/bin/whisper",
                    ["Model"] = "ggml-base.en.bin"
                }
            }
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithWhisperCppProviderWithoutBinary_ReturnsFalse()
    {
        // Arrange - whisper.cpp without binary path should fail
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAudioConfigured_WithOpenAiProviderAndApiKeyAndModel_ReturnsTrue()
    {
        // Arrange - OpenAI requires API key and model
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.OpenAI] = new Dictionary<string, string>
                {
                    ["ApiKey"] = "sk-test-key",
                    ["Model"] = "whisper-1"
                }
            }
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAudioConfigured_WithOpenAiProviderAndNoApiKey_ReturnsFalse()
    {
        // Arrange - OpenAI without API key should fail
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAudioConfigured_WithOpenAiProviderAndEmptyApiKey_ReturnsFalse()
    {
        // Arrange - OpenAI with empty API key should fail
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.OpenAI] = new Dictionary<string, string>
                {
                    ["ApiKey"] = ""
                }
            }
        };

        // Act
        var result = _validator.IsAudioConfigured(config);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetMissingConfiguration_WithFullyConfiguredWhisperCpp_ReturnsEmpty()
    {
        // Arrange - whisper.cpp requires binary path and model
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.WhisperCpp] = new Dictionary<string, string>
                {
                    ["BinaryPath"] = "/usr/local/bin/whisper",
                    ["Model"] = "ggml-base.en.bin"
                }
            }
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void GetMissingConfiguration_WithFullyConfiguredBuiltInLocal_ReturnsEmpty()
    {
        // Arrange - built-in local requires a model
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.BuiltInLocal,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.BuiltInLocal] = new Dictionary<string, string>
                {
                    ["Model"] = "whisper-large-v3-turbo"
                }
            }
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void GetMissingConfiguration_WithOpenAiAndNoApiKey_ReturnsConfigItem()
    {
        // Arrange
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(1);
        missing[0].Should().Contain("OpenAI");
    }

    [Fact]
    public void GetMissingConfiguration_WithEmptyProvider_ReturnsProviderItem()
    {
        // Arrange
        var config = new TranscribeOptions
        {
            SttProvider = ""
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(1);
        missing[0].Should().Contain("STT Provider");
    }

    [Fact]
    public void GetMissingConfiguration_WithNullProvider_ReturnsProviderItem()
    {
        // Arrange
        var config = new TranscribeOptions
        {
            SttProvider = null!
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().HaveCount(1);
        missing[0].Should().Contain("STT Provider");
    }

    [Fact]
    public void GetMissingConfiguration_WithOpenAiAndValidApiKeyAndModel_ReturnsEmpty()
    {
        // Arrange - OpenAI requires both API key and model
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.OpenAI] = new Dictionary<string, string>
                {
                    ["ApiKey"] = "sk-test-key",
                    ["Model"] = "whisper-1"
                }
            }
        };

        // Act
        var missing = _validator.GetMissingConfiguration(config);

        // Assert
        missing.Should().BeEmpty();
    }
}
