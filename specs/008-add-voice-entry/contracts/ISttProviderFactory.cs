using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Factory for creating and selecting STT providers based on configuration and availability.
/// Handles the auto/local/openai selection strategy with fallback logic.
/// </summary>
public interface ISttProviderFactory
{
    /// <summary>
    /// Gets an STT provider based on the selection strategy.
    /// </summary>
    /// <param name="selection">
    /// The STT selection strategy:
    /// - Auto: Try local first, fallback to OpenAI if unavailable
    /// - Local: Return local provider only (fail if unavailable)
    /// - OpenAI: Return OpenAI provider only
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result containing the selected ISttProvider on success.
    /// Result with error message if no suitable provider is available.
    /// </returns>
    /// <remarks>
    /// For Auto selection:
    /// 1. Check if local provider is available
    /// 2. If yes, return local provider
    /// 3. If no, check if OpenAI provider is available
    /// 4. If yes, return OpenAI provider
    /// 5. If no, return error indicating no providers available
    /// </remarks>
    Task<Result<ISttProvider>> GetProviderAsync(
        SttSelection selection,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific STT provider by engine type.
    /// </summary>
    /// <param name="engine">The STT engine type (Local or OpenAI).</param>
    /// <returns>
    /// The requested ISttProvider implementation.
    /// Throws InvalidOperationException if the engine type is not supported.
    /// </returns>
    ISttProvider GetProvider(SttEngine engine);
}

