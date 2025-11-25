using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.Audio;

/// <summary>
/// Information about an available Whisper.NET model.
/// </summary>
/// <param name="Id">Model identifier (e.g., "base.en", "large-v3-turbo").</param>
/// <param name="SizeMb">Approximate model size in megabytes.</param>
/// <param name="Recommended">Whether this model is recommended for general use.</param>
public sealed record WhisperNetModelInfo(string Id, int SizeMb, bool Recommended);

/// <summary>
/// Information about a downloaded Whisper.NET model.
/// </summary>
/// <param name="ModelId">Model identifier.</param>
/// <param name="FilePath">Full path to the downloaded model file.</param>
public sealed record WhisperNetDownloadedModel(string ModelId, string FilePath);

/// <summary>
/// Manages Whisper.NET model operations including listing, downloading, and path resolution.
/// Uses Whisper.NET's built-in downloader to fetch models from Hugging Face.
/// </summary>
public interface IWhisperNetModelManager
{
    /// <summary>
    /// Gets the default directory where models are stored.
    /// </summary>
    string DefaultModelDirectory { get; }

    /// <summary>
    /// Lists all available Whisper models that can be downloaded.
    /// </summary>
    /// <returns>Collection of available model information.</returns>
    IReadOnlyList<WhisperNetModelInfo> ListAvailableModels();

    /// <summary>
    /// Lists all models that have been downloaded locally.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of downloaded model information.</returns>
    Task<IReadOnlyList<WhisperNetDownloadedModel>> ListDownloadedModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific model has been downloaded.
    /// </summary>
    /// <param name="modelId">Model identifier to check.</param>
    /// <returns>True if the model exists locally.</returns>
    bool IsModelDownloaded(string modelId);

    /// <summary>
    /// Gets the local file path for a downloaded model.
    /// </summary>
    /// <param name="modelId">Model identifier.</param>
    /// <returns>Full path to the model file, or null if not downloaded.</returns>
    string? GetModelPath(string modelId);

    /// <summary>
    /// Downloads a model from Hugging Face using Whisper.NET's built-in downloader.
    /// </summary>
    /// <param name="modelId">Model identifier to download.</param>
    /// <param name="progress">Optional progress callback (0-100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the local file path on success.</returns>
    Task<Result<string>> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default);
}
