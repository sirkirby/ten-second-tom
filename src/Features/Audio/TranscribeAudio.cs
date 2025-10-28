using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Transcribes an audio file to text.
/// </summary>
public static class TranscribeAudio
{
    /// <summary>
    /// Command to transcribe an audio file to text.
    /// </summary>
    public sealed record Command : IRequest<Result<TranscriptionResult>>
    {
        /// <summary>
        /// Gets the path to the audio file to transcribe.
        /// </summary>
        public required string AudioFilePath { get; init; }

        /// <summary>
        /// Gets the audio configuration for STT provider selection.
        /// This includes the STT provider, API key, and fallback settings.
        /// </summary>
        public required AudioConfiguration AudioConfig { get; init; }
    }

    /// <summary>
    /// Handles the <see cref="Command"/> to transcribe audio to text.
    /// Orchestrates STT provider selection and transcription.
    /// </summary>
    public sealed class Handler(
        ISttProviderFactory providerFactory,
        IOptions<AudioConfiguration> audioConfig,
        ILogger<Handler> logger) : IRequestHandler<Command, Result<TranscriptionResult>>
    {
        private readonly AudioConfiguration _audioConfig = audioConfig.Value;

        /// <summary>
        /// Handles the TranscribeAudio command to transcribe an audio file.
        /// </summary>
        /// <param name="request">The command containing the audio file path and audio configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result containing the TranscriptionResult or an error.</returns>
        public async Task<Result<TranscriptionResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.AudioFilePath))
            {
                throw new ArgumentException("Audio file path cannot be null or empty.", nameof(request));
            }

            logger.LogInformation(
                "Transcribing audio file {AudioFile} using provider {Provider} (CloudFallback={CloudFallback})",
                request.AudioFilePath,
                request.AudioConfig.SttProvider,
                request.AudioConfig.SttFallbackEnabled);

            // Get the appropriate STT provider based on configuration
            var provider = await providerFactory.GetProviderAsync(request.AudioConfig, cancellationToken);

            if (provider == null)
            {
                return Result<TranscriptionResult>.Failure(
                    $"No STT provider available for provider: {request.AudioConfig.SttProvider}");
            }

            logger.LogInformation("Using STT engine: {SttEngine}", provider.Engine);

            // Transcribe the audio
            var result = await provider.TranscribeAsync(request.AudioFilePath, cancellationToken);

            if (result.IsSuccess)
            {
                var transcription = result.Value;
                logger.LogInformation(
                    "Transcription completed: Engine={Engine}, Model={Model}, Duration={Duration}s, WordCount={WordCount}",
                    transcription.SttEngine,
                    transcription.SttModel ?? "unknown",
                    transcription.ProcessingDuration.TotalSeconds,
                    transcription.WordCount);
            }
            else
            {
                logger.LogError("Transcription failed: {Error}", result.Error);
            }

            return result;
        }
    }
}
