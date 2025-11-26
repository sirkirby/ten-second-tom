using Spectre.Console;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Provides reusable transcription (STT) configuration validation logic for CLI commands.
/// Handles configuration checks, user prompts, and error formatting.
/// </summary>
/// <remarks>
/// Use this helper at the start of any command that requires transcription functionality.
/// It will validate transcription configuration and prompt users to run setup if needed.
/// </remarks>
public static class AudioConfigurationHelper
{
    /// <summary>
    /// Ensures transcription configuration is complete, prompting user to run setup if needed.
    /// Handles both JSON and text output modes.
    /// </summary>
    /// <param name="validator">The transcription configuration validator.</param>
    /// <param name="configuration">The transcription configuration to validate.</param>
    /// <param name="commandName">Name of the command requiring transcription configuration (for error messages).</param>
    /// <param name="jsonOutput">Whether to output errors in JSON format.</param>
    /// <returns>Success result if configured, failure result with error message if not.</returns>
    public static Result<bool> EnsureAudioConfigured(
        IAudioConfigurationValidator validator,
        TranscribeOptions configuration,
        string commandName,
        bool jsonOutput)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(commandName);

        // Check if transcription is configured
        if (validator.IsAudioConfigured(configuration))
        {
            return Result<bool>.Success(true);
        }

        // Get list of missing configuration items
        var missingItems = validator.GetMissingConfiguration(configuration);

        // Build error message
        var errorMessage = "Audio configuration incomplete. Missing:";
        foreach (var item in missingItems)
        {
            errorMessage += $"\n  - {item}";
        }

        // Handle output based on format
        if (jsonOutput)
        {
            HandleAudioConfigurationErrorJson(errorMessage, commandName);
        }
        else
        {
            HandleAudioConfigurationErrorInteractive(missingItems, commandName);
        }

        return Result<bool>.Failure(errorMessage);
    }

    /// <summary>
    /// Handles audio configuration error display in JSON format.
    /// </summary>
    private static void HandleAudioConfigurationErrorJson(string errorMessage, string commandName)
    {
        string json = JsonOutputFormatter.FormatFailure(commandName, errorMessage, DateTimeOffset.UtcNow);
        Console.WriteLine(json);
    }

    /// <summary>
    /// Handles audio configuration error display in interactive (text) format.
    /// Prompts user to run audio setup wizard.
    /// </summary>
    private static void HandleAudioConfigurationErrorInteractive(IReadOnlyList<string> missingItems, string commandName)
    {
        // Display error panel
        var panel = new Panel(
            new Markup(
                $"[yellow]⚠ Audio configuration incomplete[/]\n\n" +
                $"The [cyan]{commandName}[/] command requires audio settings to be configured.\n\n" +
                $"[bold]Missing configuration:[/]\n" +
                string.Join("\n", missingItems.Select(item => $"  • {item}"))
            ))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 0, 1, 0)
        };

        AnsiConsole.WriteLine();
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        // Show helpful guidance
        AnsiConsole.MarkupLine("[dim]Configure transcription settings with:[/]");
        AnsiConsole.MarkupLine("  [cyan]tom transcribe config[/]");
        AnsiConsole.WriteLine();
    }
}
