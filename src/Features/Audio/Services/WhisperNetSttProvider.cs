using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Abstractions.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using Whisper.net;
using Whisper.net.Ggml;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Whisper.NET-based STT provider for local speech-to-text transcription.
/// Uses native bindings to whisper.cpp - no external binary installation required.
/// </summary>
public sealed class WhisperNetSttProvider : ISttProvider, ISupportsModelManagement
{
    private readonly AudioOptions _config;
    private readonly IWhisperNetModelManager _modelManager;
    private readonly ILogger<WhisperNetSttProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperNetSttProvider"/> class.
    /// </summary>
    public WhisperNetSttProvider(
        IOptions<AudioOptions> config,
        IWhisperNetModelManager modelManager,
        ILogger<WhisperNetSttProvider> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public SttEngine Engine => SttEngine.Local;

    /// <summary>
    /// Gets the model path from configuration.
    /// </summary>
    private string? GetModelPath()
    {
        return _config.GetSttModel(SttProviders.WhisperCpp);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Whisper.NET is always available - no external binary needed
        // Just check if a model is configured and exists
        var configuredModelPath = GetModelPath();
        if (string.IsNullOrWhiteSpace(configuredModelPath))
        {
            _logger.LogDebug("Whisper.NET model path not configured");
            return Task.FromResult(false);
        }

        // Expand user home directory if needed
        var modelPath = configuredModelPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        if (!File.Exists(modelPath))
        {
            _logger.LogDebug("Whisper.NET model file not found: {ModelPath}", modelPath);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
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

        var configuredModelPath = GetModelPath();
        if (string.IsNullOrWhiteSpace(configuredModelPath))
        {
            return Result<TranscriptionResult>.Failure("Whisper.NET model path not configured");
        }

        // Expand user home directory if needed
        var modelPath = configuredModelPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        if (!File.Exists(modelPath))
        {
            return Result<TranscriptionResult>.Failure($"Whisper.NET model file not found: {modelPath}");
        }

        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Starting Whisper.NET transcription with model: {ModelPath}", modelPath);

            // Create whisper factory from model
            using var whisperFactory = WhisperFactory.FromPath(modelPath);

            // Create processor with auto language detection
            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            // Process the audio file
            var segments = new List<string>();
            await using var fileStream = File.OpenRead(audioFilePath);

            await foreach (var segment in processor.ProcessAsync(fileStream, cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    segments.Add(segment.Text.Trim());
                }
            }

            stopwatch.Stop();

            var transcriptText = string.Join(" ", segments);

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                return Result<TranscriptionResult>.Failure("Whisper.NET returned empty transcript");
            }

            // Calculate word count
            var wordCount = transcriptText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            // Extract model name from path
            var modelName = Path.GetFileName(modelPath);

            var result = new TranscriptionResult
            {
                AudioReference = audioFilePath,
                TranscriptText = transcriptText,
                SttEngine = SttEngine.Local,
                SttModel = modelName,
                ProcessingDuration = stopwatch.Elapsed,
                TranscribedAt = startTime,
                WordCount = wordCount
            };

            _logger.LogInformation(
                "Whisper.NET transcription completed: Model={Model}, Duration={Duration}s, WordCount={WordCount}",
                modelName,
                result.ProcessingDuration.TotalSeconds,
                wordCount);

            return Result<TranscriptionResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Whisper.NET transcription failed");
            return Result<TranscriptionResult>.Failure($"Whisper.NET transcription failed: {ex.Message}");
        }
    }

    #region ISupportsModelManagement Implementation

    /// <inheritdoc/>
    public Task<IEnumerable<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var models = _modelManager.ListAvailableModels();
        var downloaded = _modelManager.ListDownloadedModelsAsync(cancellationToken).GetAwaiter().GetResult();
        var downloadedIds = downloaded.Select(d => d.ModelId).ToHashSet();

        // Format: "model-id (size MB) ★ (downloaded)" or "model-id (size MB)"
        var result = models.Select(m =>
        {
            var status = downloadedIds.Contains(m.Id) ? " (downloaded)" : "";
            var recommended = m.Recommended ? " ★" : "";
            return $"{m.Id} ({m.SizeMb} MB){recommended}{status}";
        });

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task<Result> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _modelManager.DownloadModelAsync(modelId, progress, cancellationToken);

        if (result.IsSuccess)
        {
            return Result.Success();
        }

        return Result.Failure(result.Error ?? "Download failed");
    }

    #endregion
}
