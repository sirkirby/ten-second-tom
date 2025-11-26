using System;
using System.IO;
using System.Threading;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Helper methods for persisting voice recordings and their transcripts across note/today flows.
/// Relies on the shared <see cref="TranscribeLibraryAudio"/> handler to write markdown transcripts.
/// </summary>
internal static class VoiceCapturePersistence
{
    /// <summary>
    /// Builds a deterministic base filename from an entry identifier (e.g., note-10-21-2025-1 → 10-21-2025_1).
    /// Falls back to the audio filename when the entry identifier cannot be parsed.
    /// </summary>
    public static string BuildVoiceEntryBaseName(string entryId, string fallbackFilename)
    {
        var entryIdParts = entryId.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (entryIdParts.Length >= 5)
        {
            var month = entryIdParts[1];
            var day = entryIdParts[2];
            var year = entryIdParts[3];
            var number = entryIdParts[4];
            return $"{month}-{day}-{year}_{number}";
        }

        var fallbackBase = Path.GetFileNameWithoutExtension(fallbackFilename);
        return string.IsNullOrWhiteSpace(fallbackBase)
            ? $"voice-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"
            : fallbackBase;
    }

    /// <summary>
    /// Persists the provided audio/transcription pair into the library using the shared handler.
    /// </summary>
    public static async Task<Result<PersistedVoiceCapture>> PersistAsync(
        IMediator mediator,
        string audioFilePath,
        string recordingBaseName,
        TranscribeOptions transcribeConfig,
        AudioLibraryScope scope,
        TranscriptionResult transcription,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(transcribeConfig);
        ArgumentNullException.ThrowIfNull(transcription);
        ArgumentNullException.ThrowIfNull(logger);

        var command = new TranscribeLibraryAudio.Command
        {
            AudioFilePath = audioFilePath,
            RecordingBaseName = recordingBaseName,
            TranscribeConfig = transcribeConfig,
            Source = scope,
            ForceOverwrite = true,
            ExistingTranscription = transcription
        };

        var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value is null)
        {
            logger.LogWarning(
                "Failed to persist voice capture {Recording}: {Error}",
                recordingBaseName,
                result.Error);
            return Result<PersistedVoiceCapture>.Failure(result.Error ?? "Voice capture persistence failed.");
        }

        var payload = new PersistedVoiceCapture(
            result.Value.AudioFilePath,
            result.Value.TranscriptFilePath);

        return Result<PersistedVoiceCapture>.Success(payload);
    }
}
