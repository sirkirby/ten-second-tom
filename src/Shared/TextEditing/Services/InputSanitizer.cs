using System.Text.RegularExpressions;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Shared.TextEditing.Services;

/// <summary>
/// Service for sanitizing user input by removing ANSI escape sequences
/// and terminal control codes while preserving legitimate content.
/// </summary>
public sealed partial class InputSanitizer
{
    // ANSI escape sequence pattern: ESC [ followed by parameters and command letter
    // Also matches other control sequences starting with ESC
    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled)]
    private static partial Regex AnsiEscapePattern();

    /// <summary>
    /// Sanitize input text by stripping ANSI escape sequences and control codes
    /// while preserving printable characters, newlines, tabs, and Unicode content.
    /// </summary>
    /// <param name="input">The raw input text to sanitize</param>
    /// <returns>Sanitized text with metadata about the sanitization process</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method for consistency and future extensibility")]
    public SanitizedText Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new SanitizedText
            {
                Content = string.Empty,
                WasSanitized = false,
                OriginalLength = 0
            };
        }

        var originalLength = input.Length;

        // Remove ANSI escape sequences
        var sanitized = AnsiEscapePattern().Replace(input, string.Empty);

        var wasSanitized = sanitized.Length != originalLength;

        return new SanitizedText
        {
            Content = sanitized,
            WasSanitized = wasSanitized,
            OriginalLength = originalLength
        };
    }
}
