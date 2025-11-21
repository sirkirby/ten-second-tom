namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a note file item in a list of notes.
/// Used for displaying available notes to users.
/// </summary>
public sealed class NoteListItem
{
    /// <summary>
    /// Gets the note filename without extension.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the full path to the note file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the last modification timestamp of the note.
    /// </summary>
    public DateTimeOffset LastModified { get; init; }
}
