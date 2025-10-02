namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a prompt template for LLM interactions.
/// Templates use {{VARIABLE_NAME}} syntax for variable substitution.
/// </summary>
public record PromptTemplate
{
    /// <summary>
    /// Gets the unique identifier for the template (kebab-case).
    /// Examples: "daily-summary-v1", "weekly-review-v2"
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the template content with variable placeholders.
    /// Variables should be in the format: {{VARIABLE_NAME}}
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the type of prompt template.
    /// </summary>
    public required TemplateType TemplateType { get; init; }

    /// <summary>
    /// Gets optional description of the template's purpose.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Defines the types of prompt templates.
/// </summary>
public enum TemplateType
{
    /// <summary>
    /// Template for daily summary generation.
    /// </summary>
    DailySummary,

    /// <summary>
    /// Template for weekly review generation.
    /// </summary>
    WeeklySummary,

    /// <summary>
    /// System-level prompt template.
    /// </summary>
    SystemPrompt
}
