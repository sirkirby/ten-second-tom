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

    private OpenAiSttProvider CreateProvider(TranscribeOptions? transcribeConfig = null)
    {
        transcribeConfig ??= new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI,
            Providers = new Dictionary<string, Dictionary<string, string>>
            {
                [SttProviders.OpenAI] = new()
                {
                    ["Model"] = "whisper-1",
                    ["ApiKey"] = "test-api-key"
                }
            }
        };

        return new OpenAiSttProvider(Options.Create(transcribeConfig), _mockLogger.Object);
    }
}

