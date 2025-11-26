using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Features.Generate;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Note;

/// <summary>
/// Builds the <c>note</c> CLI command with subcommands:
/// <list type="bullet">
///   <item><c>note</c> (default) - Create a quick note</item>
///   <item><c>note list</c> - List all available notes</item>
/// </list>
/// </summary>
public sealed class NoteCliCommandBuilder : ICommandBuilder
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower
    };

    /// <inheritdoc />
    public int Priority => 15;

    /// <inheritdoc />
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

        noteCommand.Arguments.Add(contentArgument);
        noteCommand.Options.Add(noEditOption);
        noteCommand.Options.Add(jsonOutputOption);

        // Default action: create a note
        noteCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? content = parseResult.GetValue(contentArgument);
            bool noEdit = parseResult.GetValue(noEditOption);

            await NoteCommandHandler.ExecuteAsync(
                serviceProvider,
                content,
                noEdit,
                jsonOutput).ConfigureAwait(false);

            return 0;
        });

        // Add list subcommand
        noteCommand.Subcommands.Add(BuildListSubcommand(serviceProvider, jsonOutputOption));

        return noteCommand;
    }

    private static Command BuildListSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var listCommand = new Command("list", "List all available notes");
        listCommand.Options.Add(jsonOutputOption);

        listCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var mediator = serviceProvider.GetRequiredService<IMediator>();

            var listResult = await mediator.Send(new ListNotes.Query(), CancellationToken.None);

            if (!listResult.IsSuccess)
            {
                CommandOutputFormatter.WriteError(
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
                    JsonOptions);
                Console.WriteLine(json);
            }
            else
            {
                if (notes.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No notes found. Create one with 'tom note'.[/]");
                    return 0;
                }

                AnsiConsole.MarkupLine("[bold cyan]Available Notes:[/]");
                AnsiConsole.WriteLine();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Filename")
                    .AddColumn("Last Modified");

                foreach (var n in notes)
                {
                    table.AddRow(
                        n.FileName.EscapeMarkup(),
                        n.LastModified.ToString("MMM dd, yyyy h:mm tt"));
                }

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[dim]Total: {notes.Count} note(s)[/]");
            }

            return 0;
        });

        return listCommand;
    }
}
