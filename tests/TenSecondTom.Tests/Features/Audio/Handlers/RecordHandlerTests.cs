using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Features.Audio;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using Xunit;
using RecordFeature = TenSecondTom.Features.Audio.Record;

namespace TenSecondTom.Tests.Features.Audio.Handlers;

/// <summary>
/// Unit tests for <see cref="RecordFeature.Handler"/> ensuring recordings are persisted via TranscribeLibraryAudio.
/// </summary>
public sealed class RecordHandlerTests
{
    private static readonly AudioOptions DefaultAudioOptions = new()
    {
        SttProvider = SttProviders.WhisperCpp,
        SttModel = "/models/ggml-base.en.bin"
    };

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IAudioPreprocessor> _audioPreprocessor = new();
    private readonly Mock<ILogger<RecordFeature.Handler>> _logger = new();

    [Fact]
    public async Task Handle_WithSuccessfulFlow_UsesMarkdownTranscriptAndLibraryHandler()
    {
        // Arrange
        var storageRoot = CreateTempDirectory();
        try
        {
            var recording = CreateAudioRecording();
            SetupRecordMediator(recording);
            SetupPreprocessor(recording);

            TranscribeLibraryAudio.Command? capturedCommand = null;

            _mediator
                .Setup(m => m.Send(It.IsAny<TranscribeLibraryAudio.Command>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TranscribeLibraryAudio.Command command, CancellationToken _) =>
                {
                    capturedCommand = command;

                    var recordingDirectory = Path.Combine(storageRoot, DirectoryNames.Recording);
                    Directory.CreateDirectory(recordingDirectory);

                    var audioFilePath = Path.Combine(recordingDirectory, $"{command.RecordingBaseName}.wav");
                    if (!File.Exists(audioFilePath))
                    {
                        File.WriteAllBytes(audioFilePath, new byte[256]);
                    }

                    var payload = new TranscribeLibraryAudio.TranscribedLibraryRecording
                    {
                        RecordingBaseName = command.RecordingBaseName,
                        AudioFilePath = audioFilePath,
                        TranscriptFilePath = Path.Combine(storageRoot, DirectoryNames.Recording, $"{command.RecordingBaseName}.md"),
                        Transcription = CreateTranscriptionResult()
                    };

                    return Result<TranscribeLibraryAudio.TranscribedLibraryRecording>.Success(payload);
                });

            var handler = CreateHandler(storageRoot);
            var command = new RecordFeature.Command
            {
                AudioConfig = DefaultAudioOptions,
                MaxDurationSeconds = 10
            };

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value!.TranscriptionFilePath.Should().EndWith(".md");

            capturedCommand.Should().NotBeNull();
            capturedCommand!.AudioFilePath.Should().Be(recording.FilePath);
            capturedCommand.Source.Should().Be(AudioLibraryScope.Recording);
            capturedCommand.ForceOverwrite.Should().BeFalse();

            var todayPrefix = DateTimeOffset.UtcNow.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
            capturedCommand.RecordingBaseName.Should().StartWith(todayPrefix);

            _mediator.Verify(m => m.Send(It.IsAny<TranscribeLibraryAudio.Command>(), It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            TryDeleteDirectory(storageRoot);
        }
    }

    private RecordFeature.Handler CreateHandler(string storageRoot)
    {
        var storageOptions = Options.Create(new StorageOptions
        {
            RootDirectory = storageRoot
        });

        var audioOptionsMonitor = new Mock<IOptionsMonitor<AudioOptions>>();
        audioOptionsMonitor
            .Setup(m => m.CurrentValue)
            .Returns(new AudioOptions
            {
                Timeouts = new RecordingTimeoutsOptions
                {
                    RecordSeconds = 120
                }
            });

        var fileSystem = new FileSystem();

        return new RecordFeature.Handler(
            _mediator.Object,
            _audioPreprocessor.Object,
            storageOptions,
            audioOptionsMonitor.Object,
            fileSystem,
            _logger.Object);
    }

    private static AudioRecording CreateAudioRecording()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"tom-test-recording-{Guid.NewGuid()}.wav");
        return new AudioRecording
        {
            Filename = Path.GetFileName(tempPath),
            FilePath = tempPath,
            Duration = TimeSpan.FromSeconds(8),
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = DateTimeOffset.UtcNow,
            FileSizeBytes = 4096
        };
    }

    private static TranscriptionResult CreateTranscriptionResult()
    {
        return new TranscriptionResult
        {
            AudioReference = "audio",
            TranscriptText = "sample transcript",
            SttEngine = SttEngine.Local,
            SttModel = "ggml-base.en",
            ProcessingDuration = TimeSpan.FromSeconds(2),
            TranscribedAt = DateTimeOffset.UtcNow,
            WordCount = 3
        };
    }

    private void SetupRecordMediator(AudioRecording recording)
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<RecordAudio.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AudioRecording>.Success(recording));
    }

    private void SetupPreprocessor(AudioRecording recording)
    {
        var preprocessingResult = new PreprocessingResult
        {
            ProcessedFilePath = recording.FilePath,
            OriginalSizeBytes = recording.FileSizeBytes,
            ProcessedSizeBytes = recording.FileSizeBytes,
            OriginalDuration = recording.Duration,
            ProcessedDuration = recording.Duration,
            ProcessingTime = TimeSpan.FromSeconds(1)
        };

        _audioPreprocessor
            .Setup(p => p.PreprocessAsync(recording.FilePath, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PreprocessingResult>.Success(preprocessingResult));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tst-record-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests
        }
    }
}
