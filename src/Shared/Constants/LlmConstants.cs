namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Constants for LLM model identifiers, display names, and cost tiers.
/// </summary>
internal static class LlmConstants
{
    /// <summary>
    /// Cost tier categories for models.
    /// </summary>
    internal static class CostTiers
    {
        internal const string Budget = "Budget";
        internal const string Balanced = "Balanced";
        internal const string Premium = "Premium";
    }

    /// <summary>
    /// OpenAI model identifiers
    /// </summary>
    internal static class OpenAIModels
    {
        internal const string Gpt4oMini = "gpt-4o-mini";
        internal const string Gpt4o = "gpt-4o";
        internal const string Gpt35Turbo = "gpt-3.5-turbo";
    }

    /// <summary>
    /// Anthropic model identifiers
    /// </summary>
    internal static class AnthropicModels
    {
        internal const string Claude35Haiku = "claude-3-5-haiku-20241022";
        internal const string Claude35Sonnet = "claude-3-5-sonnet-20241022";
        internal const string Claude3Opus = "claude-3-opus-20240229";
    }

    /// <summary>
    /// Display names for models shown in UI
    /// </summary>
    internal static class DisplayNames
    {
        internal const string Gpt4oMini = "GPT-4o Mini";
        internal const string Gpt4o = "GPT-4o";
        internal const string Gpt35Turbo = "GPT-3.5 Turbo";

        internal const string Claude35Haiku = "Claude 3.5 Haiku";
        internal const string Claude35Sonnet = "Claude 3.5 Sonnet";
        internal const string Claude3Opus = "Claude 3 Opus";
    }

    /// <summary>
    /// Descriptions of model capabilities
    /// </summary>
    internal static class Descriptions
    {
        internal const string Gpt4oMini = "Fast and economical for most tasks";
        internal const string Gpt4o = "Best balance of cost and capability";
        internal const string Gpt35Turbo = "Lowest cost option for simple tasks";

        internal const string Claude35Haiku = "Fast and economical for straightforward tasks";
        internal const string Claude35Sonnet = "Latest Sonnet with excellent performance";
        internal const string Claude3Opus = "Most capable model with highest quality";
    }
}
