namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Formats transcript text for console display.
/// Handles truncation of long transcripts for better user experience.
/// </summary>
public static class TranscriptFormatter
{
    /// <summary>
    /// Default maximum length before truncation is applied to console output.
    /// Transcripts longer than this will show first and last preview sections.
    /// </summary>
    public const int DefaultMaxDisplayLength = 1000;

    /// <summary>
    /// Default length of preview text shown from start and end when truncated.
    /// When truncating, this many characters are shown from the beginning and end.
    /// </summary>
    public const int DefaultPreviewLength = 400;

    /// <summary>
    /// Formats a transcript for console display, truncating if necessary.
    /// </summary>
    /// <param name="transcript">The full transcript text.</param>
    /// <param name="maxDisplayLength">Maximum length before truncation.</param>
    /// <param name="previewLength">Length of preview from start and end when truncated.</param>
    /// <returns>A tuple containing the formatted transcript and a boolean indicating if it was truncated.</returns>
    public static (string FormattedText, bool WasTruncated, int TruncatedCharacters) FormatForDisplay(
        string transcript,
        int maxDisplayLength = DefaultMaxDisplayLength,
        int previewLength = DefaultPreviewLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcript);

        if (maxDisplayLength <= 0)
        {
            throw new ArgumentException("Max display length must be positive", nameof(maxDisplayLength));
        }

        if (previewLength <= 0)
        {
            throw new ArgumentException("Preview length must be positive", nameof(previewLength));
        }

        if (previewLength * 2 >= maxDisplayLength)
        {
            throw new ArgumentException("Preview length * 2 must be less than max display length", nameof(previewLength));
        }

        var trimmedTranscript = transcript.Trim();

        if (trimmedTranscript.Length <= maxDisplayLength)
        {
            return (trimmedTranscript, false, 0);
        }

        // Truncate: show first and last portions
        var firstPart = trimmedTranscript[..previewLength];
        var lastPart = trimmedTranscript[^previewLength..];
        var truncatedChars = trimmedTranscript.Length - (previewLength * 2);

        var formatted = $"{firstPart}\n\n... [Transcript truncated - {truncatedChars:N0} more characters] ...\n\n{lastPart}";

        return (formatted, true, truncatedChars);
    }

    /// <summary>
    /// Strips YAML frontmatter from transcript content.
    /// </summary>
    /// <param name="transcriptContent">The transcript content with potential YAML frontmatter.</param>
    /// <returns>The transcript text without frontmatter.</returns>
    public static string StripFrontmatter(string transcriptContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptContent);

        var lines = transcriptContent.Split('\n');
        bool inFrontmatter = false;
        var transcriptText = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.Trim() == "---")
            {
                inFrontmatter = !inFrontmatter;
                continue;
            }

            if (!inFrontmatter && !string.IsNullOrWhiteSpace(line))
            {
                transcriptText.AppendLine(line);
            }
        }

        return transcriptText.ToString().Trim();
    }
}
