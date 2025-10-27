using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Handlers;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Results;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Tests.Features.Audio.Handlers;

/// <summary>
/// Tests for <see cref="TranscribeAudioCommandHandler"/>.
/// Validates STT provider selection, transcription orchestration, and fallback logic.
/// </summary>
public sealed class TranscribeAudioCommandHandlerTests
{
    private readonly Mock<ISttProviderFactory> _mockFactory;
    private readonly Mock<ISttProvider> _mockLocalProvider;
    private readonly Mock<ISttProvider> _mockOpenAiProvider;
    private readonly Mock<IOptions<AudioConfiguration>> _mockAudioConfig;
    private readonly Mock<ILogger<TranscribeAudioCommandHandler>> _mockLogger;

    public TranscribeAudioCommandHandlerTests()
    {
        _mockFactory = new Mock<ISttProviderFactory>();
        _mockLocalProvider = new Mock<ISttProvider>();
        _mockOpenAiProvider = new Mock<ISttProvider>();
        _mockAudioConfig = new Mock<IOptions<AudioConfiguration>>();
        _mockLogger = new Mock<ILogger<TranscribeAudioCommandHandler>>();

        _mockLocalProvider.Setup(p => p.Engine).Returns(SttEngine.Local);
        _mockOpenAiProvider.Setup(p => p.Engine).Returns(SttEngine.OpenAI);

        // Setup default audio configuration
        _mockAudioConfig.Setup(c => c.Value).Returns(CreateDefaultAudioConfiguration());
    }

    [Fact]
    public async Task Handle_WithAutoSelection_UsesFactory()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var transcription = CreateSampleTranscription(audioPath, SttEngine.Local);

