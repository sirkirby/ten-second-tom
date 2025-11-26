using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Infrastructure.Auth;
using MediatR;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Note;

/// <summary>
/// Handles the execution of the 'note' command.
/// Captures quick notes without LLM processing.
/// </summary>
public static class NoteCommandHandler
{
    /// <summary>
    /// Executes the note command by capturing user content and creating a note entry.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="content">Optional note content from command line.</param>
    /// <param name="noEdit">Whether to skip the interactive editor.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ExecuteAsync(
        IServiceProvider serviceProvider,
        string? content,
        bool noEdit,
        bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        // Resolve required services
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        var textEditor = serviceProvider.GetRequiredService<IInteractiveTextEditor>();
        var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
        var logger = serviceProvider.GetRequiredService<ILogger<CreateNote.Handler>>();

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first (before collecting user input)
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Note,
            jsonOutput,
            CancellationToken.None).ConfigureAwait(false);

        if (!authResult.IsSuccess)
        {
            return;
        }

        // Validate: --no-edit requires content argument
        if (noEdit && string.IsNullOrWhiteSpace(content))
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure(
                    CommandNames.Note,
                    "--no-edit flag requires content argument. Usage: tom note \"your note here\" --no-edit",
                    DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --no-edit flag requires content argument");
                AnsiConsole.MarkupLine("Usage: tom note \"your note here\" --no-edit");
            }
            return;
        }

        // Gather content
        string noteContent;

        if (noEdit && !string.IsNullOrWhiteSpace(content))
        {
            // Use CLI argument directly
            noteContent = content;
        }
        else
        {
            // Interactive editor mode
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[bold cyan]📝 Quick Note[/]");
                AnsiConsole.MarkupLine("[dim](Press Ctrl+D when done, Ctrl+C to cancel)[/]\n");
            }

            var editorConfig = EditorConfiguration.Default with { Title = "Quick Note" };
            EditorResult editorResult = await textEditor.EditAsync(
                initialContent: content, // Use content as initial content if provided
                configuration: editorConfig,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            if (editorResult.IsCancelled)
            {
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine("[yellow]Note creation cancelled.[/]");
                }
                return;
            }

            if (editorResult.IsError)
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Note,
                        $"Editor error: {editorResult.ErrorMessage}",
                        DateTimeOffset.UtcNow);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Editor error: {editorResult.ErrorMessage.EscapeMarkup()}[/]");
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(editorResult.Content))
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Note,
                        "No content entered",
                        DateTimeOffset.UtcNow);
                    Console.WriteLine(json);
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No content entered. Exiting.[/]");
                }
                return;
            }

            noteContent = editorResult.Content.Trim();

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] Content saved\n");
            }
        }

        // Create command
        var command = new CreateNote.Command
        {
            Content = noteContent,
            IsVoiceNote = false,
            AudioFilePath = null
        };

        // Execute command
        Shared.Models.Note? note = null;
        Result<Shared.Models.Note> commandResult;

        if (jsonOutput)
        {
            commandResult = await mediator.Send(command, CancellationToken.None).ConfigureAwait(false);
            if (commandResult.IsSuccess)
            {
                note = commandResult.Value;
            }
        }
        else
        {
            commandResult = Result<Shared.Models.Note>.Failure("Not executed");
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Saving note...[/]", async ctx =>
                {
                    commandResult = await mediator.Send(command, CancellationToken.None).ConfigureAwait(false);

                    if (commandResult.IsSuccess)
                    {
                        note = commandResult.Value;
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error:[/] {commandResult.Error.EscapeMarkup()}");
                    }
                }).ConfigureAwait(false);
        }

        // Display results
        if (jsonOutput)
        {
            // JSON output mode
            object? jsonData = null;
            if (commandResult.IsSuccess && note != null)
            {
                jsonData = new
                {
                    entryId = note.EntryId,
                    timestamp = note.Timestamp,
                    entryNumber = note.EntryNumber,
                    isVoiceNote = note.IsVoiceNote,
                    contentLength = note.Content.Length
                };
            }

            string json = commandResult.IsSuccess
                ? JsonOutputFormatter.FormatSuccess(CommandNames.Note, jsonData, DateTimeOffset.UtcNow)
                : JsonOutputFormatter.FormatFailure(CommandNames.Note, commandResult.Error, DateTimeOffset.UtcNow);
            Console.WriteLine(json);
        }
        else if (note != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✓ Note created successfully![/]");
            AnsiConsole.WriteLine();

            // Show preview of the note content
            string[] contentLines = note.Content.Split('\n');
            bool isTruncated = contentLines.Length > 5;
            string preview = isTruncated
                ? string.Join('\n', contentLines.Take(5))
                : note.Content;

            var panel = new Panel(new Markup($"""
                [bold]Entry ID:[/] {note.EntryId}
                [bold]Timestamp:[/] {note.Timestamp:yyyy-MM-dd HH:mm:ss}
                [bold]Entry Number:[/] {note.EntryNumber}

                [bold cyan]Content:[/]
                {Markup.Escape(preview)}
                """))
            {
                Header = new PanelHeader("📝 Quick Note"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Color.Cyan1)
            };

            AnsiConsole.Write(panel);

            // Show clickable file path
            var rootDir = storageOptions.Value.GetEffectiveStorageDirectory();
            string fullPath = Path.Combine(rootDir, note.FilePath);
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Saved to:[/] [link]{fullPath.EscapeMarkup()}[/]");

            if (isTruncated)
            {
                AnsiConsole.MarkupLine("[dim]... (content truncated in preview)[/]");
            }
        }
    }
}
