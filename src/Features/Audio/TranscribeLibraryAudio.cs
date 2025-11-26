using System.IO;
using System.IO.Abstractions;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Transcribes an existing audio file (note, recording, or external) into the recording library.
/// </summary>
public static class TranscribeLibraryAudio
{
    /// <summary>
    /// Command for transcribing an existing audio file and storing the transcript alongside recordings.
    /// </summary>
    public sealed record Command : IRequest<Result<TranscribedLibraryRecording>>
    {
        /// <summary>
        /// Absolute path to the source audio file.
        /// </summary>
        public required string AudioFilePath { get; init; }

        /// <summary>
        /// Base filename (without extension) for the destination recording entry.
        /// </summary>
        public required string RecordingBaseName { get; init; }

        /// <summary>
        /// Configured transcribe options (STT provider/model selection).
        /// </summary>
        public required TranscribeOptions TranscribeConfig { get; init; }

        /// <summary>
        /// Indicates where the audio came from (note/recording/external) for logging and metrics.
        /// </summary>
        public required AudioLibraryScope Source { get; init; }

        /// <summary>
        /// Allows overwriting existing audio/transcript pairs when true.
        /// </summary>
        public bool ForceOverwrite { get; init; }

        /// <summary>
        /// Optional transcription result to reuse instead of running STT again.
        /// </summary>
        public TranscriptionResult? ExistingTranscription { get; init; }
    }

    /// <summary>
    /// Result payload describing the stored transcription.
    /// </summary>
    public sealed record TranscribedLibraryRecording
    {
        public required string RecordingBaseName { get; init; }
        public required string AudioFilePath { get; init; }
        public required string TranscriptFilePath { get; init; }
        public required TranscriptionResult Transcription { get; init; }
    }

