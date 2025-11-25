using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.Audio;

/// <summary>
/// Manages whisper.cpp GGML model files - listing available models from the catalog
/// and downloading them from Hugging Face.
/// </summary>
public interface IWhisperCppModelManager
{
    /// <summary>
    /// Gets the default directory where whisper.cpp models are stored.
    /// Typically ~/.cache/whisper/ to share with other whisper.cpp tools.
    /// </summary>
    string DefaultModelDirectory { get; }

    /// <summary>
    /// Lists all available whisper.cpp models from the catalog.
    /// </summary>
    /// <returns>Collection of available model information.</returns>
    IReadOnlyList<WhisperCppModelInfo> ListAvailableModels();

    /// <summary>
    /// Lists all downloaded models in the model directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of downloaded model information with file paths.</returns>
    Task<IReadOnlyList<WhisperCppDownloadedModel>> ListDownloadedModelsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific model is downloaded.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "base.en", "large-v3").</param>
    /// <returns>True if the model is downloaded; otherwise, false.</returns>
    bool IsModelDownloaded(string modelId);

    /// <summary>
    /// Gets the file path for a downloaded model.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "base.en", "large-v3").</param>
    /// <returns>The full path to the model file, or null if not downloaded.</returns>
    string? GetModelPath(string modelId);

    /// <summary>
    /// Downloads a model from Hugging Face.
    /// </summary>
    /// <param name="modelId">The model identifier (e.g., "base.en", "large-v3").</param>
    /// <param name="progress">Optional callback for download progress (0-100 percentage).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success with the file path, or failure with error message.</returns>
    Task<Result<string>> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about an available whisper.cpp model.
/// </summary>
public sealed record WhisperCppModelInfo
{
    /// <summary>
    /// Gets the model identifier used for selection (e.g., "base.en", "large-v3").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the display name for the model.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the expected file name (e.g., "ggml-base.en.bin").
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets a description of the model's characteristics.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the approximate file size in megabytes.
    /// </summary>
    public required int SizeMb { get; init; }

    /// <summary>
    /// Gets the Hugging Face download URL.
    /// </summary>
    public required string DownloadUrl { get; init; }

    /// <summary>
    /// Gets whether this is an English-only model (faster, more accurate for English).
    /// </summary>
    public bool EnglishOnly { get; init; }

    /// <summary>
    /// Gets whether this is a recommended model for general use.
    /// </summary>
    public bool Recommended { get; init; }
}

/// <summary>
/// Information about a downloaded whisper.cpp model.
/// </summary>
public sealed record WhisperCppDownloadedModel
{
    /// <summary>
    /// Gets the model information.
    /// </summary>
    public required WhisperCppModelInfo Model { get; init; }

    /// <summary>
    /// Gets the full file path to the downloaded model.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the actual file size in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }
}
