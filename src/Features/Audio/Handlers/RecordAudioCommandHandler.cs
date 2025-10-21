using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Handlers;

/// <summary>
/// Handles the <see cref="RecordAudioCommand"/> to record audio.
/// Orchestrates audio recording using the configured audio recorder.
/// </summary>
public sealed class RecordAudioCommandHandler : IRequestHandler<RecordAudioCommand, Result<AudioRecording>>
{
    private readonly IAudioRecorder _recorder;
    private readonly ILogger<RecordAudioCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordAudioCommandHandler"/> class.
    /// </summary>
    /// <param name="recorder">The audio recorder service.</param>
    /// <param name="logger">The logger instance.</param>
    public RecordAudioCommandHandler(
        IAudioRecorder recorder,
        ILogger<RecordAudioCommandHandler> logger)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the RecordAudioCommand to record audio to a file.
    /// </summary>
    /// <param name="request">The command containing the output path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the AudioRecording or an error.</returns>
    public async Task<Result<AudioRecording>> Handle(
        RecordAudioCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OutputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty.", nameof(request));
        }

        _logger.LogInformation("Recording audio to {OutputPath}", request.OutputPath);

        var result = await _recorder.RecordAsync(request.OutputPath, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Audio recording completed: Duration={Duration}s, Size={SizeBytes} bytes",
                result.Value.Duration.TotalSeconds,
                result.Value.FileSizeBytes);
        }
        else
        {
            _logger.LogError("Audio recording failed: {Error}", result.Error);
        }

        return result;
    }
}

/// <summary>
/// Marker interface for request handlers.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
