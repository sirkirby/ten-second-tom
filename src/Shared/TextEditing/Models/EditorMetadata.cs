namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Metadata about an editing session, useful for telemetry and diagnostics.
/// </summary>
public sealed record EditorMetadata
{
    /// <summary>
    /// Session identifier for correlation
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Duration of the editing session
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of lines in final content
    /// </summary>
    public int LineCount { get; init; }

    /// <summary>
    /// Number of characters in final content
    /// </summary>
    public int CharacterCount { get; init; }

    /// <summary>
    /// Whether content was modified from initial state
    /// </summary>
    public bool WasModified { get; init; }

    /// <summary>
    /// Empty metadata for cancelled/error scenarios
    /// </summary>
    public static readonly EditorMetadata Empty = new()
    {
        SessionId = Guid.Empty,
        Duration = TimeSpan.Zero,
        LineCount = 0,
        CharacterCount = 0,
        WasModified = false
    };

    /// <summary>
    /// Create metadata from a completed session
    /// </summary>
    /// <param name="session">The session to extract metadata from</param>
    /// <returns>Metadata extracted from the session</returns>
    /// <exception cref="ArgumentNullException">Thrown when session is null</exception>
    public static EditorMetadata FromSession(TextEditingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new()
        {
            SessionId = session.SessionId,
            Duration = session.Duration,
            LineCount = session.LineCount,
            CharacterCount = session.ContentLength,
            WasModified = session.HasChanges
        };
    }
}
