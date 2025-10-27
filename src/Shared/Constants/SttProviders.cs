namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides constants representing supported Speech-to-Text providers.
/// </summary>
public static class SttProviders
{
    /// <summary>
    /// Local whisper.cpp provider (requires installation, no API key needed).
    /// </summary>
    public const string WhisperCpp = "whisper-cpp";

    /// <summary>
    /// Default whisper.cpp STT model to use.
    /// </summary>
    public const string WhisperCppDefaultSTTModel = "ggml-base.en.bin";

    /// <summary>
    /// OpenAI Whisper API provider (cloud-based, requires API key).
    /// </summary>
    public const string OpenAI = "openai";

    /// <summary>
    /// OpenAI Speech-to-Text model to use.
    /// </summary>
    public const string OpenAIDefaultSTTModel = "whisper-1";

    /// <summary>
    /// All supported STT provider names (lowercase for normalization).
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        WhisperCpp,
        OpenAI
    ];

    /// <summary>
    /// Checks if a provider requires an API key.
    /// </summary>
    /// <param name="provider">The STT provider name</param>
    /// <returns>True if the provider requires an API key, false otherwise</returns>
    public static bool RequiresApiKey(string provider)
    {
        return provider?.ToLowerInvariant() switch
        {
            OpenAI => true,
            WhisperCpp => false,
            _ => false
        };
    }

    /// <summary>
    /// Checks if a provider supports fallback to a secondary provider.
    /// </summary>
    /// <param name="provider">The STT provider name</param>
    /// <returns>True if the provider can enable a fallback provider, false otherwise</returns>
    public static bool SupportsFallback(string provider)
    {
        return provider?.ToLowerInvariant() switch
        {
            WhisperCpp => true,  // Can fallback to cloud providers
            OpenAI => false,      // Fallback not yet supported for cloud-based providers
            _ => false
        };
    }
}
