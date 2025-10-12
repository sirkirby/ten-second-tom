using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Represents a curated LLM model with metadata for display and validation.
/// </summary>
/// <param name="Id">Unique model identifier used by the provider API (e.g., "gpt-4o-mini").</param>
/// <param name="DisplayName">Human-readable name shown in UI (e.g., "GPT-4o Mini").</param>
/// <param name="Provider">The LLM provider this model belongs to.</param>
/// <param name="CostTier">Cost category: "Budget", "Balanced", or "Premium".</param>
/// <param name="Description">Brief description of model capabilities (max 100 chars).</param>
/// <param name="IsDefault">Whether this is the default model for its provider.</param>
public sealed record SupportedModel(
    string Id,
    string DisplayName,
    LlmProvider Provider,
    string CostTier,
    string Description,
    bool IsDefault = false)
{
    /// <summary>
    /// Gets the unique model identifier used by the provider API.
    /// </summary>
    public string Id { get; init; } = ValidateId(Id);

    /// <summary>
    /// Gets the human-readable name shown in UI.
    /// </summary>
    public string DisplayName { get; init; } = ValidateDisplayName(DisplayName);

    /// <summary>
    /// Gets the LLM provider this model belongs to.
    /// </summary>
    public LlmProvider Provider { get; init; } = Provider;

    /// <summary>
    /// Gets the cost category.
    /// </summary>
    public string CostTier { get; init; } = ValidateCostTier(CostTier);

    /// <summary>
    /// Gets the brief description of model capabilities.
    /// </summary>
    public string Description { get; init; } = ValidateDescription(Description);

    /// <summary>
    /// Gets a value indicating whether this is the default model for its provider.
    /// </summary>
    public bool IsDefault { get; init; } = IsDefault;

    private static string ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Model ID cannot be null or whitespace.", nameof(id));
        }

        return id;
    }

    private static string ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be null or whitespace.", nameof(displayName));
        }

        return displayName;
    }

    private static string ValidateCostTier(string costTier)
    {
        if (string.IsNullOrWhiteSpace(costTier))
        {
            throw new ArgumentException("Cost tier cannot be null or whitespace.", nameof(costTier));
        }

        var validTiers = new[] { "Budget", "Balanced", "Premium" };
        if (!validTiers.Contains(costTier))
        {
            throw new ArgumentException(
                $"Cost tier must be one of: {string.Join(", ", validTiers)}. Got: {costTier}",
                nameof(costTier));
        }

        return costTier;
    }

    private static string ValidateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description cannot be null or whitespace.", nameof(description));
        }

        if (description.Length > 100)
        {
            throw new ArgumentException(
                $"Description must be 100 characters or less. Got {description.Length} characters.",
                nameof(description));
        }

        return description;
    }
}
