using System.Diagnostics;
using System.Text;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using TenSecondTom.Shared.Abstractions.LocalAi;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.LocalAi;

/// <summary>
/// Implementation of <see cref="ILocalAiEngine"/> using Microsoft AI Foundry Local SDK.
/// Encapsulates all Foundry Local runtime initialization and model management.
/// Models are stored under the app's root directory in a 'models' subdirectory.
/// </summary>
/// <remarks>
/// This is an experimental provider. The SDK's audio transcription has known limitations
/// with long audio files (truncation after ~30 seconds). For reliable transcription of
/// longer recordings, use the whisper-cpp provider instead.
/// </remarks>
public sealed class LocalAiEngine : ILocalAiEngine
{
    private readonly ILogger<LocalAiEngine> _logger;
    private readonly StorageOptions _storageOptions;
    private bool _initialized;

    public LocalAiEngine(
        IOptions<StorageOptions> storageOptions,
        ILogger<LocalAiEngine> logger)
    {
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            _logger.LogInformation("Initializing Foundry Local Manager");

            // Determine model cache directory under app's root directory
            var rootDirectory = _storageOptions.RootDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    DirectoryNames.ApplicationRoot);

            var appDataDir = Path.Combine(rootDirectory, "foundry");
            var modelCacheDir = Path.Combine(appDataDir, "models");
            var logsDir = Path.Combine(appDataDir, "logs");

            // Ensure directories exist
            Directory.CreateDirectory(modelCacheDir);
            Directory.CreateDirectory(logsDir);

            _logger.LogInformation("Foundry Local model cache directory: {ModelCacheDir}", modelCacheDir);

            // Create configuration for Foundry Local with custom cache location
            var config = new Microsoft.AI.Foundry.Local.Configuration
            {
                AppName = "TenSecondTom",
                LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Debug,
                AppDataDir = appDataDir,
                ModelCacheDir = modelCacheDir,
                LogsDir = logsDir
            };

            // Create the singleton manager instance with config and logger
            await FoundryLocalManager.CreateAsync(config, _logger);

