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
    /// Gets the file path where this entry should be stored (relative to storage root).
    /// Daily entries: note/MM-DD-YYYY_N_generated.md (LLM-processed)
    /// Note entries: note/MM-DD-YYYY_N.md (user input)
    /// Weekly entries: thisweek/YYYY-WW-DayOfWeek-N.md
    /// Generate entries: Use the FilePath property set during creation (stored separately)
    /// </summary>
    public string FilePath
    {
        get
        {
            return Command switch
            {
                CommandNames.Today => $"{DirectoryNames.Note}/{Timestamp:MM-dd-yyyy}_{EntryNumber}_generated.md",
                CommandNames.Note => $"{DirectoryNames.Note}/{Timestamp:MM-dd-yyyy}_{EntryNumber}.md",
                CommandNames.ThisWeek => GetWeeklyPath(),
                CommandNames.Generate => _filePath ?? throw new InvalidOperationException("FilePath not set for generate entry"),
                _ => throw new InvalidOperationException($"Unknown command: {Command}")
            };
        }
        init => _filePath = value;
    }

    private readonly string? _filePath;

    private string GetWeeklyPath()
    {
        var calendar = CultureInfo.InvariantCulture.Calendar;
        var weekNumber = calendar.GetWeekOfYear(
            Timestamp.DateTime,
            CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
        var dayOfWeek = Timestamp.DateTime.ToString("ddd", CultureInfo.InvariantCulture);
        return $"{CommandNames.ThisWeek}/{Timestamp.Year:0000}-{weekNumber:00}-{dayOfWeek}-{EntryNumber}.md";
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
