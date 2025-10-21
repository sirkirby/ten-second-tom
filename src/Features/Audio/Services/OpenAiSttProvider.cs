using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// OpenAI Whisper API STT provider implementation.
/// Uses OpenAI's cloud-based Whisper API for speech-to-text transcription.
/// </summary>
public sealed class OpenAiSttProvider : ISttProvider
{
    private readonly IConfiguration _configuration;
    private readonly ConfigurationSettings _configSettings;
    private readonly ILogger<OpenAiSttProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiSttProvider"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration (for API keys and provider).</param>
    /// <param name="configSettings">User configuration settings (for LLM provider and STT model).</param>
    /// <param name="logger">Logger instance.</param>
    public OpenAiSttProvider(
        IConfiguration configuration,
        IOptions<ConfigurationSettings> configSettings,
        ILogger<OpenAiSttProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configSettings = configSettings?.Value ?? throw new ArgumentNullException(nameof(configSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public SttEngine Engine => SttEngine.OpenAI;

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Only available if provider is OpenAI and API key is configured
        if (_configSettings.Llm.Provider != LlmProvider.OpenAI)
        {
            _logger.LogDebug("OpenAI STT not available: Provider is {Provider}, not OpenAI", _configSettings.Llm.Provider);
            return Task.FromResult(false);
        }

        var apiKey = _configuration["Llm:ApiKey"];
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

        // Verify provider is OpenAI
        if (_configSettings.Llm.Provider != LlmProvider.OpenAI)
        {
            return Result<TranscriptionResult>.Failure(
                $"Cannot use OpenAI STT: Current provider is {_configSettings.Llm.Provider}. Change to OpenAI in 'tom config llm'.");
        }

        var apiKey = _configuration["Llm:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Result<TranscriptionResult>.Failure("OpenAI API key not configured. Run 'tom setup' to configure your API key.");
        }

        // Get STT model from config or use default
        var model = _configSettings.Llm.SpeechToTextModel ?? "whisper-1";

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
