using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Setup.Services;

/// <summary>
/// Validates LLM model configurations using the ModelRegistry.
/// </summary>
public static class ModelValidator
{
    /// <summary>
    /// Validates whether the specified model is valid for the given provider.
    /// </summary>
    /// <param name="modelId">The model identifier to validate.</param>
    /// <param name="provider">The LLM provider.</param>
    /// <returns>A tuple indicating whether the model is valid and an error message if invalid.</returns>
    public static (bool IsValid, string? ErrorMessage) Validate(string? modelId, LlmProvider provider)
    {
        // Null or empty model is acceptable - defaults will be used
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return (true, null);
        }

        // Check if model exists in registry
        SupportedModel? model = ModelRegistry.GetById(modelId);
        if (model is null)
        {
            IReadOnlyList<SupportedModel> validModels = ModelRegistry.GetByProvider(provider);
            string validModelsList = string.Join(", ", validModels.Select(m => $"'{m.Id}'"));
            return (false, $"Model '{modelId}' is not recognized. Valid models for {provider}: {validModelsList}");
        }

        // Check if model belongs to the specified provider
        if (model.Provider != provider)
        {
            return (false, 
                $"Model '{modelId}' belongs to {model.Provider} but provider is set to {provider}. " +
                $"Run 'tom config llm' to select a compatible model.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates whether the specified model is valid for the given provider and throws if invalid.
    /// </summary>
    /// <param name="modelId">The model identifier to validate.</param>
    /// <param name="provider">The LLM provider.</param>
    /// <exception cref="InvalidOperationException">Thrown when the model is invalid for the provider.</exception>
    public static void ValidateOrThrow(string? modelId, LlmProvider provider)
    {
        (bool isValid, string? errorMessage) = Validate(modelId, provider);
        
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    /// <summary>
    /// Gets the model to use, applying defaults if necessary.
    /// </summary>
    /// <param name="modelId">The configured model identifier (may be null/empty).</param>
    /// <param name="provider">The LLM provider.</param>
    /// <returns>The model ID to use (either the provided one or the default).</returns>
    public static string GetEffectiveModel(string? modelId, LlmProvider provider)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return ModelRegistry.GetDefault(provider).Id;
        }

        // Validate the provided model
        ValidateOrThrow(modelId, provider);
        return modelId;
    }
}
