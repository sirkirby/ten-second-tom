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
    /// <param name="configuration">The audio configuration containing provider settings.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// The selected STT provider instance, or null if no suitable provider is available.
    /// </returns>
    /// <remarks>
    /// Selection logic based on SttProvider setting:
    /// <list type="bullet">
    /// <item>"built-in-local": Uses Microsoft AI Foundry Local SDK for transcription (default).</item>
    /// <item>"whisper-cpp": Uses external whisper.cpp binary installation.</item>
    /// <item>"openai": Uses OpenAI Whisper API (requires API key).</item>
    /// </list>
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
