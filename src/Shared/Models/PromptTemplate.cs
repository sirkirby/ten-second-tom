using TenSecondTom.Shared.Constants;

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

    /// <summary>
    /// Gets the source of the template (embedded, filesystem, etc.).
    /// </summary>
    public TemplateSource? Source { get; init; }

    /// <summary>
    /// Gets optional metadata parsed from template YAML front matter.
    /// </summary>
    public TemplateMetadata? Metadata { get; init; }
}

/// <summary>
/// Defines the types of prompt templates.
/// </summary>
public enum TemplateType
{
    /// <summary>
    /// Template for daily summary generation.
    /// </summary>
    Daily,

    /// <summary>
    /// Alias for daily summary (backward compatibility).
    /// </summary>
    DailySummary = Daily,

    /// <summary>
    /// Template for weekly review generation.
    /// </summary>
    Weekly,

    /// <summary>
    /// Alias for weekly summary (backward compatibility).
    /// </summary>
    WeeklySummary = Weekly,

    /// <summary>
    /// System-level prompt template.
    /// </summary>
    SystemPrompt
}

/// <summary>
/// Defines the source of a template.
/// </summary>
public enum TemplateSource
{
    /// <summary>
    /// Template is embedded in the application resources.
    /// </summary>
    Embedded,

    /// <summary>
    /// Template is loaded from the filesystem.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Template is user-created or custom.
    /// </summary>
    Custom
}

/// <summary>
/// Metadata parsed from template YAML front matter.
/// </summary>
public sealed class TemplateMetadata
{
    /// <summary>
    /// Gets or sets the template type from YAML.
    /// </summary>
    public TemplateType TemplateType { get; init; }

    /// <summary>
    /// Gets or sets the display title of the template.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets optional description from YAML.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets optional version string.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets or sets optional author information.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Gets or sets the date the template was created.
    /// </summary>
    public DateTime? CreatedDate { get; init; }

    /// <summary>
    /// Gets or sets optional tags for categorizing templates.
    /// </summary>
#pragma warning disable CA1819 // Properties should not return arrays - part of public contract
    public string[]? Tags { get; init; }
#pragma warning restore CA1819

    /// <summary>
    /// Validates the metadata structure and returns validation errors.
    /// </summary>
    /// <returns>
    /// Empty list if valid; otherwise, list of validation error messages.
    /// </returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        // Required field validation
        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Title is required");
        else if (Title.Length > TemplateConstants.MaxTitleLength)
            errors.Add($"Title must be {TemplateConstants.MaxTitleLength} characters or less");

        if (!Enum.IsDefined(TemplateType))
            errors.Add($"Invalid template type: {TemplateType}");

        // Optional field validation
        if (Description?.Length > TemplateConstants.MaxDescriptionLength)
            errors.Add($"Description must be {TemplateConstants.MaxDescriptionLength} characters or less");

        if (Author?.Length > TemplateConstants.MaxAuthorLength)
            errors.Add($"Author must be {TemplateConstants.MaxAuthorLength} characters or less");

        if (Tags?.Length > TemplateConstants.MaxTagsCount)
            errors.Add($"Maximum {TemplateConstants.MaxTagsCount} tags allowed");

        if (Tags?.Any(tag => tag.Length > TemplateConstants.MaxTagLength) == true)
            errors.Add($"Each tag must be {TemplateConstants.MaxTagLength} characters or less");

        return errors.AsReadOnly();
    }
}
