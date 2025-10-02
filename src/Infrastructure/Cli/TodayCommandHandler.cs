using Spectre.Console;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
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
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Console application, no synchronization context")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA1849:Call async methods when in an async method", Justification = "Spectre.Console Ask/Confirm are synchronous by design")]
    public static async Task ExecuteAsync(CreateDailyEntryHandler handler, IAuthenticationService authService, string? providerOverride)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(authService);

        // Show warning if using mock authentication
        if (authService is MockAuthenticationService)
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
                    AnsiConsole.MarkupLine($"[red]✗ Authentication failed:[/] {authResult.Error}");
                    return;
                }
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types - top-level handler for user-facing error display
        catch (Exception ex)
#pragma warning restore CA1031
        {
            AnsiConsole.MarkupLine($"[red]✗ Authentication error:[/] {ex.Message}");
            return;
        }

        AnsiConsole.MarkupLine("[bold cyan]📝 Today's Reflection[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Answer 3-5 questions about your day. Press Ctrl+C to cancel.[/]");
        AnsiConsole.WriteLine();

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

        // Execute command with progress indicator
        DailyEntry? entry = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("cyan"))
            .StartAsync("[cyan]Processing your reflection...[/]", async ctx =>
            {
                Result<DailyEntry> result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    entry = result.Value;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {result.Error}");
                }
            }).ConfigureAwait(false);

        // Display results
        if (entry != null)
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
