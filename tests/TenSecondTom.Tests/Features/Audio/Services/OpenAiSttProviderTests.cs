using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class OpenAiSttProviderTests
{
    private readonly Mock<ILogger<OpenAiSttProvider>> _mockLogger = new();

    [Fact]
    public void Engine_ReturnsOpenAI()
    {
        var provider = CreateProvider();

        provider.Engine.Should().Be(SttEngine.OpenAI);
    }

    private OpenAiSttProvider CreateProvider(AudioOptions? audioConfig = null)
    {
        audioConfig ??= new AudioOptions
        {
            SttProvider = SttProviders.OpenAI,
            SttModel = "whisper-1",
            SttApiKey = "test-api-key"
        };

        return new OpenAiSttProvider(Options.Create(audioConfig), _mockLogger.Object);
    }
}

