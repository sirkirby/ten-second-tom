using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Manages whisper.cpp GGML model files - listing available models from the catalog
/// and downloading them from Hugging Face.
/// </summary>
public sealed class WhisperCppModelManager : IWhisperCppModelManager
{
    private const string HuggingFaceBaseUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhisperCppModelManager> _logger;

    /// <summary>
    /// Static catalog of available whisper.cpp models from Hugging Face.
    /// </summary>
    private static readonly IReadOnlyList<WhisperCppModelInfo> ModelCatalog =
    [
        // Tiny models - fastest, least accurate
        new WhisperCppModelInfo
        {
            Id = "tiny",
            Name = "Tiny (Multilingual)",
            FileName = "ggml-tiny.bin",
            Description = "Fastest, lowest accuracy. Good for quick tests.",
            SizeMb = 75,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-tiny.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "tiny.en",
            Name = "Tiny (English)",
            FileName = "ggml-tiny.en.bin",
            Description = "Fastest English-only model. Good for quick tests.",
            SizeMb = 75,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-tiny.en.bin",
            EnglishOnly = true
        },

        // Base models - good balance for basic use
        new WhisperCppModelInfo
        {
            Id = "base",
            Name = "Base (Multilingual)",
            FileName = "ggml-base.bin",
            Description = "Good balance of speed and accuracy for multiple languages.",
            SizeMb = 142,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-base.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "base.en",
            Name = "Base (English)",
            FileName = "ggml-base.en.bin",
            Description = "Recommended for English. Good balance of speed and accuracy.",
            SizeMb = 142,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-base.en.bin",
            EnglishOnly = true,
            Recommended = true
        },

        // Small models - higher accuracy
        new WhisperCppModelInfo
        {
            Id = "small",
            Name = "Small (Multilingual)",
            FileName = "ggml-small.bin",
            Description = "Higher accuracy, moderate speed. Good for multiple languages.",
            SizeMb = 466,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-small.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "small.en",
            Name = "Small (English)",
            FileName = "ggml-small.en.bin",
            Description = "Higher accuracy English-only model. Good for longer recordings.",
            SizeMb = 466,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-small.en.bin",
            EnglishOnly = true
        },

        // Medium models - high accuracy
        new WhisperCppModelInfo
        {
            Id = "medium",
            Name = "Medium (Multilingual)",
            FileName = "ggml-medium.bin",
            Description = "High accuracy, slower. Good for difficult audio.",
            SizeMb = 1533,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-medium.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "medium.en",
            Name = "Medium (English)",
            FileName = "ggml-medium.en.bin",
            Description = "High accuracy English-only. Good for difficult audio.",
            SizeMb = 1533,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-medium.en.bin",
            EnglishOnly = true
        },

        // Large models - highest accuracy
        new WhisperCppModelInfo
        {
            Id = "large-v1",
            Name = "Large v1",
            FileName = "ggml-large-v1.bin",
            Description = "Original large model. High accuracy, slow.",
            SizeMb = 3094,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v1.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "large-v2",
            Name = "Large v2",
            FileName = "ggml-large-v2.bin",
            Description = "Improved large model. Higher accuracy than v1.",
            SizeMb = 3094,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v2.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "large-v3",
            Name = "Large v3",
            FileName = "ggml-large-v3.bin",
            Description = "Latest large model. Best accuracy, slowest.",
            SizeMb = 3094,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "large-v3-turbo",
            Name = "Large v3 Turbo",
            FileName = "ggml-large-v3-turbo.bin",
            Description = "Optimized large v3. Near-best accuracy, faster than v3.",
            SizeMb = 1614,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3-turbo.bin",
            EnglishOnly = false,
            Recommended = true
        },

        // Quantized models for constrained environments
        new WhisperCppModelInfo
        {
            Id = "large-v3-turbo-q5_0",
            Name = "Large v3 Turbo (Q5)",
            FileName = "ggml-large-v3-turbo-q5_0.bin",
            Description = "Quantized turbo model. Good accuracy with smaller size.",
            SizeMb = 574,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3-turbo-q5_0.bin",
            EnglishOnly = false
        },
        new WhisperCppModelInfo
        {
            Id = "large-v3-q5_0",
            Name = "Large v3 (Q5)",
            FileName = "ggml-large-v3-q5_0.bin",
            Description = "Quantized large v3. Good accuracy with smaller size.",
            SizeMb = 1080,
            DownloadUrl = $"{HuggingFaceBaseUrl}/ggml-large-v3-q5_0.bin",
            EnglishOnly = false
        }
    ];

