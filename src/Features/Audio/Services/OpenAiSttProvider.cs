using TenSecondTom.Features.Audio.Constants;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// OpenAI Whisper API STT provider implementation.
/// Uses OpenAI's cloud-based Whisper API for speech-to-text transcription.
/// </summary>
public sealed class OpenAiSttProvider : ISttProvider
{
    private readonly AudioOptions _audioConfig;
    private readonly ILogger<OpenAiSttProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiSttProvider"/> class.
    /// </summary>
    /// <param name="audioConfig">Audio configuration (for STT provider and API key).</param>
    /// <param name="logger">Logger instance.</param>
    public OpenAiSttProvider(
        IOptions<AudioOptions> audioConfig,
        ILogger<OpenAiSttProvider> logger)
    {
        _audioConfig = audioConfig?.Value ?? throw new ArgumentNullException(nameof(audioConfig));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public SttEngine Engine => SttEngine.OpenAI;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Only available if STT provider is OpenAI and API key is configured
        if (_audioConfig.SttProvider != SttProviders.OpenAI)
        {
            _logger.LogDebug("OpenAI STT not available: Provider is {Provider}, not OpenAI", _audioConfig.SttProvider);
            return Task.FromResult(false);
        }

        var apiKey = _audioConfig.SttApiKey;
        var isAvailable = !string.IsNullOrWhiteSpace(apiKey);

        if (!isAvailable)
        {
            _logger.LogDebug("OpenAI STT not available: API key not configured");
        }

        return Task.FromResult(isAvailable);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult>> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioFilePath);

        if (!File.Exists(audioFilePath))
        {
            return Result<TranscriptionResult>.Failure($"Audio file not found: {audioFilePath}");
        }

        // Verify STT provider is OpenAI
        if (_audioConfig.SttProvider != SttProviders.OpenAI)
        {
            return Result<TranscriptionResult>.Failure(
                $"Cannot use OpenAI STT: Current STT provider is {_audioConfig.SttProvider}. Change to OpenAI in 'tom config audio'.");
        }

        var apiKey = _audioConfig.SttApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Result<TranscriptionResult>.Failure("OpenAI STT API key not configured. Run 'tom config audio' to configure your API key.");
        }

        // Get STT model from audio config or use default
        var model = string.IsNullOrWhiteSpace(_audioConfig.SttModel)
            ? SttProviders.OpenAIDefaultSTTModel
            : _audioConfig.SttModel;

        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var client = new OpenAIClient(apiKey);
            var audioClient = client.GetAudioClient(model);

            _logger.LogDebug(
                "Starting OpenAI transcription: Model={Model}, File={File}",
                model,
                audioFilePath);

            // Read audio file as stream
            await using var audioStream = File.OpenRead(audioFilePath);
            var filename = Path.GetFileName(audioFilePath);

            var response = await audioClient.TranscribeAudioAsync(audioStream, filename, cancellationToken: cancellationToken);

            stopwatch.Stop();

            if (response?.Value == null)
            {
                return Result<TranscriptionResult>.Failure("OpenAI returned null response");
            }

            var transcriptText = response.Value.Text;

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                return Result<TranscriptionResult>.Failure("OpenAI returned empty transcript");
            }

            // Trim whitespace
            transcriptText = transcriptText.Trim();

            // Calculate word count
            var wordCount = transcriptText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            var result = new TranscriptionResult
            {
                AudioReference = audioFilePath,
                TranscriptText = transcriptText,
                SttEngine = SttEngine.OpenAI,
                SttModel = model,
                ProcessingDuration = stopwatch.Elapsed,
                TranscribedAt = startTime,
                WordCount = wordCount,
                Language = response.Value.Language
            };

            _logger.LogInformation(
                "OpenAI transcription completed: Model={Model}, Duration={Duration}s, WordCount={WordCount}",
                model,
                result.ProcessingDuration.TotalSeconds,
                wordCount);

            return Result<TranscriptionResult>.Success(result);
        }
        catch (Exception ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "OpenAI authentication failed: Invalid API key");
            return Result<TranscriptionResult>.Failure("OpenAI authentication failed: Invalid API key");
        }
        catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
        {
            _logger.LogWarning(ex, "OpenAI rate limit exceeded");
            return Result<TranscriptionResult>.Failure("OpenAI rate limit exceeded. Please try again later.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OpenAI transcription failed: {Message}", ex.Message);
            return Result<TranscriptionResult>.Failure($"OpenAI transcription failed: {ex.Message}");
        }
    }
}