    /// <summary>
    /// Handler wiring together transcription, copying, and markdown generation.
    /// </summary>
    public sealed class Handler(
        IMediator mediator,
        IOptions<StorageOptions> storageOptions,
        IFileSystem fileSystem,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<TranscribedLibraryRecording>>
    {
        private readonly IMediator _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        private readonly ILogger<Handler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly string _storageRoot = (storageOptions ?? throw new ArgumentNullException(nameof(storageOptions)))
            .Value
            .GetEffectiveStorageDirectory();

        public async Task<Result<TranscribedLibraryRecording>> Handle(Command request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.AudioFilePath))
            {
                return Result<TranscribedLibraryRecording>.Failure("Audio file path is required.");
            }

            if (string.IsNullOrWhiteSpace(request.RecordingBaseName))
            {
                return Result<TranscribedLibraryRecording>.Failure("Recording name is required.");
            }

            if (request.RecordingBaseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return Result<TranscribedLibraryRecording>.Failure("Recording name contains invalid characters.");
            }

            if (!_fileSystem.File.Exists(request.AudioFilePath))
            {
                return Result<TranscribedLibraryRecording>.Failure(
                    $"Audio file not found: {request.AudioFilePath}");
            }

            var libraryDirectoryName = GetLibraryDirectoryName(request.Source);
            var recordingDirectory = _fileSystem.Path.Combine(_storageRoot, libraryDirectoryName);
            EnsureDirectory(recordingDirectory);

            // Preserve the source file extension (WAV for new recordings, MP3 for legacy)
            var sourceExtension = _fileSystem.Path.GetExtension(request.AudioFilePath);
            var audioDestination = _fileSystem.Path.Combine(recordingDirectory, $"{request.RecordingBaseName}{sourceExtension}");
            var transcriptDestination = _fileSystem.Path.Combine(recordingDirectory, $"{request.RecordingBaseName}.md");

            var sameFile = PathsEqual(request.AudioFilePath, audioDestination);

            if (!sameFile && _fileSystem.File.Exists(audioDestination) && !request.ForceOverwrite)
            {
                return Result<TranscribedLibraryRecording>.Failure(
                    $"A recording named '{request.RecordingBaseName}' already exists. Use --force to overwrite.");
            }

            if (_fileSystem.File.Exists(transcriptDestination) && !request.ForceOverwrite)
            {
                return Result<TranscribedLibraryRecording>.Failure(
                    $"Transcript already exists for '{request.RecordingBaseName}'. Use --force to overwrite.");
            }

            try
            {
                if (!sameFile)
                {
                    _fileSystem.File.Copy(request.AudioFilePath, audioDestination, overwrite: request.ForceOverwrite);
                    _logger.LogInformation(
                        "Copied audio {Source} -> {Destination} for scope {Scope}",
                        request.AudioFilePath,
                        audioDestination,
                        request.Source);
                }

                Result<TranscriptionResult> transcriptionResult;
                if (request.ExistingTranscription is not null)
                {
                    transcriptionResult = Result<TranscriptionResult>.Success(request.ExistingTranscription);
                    _logger.LogDebug("Using provided transcription result for {Recording}", request.RecordingBaseName);
                }
                else
                {
                    var transcribeCommand = new TranscribeAudio.Command
                    {
                        AudioFilePath = audioDestination,
                        TranscribeConfig = request.TranscribeConfig
                    };

                    transcriptionResult = await _mediator.Send(transcribeCommand, cancellationToken);
                }
                if (!transcriptionResult.IsSuccess || transcriptionResult.Value is null)
                {
                    if (!sameFile && _fileSystem.File.Exists(audioDestination))
                    {
                        _fileSystem.File.Delete(audioDestination);
                    }

                    return Result<TranscribedLibraryRecording>.Failure(
                        transcriptionResult.Error ?? "Transcription failed.");
                }

                var transcriptContent = BuildTranscriptContent(
                    request.RecordingBaseName,
                    audioDestination,
                    transcriptionResult.Value);

                await _fileSystem.File.WriteAllTextAsync(
                    transcriptDestination,
                    transcriptContent,
                    cancellationToken);

                var payload = new TranscribedLibraryRecording
                {
                    RecordingBaseName = request.RecordingBaseName,
                    AudioFilePath = audioDestination,
                    TranscriptFilePath = transcriptDestination,
                    Transcription = transcriptionResult.Value
                };

                _logger.LogInformation(
                    "Transcription stored for {Recording} ({Engine})",
                    request.RecordingBaseName,
                    transcriptionResult.Value.SttEngine);

                return Result<TranscribedLibraryRecording>.Success(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to transcribe library audio {Recording}", request.RecordingBaseName);

                // Attempt to clean up partially written files when overwrite isn't intended
                if (_fileSystem.File.Exists(transcriptDestination))
                {
                    _fileSystem.File.Delete(transcriptDestination);
                }

                return Result<TranscribedLibraryRecording>.Failure(
                    $"Failed to transcribe recording: {ex.Message}");
            }
        }

        private static string GetLibraryDirectoryName(AudioLibraryScope scope)
        {
            return scope switch
            {
                AudioLibraryScope.Note => DirectoryNames.Note,
                AudioLibraryScope.Today => DirectoryNames.Today,
                _ => DirectoryNames.Recording
            };
        }

        private void EnsureDirectory(string directory)
        {
            if (_fileSystem.Directory.Exists(directory))
            {
                return;
            }

            _fileSystem.Directory.CreateDirectory(directory);
            _logger.LogDebug("Created recording directory at {Directory}", directory);
        }

        private bool PathsEqual(string pathA, string pathB)
        {
            var fullA = _fileSystem.Path.GetFullPath(pathA);
            var fullB = _fileSystem.Path.GetFullPath(pathB);
            return string.Equals(fullA, fullB, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildTranscriptContent(
            string recordingBaseName,
            string audioPath,
            TranscriptionResult transcription)
        {
            var sb = new StringBuilder();
            var timestamp = transcription.TranscribedAt == default
                ? DateTimeOffset.UtcNow
                : transcription.TranscribedAt;

            sb.AppendLine("---");
            sb.AppendLine($"recording-id: {recordingBaseName}");
            sb.AppendLine($"timestamp: {timestamp:O}");
            sb.AppendLine($"audio-path: {audioPath}");
            sb.AppendLine($"stt-engine: {transcription.SttEngine}");
            if (!string.IsNullOrWhiteSpace(transcription.SttModel))
            {
                sb.AppendLine($"stt-model: {transcription.SttModel}");
            }
            sb.AppendLine($"word-count: {transcription.WordCount}");
            sb.AppendLine($"processing-duration-seconds: {transcription.ProcessingDuration.TotalSeconds:F2}");
            if (!string.IsNullOrWhiteSpace(transcription.Language))
            {
                sb.AppendLine($"language: {transcription.Language}");
            }
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine(transcription.TranscriptText);

            return sb.ToString();
        }
    }
}
