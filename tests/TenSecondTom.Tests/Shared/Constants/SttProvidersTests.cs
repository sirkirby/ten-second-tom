using FluentAssertions;
using TenSecondTom.Shared.Constants;
using Xunit;

namespace TenSecondTom.Tests.Shared.Constants;

/// <summary>
/// Unit tests for SttProviders constants.
/// Tests that all STT provider constants are properly defined.
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
    public void WhisperCppDefaultSTTModel_ShouldBeCorrect()
    {
        // Assert
        SttProviders.WhisperCppDefaultSTTModel.Should().Be("ggml-base.en.bin");
    }

    [Fact]
    public void OpenAIDefaultSTTModel_ShouldBeCorrect()
    {
        // Assert
        SttProviders.OpenAIDefaultSTTModel.Should().Be("whisper-1");
    }
}