            _initialized = true;
            _logger.LogInformation("Foundry Local Manager initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Foundry Local Manager");
            throw;
        }
    }

    public async Task<Result<string>> CompleteAsync(
        string modelId,
        string prompt,
        int? maxTokens = null,
        double? temperature = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync();
            var mgr = FoundryLocalManager.Instance;
            var catalog = await mgr.GetCatalogAsync();

            var model = await catalog.GetModelAsync(modelId);
            if (model == null)
            {
                return Result<string>.Failure($"Model '{modelId}' not found in Foundry Local catalog.");
            }

            if (!await model.IsCachedAsync())
            {
                return Result<string>.Failure($"Model '{modelId}' is not downloaded. Please run 'tom llm --download-model {modelId}' first.");
            }

            await model.LoadAsync();

            try
            {
                var chatClient = await model.GetChatClientAsync();

                var messages = new List<ChatMessage>
                {
                    ChatMessage.FromSystem("You are a helpful assistant."),
                    ChatMessage.FromUser(prompt)
                };

                var responseText = string.Empty;
                var streamingResponse = chatClient.CompleteChatStreamingAsync(messages, ct: cancellationToken);

                await foreach (var chunk in streamingResponse)
                {
                    if (chunk.Choices != null && chunk.Choices.Count > 0 && chunk.Choices[0].Message?.Content != null)
                    {
                        responseText += chunk.Choices[0].Message.Content;
                    }
                }

                return Result<string>.Success(responseText);
            }
            finally
            {
                await model.UnloadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion with local model {ModelId}", modelId);
            return Result<string>.Failure($"Local AI completion failed: {ex.Message}");
        }
    }

    public async Task<Result<TranscriptionResult>> TranscribeAsync(
        string modelId,
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync();
            var mgr = FoundryLocalManager.Instance;
            var catalog = await mgr.GetCatalogAsync();

            var model = await catalog.GetModelAsync(modelId);
            if (model == null)
            {
                return Result<TranscriptionResult>.Failure($"Model '{modelId}' not found in Foundry Local catalog.");
            }

            if (!await model.IsCachedAsync())
            {
                return Result<TranscriptionResult>.Failure($"Model '{modelId}' is not downloaded. Please run 'tom stt --download-model {modelId}' first.");
            }

            await model.LoadAsync();

            var startTime = DateTimeOffset.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var fileInfo = new FileInfo(audioFilePath);
                var fileSizeMb = fileInfo.Length / (1024.0 * 1024.0);

                _logger.LogInformation(
                    "Starting transcription: File={FilePath}, Size={SizeMB:F2} MB",
                    audioFilePath,
                    fileSizeMb);

                // Use the streaming transcription API (follows Microsoft example)
                var audioClient = await model.GetAudioClientAsync();
                var response = audioClient.TranscribeAudioStreamingAsync(audioFilePath, cancellationToken);

                var fullText = new StringBuilder();

                await foreach (var chunk in response)
                {
                    fullText.Append(chunk.Text ?? string.Empty);
                }

                stopwatch.Stop();

                var transcriptText = fullText.ToString().Trim();

                _logger.LogInformation(
                    "Transcription complete: {CharCount} chars, took {Duration:F2}s",
                    transcriptText.Length,
                    stopwatch.Elapsed.TotalSeconds);

                if (string.IsNullOrWhiteSpace(transcriptText))
                {
                    return Result<TranscriptionResult>.Failure("Local AI returned empty transcript");
                }

                var wordCount = transcriptText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                return Result<TranscriptionResult>.Success(new TranscriptionResult
                {
                    AudioReference = audioFilePath,
                    TranscriptText = transcriptText,
                    SttEngine = SttEngine.Local,
                    SttModel = modelId,
                    ProcessingDuration = stopwatch.Elapsed,
                    TranscribedAt = startTime,
                    WordCount = wordCount,
                    Language = null
                });
            }
            finally
            {
                await model.UnloadAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transcribing with local model {ModelId}", modelId);
            return Result<TranscriptionResult>.Failure($"Local AI transcription failed: {ex.Message}");
        }
    }

    public async Task<Result> EnsureModelAvailableAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync();
            var mgr = FoundryLocalManager.Instance;
            var catalog = await mgr.GetCatalogAsync();

            var model = await catalog.GetModelAsync(modelId);
            if (model == null)
            {
                return Result.Failure($"Model '{modelId}' not found in Foundry Local catalog.");
            }

            if (await model.IsCachedAsync())
            {
                _logger.LogInformation("Model {ModelId} is already downloaded", modelId);
                progress?.Invoke(100.0);
                return Result.Success();
            }

            _logger.LogInformation("Downloading model {ModelId}...", modelId);

            await model.DownloadAsync(p =>
            {
                if (p % 10 == 0)
                {
                    _logger.LogInformation("Downloading {ModelId}: {Progress}%", modelId, p);
                }
                progress?.Invoke(p);
            }, ct: cancellationToken);

            _logger.LogInformation("Model {ModelId} downloaded successfully", modelId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading model {ModelId}", modelId);
            return Result.Failure($"Failed to download model: {ex.Message}");
        }
    }

    public async Task<IEnumerable<string>> ListAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await InitializeAsync();
            var mgr = FoundryLocalManager.Instance;
            var catalog = await mgr.GetCatalogAsync();
            var models = await catalog.ListModelsAsync();

            var results = new List<string>();
            foreach (var model in models)
            {
                if (model.Variants != null)
                {
                    foreach (var variant in model.Variants)
                    {
                        if (!string.IsNullOrEmpty(variant.Alias))
                        {
                            results.Add(variant.Alias);
                        }
                    }
                }
            }

            return results.Distinct();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing available models");
            return Enumerable.Empty<string>();
        }
    }
}
