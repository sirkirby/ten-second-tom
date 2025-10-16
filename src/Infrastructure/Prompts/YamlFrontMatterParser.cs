using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenSecondTom.Infrastructure.Prompts;

/// <summary>
/// Parses YAML front matter from markdown template files.
/// </summary>
public sealed class YamlFrontMatterParser(ILogger<YamlFrontMatterParser> logger)
{
    private const string FrontMatterDelimiter = "---";
    private readonly ILogger<YamlFrontMatterParser> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Parses a template file content and extracts YAML front matter and content.
    /// </summary>
    /// <param name="content">The raw template file content.</param>
    /// <returns>A result containing the parsed template metadata and content.</returns>
    public Result<ParsedTemplate> Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<ParsedTemplate>.Failure("Template content cannot be null or empty");
        }

        try
        {
            // Check if content starts with front matter delimiter
            if (!content.TrimStart().StartsWith(FrontMatterDelimiter, StringComparison.Ordinal))
            {
                // No front matter - return content as-is with null metadata
                return Result<ParsedTemplate>.Success(new ParsedTemplate
                {
                    Metadata = null,
                    Content = content
                });
            }

            // Split content by lines
            string[] lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            // Find the first delimiter (should be at line 0 or after whitespace)
            int firstDelimiterIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Trim() == FrontMatterDelimiter)
                {
                    firstDelimiterIndex = i;
                    break;
                }
            }

            if (firstDelimiterIndex == -1)
            {
                return Result<ParsedTemplate>.Success(new ParsedTemplate
                {
                    Metadata = null,
                    Content = content
                });
            }

            // Find the closing delimiter
            int secondDelimiterIndex = -1;
            for (int i = firstDelimiterIndex + 1; i < lines.Length; i++)
            {
                if (lines[i].Trim() == FrontMatterDelimiter)
                {
                    secondDelimiterIndex = i;
                    break;
                }
            }

            if (secondDelimiterIndex == -1)
            {
                return Result<ParsedTemplate>.Failure("YAML front matter is not properly closed with '---'");
            }

            // Extract YAML content between delimiters
            string[] yamlLines = lines[(firstDelimiterIndex + 1)..secondDelimiterIndex];
            string yamlContent = string.Join(Environment.NewLine, yamlLines);

            // Extract template content after closing delimiter
            string[] contentLines = lines[(secondDelimiterIndex + 1)..];
            string templateContent = string.Join(Environment.NewLine, contentLines).TrimStart();

            // Parse YAML
            TemplateMetadata? metadata = null;
            if (!string.IsNullOrWhiteSpace(yamlContent))
            {
                try
                {
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();

                    var yamlData = deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
                    metadata = MapToTemplateMetadata(yamlData);
                }
#pragma warning disable CA1031 // Do not catch general exception types - intentional graceful handling
                catch (Exception ex)
#pragma warning restore CA1031
                {
#pragma warning disable CA1848 // Use LoggerMessage delegates - low-frequency parsing operation
                    _logger.LogWarning(ex, "Failed to parse YAML front matter");
#pragma warning restore CA1848
                    return Result<ParsedTemplate>.Failure($"Invalid YAML front matter: {ex.Message}");
                }
            }

            return Result<ParsedTemplate>.Success(new ParsedTemplate
            {
                Metadata = metadata,
                Content = templateContent
            });
        }
#pragma warning disable CA1031 // Do not catch general exception types - intentional graceful handling
        catch (Exception ex)
#pragma warning restore CA1031
        {
#pragma warning disable CA1848 // Use LoggerMessage delegates - low-frequency parsing operation
            _logger.LogError(ex, "Unexpected error parsing template");
#pragma warning restore CA1848
            return Result<ParsedTemplate>.Failure($"Failed to parse template: {ex.Message}");
        }
    }

    private static TemplateMetadata MapToTemplateMetadata(Dictionary<string, object> yamlData)
    {
        var metadata = new TemplateMetadata
        {
            TemplateType = ParseTemplateType(yamlData.GetValueOrDefault("templateType")),
            Title = yamlData.GetValueOrDefault("title")?.ToString(),
            Description = yamlData.GetValueOrDefault("description")?.ToString(),
            Version = yamlData.GetValueOrDefault("version")?.ToString(),
            Author = yamlData.GetValueOrDefault("author")?.ToString(),
            CreatedDate = ParseDateTime(yamlData.GetValueOrDefault("createdDate")),
            Tags = ParseTags(yamlData.GetValueOrDefault("tags"))
        };

        return metadata;
    }

    private static TemplateType ParseTemplateType(object? value)
    {
        if (value == null)
            return TemplateType.Daily; // Default

#pragma warning disable CA1308 // Normalize strings to uppercase - YAML keys are conventionally lowercase
        string? strValue = value.ToString()?.ToLowerInvariant();
#pragma warning restore CA1308
        return strValue switch
        {
            "daily" => TemplateType.Daily,
            "weekly" => TemplateType.Weekly,
            "systemprompt" => TemplateType.SystemPrompt,
            _ => TemplateType.Daily
        };
    }

    private static DateTime? ParseDateTime(object? value)
    {
        if (value == null)
            return null;

        if (value is DateTime dt)
            return dt;

        if (DateTime.TryParse(value.ToString(), out DateTime parsed))
            return parsed;

        return null;
    }

    private static string[]? ParseTags(object? value)
    {
        if (value == null)
            return null;

        if (value is string[] strArray)
            return strArray;

        if (value is List<object> objList)
            return objList.Select(o => o.ToString() ?? string.Empty).ToArray();

        if (value is string str)
            return [str];

        return null;
    }
}

/// <summary>
/// Represents a parsed template with metadata and content separated.
/// </summary>
public sealed class ParsedTemplate
{
    /// <summary>
    /// Gets the parsed metadata from YAML front matter, or null if no front matter exists.
    /// </summary>
    public TemplateMetadata? Metadata { get; init; }

    /// <summary>
    /// Gets the template content without the front matter section.
    /// </summary>
    public required string Content { get; init; }
}
