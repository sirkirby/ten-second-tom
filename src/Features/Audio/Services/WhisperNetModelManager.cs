using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Results;
using Whisper.net.Ggml;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Manages Whisper.NET model operations using the built-in Hugging Face downloader.
/// Models are stored in ~/.cache/whisper-net/ for consistency with other local model stores.
/// </summary>
public sealed class WhisperNetModelManager : IWhisperNetModelManager
{
    private readonly ILogger<WhisperNetModelManager> _logger;

    /// <summary>
    /// Static catalog of available Whisper models with metadata.
    /// Maps model ID to GgmlType and approximate size.
    /// </summary>
    private static readonly IReadOnlyList<(string Id, GgmlType Type, int SizeMb, bool Recommended)> ModelCatalog =
    [
        ("tiny", GgmlType.Tiny, 75, false),
        ("tiny.en", GgmlType.TinyEn, 75, false),
        ("base", GgmlType.Base, 142, false),
        ("base.en", GgmlType.BaseEn, 142, true),  // Recommended for English
        ("small", GgmlType.Small, 466, false),
        ("small.en", GgmlType.SmallEn, 466, false),
        ("medium", GgmlType.Medium, 1533, false),
        ("medium.en", GgmlType.MediumEn, 1533, false),
        ("large-v1", GgmlType.LargeV1, 3094, false),
        ("large-v2", GgmlType.LargeV2, 3094, false),
        ("large-v3", GgmlType.LargeV3, 3094, false),
        ("large-v3-turbo", GgmlType.LargeV3Turbo, 1614, true)  // Recommended for quality/speed balance
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperNetModelManager"/> class.
    /// </summary>
    public WhisperNetModelManager(ILogger<WhisperNetModelManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ensure model directory exists
        if (!Directory.Exists(DefaultModelDirectory))
        {
            Directory.CreateDirectory(DefaultModelDirectory);
        }
    }

    /// <inheritdoc/>
    public string DefaultModelDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "whisper-net");

    /// <inheritdoc/>
    public IReadOnlyList<WhisperNetModelInfo> ListAvailableModels()
    {
        return ModelCatalog
            .Select(m => new WhisperNetModelInfo(m.Id, m.SizeMb, m.Recommended))
            .ToList();
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WhisperNetDownloadedModel>> ListDownloadedModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var downloaded = new List<WhisperNetDownloadedModel>();

        foreach (var model in ModelCatalog)
        {
            var fileName = GetModelFileName(model.Id);
            var filePath = Path.Combine(DefaultModelDirectory, fileName);

            if (File.Exists(filePath))
            {
                downloaded.Add(new WhisperNetDownloadedModel(model.Id, filePath));
            }
        }

        return Task.FromResult<IReadOnlyList<WhisperNetDownloadedModel>>(downloaded);
    }

    /// <inheritdoc/>
    public bool IsModelDownloaded(string modelId)
    {
        var fileName = GetModelFileName(modelId);
        var filePath = Path.Combine(DefaultModelDirectory, fileName);
        return File.Exists(filePath);
    }

    /// <inheritdoc/>
    public string? GetModelPath(string modelId)
    {
        var fileName = GetModelFileName(modelId);
        var filePath = Path.Combine(DefaultModelDirectory, fileName);
        return File.Exists(filePath) ? filePath : null;
    }

    /// <inheritdoc/>
    public async Task<Result<string>> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var modelEntry = ModelCatalog.FirstOrDefault(m =>
            m.Id.Equals(modelId, StringComparison.OrdinalIgnoreCase));

        if (modelEntry == default)
        {
            return Result<string>.Failure($"Unknown model: {modelId}");
        }

        var fileName = GetModelFileName(modelId);
        var filePath = Path.Combine(DefaultModelDirectory, fileName);

        // If already downloaded, return success
        if (File.Exists(filePath))
        {
            _logger.LogInformation("Model {ModelId} already exists at {FilePath}", modelId, filePath);
            progress?.Invoke(100);
            return Result<string>.Success(filePath);
        }

        try
        {
            _logger.LogInformation("Downloading model {ModelId} from Hugging Face...", modelId);
            progress?.Invoke(0);

            // Use Whisper.NET's built-in downloader
            // Note: GetGgmlModelAsync doesn't support cancellation, but stream operations below do
#pragma warning disable CA2016 // Forward cancellation token - API doesn't support it
            await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(modelEntry.Type);
#pragma warning restore CA2016

            // Create temp file first to avoid partial downloads
            var tempPath = filePath + ".tmp";

            await using (var fileStream = File.Create(tempPath))
            {
                // Copy with progress tracking
                var buffer = new byte[81920]; // 80KB buffer
                var totalBytes = modelEntry.SizeMb * 1024L * 1024L; // Approximate
                var bytesRead = 0L;
                int read;

                while ((read = await modelStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    bytesRead += read;

                    // Report progress (approximate since we use estimated size)
                    var progressPercent = Math.Min(99, (double)bytesRead / totalBytes * 100);
                    progress?.Invoke(progressPercent);
                }
            }

            // Rename temp to final
            File.Move(tempPath, filePath, overwrite: true);

            progress?.Invoke(100);
            _logger.LogInformation("Model {ModelId} downloaded successfully to {FilePath}", modelId, filePath);

            return Result<string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download model {ModelId}", modelId);
            return Result<string>.Failure($"Failed to download model: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the standard filename for a model.
    /// </summary>
    private static string GetModelFileName(string modelId)
    {
        // Convert model ID to standard ggml filename format
        // e.g., "base.en" -> "ggml-base.en.bin"
        return $"ggml-{modelId}.bin";
    }
}
