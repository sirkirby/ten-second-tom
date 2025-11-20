namespace TenSecondTom.Shared.Models;

/// <summary>
/// Supported LLM providers
/// </summary>
public enum LlmProvider
{
    /// <summary>
    /// OpenAI (GPT models)
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic (Claude).
    /// </summary>
    Anthropic,

    /// <summary>
    /// Local OpenAI-compatible provider (e.g., llama.cpp, Ollama, LM Studio).
    /// </summary>
    LocalOpenAiCompatible
}