    public WhisperCppModelManager(
        IHttpClientFactory httpClientFactory,
        ILogger<WhisperCppModelManager> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string DefaultModelDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache",
            "whisper");

    /// <inheritdoc/>
    public IReadOnlyList<WhisperCppModelInfo> ListAvailableModels() => ModelCatalog;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WhisperCppDownloadedModel>> ListDownloadedModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<WhisperCppDownloadedModel>();

        if (!Directory.Exists(DefaultModelDirectory))
        {
            return results;
        }

        await Task.Run(() =>
        {
            foreach (var model in ModelCatalog)
            {
                var filePath = Path.Combine(DefaultModelDirectory, model.FileName);
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    results.Add(new WhisperCppDownloadedModel
                    {
                        Model = model,
                        FilePath = filePath,
                        FileSizeBytes = fileInfo.Length
                    });
                }
            }
        }, cancellationToken);

        return results;
    }

    /// <inheritdoc/>
    public bool IsModelDownloaded(string modelId)
    {
        var model = ModelCatalog.FirstOrDefault(m =>
            m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));

        if (model == null)
        {
            return false;
        }

        var filePath = Path.Combine(DefaultModelDirectory, model.FileName);
        return File.Exists(filePath);
    }

    /// <inheritdoc/>
    public string? GetModelPath(string modelId)
    {
        var model = ModelCatalog.FirstOrDefault(m =>
            m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));

        if (model == null)
        {
            return null;
        }

        var filePath = Path.Combine(DefaultModelDirectory, model.FileName);
        return File.Exists(filePath) ? filePath : null;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var model = ModelCatalog.FirstOrDefault(m =>
            m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));

        if (model == null)
        {
            var availableIds = string.Join(", ", ModelCatalog.Select(m => m.Id));
            return Result<string>.Failure(
                $"Unknown model '{modelId}'. Available models: {availableIds}");
        }

        // Ensure directory exists
        Directory.CreateDirectory(DefaultModelDirectory);

        var filePath = Path.Combine(DefaultModelDirectory, model.FileName);
        var tempPath = filePath + ".download";

        // Check if already downloaded
        if (File.Exists(filePath))
        {
            _logger.LogInformation("Model {ModelId} already exists at {Path}", modelId, filePath);
            progress?.Invoke(100);
            return Result<string>.Success(filePath);
        }

        _logger.LogInformation(
            "Downloading model {ModelId} from {Url} to {Path}",
            modelId,
            model.DownloadUrl,
            filePath);

        try
        {
            using var httpClient = _httpClientFactory.CreateClient("WhisperModelDownload");
            httpClient.Timeout = TimeSpan.FromMinutes(30); // Large files may take a while

            using var response = await httpClient.GetAsync(
                model.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? (model.SizeMb * 1024L * 1024L);

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            var buffer = new byte[81920];
            long bytesRead = 0;
            int read;
            var lastProgressReport = 0.0;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;

                var currentProgress = (double)bytesRead / totalBytes * 100;

                // Report progress every 1%
                if (currentProgress - lastProgressReport >= 1)
                {
                    progress?.Invoke(currentProgress);
                    lastProgressReport = currentProgress;

                    _logger.LogDebug(
                        "Download progress: {Progress:F1}% ({BytesRead:N0} / {TotalBytes:N0} bytes)",
                        currentProgress,
                        bytesRead,
                        totalBytes);
                }
            }

            progress?.Invoke(100);

            // Move temp file to final location
            File.Move(tempPath, filePath, overwrite: true);

            _logger.LogInformation(
                "Model {ModelId} downloaded successfully to {Path} ({Size:N0} bytes)",
                modelId,
                filePath,
                bytesRead);

            return Result<string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            // Clean up partial download
            TryDeleteFile(tempPath);
            throw;
        }
        catch (HttpRequestException ex)
        {
            TryDeleteFile(tempPath);
            _logger.LogError(ex, "Failed to download model {ModelId} from {Url}", modelId, model.DownloadUrl);
            return Result<string>.Failure($"Download failed: {ex.Message}");
        }
        catch (IOException ex)
        {
            TryDeleteFile(tempPath);
            _logger.LogError(ex, "Failed to write model {ModelId} to {Path}", modelId, filePath);
            return Result<string>.Failure($"Failed to save model: {ex.Message}");
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete partial download file: {Path}", path);
        }
    }
}
