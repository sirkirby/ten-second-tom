using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Features.Audio.Services;

public sealed class LocalWhisperSttProviderTests
{
    private readonly Mock<ILogger<LocalWhisperSttProvider>> _mockLogger = new();
    private readonly AudioConfiguration _config = new()
    {
        SttBinaryPath = "whisper-cpp",
        SttModel = "/path/to/ggml-base.en.bin"
    };

    [Fact]
    public void Engine_ReturnsLocal()
    {
        var provider = CreateProvider();

        provider.Engine.Should().Be(SttEngine.Local);
    }

    private LocalWhisperSttProvider CreateProvider(AudioConfiguration? config = null)
    {
        config ??= _config;
        return new LocalWhisperSttProvider(Options.Create(config), _mockLogger.Object);
    }
}

