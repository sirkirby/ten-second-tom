using System.Collections.Frozen;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Setup.Models;

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
            Id: LlmConstants.OpenAIModels.Gpt4oMini,
            DisplayName: LlmConstants.DisplayNames.Gpt4oMini,
            Provider: LlmProvider.OpenAI,
            CostTier: LlmConstants.CostTiers.Budget,
            Description: LlmConstants.Descriptions.Gpt4oMini,
            IsDefault: true),
        new SupportedModel(
            Id: LlmConstants.OpenAIModels.Gpt4o,
            DisplayName: LlmConstants.DisplayNames.Gpt4o,
            Provider: LlmProvider.OpenAI,
            CostTier: LlmConstants.CostTiers.Balanced,
            Description: LlmConstants.Descriptions.Gpt4o,
            IsDefault: false),
        new SupportedModel(
            Id: LlmConstants.OpenAIModels.ChatGpt4oLatest,
            DisplayName: LlmConstants.DisplayNames.ChatGpt4oLatest,
            Provider: LlmProvider.OpenAI,
            CostTier: LlmConstants.CostTiers.Balanced,
            Description: LlmConstants.Descriptions.ChatGpt4oLatest,
            IsDefault: false)
    ];

    // Initialize Anthropic models (Claude 4 series with budget, balanced, and premium options)
    private static readonly List<SupportedModel> _anthropicModels =
    [
        // Budget tier
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.Claude3Haiku,
            DisplayName: LlmConstants.DisplayNames.Claude3Haiku,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Budget,
            Description: LlmConstants.Descriptions.Claude3Haiku,
            IsDefault: true),
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.Claude35Haiku,
            DisplayName: LlmConstants.DisplayNames.Claude35Haiku,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Budget,
            Description: LlmConstants.Descriptions.Claude35Haiku,
            IsDefault: false),
        // Balanced tier
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeSonnet4,
            DisplayName: LlmConstants.DisplayNames.ClaudeSonnet4,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Balanced,
            Description: LlmConstants.Descriptions.ClaudeSonnet4,
            IsDefault: false),
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeSonnet45,
            DisplayName: LlmConstants.DisplayNames.ClaudeSonnet45,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Balanced,
            Description: LlmConstants.Descriptions.ClaudeSonnet45,
            IsDefault: false),
        // Premium tier
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeOpus4,
            DisplayName: LlmConstants.DisplayNames.ClaudeOpus4,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Premium,
            Description: LlmConstants.Descriptions.ClaudeOpus4,
            IsDefault: false),
        new SupportedModel(
            Id: LlmConstants.AnthropicModels.ClaudeOpus41,
            DisplayName: LlmConstants.DisplayNames.ClaudeOpus41,
            Provider: LlmProvider.Anthropic,
            CostTier: LlmConstants.CostTiers.Premium,
            Description: LlmConstants.Descriptions.ClaudeOpus41,
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
