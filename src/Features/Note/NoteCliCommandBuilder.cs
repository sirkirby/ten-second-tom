using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Note;

/// <summary>
/// Provides the /note command via ICommandBuilder discovery.
/// Creates quick notes without LLM processing.
/// </summary>
public sealed class NoteCliCommandBuilder : ICommandBuilder
{
    /// <summary>
    /// Gets the priority for this command builder.
    /// Priority 15 ensures note command appears before today (20) in help output.
    /// </summary>
    public int Priority => 15;

    /// <summary>
    /// Builds the 'note' command with its options and handler.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Shared JSON output option.</param>
    /// <returns>The configured note command.</returns>
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var noteCommand = new Command("note", "Capture a quick note without AI processing");

        var contentArgument = new Argument<string?>("content")
        {
            Description = "Note content. If omitted, opens interactive editor.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var noEditOption = new Option<bool>("--no-edit")
        {
            Description = "Skip interactive editor and use content from command line argument."
        };

        var voiceOption = new Option<bool>("--voice")
        {
            Description = "Capture note using voice recording."
        };

        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, or openai. Only used with --voice."
        };

        noteCommand.Arguments.Add(contentArgument);
        noteCommand.Options.Add(noEditOption);
        noteCommand.Options.Add(voiceOption);
        noteCommand.Options.Add(sttOption);
        noteCommand.Options.Add(jsonOutputOption);

        noteCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? content = parseResult.GetValue(contentArgument);
            bool noEdit = parseResult.GetValue(noEditOption);
            bool useVoice = parseResult.GetValue(voiceOption);
            string? stt = parseResult.GetValue(sttOption);

            await NoteCommandHandler.ExecuteAsync(
                serviceProvider,
                content,
                noEdit,
                useVoice,
                stt,
                jsonOutput).ConfigureAwait(false);
        });

        return noteCommand;
    }
}
