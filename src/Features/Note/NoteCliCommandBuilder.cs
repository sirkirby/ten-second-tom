using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Note;

/// <summary>
/// Provides the /note command via ICommandBuilder discovery.
/// Creates quick notes without LLM processing.
/// </summary>
public sealed class NoteCliCommandBuilder : ICommandBuilder
{
    private static readonly System.Text.Json.JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

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

        var listOption = new Option<bool>("--list")
        {
            Description = "List all available notes and exit."
        };

        noteCommand.Arguments.Add(contentArgument);
        noteCommand.Options.Add(noEditOption);
        noteCommand.Options.Add(voiceOption);
        noteCommand.Options.Add(sttOption);
        noteCommand.Options.Add(listOption);
        noteCommand.Options.Add(jsonOutputOption);

        noteCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? content = parseResult.GetValue(contentArgument);
            bool noEdit = parseResult.GetValue(noEditOption);
            bool useVoice = parseResult.GetValue(voiceOption);
            string? stt = parseResult.GetValue(sttOption);
            bool listNotes = parseResult.GetValue(listOption);

            // Handle --list option: display notes and exit
            if (listNotes)
            {
                var mediator = serviceProvider.GetRequiredService<MediatR.IMediator>();
                var listResult = await mediator.Send(new TenSecondTom.Features.Generate.ListNotes.Query(), CancellationToken.None);

                if (!listResult.IsSuccess)
                {
                    TenSecondTom.Infrastructure.Cli.CommandOutputFormatter.WriteError(
                        listResult.Error ?? "Failed to list notes",
                        jsonOutput);
                    return 1;
                }

                var notes = listResult.Value;

                if (jsonOutput)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(
                        new
                        {
                            success = true,
                            notes = notes.Select(n => new
                            {
                                filename = n.FileName,
                                last_modified = n.LastModified
                            })
                        },
                        SnakeCaseJsonOptions);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold cyan]Available Notes:[/]");
                    AnsiConsole.WriteLine();

                    var table = new Table()
                        .AddColumn(new TableColumn("Filename"))
                        .AddColumn(new TableColumn("Last Modified"));

                    foreach (var n in notes)
                    {
                        table.AddRow(
                            Markup.Escape(n.FileName),
                            n.LastModified.ToString("MMM dd, yyyy h:mm tt"));
                    }

                    AnsiConsole.Write(table);
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Total: {notes.Count} note(s)[/]");
                }

                return 0;
            }

            await NoteCommandHandler.ExecuteAsync(
                serviceProvider,
                content,
                noEdit,
                useVoice,
                stt,
                jsonOutput).ConfigureAwait(false);

            return 0;
        });

        return noteCommand;
    }
}