        _mockFactory
            .Setup(f => f.GetProviderAsync(
                It.Is<AudioConfiguration>(c => c.SttFallbackEnabled == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SttEngine.Should().Be(SttEngine.Local);
        _mockFactory.Verify(
            f => f.GetProviderAsync(
                It.Is<AudioConfiguration>(c => c.SttFallbackEnabled == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithLocalSelection_UsesLocalProvider()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig()
        };

        var transcription = CreateSampleTranscription(audioPath, SttEngine.Local);

        _mockFactory
            .Setup(f => f.GetProviderAsync(
                It.Is<AudioConfiguration>(c => c.SttFallbackEnabled == false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SttEngine.Should().Be(SttEngine.Local);
    }

    [Fact]
    public async Task Handle_WithOpenAISelection_UsesOpenAIProvider()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(sttProvider: SttProviders.OpenAI)
        };

        var transcription = CreateSampleTranscription(audioPath, SttEngine.OpenAI);

        _mockFactory
            .Setup(f => f.GetProviderAsync(
                It.Is<AudioConfiguration>(c => c.SttProvider == SttProviders.OpenAI),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockOpenAiProvider.Object);

        _mockOpenAiProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SttEngine.Should().Be(SttEngine.OpenAI);
    }

    [Fact]
    public async Task Handle_WhenProviderUnavailable_ReturnsFailure()
    {
        // Arrange
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = "/path/to/audio.wav",
            AudioConfig = CreateTestAudioConfig()
        };

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ISttProvider?)null);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("provider");
        result.Error.Should().Contain("available");
    }

    [Fact]
    public async Task Handle_WhenTranscriptionFails_ReturnsFailure()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Failure("Transcription error"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Transcription error");
    }

    [Fact]
    public async Task Handle_LogsProviderSelection()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var transcription = CreateSampleTranscription(audioPath, SttEngine.Local);

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.ToString()!.Contains("STT", StringComparison.OrdinalIgnoreCase),
            "Should log STT provider selection");
    }

    [Fact]
    public async Task Handle_LogsTranscriptionMetrics()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var transcription = CreateSampleTranscription(audioPath, SttEngine.Local);

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.ToString()!.Contains("duration", StringComparison.OrdinalIgnoreCase) ||
            i.ToString()!.Contains("word", StringComparison.OrdinalIgnoreCase),
            "Should log transcription metrics (duration, word count)");
    }

    [Fact]
    public async Task Handle_DoesNotLogTranscriptContent()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var transcription = new TranscriptionResult
        {
            AudioReference = audioPath,
            TranscriptText = "This is secret confidential content",
            SttEngine = SttEngine.Local,
            SttModel = "ggml-base.en",
            ProcessingDuration = TimeSpan.FromSeconds(5),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = 5
        };

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().NotContain(i =>
            i.ToString()!.Contains("secret confidential content", StringComparison.OrdinalIgnoreCase),
            "Should NEVER log transcript content for privacy");
    }

    [Fact]
    public async Task Handle_ValidatesTranscriptionResult()
    {
        // Arrange
        var audioPath = "/path/to/audio.wav";
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = audioPath,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var transcription = CreateSampleTranscription(audioPath, SttEngine.Local);

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockLocalProvider.Object);

        _mockLocalProvider
            .Setup(p => p.TranscribeAsync(audioPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.IsValid().Should().BeTrue("Transcription result should be valid");
        result.Value.WordCount.Should().BeGreaterThan(0);
        result.Value.ProcessingDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Handle_RespectsCancellationToken()
    {
        // Arrange
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = "/path/to/audio.wav",
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockFactory
            .Setup(f => f.GetProviderAsync(It.IsAny<AudioConfiguration>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_WithEmptyAudioPath_ThrowsException()
    {
        // Arrange
        var command = new TranscribeAudioCommand
        {
            AudioFilePath = string.Empty,
            AudioConfig = CreateTestAudioConfig(cloudFallbackEnabled: true)
        };

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*audio*path*");
    }

    private TranscribeAudioCommandHandler CreateHandler()
    {
        return new TranscribeAudioCommandHandler(_mockFactory.Object, _mockAudioConfig.Object, _mockLogger.Object);
    }

    private static AudioConfiguration CreateDefaultAudioConfiguration()
    {
        return new AudioConfiguration
        {
            SttProvider = SttProviders.WhisperCpp,
            SttApiKey = null,
            SttFallbackEnabled = false,
            KeepFiles = true,
            Recorder = new RecorderConfiguration(),
            Preprocessing = new PreprocessingConfiguration(),
            Timeouts = new RecordingTimeoutsConfiguration()
        };
    }

    private static AudioConfiguration CreateTestAudioConfig(
        string sttProvider = SttProviders.WhisperCpp,
        string? sttApiKey = null,
        bool cloudFallbackEnabled = false)
    {
        return new AudioConfiguration
        {
            SttProvider = sttProvider,
            SttApiKey = sttApiKey,
            SttFallbackEnabled = cloudFallbackEnabled,
            KeepFiles = false,
            Recorder = new RecorderConfiguration
            {
                FfmpegPath = "ffmpeg",
                InputVolume = 1.0,
                EnableNoiseReduction = true,
                EnableFrequencyFilters = true
            },
            SttBinaryPath = "whisper-cli",
            SttModel = "models/ggml-base.en.bin",
            Preprocessing = new PreprocessingConfiguration
            {
                RemoveSilence = true,
                SilenceThresholdDb = -50,
                MinimumSilenceDurationMs = 500
            },
            Timeouts = new RecordingTimeoutsConfiguration
            {
                TodaySeconds = 300,
                RecordSeconds = 600
            }
        };
    }

    private static TranscriptionResult CreateSampleTranscription(string audioPath, SttEngine engine)
    {
        return new TranscriptionResult
        {
            AudioReference = audioPath,
            TranscriptText = "This is a sample transcription for testing purposes.",
            SttEngine = engine,
            SttModel = engine == SttEngine.Local ? "ggml-base.en" : "whisper-1",
            ProcessingDuration = TimeSpan.FromSeconds(5),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = 9
        };
    }
}
