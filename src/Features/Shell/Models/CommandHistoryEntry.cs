namespace TenSecondTom.Features.Shell.Models;

/// <summary>
/// Records a single command execution with its outcome.
/// Stored in session history for Arrow Up/Down navigation.
/// </summary>
public sealed record CommandHistoryEntry
{
    /// <summary>
    /// Incrementing sequence number across the session (not array index).
    /// </summary>
    public required int SequenceNumber { get; init; }

    /// <summary>
    /// The command text entered by the user (e.g., "/today").
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// UTC timestamp when the command was executed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// True if the command completed successfully without errors.
    /// </summary>
    public required bool WasSuccessful { get; init; }

    /// <summary>
    /// True if the command was cancelled via Ctrl+C.
    /// </summary>
    public bool WasInterrupted { get; init; }

    /// <summary>
    /// First 100 characters of output or error message (truncated at word boundary).
    /// Null if no result to display.
    /// </summary>
    public string? ResultSummary { get; init; }

    /// <summary>
    /// Validates the history entry constraints.
    /// </summary>
    public bool IsValid() =>
        SequenceNumber > 0 &&
        !string.IsNullOrWhiteSpace(Command) &&
        !(WasSuccessful && WasInterrupted) &&
        (ResultSummary == null || ResultSummary.Length <= 100);

    /// <summary>
    /// Truncates a result message to 100 characters at word boundary.
    /// </summary>
    public static string? TruncateResultSummary(string? result)
    {
        if (result == null || result.Length <= 100)
            return result;

        // Find last space before position 97 to allow for "..."
        int lastSpace = result[..97].LastIndexOf(' ');
        int truncateAt = lastSpace > 0 ? lastSpace : 97;

        return result[..truncateAt] + "...";
    }
}
