using System.Collections.Frozen;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Centralized registry of all supported LLM models.
/// Provides validation and lookup capabilities for model selection.
/// </summary>
public static class ModelRegistry
{
    // Initialize OpenAI models
    private static readonly List<SupportedModel> _openAIModels =
    [
        new SupportedModel(
            Id: LlmConstants.OpenAIModels.GPTNano,
            DisplayName: LlmConstants.OpenAIModels.GPTNanoDisplayName,
            Provider: LlmProvider.OpenAI,
            CostTier: LlmConstants.CostTiers.Budget,
            Description: LlmConstants.OpenAIModels.GPTNanoDescription,
            IsDefault: false),
        new SupportedModel(
            Id: LlmConstants.OpenAIModels.GPTMini,
            DisplayName: LlmConstants.OpenAIModels.GPTMiniDisplayName,
            Provider: LlmProvider.OpenAI,
            CostTier: LlmConstants.CostTiers.Balanced,
            Description: LlmConstants.OpenAIModels.GPTMiniDescription,
            IsDefault: true),
        new SupportedModel(
            Id: LlmConstants.OpenAIModels.GPTStandard,
            DisplayName: LlmConstants.OpenAIModels.GPTStandardDisplayName,
            Provider: LlmProvider.OpenAI,
            CostTier: LlmConstants.CostTiers.Premium,
            Description: LlmConstants.OpenAIModels.GPTStandardDescription,
            IsDefault: false)
    ];

        // Initialize Anthropic models (Claude 4 series)
    private static readonly List<SupportedModel> _anthropicModels =
    [
        // Budget tier
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeHaiku,
            DisplayName: LlmConstants.AnthropicModels.ClaudeHaikuDisplayName,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Budget,
            Description: LlmConstants.AnthropicModels.ClaudeHaikuDescription,
            IsDefault: true),
        // Balanced tier
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeSonnet,
            DisplayName: LlmConstants.AnthropicModels.ClaudeSonnetDisplayName,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Balanced,
            Description: LlmConstants.AnthropicModels.ClaudeSonnetDescription,
            IsDefault: false),
        // Premium tier
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeOpus,
            DisplayName: LlmConstants.AnthropicModels.ClaudeOpusDisplayName,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Premium,
            Description: LlmConstants.AnthropicModels.ClaudeOpusDescription,
            IsDefault: false)
    ];

    /// <summary>
    /// Gets all supported OpenAI models.
    /// </summary>
    public static IReadOnlyList<SupportedModel> OpenAIModels { get; } = _openAIModels.AsReadOnly();

    /// <summary>
    /// Gets all supported Anthropic models.
    /// </summary>
    public static IReadOnlyList<SupportedModel> AnthropicModels { get; } = _anthropicModels.AsReadOnly();

    /// <summary>
    /// Gets all supported models across all providers.
    /// </summary>
    public static IReadOnlyList<SupportedModel> AllModels { get; } =
        _openAIModels.Concat(_anthropicModels).ToList().AsReadOnly();

    private static readonly FrozenDictionary<string, SupportedModel> ModelsById =
        AllModels.ToFrozenDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<LlmProvider, SupportedModel> DefaultsByProvider =
        AllModels.Where(m => m.IsDefault).ToFrozenDictionary(m => m.Provider);

    /// <summary>
    /// Gets the default model for the specified provider.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <returns>The default model for the provider.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no default model is configured for the provider.</exception>
    public static SupportedModel GetDefault(LlmProvider provider)
    {
        if (!DefaultsByProvider.TryGetValue(provider, out SupportedModel? model))
        {
            throw new InvalidOperationException(
                $"No default model configured for provider: {provider}");
        }

        return model;
    }

    /// <summary>
    /// Validates whether the specified model ID is supported for the given provider.
    /// </summary>
    /// <param name="modelId">The model identifier to validate.</param>
    /// <param name="provider">The LLM provider.</param>
    /// <returns><c>true</c> if the model is valid for the provider; otherwise, <c>false</c>.</returns>
    public static bool IsValid(string modelId, LlmProvider provider)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        if (!ModelsById.TryGetValue(modelId, out SupportedModel? model))
        {
            return false;
        }

        return model.Provider == provider;
    }

    /// <summary>
    /// Retrieves a model by its identifier.
    /// </summary>
    /// <param name="modelId">The model identifier.</param>
    /// <returns>The supported model if found; otherwise, <c>null</c>.</returns>
    public static SupportedModel? GetById(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return null;
        }

        return ModelsById.GetValueOrDefault(modelId);
    }

    /// <summary>
    /// Gets all models for the specified provider.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <returns>A read-only list of models for the provider.</returns>
    public static IReadOnlyList<SupportedModel> GetByProvider(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.OpenAI => OpenAIModels,
            LlmProvider.Anthropic => AnthropicModels,
            _ => Array.Empty<SupportedModel>()
        };
    }
}
