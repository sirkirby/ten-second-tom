namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Defines the possible outcomes when an editing session completes.
/// </summary>
public enum EditorOutcome
{
    /// <summary>
    /// User explicitly saved the content (pressed Save in confirmation)
    /// </summary>
    Saved,

    /// <summary>
    /// User cancelled the session without saving (pressed Cancel or Ctrl+C)
    /// </summary>
    Cancelled,

    /// <summary>
    /// Session timed out (if timeout implemented in future)
    /// </summary>
    TimedOut,

    /// <summary>
    /// An error occurred during editing (terminal issues, etc.)
    /// </summary>
    Error
}
