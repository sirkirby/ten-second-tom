/// <summary>
/// Represents YAML front matter metadata from a prompt template file.
/// </summary>
/// <remarks>
/// Parsed from YAML front matter delimited by "---" at the beginning of .md files.
///
/// Example YAML:
/// ---
/// templateType: daily
/// title: Daily Summary
/// description: Default template for daily journal entries
/// version: 1.0
/// author: John Doe
/// ---
///
/// Required fields: templateType, title
/// Optional fields: description, version, author, createdDate, tags
/// </remarks>
public sealed record TemplateMetadata
{
    /// <summary>
    /// Gets the type of template (daily or weekly).
    /// </summary>
    /// <remarks>
    /// Required field. Must be "daily" or "weekly" (case-insensitive in YAML).
    /// Used to filter templates by command context.
    /// </remarks>
    public required TemplateType TemplateType { get; init; }

    /// <summary>
    /// Gets the display title for the template.
    /// </summary>
    /// <remarks>
    /// Required field. Max 200 characters.
    /// Shown in template selection UI.
    /// Should be concise and descriptive.
    /// </remarks>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the optional description of the template's purpose.
    /// </summary>
    /// <remarks>
    /// Optional field. Max 500 characters.
    /// Shown in template selection UI alongside title.
    /// Helps users choose between similar templates.
    /// </remarks>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the template version (semantic versioning recommended).
    /// </summary>
    /// <remarks>
    /// Optional field. Default "1.0" if not specified.
    /// Used to track template changes over time.
    /// Advisory warning logged if not in semver format.
    /// </remarks>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the author name for custom templates.
    /// </summary>
    /// <remarks>
    /// Optional field. Max 100 characters.
    /// Not used by default templates.
    /// Useful for shared custom templates.
    /// </remarks>
    public string? Author { get; init; }

    /// <summary>
    /// Gets the date the template was created.
    /// </summary>
    /// <remarks>
    /// Optional field. ISO 8601 format in YAML.
    /// Not used in v1 but reserved for future features.
    /// </remarks>
    public DateTime? CreatedDate { get; init; }

    /// <summary>
    /// Gets optional tags for categorizing templates.
    /// </summary>
    /// <remarks>
    /// Optional field. Max 20 tags, each max 50 characters.
    /// Reserved for future categorization features.
    /// Not used in v1 - included for forward compatibility.
    /// </remarks>
    public string[]? Tags { get; init; }

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
        else if (Title.Length > 200)
            errors.Add("Title must be 200 characters or less");

        if (!Enum.IsDefined(typeof(TemplateType), TemplateType))
            errors.Add($"Invalid template type: {TemplateType}");

        // Optional field validation
        if (Description?.Length > 500)
            errors.Add("Description must be 500 characters or less");

        if (Author?.Length > 100)
            errors.Add("Author must be 100 characters or less");

        if (Tags is { Count: > 20 })
            errors.Add("Maximum 20 tags allowed");

        if (Tags?.Any(tag => tag.Length > 50) == true)
            errors.Add("Each tag must be 50 characters or less");

        return errors.AsReadOnly();
    }
}

/// <summary>
/// Enum defining valid template types.
/// </summary>
/// <remarks>
/// Maps to YAML string values (case-insensitive):
/// - "daily" -> TemplateType.Daily
/// - "weekly" -> TemplateType.Weekly
///
/// Future types can be added without breaking changes.
/// </remarks>
public enum TemplateType
{
    /// <summary>
    /// Template for daily summary generation (used with "today" command).
    /// </summary>
    Daily,

    /// <summary>
    /// Template for weekly review generation (used with "thisweek" command).
    /// </summary>
    Weekly
}
