using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Options;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using MediatR;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Requests;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Records audio, transcribes it, and stores both files in the recording/ directory.
/// This is used by the 'tom record' CLI command.
/// </summary>
public static class Record
{
    /// <summary>
    /// Command to record audio, transcribe it, and store both files in the recording/ directory.
    /// This command is used by the 'tom record' CLI command.
    /// </summary>
    public sealed record Command : IRequest<Result<StoredRecording>>
    {
        /// <summary>
        /// Gets the audio configuration for STT provider selection.
        /// This includes the STT provider, API key, and fallback settings.
        /// </summary>
        public required AudioOptions AudioConfig { get; init; }

        /// <summary>
        /// Gets the maximum recording duration in seconds.
        /// If not specified, uses the configured default from Audio:Timeouts:RecordSeconds.
        /// </summary>
        public int? MaxDurationSeconds { get; init; }

        /// <summary>
        /// Validates the command.
        /// </summary>
        /// <returns>True if valid; otherwise, false.</returns>
        public bool IsValid()
        {
            return MaxDurationSeconds is null or > 0;
        }
    }

    /// <summary>
    /// Handler for Record command.
    /// Orchestrates: record audio → preprocess (optional) → transcribe → save to recording/ directory.
    /// </summary>
    public sealed class Handler(
        IMediator mediator,
        IAudioPreprocessor audioPreprocessor,
        IOptions<StorageOptions> storageOptions,
        IOptionsMonitor<AudioOptions> audioOptions,
        IFileSystem fileSystem,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<StoredRecording>>
    {
        private readonly StorageOptions _storageOptions = storageOptions.Value;
        private readonly IOptionsMonitor<AudioOptions> _audioOptionsMonitor = audioOptions;
        private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

        /// <inheritdoc/>
        public async Task<Result<StoredRecording>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (!request.IsValid())
            {
                return Result<StoredRecording>.Failure("Invalid RecordCommand: MaxDurationSeconds must be positive");
            }

            // Get the effective storage directory using extension method
            var storageBaseDir = _storageOptions.GetEffectiveStorageDirectory();

            if (string.IsNullOrWhiteSpace(storageBaseDir))
            {
                return Result<StoredRecording>.Failure("Storage directory is not configured");
            }

            var recordingDir = _fileSystem.Path.Combine(storageBaseDir, DirectoryNames.Recording);
            if (!_fileSystem.Directory.Exists(recordingDir))
            {
                try
                {
                    _fileSystem.Directory.CreateDirectory(recordingDir);
                    logger.LogDebug("Created recording directory at {RecordingDir}", recordingDir);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to create recording directory at {RecordingDir}", recordingDir);
                    return Result<StoredRecording>.Failure($"Failed to create recording directory: {ex.Message}");
                }
            }

            // Determine timeout: use command-specific value or fall back to config default
            var currentAudioOptions = _audioOptionsMonitor.CurrentValue;
            int? maxDurationSeconds = request.MaxDurationSeconds ?? currentAudioOptions.Timeouts.RecordSeconds;

            logger.LogInformation("Starting record command with STT provider: {Provider}, Target directory: {RecordingDir}, Max duration: {MaxDuration}s",
                request.AudioConfig.SttProvider, recordingDir, maxDurationSeconds);

            // Step 1: Record audio - the recorder will save to a temp file first, then we move it
            var tempAudioPath = _fileSystem.Path.Combine(_fileSystem.Path.GetTempPath(), $"tom-recording-{Guid.NewGuid()}.wav");

            var recordCommand = new RecordAudio.Command
            {
                OutputPath = tempAudioPath,
                MaxDurationSeconds = maxDurationSeconds
            };

            var recordResult = await mediator.Send(recordCommand, cancellationToken);
            if (!recordResult.IsSuccess || recordResult.Value is null)
            {
                logger.LogError("Audio recording failed: {Error}", recordResult.Error);

                // Send error notification (non-blocking, fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var notificationCommand = new SendNotificationRequest(
                            Title: "Recording Failed",
                            Message: $"Audio recording failed: {recordResult.Error ?? "Unknown error"}\n\nPlease check your microphone configuration.",
                            Priority: NotificationPriority.High,
                            TimeoutSeconds: null,
                            Actions: null);

                        var notificationResult = await mediator.Send(notificationCommand, CancellationToken.None);

                        if (!notificationResult.IsSuccess)
                        {
                            logger.LogWarning(
                                "Failed to send recording error notification: {Error}",
                                notificationResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Unexpected error sending recording error notification (non-critical)");
                    }
                }, CancellationToken.None);

                return Result<StoredRecording>.Failure(recordResult.Error ?? "Audio recording failed");
            }

            var recording = recordResult.Value;
            logger.LogInformation("Audio recorded successfully: {Duration}s", recording.Duration.TotalSeconds);

            // Step 1.5: Preprocess audio (remove silence if configured)
            var preprocessResult = await audioPreprocessor.PreprocessAsync(
                recording.FilePath,
                replaceOriginal: true,
                cancellationToken);

