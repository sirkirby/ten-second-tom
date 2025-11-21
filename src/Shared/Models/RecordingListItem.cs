namespace TenSecondTom.Shared.Models;

/// <summary>
/// Lightweight display model for recording selection UI.
/// Represents a recording in the selection list with display-friendly information.
/// </summary>
public sealed record RecordingListItem
{
    /// <summary>
    /// Gets the base name of the recording (without extension).
    /// Format: M-D-Y_Increment
    /// Example: "10-21-2025_1"
    /// </summary>
    public required string RecordingBaseName { get; init; }

    /// <summary>
    /// Gets the full path to the transcript file.
    /// </summary>
    public required string TranscriptFilePath { get; init; }

    /// <summary>
    /// Gets the recording timestamp parsed from filename.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets the formatted display date for UI.
    /// Format: "Oct 24, 2025 2:30 PM"
    /// </summary>
    public required string FormattedDate { get; init; }

    /// <summary>
    /// Gets the word count of the transcript.
    /// </summary>
    public required int WordCount { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets formatted file size for display (e.g., "12.5 KB").
    /// </summary>
    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    /// <summary>
    /// Gets the display label for selection UI.
    /// Format: "Oct 24, 2025 2:30 PM • 234 words • 1.2 KB"
    /// </summary>
    public string DisplayLabel => $"{FormattedDate} • {WordCount} words • {FormattedFileSize}";

    private static string FormatFileSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
}
