using TenSecondTom.Infrastructure.Storage;

namespace TenSecondTom.Features.Generate.Models;

/// <summary>
/// Result of LLM processing with comprehensive metadata.
/// Represents the complete output including content, generation details, and token usage.
/// </summary>
public sealed record GeneratedOutput
{
    /// <summary>
    /// Gets the generated content from LLM.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the base name of the source input (recording or note).
    /// </summary>
    public required string InputName { get; init; }

    /// <summary>
    /// Gets the type of input (Recording or Note).
    /// </summary>
    public required string InputType { get; init; }

    /// <summary>
    /// Gets the template ID used for generation.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the template title for display.
    /// </summary>
    public required string TemplateTitle { get; init; }

    /// <summary>
    /// Gets the timestamp when output was generated.
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Gets the LLM provider used.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the model used for generation.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Gets the number of input tokens consumed.
    /// </summary>
    public required int InputTokens { get; init; }

    /// <summary>
    /// Gets the number of output tokens generated.
    /// </summary>
    public required int OutputTokens { get; init; }

    /// <summary>
    /// Gets whether the input was truncated due to token limits.
    /// </summary>
    public required bool WasTruncated { get; init; }

    /// <summary>
    /// Gets the original transcript word count (before truncation).
    /// </summary>
    public required int OriginalWordCount { get; init; }

    /// <summary>
    /// Gets the output file path where content was saved.
    /// </summary>
    public string? OutputFilePath { get; init; }

    /// <summary>
    /// Gets the total tokens used (input + output).
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Formats the output as markdown with YAML front matter metadata header.
    /// Uses the same format as Today and ThisWeek commands for consistency.
    /// </summary>
    /// <returns>A markdown-formatted string with YAML front matter and content.</returns>
    public string ToMarkdown()
    {
        var frontmatter = new Dictionary<string, object>
        {
            ["entry-id"] = MarkdownFormatter.CreateGenerateEntryId(InputName, TemplateId),
            ["command"] = "generate",
            ["input-name"] = InputName,
            ["input-type"] = InputType,
            ["template-id"] = TemplateId,
            ["template-title"] = TemplateTitle,
            ["timestamp"] = MarkdownFormatter.FormatTimestamp(GeneratedAt),
            ["llm-provider"] = ProviderName,
            ["llm-model"] = ModelName,
            ["tokens-used"] = TotalTokens,
            ["input-tokens"] = InputTokens,
            ["output-tokens"] = OutputTokens,
            ["truncated"] = WasTruncated
        };

        if (WasTruncated)
        {
            frontmatter["original-word-count"] = OriginalWordCount;
        }

        return MarkdownFormatter.FormatWithYamlFrontMatter(frontmatter, Content);
    }
}
