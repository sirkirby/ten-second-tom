using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Factory for creating and selecting STT provider instances.
/// Implements the STT selection strategy (auto, local, or remote).
/// </summary>
public interface ISttProviderFactory
{
    /// <summary>
    /// Gets the appropriate STT provider based on the selection strategy.
    /// </summary>
    /// <param name="selection">The STT selection strategy to use.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// The selected STT provider instance, or null if no suitable provider is available.
    /// </returns>
    /// <remarks>
    /// Selection logic:
    /// - <see cref="SttSelection.Auto"/>: Try local first, fallback to OpenAI if unavailable.
    /// - <see cref="SttSelection.Local"/>: Return local provider only (null if unavailable).
    /// - <see cref="SttSelection.OpenAI"/>: Return OpenAI provider only.
    /// </remarks>
    Task<ISttProvider?> GetProviderAsync(
        SttSelection selection = SttSelection.Auto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific STT provider by engine type.
    /// </summary>
    /// <param name="engine">The STT engine type.</param>
    /// <returns>The STT provider for the specified engine.</returns>
    ISttProvider GetProvider(SttEngine engine);
}
