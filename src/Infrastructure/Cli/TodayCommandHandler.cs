using Spectre.Console;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.Shared.TextEditing.Models;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the execution of the 'today' command.
/// Prompts the user for daily reflections and creates a daily entry.
/// </summary>
public static class TodayCommandHandler
{
    /// <summary>
    /// Executes the today command by prompting the user and creating a daily entry.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="textEditor">The interactive text editor for multi-line input.</param>
    /// <param name="notes">Optional notes content from command line.</param>
    /// <param name="noEdit">Whether to skip the interactive editor.</param>
    /// <param name="useDefaultTemplate">Whether to use the default template automatically.</param>
    /// <param name="templateName">Optional template name to use.</param>
    /// <param name="providerOverride">Optional LLM provider override.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1849:Call async methods when in an async method", Justification = "Spectre.Console Ask/Confirm are synchronous by design")]
    public static async Task ExecuteAsync(
        IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>> handler,
        IAuthenticationService authService,
        IInteractiveTextEditor textEditor,
        string? notes,
        bool noEdit,
        bool useDefaultTemplate,
        string? templateName,
        string? providerOverride,
        bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(textEditor);

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first (before collecting user input)
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.Today,
            jsonOutput,
            CancellationToken.None).ConfigureAwait(false);

        if (!authResult.IsSuccess)
        {
            return;
        }

        // Validate: --no-edit requires notes argument
        if (noEdit && string.IsNullOrWhiteSpace(notes))
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure(
                    CommandNames.Today,
                    "--no-edit flag requires notes argument. Usage: tom today \"your notes here\" --no-edit",
                    DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --no-edit flag requires notes argument");
                AnsiConsole.MarkupLine("Usage: tom today \"your notes here\" --no-edit");
            }
            return;
        }

        // Gather content
        string content;

        if (noEdit && !string.IsNullOrWhiteSpace(notes))
        {
            // Use CLI argument directly
            content = notes;
        }
        else
        {
            // Interactive editor mode
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[bold cyan]📝 Notes for Today[/]");
                AnsiConsole.MarkupLine("[dim](Press Ctrl+D when done, Ctrl+C to cancel)[/]\n");
            }

            var editorConfig = EditorConfiguration.Default with { Title = "Notes for Today" };
            EditorResult editorResult = await textEditor.EditAsync(
                initialContent: notes, // Use notes as initial content if provided
                configuration: editorConfig,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            if (editorResult.IsCancelled)
            {
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine("[yellow]Entry creation cancelled.[/]");
                }
                return;
            }

            if (editorResult.IsError)
            {
                if (jsonOutput)
                {
                    string json = JsonOutputFormatter.FormatFailure(
                        CommandNames.Today,
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
                        CommandNames.Today,
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

            content = editorResult.Content.Trim();

            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("\n[green]✓[/] Content saved\n");
            }
        }

        // Create command
        var command = new CreateDailyEntryCommand
        {
            Content = content,
            TemplateName = templateName,
            UseDefaultTemplate = useDefaultTemplate,
            LlmProviderOverride = providerOverride
        };

        // Execute command with progress indicator (only show progress in non-JSON mode)
        DailyEntry? entry = null;
        Result<DailyEntry> commandResult;
        
        if (jsonOutput)
        {
            commandResult = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);
            if (commandResult.IsSuccess)
            {
                entry = commandResult.Value;
            }
        }
        else
        {
            commandResult = Result<DailyEntry>.Failure("Not executed");
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("cyan"))
                .StartAsync("[cyan]Processing your reflection...[/]", async ctx =>
                {
                    commandResult = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

                    if (commandResult.IsSuccess)
                    {
                        entry = commandResult.Value;
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
            if (commandResult.IsSuccess && entry != null)
            {
                jsonData = new
                {
                    entryId = entry.EntryId,
                    timestamp = entry.Timestamp,
                    provider = entry.Metadata.LlmProvider,
                    summary = new
                    {
                        keyEvents = entry.Summary.KeyEvents,
                        themes = entry.Summary.Themes,
                        todoItems = entry.Summary.TodoItems.Select(t => new { description = t.Description, isCompleted = t.IsCompleted }),
                        importantPeople = entry.Summary.ImportantPeople,
                        notableTasks = entry.Summary.NotableTasks
                    }
                };
            }

            string json = commandResult.IsSuccess
                ? JsonOutputFormatter.FormatSuccess(CommandNames.Today, jsonData, DateTimeOffset.UtcNow)
                : JsonOutputFormatter.FormatFailure(CommandNames.Today, commandResult.Error, DateTimeOffset.UtcNow);
            Console.WriteLine(json);
        }
        else if (entry != null)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]✓ Daily entry created successfully![/]");
            AnsiConsole.WriteLine();

            var panel = new Panel(new Markup($"""
                [bold]Entry ID:[/] {entry.EntryId}
                [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
                [bold]Provider:[/] {entry.Metadata.LlmProvider}

                [bold cyan]Summary:[/]
                [dim]{entry.LlmResponse.Split('\n').Take(5).Aggregate((a, b) => a + "\n" + b)}...[/]
                """))
            {
                Header = new PanelHeader("📋 Daily Entry Summary"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(foreground: Color.Cyan1)
            };

            AnsiConsole.Write(panel);

            // Show key events if any
            if (entry.Summary.KeyEvents.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Key Events:[/]");
                foreach (string keyEvent in entry.Summary.KeyEvents)
                {
                    AnsiConsole.MarkupLine($"  • {Markup.Escape(keyEvent)}");
                }
            }

            // Show todo items if any
            if (entry.Summary.TodoItems.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Todo Items:[/]");
                foreach (TodoItem todo in entry.Summary.TodoItems)
                {
                    string status = todo.IsCompleted ? "✓" : "○";
                    AnsiConsole.MarkupLine($"  {status} {Markup.Escape(todo.Description)}");
                }
            }
        }
    }
}
