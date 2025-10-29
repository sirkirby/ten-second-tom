using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Services;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Records audio to a specified file path.
/// </summary>
public static class RecordAudio
{
    /// <summary>
    /// Command to record audio to a specified file path.
    /// </summary>
    public sealed record Command : IRequest<Result<AudioRecording>>
    {
        /// <summary>
        /// Gets the output path where the audio file should be saved.
        /// </summary>
        public required string OutputPath { get; init; }

        /// <summary>
        /// Gets the maximum recording duration in seconds.
        /// If null, records indefinitely until user stops.
        /// If specified, prompts user to continue when timeout is reached.
        /// </summary>
        public int? MaxDurationSeconds { get; init; }
    }

    /// <summary>
    /// Handles the <see cref="Command"/> to record audio.
    /// Orchestrates audio recording using the configured audio recorder.
    /// </summary>
    public sealed class Handler(
        IAudioRecorder recorder,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<AudioRecording>>
    {
        /// <summary>
        /// Handles the RecordAudio command to record audio to a file.
        /// </summary>
        /// <param name="request">The command containing the output path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result containing the AudioRecording or an error.</returns>
        public async Task<Result<AudioRecording>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.OutputPath))
            {
                throw new ArgumentException("Output path cannot be null or empty.", nameof(request));
            }

            logger.LogInformation("Recording audio to {OutputPath} with max duration: {MaxDuration}s",
                request.OutputPath,
                request.MaxDurationSeconds?.ToString() ?? "unlimited");

            var result = await recorder.RecordAsync(request.OutputPath, request.MaxDurationSeconds, cancellationToken);

            if (result.IsSuccess)
            {
                logger.LogInformation(
                    "Audio recording completed: Duration={Duration}s, Size={SizeBytes} bytes",
                    result.Value.Duration.TotalSeconds,
                    result.Value.FileSizeBytes);
            }
            else
            {
                logger.LogError("Audio recording failed: {Error}", result.Error);
            }

            return result;
        }
    }
}
