using System.Text;
using System.Text.RegularExpressions;

namespace TenSecondTom.Shared.Extensions;

/// <summary>
/// Extension methods for string manipulation and formatting.
/// </summary>
public static partial class StringExtensions
{
    /// <summary>
    /// Strips markdown code block wrappers from content if present.
    /// This is a defensive measure to handle LLM responses that incorrectly wrap
    /// output in markdown code blocks despite instructions not to.
    /// </summary>
    /// <param name="content">The content to process.</param>
    /// <returns>
    /// The content with markdown code block wrappers removed if present,
    /// otherwise returns the original content unchanged.
    /// </returns>
    /// <remarks>
    /// Handles both:
    /// - Triple backtick markdown blocks: ```markdown\n...\n```
    /// - Triple backtick generic blocks: ```\n...\n```
    /// 
    /// The method is idempotent - calling it multiple times has no additional effect.
    /// </remarks>
    public static string StripMarkdownCodeBlock(this string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        // Trim to handle any leading/trailing whitespace
        string trimmed = content.Trim();

        // Pattern to match markdown code blocks:
        // - Starts with ``` optionally followed by "markdown" or other language identifier
        // - Contains content (captured group)
        // - Ends with ```
        // Uses non-greedy matching and handles newlines
        Match match = CodeBlockRegex().Match(trimmed);
        
        if (match.Success && match.Groups.Count > 1)
        {
            // Return the captured content (group 1) without the code block markers
            return match.Groups[1].Value;
        }

        // No code block found, return original content
        return content;
    }

    [GeneratedRegex(@"^```(?:markdown)?(?:\r?\n|\r)([\s\S]*?)(?:\r?\n|\r)```\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CodeBlockRegex();

    /// <summary>
    /// Normalizes proprietary reasoning tags (e.g. &lt;think&gt;...&lt;/think&gt;) into markdown-friendly blocks.
    /// Preserves the reasoning text while rendering it as a quoted section for readability.
    /// </summary>
    /// <param name="content">The content to normalize.</param>
    /// <returns>The content with reasoning tags converted to markdown quotes.</returns>
    public static string NormalizeReasoningTags(this string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        return ReasoningTagRegex().Replace(content, static match =>
        {
            var body = match.Groups["body"].Value.Trim();
            if (string.IsNullOrEmpty(body))
            {
                return string.Empty;
            }

            var tag = match.Groups["tag"].Value;
            var label = tag.Equals("think", StringComparison.OrdinalIgnoreCase)
                ? "Reasoning"
                : $"Reasoning ({tag})";

            var normalizedBody = body.Replace("\r\n", "\n").Trim();
            var sb = new StringBuilder();
            sb.AppendLine("<details>");
            sb.AppendLine($"<summary>{label}</summary>");
            sb.AppendLine();
            sb.AppendLine(normalizedBody);
            sb.AppendLine("</details>");
            sb.AppendLine();
            return sb.ToString();
        });
    }

    [GeneratedRegex(@"<(?<tag>[a-z0-9:_-]*think[a-z0-9:_-]*)>(?<body>[\s\S]*?)</\k<tag>>", RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningTagRegex();
}

