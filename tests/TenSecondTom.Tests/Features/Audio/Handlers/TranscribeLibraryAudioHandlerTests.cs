using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Audio.Handlers;

/// <summary>
/// Unit tests for <see cref="TranscribeLibraryAudio.Handler"/> covering note vs recording workflows.
/// </summary>
public sealed class TranscribeLibraryAudioHandlerTests
{
    private static readonly AudioOptions DefaultAudioOptions = new()
    {
        SttProvider = SttProviders.WhisperCpp,
        SttBinaryPath = "whisper-cli",
        SttModel = "/models/ggml-base.en.bin"
    };

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILogger<TranscribeLibraryAudio.Handler>> _logger = new();
    private readonly Mock<IOptions<StorageOptions>> _storageOptions = new();

    public TranscribeLibraryAudioHandlerTests()
    {
        _storageOptions.Setup(o => o.Value).Returns(new StorageOptions
        {
            RootDirectory = "/memory"
        });
    }

    [Fact]
    public async Task Handle_WithMissingAudio_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var handler = CreateHandler(fileSystem);
        var command = CreateCommand(scope: AudioLibraryScope.Note, audioPath: "/memory/note/missing.wav");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Audio file");
    }

    [Fact]
    public async Task Handle_WithExistingTranscriptAndNoForce_ReturnsFailure()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/memory/note");
        fileSystem.AddDirectory("/memory/recording");
        fileSystem.AddFile("/memory/note/10-21-2025_1.wav", new MockFileData(new byte[] { 0x52, 0x49 }));
        fileSystem.AddFile("/memory/note/10-21-2025_1.md", new MockFileData("existing transcript"));

        SetupMediatorSuccess();
        var handler = CreateHandler(fileSystem);
        var command = CreateCommand(scope: AudioLibraryScope.Note);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Handle_WithNoteScope_WritesTranscriptNextToAudio()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/memory/note");
        fileSystem.AddDirectory("/memory/recording");
        fileSystem.AddFile("/memory/note/10-21-2025_1.wav", new MockFileData(new byte[] { 0x52, 0x49, 0x46, 0x46 }));

        SetupMediatorSuccess();
        var handler = CreateHandler(fileSystem);
        var command = CreateCommand(scope: AudioLibraryScope.Note);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fileSystem.FileExists("/memory/note/10-21-2025_1.wav").Should().BeTrue();
        fileSystem.FileExists("/memory/note/10-21-2025_1.md").Should().BeTrue();
        result.Value!.RecordingBaseName.Should().Be("10-21-2025_1");
    }

    [Fact]
    public async Task Handle_WithRecordingScope_DoesNotCopyAudio()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/memory/recording");
        fileSystem.AddFile("/memory/recording/10-21-2025_1.wav", new MockFileData(new byte[] { 0x52, 0x49 }));

        SetupMediatorSuccess();
        var handler = CreateHandler(fileSystem);
        var command = CreateCommand(scope: AudioLibraryScope.Recording, audioPath: "/memory/recording/10-21-2025_1.wav");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        fileSystem.AllFiles.Count().Should().Be(2); // audio + transcript only
    }

    [Fact]
    public async Task Handle_WithExistingTranscription_SkipsMediatorTranscription()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/memory/recording");
        fileSystem.AddFile("/memory/recording/10-21-2025_1.wav", new MockFileData(new byte[] { 0x52, 0x49 }));

        var handler = CreateHandler(fileSystem);
        var transcription = CreateTranscriptionResult();
        var command = CreateCommand(
            scope: AudioLibraryScope.Recording,
            audioPath: "/memory/recording/10-21-2025_1.wav",
            existingTranscription: transcription);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mediator.Verify(m => m.Send(It.IsAny<TranscribeAudio.Command>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private TranscribeLibraryAudio.Handler CreateHandler(MockFileSystem fileSystem)
    {
        return new TranscribeLibraryAudio.Handler(
            _mediator.Object,
            _storageOptions.Object,
            fileSystem,
            _logger.Object);
    }

    private void SetupMediatorSuccess()
    {
        var transcription = CreateTranscriptionResult();

        _mediator
            .Setup(m => m.Send(It.IsAny<TranscribeAudio.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TranscriptionResult>.Success(transcription));
    }

    private static TranscriptionResult CreateTranscriptionResult()
    {
        return new TranscriptionResult
        {
            AudioReference = "audio",
            TranscriptText = "transcribed text",
            SttEngine = SttEngine.Local,
            SttModel = "ggml-base.en",
            ProcessingDuration = TimeSpan.FromSeconds(3),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = 2
        };
    }

    private static TranscribeLibraryAudio.Command CreateCommand(
        AudioLibraryScope scope,
        string audioPath = "/memory/note/10-21-2025_1.wav",
        TranscriptionResult? existingTranscription = null)
    {
        return new TranscribeLibraryAudio.Command
        {
            AudioFilePath = audioPath,
            RecordingBaseName = "10-21-2025_1",
            AudioConfig = DefaultAudioOptions,
            Source = scope,
            ForceOverwrite = false,
            ExistingTranscription = existingTranscription
        };
    }
}
