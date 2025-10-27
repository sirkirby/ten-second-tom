using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Handlers;

/// <summary>
/// Handles the <see cref="TranscribeAudioCommand"/> to transcribe audio to text.
/// Orchestrates STT provider selection and transcription.
/// </summary>
public sealed class TranscribeAudioCommandHandler : IRequestHandler<TranscribeAudioCommand, Result<TranscriptionResult>>
{
    private readonly ISttProviderFactory _providerFactory;
    private readonly AudioConfiguration _audioConfig;
    private readonly ILogger<TranscribeAudioCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranscribeAudioCommandHandler"/> class.
    /// </summary>
    /// <param name="providerFactory">The STT provider factory.</param>
    /// <param name="audioConfig">The audio configuration.</param>
    /// <param name="logger">The logger instance.</param>
    public TranscribeAudioCommandHandler(
        ISttProviderFactory providerFactory,
        IOptions<AudioConfiguration> audioConfig,
        ILogger<TranscribeAudioCommandHandler> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _audioConfig = audioConfig?.Value ?? throw new ArgumentNullException(nameof(audioConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the TranscribeAudioCommand to transcribe an audio file.
    /// </summary>
    /// <param name="request">The command containing the audio file path and audio configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the TranscriptionResult or an error.</returns>
    public async Task<Result<TranscriptionResult>> Handle(
        TranscribeAudioCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.AudioFilePath))
        {
            throw new ArgumentException("Audio file path cannot be null or empty.", nameof(request));
        }

        _logger.LogInformation(
            "Transcribing audio file {AudioFile} using provider {Provider} (CloudFallback={CloudFallback})",
            request.AudioFilePath,
            request.AudioConfig.SttProvider,
            request.AudioConfig.SttFallbackEnabled);

        // Get the appropriate STT provider based on configuration
        var provider = await _providerFactory.GetProviderAsync(request.AudioConfig, cancellationToken);

        if (provider == null)
        {
            return Result<TranscriptionResult>.Failure(
                $"No STT provider available for provider: {request.AudioConfig.SttProvider}");
        }

        _logger.LogInformation("Using STT engine: {SttEngine}", provider.Engine);

        // Transcribe the audio
        var result = await provider.TranscribeAsync(request.AudioFilePath, cancellationToken);

        if (result.IsSuccess)
        {
            var transcription = result.Value;
            _logger.LogInformation(
                "Transcription completed: Engine={Engine}, Model={Model}, Duration={Duration}s, WordCount={WordCount}",
                transcription.SttEngine,
                transcription.SttModel ?? "unknown",
                transcription.ProcessingDuration.TotalSeconds,
                transcription.WordCount);
        }
        else
        {
            _logger.LogError("Transcription failed: {Error}", result.Error);
        }

        return result;
    }
}
