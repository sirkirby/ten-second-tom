using Spectre.Console;
using System.Text.Json;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Helper class for formatting command output consistently across text and JSON modes.
/// Reduces duplication of if (jsonOutput) conditionals throughout command handlers.
/// </summary>
internal static class CommandOutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Writes a success message to console.
    /// </summary>
    /// <param name="message">Success message to display</param>
    /// <param name="jsonOutput">Whether to output in JSON format</param>
    /// <param name="jsonData">Optional JSON data object. If null, uses { success: true, message }</param>
    public static void WriteSuccess(string message, bool jsonOutput, object? jsonData = null)
    {
        if (jsonOutput)
        {
            var data = jsonData ?? new { success = true, message };
            Console.WriteLine(JsonSerializer.Serialize(data, JsonOptions));
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] {message.EscapeMarkup()}");
        }
    }

    /// <summary>
    /// Writes an error message to console.
    /// </summary>
    /// <param name="error">Error message to display</param>
    /// <param name="jsonOutput">Whether to output in JSON format</param>
    public static void WriteError(string error, bool jsonOutput)
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = false, error }, JsonOptions));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]✗[/] {error.EscapeMarkup()}");
        }
    }

    /// <summary>
    /// Writes an informational message to console.
    /// Only outputs in text mode (JSON mode skips info messages).
    /// </summary>
    /// <param name="message">Info message to display</param>
    /// <param name="jsonOutput">Whether to output in JSON format</param>
    public static void WriteInfo(string message, bool jsonOutput)
    {
        if (!jsonOutput)
        {
            AnsiConsole.MarkupLine($"[dim]{message.EscapeMarkup()}[/]");
        }
    }

    /// <summary>
    /// Writes a warning message to console.
    /// </summary>
    /// <param name="message">Warning message to display</param>
    /// <param name="jsonOutput">Whether to output in JSON format</param>
    public static void WriteWarning(string message, bool jsonOutput)
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { warning = message }, JsonOptions));
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] {message.EscapeMarkup()}");
        }
    }

    /// <summary>
    /// Writes validation error to console.
    /// </summary>
    /// <param name="field">Field name that failed validation</param>
    /// <param name="error">Error message</param>
    /// <param name="jsonOutput">Whether to output in JSON format</param>
    public static void WriteValidationError(string field, string error, bool jsonOutput)
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                validationError = new { field, error }
            }, JsonOptions));
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Invalid {field.EscapeMarkup()}:[/] {error.EscapeMarkup()}");
        }
    }
}
