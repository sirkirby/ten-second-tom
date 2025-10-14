namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Configuration options for the interactive text editor.
/// </summary>
public sealed record EditorConfiguration
{
    /// <summary>
    /// Maximum content length in characters (for performance)
    /// </summary>
    public int MaxContentLength { get; init; } = 50_000;

    /// <summary>
    /// Maximum number of lines in content
    /// </summary>
    public int MaxLineCount { get; init; } = 1_000;

    /// <summary>
    /// Whether to show hint text with keyboard shortcuts
    /// </summary>
    public bool ShowHints { get; init; } = true;

    /// <summary>
    /// Number of lines to show in preview (0 = all)
    /// </summary>
    public int PreviewLineLimit { get; init; } = 10;

    /// <summary>
    /// Whether to sanitize ANSI escape sequences from input
    /// </summary>
    public bool SanitizeInput { get; init; } = true;

    /// <summary>
    /// Optional title/prompt to display at the top of the editor
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Default configuration with sensible defaults
    /// </summary>
    public static readonly EditorConfiguration Default = new();
}
