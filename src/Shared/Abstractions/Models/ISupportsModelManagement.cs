using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.Models;

/// <summary>
/// Interface for providers that support model management operations.
/// </summary>
public interface ISupportsModelManagement
{
    /// <summary>
    /// Lists the models available for this provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of model identifiers.</returns>
    Task<IEnumerable<string>> ListModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a specific model for this provider.
    /// </summary>
    /// <param name="modelId">The identifier of the model to download.</param>
    /// <param name="progress">Optional callback for download progress (0-100 percentage).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default);
}
