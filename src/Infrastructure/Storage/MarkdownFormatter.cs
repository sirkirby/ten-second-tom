using System.Globalization;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TenSecondTom.Infrastructure.Storage;

/// <summary>
/// Utility for formatting markdown files with YAML front matter.
/// Provides consistent formatting across all command outputs (today, thisweek, generate).
/// </summary>
public static class MarkdownFormatter
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(HyphenatedNamingConvention.Instance)
        .Build();

    /// <summary>
    /// Formats content as markdown with YAML front matter header.
    /// </summary>
    /// <param name="metadata">Dictionary of metadata key-value pairs for the YAML front matter.</param>
    /// <param name="content">The main content to include after the front matter.</param>
    /// <returns>A markdown-formatted string with YAML front matter and content.</returns>
    public static string FormatWithYamlFrontMatter(
        Dictionary<string, object> metadata,
        string content)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(content);

        var sb = new StringBuilder();

        // YAML front matter
        sb.AppendLine("---");
        string yaml = YamlSerializer.Serialize(metadata);
        sb.Append(yaml);
        sb.AppendLine("---");
        sb.AppendLine();

        // Content
        sb.AppendLine(content);

        return sb.ToString();
    }

    /// <summary>
    /// Creates a standardized entry ID for memory entries.
    /// </summary>
    /// <param name="command">The command name (e.g., "today", "thisweek", "generate").</param>
    /// <param name="dateIdentifier">Date identifier (e.g., "10-21-2025", "2025-10-14").</param>
    /// <param name="entryNumber">Entry number for the day/week.</param>
    /// <returns>Formatted entry ID (e.g., "today-10-21-2025-1").</returns>
    public static string CreateEntryId(string command, string dateIdentifier, int entryNumber)
    {
        return $"{command}-{dateIdentifier}-{entryNumber}";
    }

    /// <summary>
    /// Creates a standardized entry ID for generate command outputs.
    /// </summary>
    /// <param name="recordingBaseName">Recording base name (e.g., "10-21-2025_1").</param>
    /// <param name="templateId">Template identifier used for generation.</param>
    /// <returns>Formatted entry ID (e.g., "generate-10-21-2025_1-business-meeting").</returns>
    public static string CreateGenerateEntryId(string recordingBaseName, string templateId)
    {
        return $"generate-{recordingBaseName}-{templateId}";
    }

    /// <summary>
    /// Formats a timestamp for YAML front matter (ISO 8601 format).
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>ISO 8601 formatted timestamp string.</returns>
    public static string FormatTimestamp(DateTimeOffset timestamp)
    {
        return timestamp.ToString("o", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats processing duration for YAML front matter (total seconds).
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>Total seconds as a double.</returns>
    public static double FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds;
    }
}

