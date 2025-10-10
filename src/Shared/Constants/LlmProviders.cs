namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides constants representing supported Large Language Model providers.
/// </summary>
public static class LlmProviders
{
    /// <summary>
    /// OpenAI provider name.
    /// </summary>
    public const string OpenAI = "openai";

    /// <summary>
    /// Anthropic provider name.
    /// </summary>
    public const string Anthropic = "anthropic";

    /// <summary>
    /// All supported provider names (lowercase for normalization).
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        OpenAI,
        Anthropic
    ];
}
