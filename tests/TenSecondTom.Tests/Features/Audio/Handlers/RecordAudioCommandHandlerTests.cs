using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Handlers;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Audio.Handlers;

/// <summary>
/// Tests for <see cref="RecordAudioCommandHandler"/>.
/// Validates audio recording orchestration and AudioRecording creation.
/// </summary>
public sealed class RecordAudioCommandHandlerTests
{
    private readonly Mock<IAudioRecorder> _mockRecorder;
    private readonly Mock<ILogger<RecordAudioCommandHandler>> _mockLogger;

    public RecordAudioCommandHandlerTests()
    {
        _mockRecorder = new Mock<IAudioRecorder>();
        _mockLogger = new Mock<ILogger<RecordAudioCommandHandler>>();
    }

    [Fact]
    public async Task Handle_WithValidOutputPath_ReturnsAudioRecording()
    {
        // Arrange
        var outputPath = "/path/to/recording.wav";
        var command = new RecordAudioCommand
        {
            OutputPath = outputPath
        };

        var expectedRecording = new AudioRecording
        {
            Filename = "recording.wav",
            FilePath = outputPath,
            Duration = TimeSpan.FromSeconds(10),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 320000
        };

        _mockRecorder
            .Setup(r => r.RecordAsync(outputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Success(expectedRecording));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedRecording);
        result.Value.FilePath.Should().Be(outputPath);
        result.Value.Duration.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Handle_CallsAudioRecorderWithCorrectPath()
    {
        // Arrange
        var outputPath = "/path/to/recording.wav";
        var command = new RecordAudioCommand { OutputPath = outputPath };

        var recording = new AudioRecording
        {
            Filename = "recording.wav",
            FilePath = outputPath,
            Duration = TimeSpan.FromSeconds(5),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 160000
        };

        _mockRecorder
            .Setup(r => r.RecordAsync(outputPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Success(recording));

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockRecorder.Verify(
            r => r.RecordAsync(outputPath, It.IsAny<CancellationToken>()),
            Times.Once,
            "Should call AudioRecorder.RecordAsync with the specified output path");
    }

    [Fact]
    public async Task Handle_WhenRecordingFails_ReturnsFailure()
    {
        // Arrange
        var command = new RecordAudioCommand { OutputPath = "/path/to/recording.wav" };

        _mockRecorder
            .Setup(r => r.RecordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Failure("FFmpeg not found"));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("FFmpeg not found");
    }

    [Fact]
    public async Task Handle_LogsRecordingStart()
    {
        // Arrange
        var outputPath = "/path/to/recording.wav";
        var command = new RecordAudioCommand { OutputPath = outputPath };

        var recording = new AudioRecording
        {
            Filename = "recording.wav",
            FilePath = outputPath,
            Duration = TimeSpan.FromSeconds(5),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 160000
        };

        _mockRecorder
            .Setup(r => r.RecordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Success(recording));

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.Method.Name == "Log" &&
            i.ToString()!.Contains("Recording", StringComparison.OrdinalIgnoreCase),
            "Should log recording activity");
    }

    [Fact]
    public async Task Handle_LogsRecordingCompletion()
    {
        // Arrange
        var outputPath = "/path/to/recording.wav";
        var command = new RecordAudioCommand { OutputPath = outputPath };

        var recording = new AudioRecording
        {
            Filename = "recording.wav",
            FilePath = outputPath,
            Duration = TimeSpan.FromSeconds(10),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 320000
        };

        _mockRecorder
            .Setup(r => r.RecordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Success(recording));

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Invocations.Should().Contain(i =>
            i.Method.Name == "Log" &&
            i.ToString()!.Contains("duration", StringComparison.OrdinalIgnoreCase),
            "Should log recording duration");
    }

    [Fact]
    public async Task Handle_RespectsCancellationToken()
    {
        // Arrange
        var command = new RecordAudioCommand { OutputPath = "/path/to/recording.wav" };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockRecorder
            .Setup(r => r.RecordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_WithNullOutputPath_ThrowsException()
    {
        // Arrange
        var command = new RecordAudioCommand { OutputPath = null! };
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*output path*");
    }

    [Fact]
    public async Task Handle_WithEmptyOutputPath_ThrowsException()
    {
        // Arrange
        var command = new RecordAudioCommand { OutputPath = string.Empty };
        var handler = CreateHandler();

        // Act
        var act = () => handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*output path*");
    }

    [Fact]
    public async Task Handle_ValidatesRecordingMetadata()
    {
        // Arrange
        var outputPath = "/path/to/recording.wav";
        var command = new RecordAudioCommand { OutputPath = outputPath };

        var recording = new AudioRecording
        {
            Filename = "recording.wav",
            FilePath = outputPath,
            Duration = TimeSpan.FromSeconds(10),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 320000
        };

        _mockRecorder
            .Setup(r => r.RecordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Success(recording));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.IsValid().Should().BeTrue("Recording metadata should be valid");
        result.Value.IsValidForWhisperCpp().Should().BeTrue(
            "Recording should meet whisper.cpp requirements");
    }

    private RecordAudioCommandHandler CreateHandler()
    {
        return new RecordAudioCommandHandler(_mockRecorder.Object, _mockLogger.Object);
    }
}
