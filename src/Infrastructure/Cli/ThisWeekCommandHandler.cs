using Spectre.Console;
using TenSecondTom.Features.ThisWeek.Commands;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the execution of the 'thisweek' command.
/// Generates a weekly review from daily entries.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API for CLI commands")]
public static class ThisWeekCommandHandler
{
    /// <summary>
    /// Executes the thisweek command to create a weekly review.
    /// </summary>
    /// <param name="handler">The command handler.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="fromDate">Optional start date for custom range.</param>
    /// <param name="toDate">Optional end date for custom range.</param>
    /// <param name="providerOverride">Optional LLM provider override.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "Console application, no synchronization context")]
    public static async Task ExecuteAsync(
        CreateWeeklyReviewHandler handler,
        IAuthenticationService authService,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? providerOverride,
        bool jsonOutput = false)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(authService);

        // Show warning if using mock authentication
        if (!jsonOutput && authService is MockAuthenticationService)
        {
            AnsiConsole.MarkupLine("[yellow]⚠ Development Mode: Authentication bypassed[/]");
            AnsiConsole.WriteLine();
        }

        // Authenticate first
        var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
            authService,
            CommandNames.ThisWeek,
            jsonOutput,
            CancellationToken.None).ConfigureAwait(false);

        if (!authResult.IsSuccess)
        {
            return;
        }

        if (!jsonOutput)
        {
            AnsiConsole.MarkupLine("[bold magenta]📅 This Week's Review[/]");
            AnsiConsole.WriteLine();
        }

        // Build command
        DateRange? customDateRange = null;
        
        if (fromDate.HasValue || toDate.HasValue)
        {
            // Validate custom date range
            if (!fromDate.HasValue || !toDate.HasValue)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Both --from-date and --to-date must be specified together.");
                return;
            }

            customDateRange = new DateRange
            {
                StartDate = fromDate.Value,
                EndDate = toDate.Value
            };

            AnsiConsole.MarkupLine($"[dim]Generating review for custom range: {fromDate.Value:yyyy-MM-dd} to {toDate.Value:yyyy-MM-dd}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Generating review for the last 7 days...[/]");
        }

        AnsiConsole.WriteLine();

        var command = new CreateWeeklyReviewCommand
        {
            CustomDateRange = customDateRange,
            LlmProviderOverride = providerOverride
        };

        // Execute command with progress indicator
        WeeklyEntry? entry = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("magenta"))
            .StartAsync("[magenta]Analyzing your week...[/]", async ctx =>
            {
                Result<WeeklyEntry> result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

                if (result.IsSuccess)
                {
                    entry = result.Value;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] {result.Error.EscapeMarkup()}");
                }
            }).ConfigureAwait(false);

        // Display results
        if (entry?.Summary == null)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]✓ Weekly review created successfully![/]");
        AnsiConsole.WriteLine();

        // Display summary panel
        var panel = new Panel(new Markup($"""
            [bold]Entry ID:[/] {entry.EntryId}
            [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
            [bold]Provider:[/] {entry.Metadata.LlmProvider}
            [bold]Date Range:[/] {entry.Summary.DateRange.StartDate:yyyy-MM-dd} to {entry.Summary.DateRange.EndDate:yyyy-MM-dd}
            """))
        {
            Header = new PanelHeader("📊 Weekly Review Summary"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Magenta1)
        };

        AnsiConsole.Write(panel);

        // Display Top 3 Accomplishments
        if (entry.Summary.TopAccomplishments.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]🏆 Top 3 Accomplishments:[/]");
            for (int i = 0; i < entry.Summary.TopAccomplishments.Count; i++)
            {
                AnsiConsole.MarkupLine($"  [green]{i + 1}.[/] {Markup.Escape(entry.Summary.TopAccomplishments[i])}");
            }
        }

        // Display Top 3 Challenges
        if (entry.Summary.TopChallenges.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold yellow]⚠️  Top 3 Challenges:[/]");
            for (int i = 0; i < entry.Summary.TopChallenges.Count; i++)
            {
                AnsiConsole.MarkupLine($"  [yellow]{i + 1}.[/] {Markup.Escape(entry.Summary.TopChallenges[i])}");
            }
        }

        // Display Key Insights
        if (entry.Summary.KeyInsights != null && entry.Summary.KeyInsights.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]💡 Key Insights:[/]");
            
            foreach (string insight in entry.Summary.KeyInsights)
            {
                if (!string.IsNullOrWhiteSpace(insight))
                {
                    AnsiConsole.MarkupLine($"  • {Markup.Escape(insight)}");
                }
            }
        }

        // Display Goals for Next Week
        if (entry.Summary.GoalsForNextWeek != null && entry.Summary.GoalsForNextWeek.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold blue]🎯 Goals for Next Week:[/]");
            
            foreach (string goal in entry.Summary.GoalsForNextWeek)
            {
                if (!string.IsNullOrWhiteSpace(goal))
                {
                    AnsiConsole.MarkupLine($"  • {Markup.Escape(goal)}");
                }
            }
        }

        AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[dim]Entry saved to: .memory/{CommandNames.ThisWeek}/[/]");
    }
}
