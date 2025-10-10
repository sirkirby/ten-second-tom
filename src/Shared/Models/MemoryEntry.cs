using System.Globalization;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a base memory entry with user input and LLM-generated response.
/// This is the foundation for daily and weekly memory entries.
/// </summary>
public record MemoryEntry
{
    /// <summary>
    /// Gets the unique identifier for this entry.
    /// Format: {command}-{date}-{number} (e.g., "today-10-01-2025-1")
    /// </summary>
    public required string EntryId { get; init; }

    /// <summary>
    /// Gets the command that created this entry (see <see cref="CommandNames"/>).
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets the timestamp when this entry was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the entry number for this day/week (1-based).
    /// </summary>
    public required int EntryNumber { get; init; }

    /// <summary>
    /// Gets the user's input/responses that were submitted.
    /// </summary>
    public required string UserInput { get; init; }

    /// <summary>
    /// Gets the LLM-generated response/summary.
    /// </summary>
    public required string LlmResponse { get; init; }

    /// <summary>
    /// Gets the metadata about the LLM processing.
    /// </summary>
    public required MemoryEntryMetadata Metadata { get; init; }

    /// <summary>
    /// Gets the file path where this entry should be stored.
    /// Daily entries: .memory/today/MM-DD-YYYY_N.md
    /// Weekly entries: .memory/thisweek/YYYY-WW_N.md
    /// </summary>
    public string FilePath
    {
        get
        {
            return Command switch
            {
                CommandNames.Today => $".memory/{CommandNames.Today}/{Timestamp:MM-dd-yyyy}_{EntryNumber}.md",
                CommandNames.ThisWeek => GetWeeklyPath(),
                _ => throw new InvalidOperationException($"Unknown command: {Command}")
            };
        }
    }

    private string GetWeeklyPath()
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;
        var weekNumber = calendar.GetWeekOfYear(
            Timestamp.DateTime,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
        return $".memory/{CommandNames.ThisWeek}/{Timestamp.Year:0000}-{weekNumber:00}_{EntryNumber}.md";
    }
}

/// <summary>
/// Metadata about the LLM processing for a memory entry.
/// </summary>
public record MemoryEntryMetadata
{
    /// <summary>
    /// Gets the LLM provider used ("OpenAI" or "Anthropic").
    /// </summary>
    public required string LlmProvider { get; init; }

    /// <summary>
    /// Gets the specific model used (e.g., "gpt-4", "claude-3-sonnet-20240229").
    /// </summary>
    public required string LlmModel { get; init; }

    /// <summary>
    /// Gets the number of tokens consumed in the LLM request.
    /// </summary>
    public int TokensUsed { get; init; }

    /// <summary>
    /// Gets the duration of the LLM processing.
    /// </summary>
    public TimeSpan ProcessingDuration { get; init; }

    /// <summary>
    /// Gets custom tags/metadata for this entry.
    /// </summary>
    public Dictionary<string, string> CustomTags { get; init; } = new();
}
