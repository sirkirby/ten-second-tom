using System;
using Spectre.Console;

namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents a discoverable audio file that can be transcribed through the CLI.
/// </summary>
public sealed record AudioLibraryItem
{
    /// <summary>
    /// Base filename without extension (e.g., 10-24-2025_1).
    /// </summary>
    public required string BaseName { get; init; }

    /// <summary>
    /// Absolute path to the .wav file on disk.
    /// </summary>
    public required string AudioFilePath { get; init; }

    /// <summary>
    /// Origin of the audio file to display context in prompts.
    /// </summary>
    public required AudioLibraryScope Scope { get; init; }

    /// <summary>
    /// Timestamp derived from filename, front matter, or last write time.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Reported file size so CLI can display helpful summaries.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// Optional duration in seconds sourced from transcript metadata.
    /// </summary>
    public double? DurationSeconds { get; init; }

    /// <summary>
    /// Indicates whether a transcript already exists for this audio entry.
    /// </summary>
    public bool TranscriptExists { get; init; }

    /// <summary>
    /// Optional friendly label describing the item in console prompts.
    /// </summary>
    public string ToDisplayLabel()
    {
        var scopeLabel = Scope switch
        {
            AudioLibraryScope.Note => "Note",
            AudioLibraryScope.Today => "Today",
            AudioLibraryScope.Recording => "Recording",
            _ => "External"
        };

        var sizeLabel = FileSizeBytes >= 1_048_576
            ? $"{FileSizeBytes / 1_048_576d:0.0} MB"
            : $"{FileSizeBytes / 1024d:0.0} KB";

        var durationLabel = DurationSeconds.HasValue
            ? " • " + TimeSpan.FromSeconds(DurationSeconds.Value).ToString(@"m\:ss")
            : string.Empty;

        var transcriptLabel = TranscriptExists
            ? " • transcript ready"
            : " • needs transcript";

        var label = $"{scopeLabel}: {BaseName} • {RecordedAt:MMM dd, yyyy h:mm tt} • {sizeLabel}{durationLabel}{transcriptLabel}";
        return Markup.Escape(label);
    }
}
