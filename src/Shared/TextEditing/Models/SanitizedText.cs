namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Represents text that has been sanitized to remove ANSI escape sequences
/// and terminal control codes.
/// </summary>
public sealed record SanitizedText
{
    /// <summary>
    /// The sanitized text content (safe for storage and display)
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Whether any content was removed during sanitization
    /// </summary>
    public bool WasSanitized { get; init; }

    /// <summary>
    /// Original length before sanitization
    /// </summary>
    public int OriginalLength { get; init; }

    /// <summary>
    /// Number of characters removed
    /// </summary>
    public int RemovedCount => OriginalLength - Content.Length;
}
