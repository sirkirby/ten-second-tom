using TenSecondTom.Features.Audio.Constants;
using TenSecondTom.Features.Audio;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Tests.Features.Audio.Services;

/// <summary>
/// Tests for <see cref="ISttProviderFactory"/> implementation.
/// Validates STT provider routing based on configuration.
/// </summary>
public sealed class SttProviderFactoryTests
{
    private readonly Mock<ISttProvider> _mockBuiltInLocalProvider;
    private readonly Mock<ISttProvider> _mockWhisperCppProvider;
    private readonly Mock<ISttProvider> _mockOpenAiProvider;
    private readonly Mock<ILogger<SttProviderFactory>> _mockLogger;

    public SttProviderFactoryTests()
    {
        _mockBuiltInLocalProvider = new Mock<ISttProvider>();
        _mockBuiltInLocalProvider.Setup(p => p.Engine).Returns(SttEngine.Local);

        _mockWhisperCppProvider = new Mock<ISttProvider>();
        _mockWhisperCppProvider.Setup(p => p.Engine).Returns(SttEngine.Local);

        _mockOpenAiProvider = new Mock<ISttProvider>();
        _mockOpenAiProvider.Setup(p => p.Engine).Returns(SttEngine.OpenAI);

        _mockLogger = new Mock<ILogger<SttProviderFactory>>();
    }

    [Fact]
    public async Task GetProviderAsync_WithBuiltInLocalProvider_ReturnsBuiltInLocal()
    {
        // Arrange - built-in local always returns without availability check
        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.BuiltInLocal
        };

        // Act
        var provider = await factory.GetProviderAsync(config);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.Local);
        provider.Should().Be(_mockBuiltInLocalProvider.Object);
        _mockBuiltInLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Never,
            "Built-in local provider should not check availability");
    }

    [Fact]
    public async Task GetProviderAsync_WithWhisperCppAndAvailable_ReturnsWhisperCpp()
    {
        // Arrange
        _mockWhisperCppProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp
        };

        // Act
        var provider = await factory.GetProviderAsync(config);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.Local);
        provider.Should().Be(_mockWhisperCppProvider.Object);
        _mockWhisperCppProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithWhisperCppAndUnavailable_ReturnsNull()
    {
        // Arrange
        _mockWhisperCppProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp
        };

        // Act
        var provider = await factory.GetProviderAsync(config);

        // Assert
        provider.Should().BeNull();
        _mockWhisperCppProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithOpenAiAndAvailable_ReturnsOpenAi()
    {
        // Arrange
        _mockOpenAiProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI
        };

        // Act
        var provider = await factory.GetProviderAsync(config);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.OpenAI);
        provider.Should().Be(_mockOpenAiProvider.Object);
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithOpenAiAndUnavailable_ReturnsNull()
    {
        // Arrange
        _mockOpenAiProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.OpenAI
        };

        // Act
        var provider = await factory.GetProviderAsync(config);

        // Assert
        provider.Should().BeNull();
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithInvalidProvider_ThrowsArgumentException()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = "invalid-provider"
        };

        // Act
        var act = () => factory.GetProviderAsync(config);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid STT provider: invalid-provider*");
    }

    [Fact]
    public async Task GetProviderAsync_LogsProviderSelection()
    {
        // Arrange
        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.BuiltInLocal
        };

        // Act
        await factory.GetProviderAsync(config);

        // Assert
        _mockLogger.Invocations.Should().NotBeEmpty("Factory should log provider selection decisions");
    }

    [Fact]
    public async Task GetProviderAsync_WithWhisperCppUnavailable_LogsWarning()
    {
        // Arrange
        _mockWhisperCppProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp
        };

        // Act
        await factory.GetProviderAsync(config);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.Method.Name == "Log",
            "Should log warning when provider unavailable");
    }

    [Fact]
    public void GetProvider_WithLocalEngine_ReturnsBuiltInLocalProvider()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider(SttEngine.Local);

        // Assert
        provider.Should().NotBeNull();
        provider.Engine.Should().Be(SttEngine.Local);
        provider.Should().Be(_mockBuiltInLocalProvider.Object,
            "GetProvider(Local) should return built-in local provider");
    }

    [Fact]
    public void GetProvider_WithOpenAIEngine_ReturnsOpenAIProvider()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider(SttEngine.OpenAI);

        // Assert
        provider.Should().NotBeNull();
        provider.Engine.Should().Be(SttEngine.OpenAI);
        provider.Should().Be(_mockOpenAiProvider.Object);
    }

    [Fact]
    public async Task GetProviderAsync_RespectsCancellationToken()
    {
        // Arrange
        CancellationToken token;
        using (var cts = new CancellationTokenSource())
        {
            cts.Cancel();
            token = cts.Token;
        }

        _mockWhisperCppProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var factory = CreateFactory();
        var config = new TranscribeOptions
        {
            SttProvider = SttProviders.WhisperCpp
        };

        // Act
        var act = () => factory.GetProviderAsync(config, token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_ValidatesBuiltInLocalProviderEngine()
    {
        // Arrange
        var invalidProvider = new Mock<ISttProvider>();
        invalidProvider.Setup(p => p.Engine).Returns(SttEngine.OpenAI); // Wrong engine type

        // Act
        var act = () => new SttProviderFactory(
            invalidProvider.Object,
            _mockWhisperCppProvider.Object,
            _mockOpenAiProvider.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Built-in local provider must have Engine=Local*");
    }

    [Fact]
    public void Constructor_ValidatesWhisperCppProviderEngine()
    {
        // Arrange
        var invalidProvider = new Mock<ISttProvider>();
        invalidProvider.Setup(p => p.Engine).Returns(SttEngine.OpenAI); // Wrong engine type

        // Act
        var act = () => new SttProviderFactory(
            _mockBuiltInLocalProvider.Object,
            invalidProvider.Object,
            _mockOpenAiProvider.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Whisper.cpp provider must have Engine=Local*");
    }

    [Fact]
    public void Constructor_ValidatesOpenAiProviderEngine()
    {
        // Arrange
        var invalidProvider = new Mock<ISttProvider>();
        invalidProvider.Setup(p => p.Engine).Returns(SttEngine.Local); // Wrong engine type

        // Act
        var act = () => new SttProviderFactory(
            _mockBuiltInLocalProvider.Object,
            _mockWhisperCppProvider.Object,
            invalidProvider.Object,
            _mockLogger.Object);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*OpenAI provider must have Engine=OpenAI*");
    }

    private SttProviderFactory CreateFactory()
    {
        return new SttProviderFactory(
            _mockBuiltInLocalProvider.Object,
            _mockWhisperCppProvider.Object,
            _mockOpenAiProvider.Object,
            _mockLogger.Object);
    }
}
