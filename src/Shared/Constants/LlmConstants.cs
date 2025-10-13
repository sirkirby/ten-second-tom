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
    /// Using latest available models from OpenAI API
    /// </summary>
    internal static class OpenAIModels
    {
        internal const string ChatGpt4oLatest = "chatgpt-4o-latest";
        internal const string Gpt4o = "gpt-4o-2024-11-20";
        internal const string Gpt4oMini = "gpt-4o-mini-2024-07-18";
    }

    /// <summary>
    /// Anthropic model identifiers (Claude 4 series)
    /// Using dated versions for API stability
    /// </summary>
    internal static class AnthropicModels
    {
        // Claude 4 Series - Latest
        internal const string ClaudeSonnet45 = "claude-sonnet-4-5-20250929";
        internal const string ClaudeOpus41 = "claude-opus-4-1-20250805";
        internal const string Claude35Haiku = "claude-3-5-haiku-20241022";
        
        // Claude 4.0 Series - Stable
        internal const string ClaudeSonnet4 = "claude-sonnet-4-20250514";
        internal const string ClaudeOpus4 = "claude-opus-4-20250514";
        
        // Claude 3 Series - Legacy Budget
        internal const string Claude3Haiku = "claude-3-haiku-20240307";
    }

    /// <summary>
    /// Display names for models shown in UI
    /// </summary>
    internal static class DisplayNames
    {
        // OpenAI Models
        internal const string ChatGpt4oLatest = "ChatGPT-4o (Latest)";
        internal const string Gpt4o = "GPT-4o (2024-11-20)";
        internal const string Gpt4oMini = "GPT-4o Mini (2024-07-18)";

        // Anthropic Claude 4 Series
        internal const string ClaudeSonnet45 = "Claude Sonnet 4.5 (2025-09-29)";
        internal const string ClaudeOpus41 = "Claude Opus 4.1 (2025-08-05)";
        internal const string Claude35Haiku = "Claude 3.5 Haiku (2024-10-22)";
        internal const string ClaudeSonnet4 = "Claude Sonnet 4.0 (2025-05-14)";
        internal const string ClaudeOpus4 = "Claude Opus 4.0 (2025-05-14)";
        internal const string Claude3Haiku = "Claude 3 Haiku (2024-03-07)";
    }

    /// <summary>
    /// Descriptions of model capabilities
    /// </summary>
    internal static class Descriptions
    {
        // OpenAI Models
        internal const string ChatGpt4oLatest = "Always points to the newest ChatGPT-4o version";
        internal const string Gpt4o = "High-performance model with excellent reasoning";
        internal const string Gpt4oMini = "Fast, affordable, and efficient for most tasks";

        // Anthropic Claude 4 Series
        internal const string ClaudeSonnet45 = "Best model for complex agents and coding with highest intelligence";
        internal const string ClaudeOpus41 = "Exceptional model for specialized complex tasks requiring advanced reasoning";
        internal const string Claude35Haiku = "Fast and compact model for near-instant responsiveness";
        internal const string ClaudeSonnet4 = "High-performance model with balanced capabilities";
        internal const string ClaudeOpus4 = "Very high intelligence and capability for specialized tasks";
        internal const string Claude3Haiku = "Quick and accurate targeted performance at lowest cost";
    }
}
