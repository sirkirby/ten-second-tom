namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Immutable result returned when an editing session completes.
/// Follows the Result pattern for explicit success/failure handling.
/// </summary>
public sealed record EditorResult
{
    /// <summary>
    /// The edited content if session was saved, empty if cancelled/error
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// How the editing session ended
    /// </summary>
    public EditorOutcome Outcome { get; init; }

    /// <summary>
    /// Whether the user saved the content
    /// </summary>
    public bool IsSaved => Outcome == EditorOutcome.Saved;

    /// <summary>
    /// Whether the user cancelled the session
    /// </summary>
    public bool IsCancelled => Outcome == EditorOutcome.Cancelled;

    /// <summary>
    /// Whether an error occurred
    /// </summary>
    public bool IsError => Outcome == EditorOutcome.Error;

    /// <summary>
    /// Error message if Outcome is Error, null otherwise
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Session metadata (duration, line count, etc.)
    /// </summary>
    public EditorMetadata Metadata { get; init; } = EditorMetadata.Empty;

    // Factory methods

    /// <summary>
    /// Create a successful result with saved content
    /// </summary>
    public static EditorResult Saved(string content, EditorMetadata metadata) => new()
    {
        Content = content,
        Outcome = EditorOutcome.Saved,
        Metadata = metadata
    };

    /// <summary>
    /// Create a cancelled result
    /// </summary>
    public static EditorResult Cancelled(EditorMetadata metadata) => new()
    {
        Content = string.Empty,
        Outcome = EditorOutcome.Cancelled,
        Metadata = metadata
    };

    /// <summary>
    /// Create an error result with message
    /// </summary>
    public static EditorResult Error(string errorMessage, EditorMetadata metadata) => new()
    {
        Content = string.Empty,
        Outcome = EditorOutcome.Error,
        ErrorMessage = errorMessage,
        Metadata = metadata
    };
}
