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
        internal const string GPTStandard = "gpt-5";
        internal const string GPTStandardDisplayName = "GPT-5 Standard";
        internal const string GPTStandardDescription = "Flagship model for coding, reasoning, and agentic tasks";
        internal const string GPTMini = "gpt-5-mini";
        internal const string GPTMiniDisplayName = "GPT-5 Mini";
        internal const string GPTMiniDescription = "Faster, cost-efficient version for well-defined tasks";
        internal const string GPTNano = "gpt-5-nano";
        internal const string GPTNanoDisplayName = "GPT-5 Nano";
        internal const string GPTNanoDescription = "Fastest, cheapest model for summarization and classification";
    }

    /// <summary>
    /// Anthropic model identifiers (Claude 4 series)
    /// Using dated versions for API stability
    /// </summary>
    internal static class AnthropicModels
    {
        internal const string ClaudeSonnet = "claude-sonnet-4-5";
        internal const string ClaudeSonnetDisplayName = "Claude Sonnet 4.5";
        internal const string ClaudeSonnetDescription = "Best model for complex agents and coding with highest intelligence";
        internal const string ClaudeOpus = "claude-opus-4-1";
        internal const string ClaudeOpusDisplayName = "Claude Opus 4.1";
        internal const string ClaudeOpusDescription = "Exceptional model for specialized complex tasks requiring advanced reasoning";
        internal const string ClaudeHaiku = "claude-haiku-4-5";
        internal const string ClaudeHaikuDisplayName = "Claude Haiku 4.5";
        internal const string ClaudeHaikuDescription = "Fast and compact model for near-instant responsiveness";
    }

    /// <summary>
    /// Default maximum input tokens for OpenAI models.
    /// All current OpenAI models (GPT-4o, GPT-4o-mini, o1 series) support 128K context windows.
    /// This limit is set to 50K to leave buffer for output tokens and system prompts.
    /// </summary>
    public const int DefaultMaxInputTokensOpenAI = 50_000;

    /// <summary>
    /// Default maximum input tokens for Anthropic models.
    /// All Anthropic models (Haiku, Sonnet, Opus across 3.x and 4.x series) support 200K context windows.
    /// This limit is set to 80K to leave buffer for output tokens and system prompts.
    /// Note: Some models like Sonnet 4.0 can use extended context (1M tokens) via API at higher cost.
    /// </summary>
    public const int DefaultMaxInputTokensAnthropic = 80_000;

    /// <summary>
    /// Token estimation multiplier (conservative).
    /// Estimated tokens = words * TokensPerWord
    /// Based on typical English text tokenization.
    /// </summary>
    public const double TokensPerWord = 1.3;

    /// <summary>
    /// Truncation safety factor (keep input at 80% of limit).
    /// Provides buffer for template content and prompt formatting.
    /// </summary>
    public const double TruncationSafetyFactor = 0.8;

    /// <summary>
    /// Maximum transcript file size in bytes (100 MB).
    /// Files larger than this are likely corrupted or invalid.
    /// </summary>
    public const long MaxTranscriptFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    /// <summary>
    /// Maximum output file size in bytes (10 MB).
    /// Outputs larger than this may indicate processing errors.
    /// </summary>
    public const long MaxOutputFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Context window sizes by provider and model class.
    /// These represent the maximum total tokens (input + output) supported by each model class.
    /// </summary>
    public static class ContextWindows
    {
        /// <summary>
        /// OpenAI models context window (all classes: standard, mini, thinking).
        /// Applies to: GPT-4o, GPT-4o-mini, o1-preview, o1-mini.
        /// </summary>
        public const int OpenAI = 128_000;

        /// <summary>
        /// Anthropic Haiku models context window.
        /// Applies to: Claude 3 Haiku, Claude 3.5 Haiku.
        /// </summary>
        public const int AnthropicHaiku = 200_000;

        /// <summary>
        /// Anthropic Sonnet models context window (standard).
        /// Applies to: Claude 3.5 Sonnet, Claude 4.0 Sonnet, Claude 4.5 Sonnet.
        /// Note: Can be extended to 1M tokens via API for some models.
        /// </summary>
        public const int AnthropicSonnet = 200_000;

        /// <summary>
        /// Anthropic Opus models context window.
        /// Applies to: Claude 3 Opus, Claude 4.0 Opus, Claude 4.1 Opus.
        /// </summary>
        public const int AnthropicOpus = 200_000;
    }

    /// <summary>
    /// Maximum output tokens by provider and model class.
    /// These represent typical output limits for each model class.
    /// </summary>
    public static class MaxOutputTokens
    {
        /// <summary>
        /// OpenAI models maximum output tokens (all classes).
        /// Applies to: GPT-4o, GPT-4o-mini, o1-preview, o1-mini.
        /// </summary>
        public const int OpenAI = 16_384;

        /// <summary>
        /// Anthropic models maximum output tokens (all classes).
        /// Applies to: All Haiku, Sonnet, and Opus models.
        /// </summary>
        public const int Anthropic = 8_192;
    }
}