            if (!preprocessResult.IsSuccess)
            {
                logger.LogWarning("Audio preprocessing failed: {Error}. Continuing with original audio.", preprocessResult.Error);
                // Continue with original audio - preprocessing failure is not fatal
            }
            else
            {
                var preprocStats = preprocessResult.Value;
                logger.LogInformation(
                    "Audio preprocessing completed: OriginalDuration={OriginalDuration}s, ProcessedDuration={ProcessedDuration}s, " +
                    "Reduction={Reduction:F1}%",
                    preprocStats.OriginalDuration.TotalSeconds,
                    preprocStats.ProcessedDuration.TotalSeconds,
                    preprocStats.DurationReductionPercent);

                // Update recording metadata with preprocessed values
                recording = new AudioRecording
                {
                    Filename = recording.Filename,
                    FilePath = recording.FilePath,
                    Duration = preprocStats.ProcessedDuration,
                    SampleRate = recording.SampleRate,
                    Channels = recording.Channels,
                    Format = recording.Format,
                    Encoding = recording.Encoding,
                    RecordedAt = recording.RecordedAt,
                    FileSizeBytes = preprocStats.ProcessedSizeBytes
                };
            }

            // Step 2: Determine entry number for today and create consistent naming pattern
            var today = DateTimeOffset.UtcNow;
            var existingFiles = _fileSystem.Directory.GetFiles(recordingDir, $"{today:MM-dd-yyyy}_*.wav");
            var nextNumber = existingFiles.Length + 1;
            var filePrefix = $"{today:MM-dd-yyyy}_{nextNumber}";

            var transcribeLibraryCommand = new TranscribeLibraryAudio.Command
            {
                AudioFilePath = recording.FilePath,
                RecordingBaseName = filePrefix,
                AudioConfig = request.AudioConfig,
                Source = AudioLibraryScope.Recording
            };

            Result<TranscribeLibraryAudio.TranscribedLibraryRecording> libraryTranscribeResult;
            try
            {
                libraryTranscribeResult = await mediator.Send(transcribeLibraryCommand, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected failure while transcribing recording entry {RecordingBaseName}", filePrefix);
                CleanupFile(recording.FilePath);
                return Result<StoredRecording>.Failure($"Transcription failed: {ex.Message}");
            }

            if (!libraryTranscribeResult.IsSuccess || libraryTranscribeResult.Value is null)
            {
                logger.LogError("Transcription failed: {Error}", libraryTranscribeResult.Error);

                // Send error notification (non-blocking, fire-and-forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var notificationCommand = new SendNotificationRequest(
                            Title: "Transcription Failed",
                            Message: $"Audio transcription failed: {libraryTranscribeResult.Error ?? "Unknown error"}\n\nPlease check your STT configuration.",
                            Priority: NotificationPriority.High,
                            TimeoutSeconds: null,
                            Actions: null);

                        var notificationResult = await mediator.Send(notificationCommand, CancellationToken.None);

                        if (!notificationResult.IsSuccess)
                        {
                            logger.LogWarning(
                                "Failed to send transcription error notification: {Error}",
                                notificationResult.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Unexpected error sending transcription error notification (non-critical)");
                    }
                }, CancellationToken.None);

                CleanupFile(recording.FilePath);
                return Result<StoredRecording>.Failure(libraryTranscribeResult.Error ?? "Transcription failed");
            }

            var transcribedRecording = libraryTranscribeResult.Value;
            var transcription = transcribedRecording.Transcription;
            logger.LogInformation("Transcription completed: {WordCount} words using {SttEngine} ({SttModel})",
                transcription.WordCount,
                transcription.SttEngine,
                transcription.SttModel);

            // The library handler now owns the audio file; remove the temporary capture
            CleanupFile(recording.FilePath);

            var audioFileInfo = _fileSystem.FileInfo.New(transcribedRecording.AudioFilePath);
            var storedRecording = new StoredRecording
            {
                AudioFilePath = transcribedRecording.AudioFilePath,
                TranscriptionFilePath = transcribedRecording.TranscriptFilePath,
                RecordedAt = today,
                Duration = recording.Duration,
                FileSizeBytes = audioFileInfo.Length,
                TranscriptionWordCount = transcription.WordCount,
                SttEngine = transcription.SttEngine,
                SttModel = transcription.SttModel
            };

            logger.LogInformation("Recording stored successfully: {AudioPath}, {TranscriptionPath}",
                storedRecording.AudioFilePath,
                storedRecording.TranscriptionFilePath);

            // Send success notification and wait for it to complete before returning to REPL
            // This ensures all log output is finished before the REPL prompt appears
            try
            {
                var notificationCommand = new SendNotificationRequest(
                    Title: "Recording Saved",
                    Message: $"Recording saved successfully:\n{Path.GetFileName(storedRecording.AudioFilePath)}",
                    Priority: NotificationPriority.Normal,
                    TimeoutSeconds: null,
                    Actions: null);

                var notificationResult = await mediator.Send(notificationCommand, CancellationToken.None);

                if (!notificationResult.IsSuccess)
                {
                    logger.LogWarning(
                        "Failed to send recording success notification: {Error}",
                        notificationResult.Error);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Unexpected error sending recording success notification (non-critical)");
            }

            return Result<StoredRecording>.Success(storedRecording);
        }

        private void CleanupFile(string filePath)
        {
            try
            {
                if (_fileSystem.File.Exists(filePath))
                {
                    _fileSystem.File.Delete(filePath);
                    logger.LogDebug("Cleaned up file: {FilePath}", filePath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cleanup file: {FilePath}", filePath);
            }
        }
    }
}
