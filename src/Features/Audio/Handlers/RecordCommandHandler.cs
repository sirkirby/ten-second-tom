using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Handlers;

/// <summary>
/// Handler for RecordCommand.
/// Orchestrates: record audio → transcribe → save to recording/ directory.
/// </summary>
public sealed class RecordCommandHandler(
    RecordAudioCommandHandler recordHandler,
    TranscribeAudioCommandHandler transcribeHandler,
    IConfiguration configuration,
    ILogger<RecordCommandHandler> logger) : IRequestHandler<RecordCommand, Result<StoredRecording>>
{
    private readonly RecordAudioCommandHandler _recordHandler = recordHandler ?? throw new ArgumentNullException(nameof(recordHandler));
    private readonly TranscribeAudioCommandHandler _transcribeHandler = transcribeHandler ?? throw new ArgumentNullException(nameof(transcribeHandler));
    private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    private readonly ILogger<RecordCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task<Result<StoredRecording>> Handle(RecordCommand request, CancellationToken cancellationToken)
    {
        if (!request.IsValid())
        {
            return Result<StoredRecording>.Failure("Invalid RecordCommand: MaxDurationSeconds must be positive");
        }

        // Create recording/ directory if it doesn't exist
        // Use Storage:MemoryDirectory (configured root) or fallback to default
        var memoryDir = _configuration[ConfigurationKeys.StorageMemoryDirectory] ??
                        _configuration[ConfigurationKeys.TenSecondTomMemoryDirectory] ??
                        Path.Combine(".", DirectoryNames.ApplicationRoot);

        if (string.IsNullOrWhiteSpace(memoryDir))
        {
            return Result<StoredRecording>.Failure("Memory directory is not configured");
        }

        var recordingDir = Path.Combine(memoryDir, "recording");
        if (!Directory.Exists(recordingDir))
        {
            try
            {
                Directory.CreateDirectory(recordingDir);
                _logger.LogDebug("Created recording directory at {RecordingDir}", recordingDir);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create recording directory at {RecordingDir}", recordingDir);
                return Result<StoredRecording>.Failure($"Failed to create recording directory: {ex.Message}");
            }
        }

        _logger.LogInformation("Starting record command with STT selection: {SttSelection}, Target directory: {RecordingDir}",
            request.SttSelection, recordingDir);

        // Step 1: Record audio - the recorder will save to a temp file first, then we move it
        var tempAudioPath = Path.Combine(Path.GetTempPath(), $"tom-recording-{Guid.NewGuid()}.wav");

        var recordCommand = new RecordAudioCommand
        {
            OutputPath = tempAudioPath
        };

        var recordResult = await _recordHandler.Handle(recordCommand, cancellationToken);
        if (!recordResult.IsSuccess || recordResult.Value is null)
        {
            _logger.LogError("Audio recording failed: {Error}", recordResult.Error);
            return Result<StoredRecording>.Failure(recordResult.Error ?? "Audio recording failed");
        }

        var recording = recordResult.Value;
        _logger.LogInformation("Audio recorded successfully: {Duration}s", recording.Duration.TotalSeconds);

        // Step 2: Transcribe audio from the temp file
        var transcribeCommand = new TranscribeAudioCommand
        {
            AudioFilePath = recording.FilePath,
            Selection = request.SttSelection
        };

        var transcribeResult = await _transcribeHandler.Handle(transcribeCommand, cancellationToken);
        if (!transcribeResult.IsSuccess || transcribeResult.Value is null)
        {
            _logger.LogError("Transcription failed: {Error}", transcribeResult.Error);

            // Clean up temp audio file
            CleanupFile(recording.FilePath);

            return Result<StoredRecording>.Failure(transcribeResult.Error ?? "Transcription failed");
        }

        var transcription = transcribeResult.Value;
        _logger.LogInformation("Transcription completed: {WordCount} words using {SttEngine} ({SttModel})",
            transcription.WordCount,
            transcription.SttEngine,
            transcription.SttModel);

        // Step 3: Move files from temp to recording/ directory with naming pattern: recording-YYYYMMdd-HHmmss.*
        var timestamp = DateTimeOffset.UtcNow;
        var filePrefix = $"recording-{timestamp:yyyyMMdd-HHmmss}";
        var audioFilePath = Path.Combine(recordingDir, $"{filePrefix}.wav");
        var transcriptionFilePath = Path.Combine(recordingDir, $"{filePrefix}.txt");

        try
        {
            // Move audio file from temp to recording directory
            File.Move(recording.FilePath, audioFilePath, overwrite: true);
            _logger.LogDebug("Moved audio file to {AudioFilePath}", audioFilePath);

            // Get file size
            var audioFileInfo = new FileInfo(audioFilePath);
            var fileSizeBytes = audioFileInfo.Length;

            // Write transcription file with YAML frontmatter metadata
            var transcriptWithMetadata = new StringBuilder();
            transcriptWithMetadata.AppendLine("---");
            transcriptWithMetadata.AppendLine($"recording-id: {filePrefix}");
            transcriptWithMetadata.AppendLine($"timestamp: {timestamp:O}");
            transcriptWithMetadata.AppendLine($"audio-duration-seconds: {recording.Duration.TotalSeconds:F2}");
            transcriptWithMetadata.AppendLine($"file-size-bytes: {fileSizeBytes}");
            transcriptWithMetadata.AppendLine($"stt-engine: {transcription.SttEngine}");
            transcriptWithMetadata.AppendLine($"stt-model: {transcription.SttModel}");
            transcriptWithMetadata.AppendLine($"word-count: {transcription.WordCount}");
            transcriptWithMetadata.AppendLine($"processing-duration-seconds: {transcription.ProcessingDuration.TotalSeconds:F2}");
            if (!string.IsNullOrEmpty(transcription.Language))
            {
                transcriptWithMetadata.AppendLine($"language: {transcription.Language}");
            }
            transcriptWithMetadata.AppendLine("---");
            transcriptWithMetadata.AppendLine();
            transcriptWithMetadata.AppendLine(transcription.TranscriptText);

            await File.WriteAllTextAsync(transcriptionFilePath, transcriptWithMetadata.ToString(), cancellationToken);
            _logger.LogDebug("Saved transcription to {TranscriptionFilePath}", transcriptionFilePath);

            // Create result
            var storedRecording = new StoredRecording
            {
                AudioFilePath = audioFilePath,
                TranscriptionFilePath = transcriptionFilePath,
                RecordedAt = timestamp,
                Duration = recording.Duration,
                FileSizeBytes = fileSizeBytes,
                TranscriptionWordCount = transcription.WordCount,
                SttEngine = transcription.SttEngine,
                SttModel = transcription.SttModel
            };

            _logger.LogInformation("Recording stored successfully: {AudioPath}, {TranscriptionPath}",
                audioFilePath,
                transcriptionFilePath);

            return Result<StoredRecording>.Success(storedRecording);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recording files to {RecordingDir}", recordingDir);

            // Attempt to clean up any partially written files
            CleanupFile(recording.FilePath);
            CleanupFile(audioFilePath);
            CleanupFile(transcriptionFilePath);

            return Result<StoredRecording>.Failure($"Failed to save recording files: {ex.Message}");
        }
    }

    private void CleanupFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Cleaned up file: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup file: {FilePath}", filePath);
        }
    }
}
