namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Represents an interactive text editing session with lifecycle management.
/// </summary>
public sealed class TextEditingSession
{
    /// <summary>
    /// Unique identifier for this editing session (for logging/tracing)
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Initial content provided when session started (may be empty for new entry)
    /// </summary>
    public string InitialContent { get; }

    /// <summary>
    /// Current content being edited (updated during session)
    /// </summary>
    public string CurrentContent { get; private set; }

    /// <summary>
    /// When the editing session started (UTC)
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// When the editing session ended (UTC), null if still active
    /// </summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>
    /// Final outcome of the editing session
    /// </summary>
    public EditorOutcome? Outcome { get; private set; }

    /// <summary>
    /// Whether the content was modified during the session
    /// </summary>
    public bool HasChanges => CurrentContent != InitialContent;

    /// <summary>
    /// Whether the session is still active
    /// </summary>
    public bool IsActive => EndedAt == null;

    /// <summary>
    /// Length of current content in characters
    /// </summary>
    public int ContentLength => CurrentContent.Length;

    /// <summary>
    /// Number of lines in current content
    /// </summary>
    public int LineCount => CurrentContent.Split('\n').Length;

    public TextEditingSession(string? initialContent = null)
    {
        SessionId = Guid.NewGuid();
        InitialContent = initialContent ?? string.Empty;
        CurrentContent = InitialContent;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update the current content during editing
    /// </summary>
    public void UpdateContent(string content)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update content of completed session");

        CurrentContent = content ?? string.Empty;
    }

    /// <summary>
    /// Complete the session with the given outcome
    /// </summary>
    public void Complete(EditorOutcome outcome)
    {
        if (!IsActive)
            throw new InvalidOperationException("Session already completed");

        Outcome = outcome;
        EndedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Duration of the editing session
    /// </summary>
    public TimeSpan Duration => (EndedAt ?? DateTime.UtcNow) - StartedAt;
}
