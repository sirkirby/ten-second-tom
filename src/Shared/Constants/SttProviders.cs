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
    /// Ten Second Tom built-in local engine (powered by Microsoft AI Foundry Local).
    /// </summary>
    public const string BuiltInLocal = "built-in-local";

    /// <summary>
    /// All supported provider names (lowercase for normalization).
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        WhisperCpp,
        OpenAI,
        BuiltInLocal
    ];
}
