using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Factory for creating and selecting STT provider instances.
/// Implements the STT selection strategy based on configuration.
/// </summary>
public interface ISttProviderFactory
{
    /// <summary>
    /// Gets the appropriate STT provider based on the audio configuration.
    /// </summary>
    /// <param name="configuration">The audio configuration containing provider and fallback settings.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// The selected STT provider instance, or null if no suitable provider is available.
    /// </returns>
    /// <remarks>
    /// Selection logic:
    /// - If SttProvider is "whisper-cpp" and SttFallbackEnabled is true: Try local first, fallback to configured provider if unavailable.
    /// - If SttProvider is "whisper-cpp" and SttFallbackEnabled is false: Return local provider only (null if unavailable).
    /// - If SttProvider is "openai": Return OpenAI provider only.
    /// </remarks>
    Task<ISttProvider?> GetProviderAsync(
        AudioOptions configuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific STT provider by engine type.
    /// </summary>
    /// <param name="engine">The STT engine type.</param>
    /// <returns>The STT provider for the specified engine.</returns>
    ISttProvider GetProvider(SttEngine engine);
}
