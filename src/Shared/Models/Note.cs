namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a quick note entry captured without LLM processing.
/// Notes are simpler than daily entries and store raw user content without AI enhancement.
/// </summary>
/// <remarks>
/// Unlike DailyEntry or MemoryEntry, Note does not include LLM-generated responses or metadata.
/// It's designed for quick capture of thoughts, ideas, or reminders without AI processing overhead.
/// Notes can optionally include voice recording information if captured via audio input.
/// </remarks>
public record Note
{
    /// <summary>
    /// Gets the unique identifier for this note.
    /// Format: note-{MM-dd-yyyy}-{number} (e.g., "note-01-21-2025-1")
    /// </summary>
    public required string EntryId { get; init; }

    /// <summary>
    /// Gets the command that created this note (always <see cref="Constants.CommandNames.Note"/>).
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets the timestamp when this note was created.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Gets the entry number for this day (1-based).
    /// Shared with today's entries when configured.
    /// </summary>
    public required int EntryNumber { get; init; }

    /// <summary>
    /// Gets the note content as entered by the user.
    /// This is the raw, unprocessed content without LLM enhancement.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets a value indicating whether this note was captured via voice.
    /// </summary>
    public bool IsVoiceNote { get; init; }

    /// <summary>
    /// Gets the audio file path if this note was captured via voice.
    /// Null for text-based notes.
    /// </summary>
    public string? AudioFilePath { get; init; }

    /// <summary>
    /// Gets the file path where this note should be stored (relative to storage root).
    /// Format: note/MM-DD-YYYY_N.md (e.g., "note/01-21-2025_1.md")
    /// </summary>
    public string FilePath => $"{Constants.CommandNames.Note}/{Timestamp:MM-dd-yyyy}_{EntryNumber}.md";
}
