using Spectre.Console;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the execution of the 'today' command.
/// Prompts the user for daily reflections and creates a daily entry.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API for CLI commands")]
public static class TodayCommandHandler
{
    private static readonly string[] DefaultPrompts =
    [
        "What happened today?",
        "Anything interesting planned for tomorrow?",
        "Unfinished tasks?"
    ];

    /// <summary>
    /// Executes the today command by prompting the user and creating a daily entry.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="providerOverride">Optional LLM provider override.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Console application, no synchronization context")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1849:Call async methods when in an async method", Justification = "Spectre.Console Ask/Confirm are synchronous by design")]
    public static async Task ExecuteAsync(CreateDailyEntryHandler handler, IAuthenticationService authService, string? providerOverride, bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(authService);

        // Show warning if using mock authentication (only in non-JSON mode)
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first (before collecting user input)
        try
        {
            bool isAuthenticated = await authService.IsAuthenticatedAsync(CancellationToken.None).ConfigureAwait(false);
            if (!isAuthenticated)
            {
                Result<UserSession> authResult = await authService.AuthenticateAsync(CancellationToken.None).ConfigureAwait(false);
                if (!authResult.IsSuccess)
                {
                    if (jsonOutput)
                    {
                        string json = JsonOutputFormatter.FormatFailure("today", authResult.Error ?? "Authentication failed", DateTimeOffset.UtcNow);
                        Console.WriteLine(json);
                    }
                    else
                    {
                        AuthenticationErrorFormatter.DisplayAuthenticationError(authResult.Error ?? "Unknown authentication error");
                    }
                    return;
                }
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types - top-level handler for user-facing error display
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (jsonOutput)
            {
                string json = JsonOutputFormatter.FormatFailure("today", $"Authentication error: {ex.Message}", DateTimeOffset.UtcNow);
                Console.WriteLine(json);
            }
            else
            {
                AuthenticationErrorFormatter.DisplayAuthenticationError(ex.Message);
            }
            return;
        }

        // Only show UI elements if not in JSON mode
        if (!jsonOutput)
        {
            AnsiConsole.MarkupLine("[bold cyan]📝 Today's Reflection[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Answer 3-5 questions about your day. Press Ctrl+C to cancel.[/]");
            AnsiConsole.WriteLine();
        }

        var responses = new Dictionary<string, string>();

        // Collect responses
        for (int i = 0; i < DefaultPrompts.Length; i++)
        {
            string question = DefaultPrompts[i];
            string answer = AnsiConsole.Ask<string>($"[yellow]{question}[/]");

            if (string.IsNullOrWhiteSpace(answer))
            {
                AnsiConsole.MarkupLine("[red]Answer cannot be empty. Please try again.[/]");
                i--; // Retry this question
                continue;
            }

            responses[question] = answer.Trim();
        }

        // Ask if user wants to add more responses (up to 5 total)
        while (responses.Count < 5)
        {
            if (!AnsiConsole.Confirm($"[dim]Add another response? ({responses.Count}/5)[/]", defaultValue: false))
            {
                break;
            }

            string customQuestion = AnsiConsole.Ask<string>("[yellow]Your question:[/]");
            if (string.IsNullOrWhiteSpace(customQuestion))
            {
                AnsiConsole.MarkupLine("[red]Question cannot be empty.[/]");
                continue;
            }

            string customAnswer = AnsiConsole.Ask<string>($"[yellow]{customQuestion}[/]");
            if (string.IsNullOrWhiteSpace(customAnswer))
            {
                AnsiConsole.MarkupLine("[red]Answer cannot be empty.[/]");
                continue;
            }

            responses[customQuestion.Trim()] = customAnswer.Trim();
        }

        // Create command
        var command = new CreateDailyEntryCommand
        {
            Responses = responses,
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
                        AnsiConsole.MarkupLine($"[red]Error:[/] {commandResult.Error}");
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
                ? JsonOutputFormatter.FormatSuccess("today", jsonData, DateTimeOffset.UtcNow)
                : JsonOutputFormatter.FormatFailure("today", commandResult.Error, DateTimeOffset.UtcNow);
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
