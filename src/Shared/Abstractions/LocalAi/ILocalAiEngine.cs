using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.LocalAi;

/// <summary>
/// Defines the contract for the local AI engine powered by Microsoft AI Foundry Local.
/// Encapsulates operations for LLM completion, streaming, and audio transcription.
/// </summary>
public interface ILocalAiEngine
{
    /// <summary>
    /// Generates a text completion for the given prompt.
    /// </summary>
    /// <param name="modelId">The logical model ID (e.g., "openai/gpt-oss-20b").</param>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="maxTokens">Optional maximum tokens.</param>
    /// <param name="temperature">Optional temperature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated completion text.</returns>
    Task<Result<string>> CompleteAsync(
        string modelId,
        string prompt,
        int? maxTokens = null,
        double? temperature = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes an audio file to text.
    /// </summary>
    /// <param name="modelId">The logical model ID (e.g., "openai/whisper").</param>
    /// <param name="audioFilePath">Path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transcription result.</returns>
    Task<Result<TranscriptionResult>> TranscribeAsync(
        string modelId,
        string audioFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures that the specified model is available (downloaded and cached).
    /// </summary>
    /// <param name="modelId">The logical model ID.</param>
    /// <param name="progress">Optional callback for download progress (0-100 percentage).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> EnsureModelAvailableAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the available models in the catalog.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available model information.</returns>
    Task<IEnumerable<string>> ListAvailableModelsAsync(CancellationToken cancellationToken = default);
}
