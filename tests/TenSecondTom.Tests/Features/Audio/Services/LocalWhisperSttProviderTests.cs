using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class LocalWhisperSttProviderTests
{
    private readonly Mock<ILogger<LocalWhisperSttProvider>> _mockLogger = new();
    private readonly Mock<IWhisperCppModelManager> _mockModelManager = new();
    private readonly TranscribeOptions _config = new()
    {
        SttProvider = SttProviders.WhisperCpp,
        Providers = new Dictionary<string, Dictionary<string, string>>
        {
            [SttProviders.WhisperCpp] = new()
            {
                ["BinaryPath"] = "whisper-cpp",
                ["Model"] = "/path/to/ggml-base.en.bin"
            }
        }
    };

    [Fact]
    public void Engine_ReturnsLocal()
    {
        var provider = CreateProvider();

        provider.Engine.Should().Be(SttEngine.Local);
    }

    private LocalWhisperSttProvider CreateProvider(TranscribeOptions? config = null)
    {
        config ??= _config;
        return new LocalWhisperSttProvider(
            Options.Create(config),
            _mockModelManager.Object,
            _mockLogger.Object);
    }
}

