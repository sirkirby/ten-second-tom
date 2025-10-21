using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Features.Audio.Services;

namespace TenSecondTom.Tests.Features.Audio.Services;

/// <summary>
/// Tests for <see cref="ISttProviderFactory"/> implementation.
/// Validates STT engine auto-selection, fallback logic, and explicit selection strategies.
/// </summary>
public sealed class SttProviderFactoryTests
{
    private readonly Mock<ISttProvider> _mockLocalProvider;
    private readonly Mock<ISttProvider> _mockOpenAiProvider;
    private readonly Mock<ILogger<SttProviderFactory>> _mockLogger;

    public SttProviderFactoryTests()
    {
        _mockLocalProvider = new Mock<ISttProvider>();
        _mockLocalProvider.Setup(p => p.Engine).Returns(SttEngine.Local);

        _mockOpenAiProvider = new Mock<ISttProvider>();
        _mockOpenAiProvider.Setup(p => p.Engine).Returns(SttEngine.OpenAI);

        _mockLogger = new Mock<ILogger<SttProviderFactory>>();
    }

    [Fact]
    public async Task GetProviderAsync_WithAutoAndLocalAvailable_ReturnsLocalProvider()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();

        // Act
        var provider = await factory.GetProviderAsync(SttSelection.Auto);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.Local);
        _mockLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithAutoAndLocalUnavailable_FallsBackToOpenAI()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockOpenAiProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();

        // Act
        var provider = await factory.GetProviderAsync(SttSelection.Auto);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.OpenAI);
        _mockLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithAutoAndBothUnavailable_ReturnsNull()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockOpenAiProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var factory = CreateFactory();

        // Act
        var provider = await factory.GetProviderAsync(SttSelection.Auto);

        // Assert
        provider.Should().BeNull();
        _mockLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_WithLocalSelectionAndAvailable_ReturnsLocalProvider()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();

        // Act
        var provider = await factory.GetProviderAsync(SttSelection.Local);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.Local);
        _mockLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProviderAsync_WithLocalSelectionAndUnavailable_ReturnsNull()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var factory = CreateFactory();

        // Act
        var provider = await factory.GetProviderAsync(SttSelection.Local);

        // Assert
        provider.Should().BeNull();
        _mockLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Never,
            "Should not fallback to OpenAI when local is explicitly requested");
    }

    [Fact]
    public async Task GetProviderAsync_WithOpenAISelection_ReturnsOpenAIProviderWithoutCheckingLocal()
    {
        // Arrange
        _mockOpenAiProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();

        // Act
        var provider = await factory.GetProviderAsync(SttSelection.OpenAI);

        // Assert
        provider.Should().NotBeNull();
        provider!.Engine.Should().Be(SttEngine.OpenAI);
        _mockLocalProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Never,
            "Should skip local provider when OpenAI is explicitly requested");
        _mockOpenAiProvider.Verify(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProviderAsync_LogsAvailabilityChecks()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();

        // Act
        await factory.GetProviderAsync(SttSelection.Auto);

        // Assert
        // Verify structured logging occurred (implementation should log engine selection)
        _mockLogger.Invocations.Should().NotBeEmpty("Factory should log provider selection decisions");
    }

    [Fact]
    public async Task GetProviderAsync_LogsFallbackWhenLocalUnavailable()
    {
        // Arrange
        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockOpenAiProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var factory = CreateFactory();

        // Act
        await factory.GetProviderAsync(SttSelection.Auto);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.Method.Name == "Log" && i.ToString()!.Contains("fallback", StringComparison.OrdinalIgnoreCase),
            "Should log fallback from local to OpenAI");
    }

    [Fact]
    public void GetProvider_WithLocalEngine_ReturnsLocalProvider()
    {
        // Arrange
        var factory = CreateFactory();

        // Act
        var provider = factory.GetProvider(SttEngine.Local);

        // Assert
        provider.Should().NotBeNull();
        provider.Engine.Should().Be(SttEngine.Local);
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
    }

    [Fact]
    public async Task GetProviderAsync_RespectsCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockLocalProvider
            .Setup(p => p.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var factory = CreateFactory();

        // Act
        var act = () => factory.GetProviderAsync(SttSelection.Auto, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private SttProviderFactory CreateFactory()
    {
        return new SttProviderFactory(
            _mockLocalProvider.Object,
            _mockOpenAiProvider.Object,
            _mockLogger.Object);
    }
}
