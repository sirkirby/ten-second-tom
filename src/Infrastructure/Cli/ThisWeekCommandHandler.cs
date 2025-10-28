using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Features.ThisWeek.Commands;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Handles the execution of the 'thisweek' command.
/// Generates a weekly review from daily entries.
/// </summary>
public static class ThisWeekCommandHandler
{
    /// <summary>
    /// Executes the thisweek command to create a weekly review.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="handler">The command handler.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="fromDate">Optional start date for custom range.</param>
    /// <param name="toDate">Optional end date for custom range.</param>
    /// <param name="providerOverride">Optional LLM provider override.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ExecuteAsync(
        IServiceProvider serviceProvider,
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
        if (entry == null)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold green]✓ Weekly review created successfully![/]");
        AnsiConsole.WriteLine();

        // Show truncated preview of the LLM response
        string[] responseLines = entry.LlmResponse.Split('\n');
        bool isTruncated = responseLines.Length > 15;
        string preview = isTruncated
            ? string.Join('\n', responseLines.Take(15))
            : entry.LlmResponse;

        // Display summary panel with LLM response
        var panel = new Panel(new Markup($"""
            [bold]Entry ID:[/] {entry.EntryId}
            [bold]Timestamp:[/] {entry.Timestamp:yyyy-MM-dd HH:mm:ss}
            [bold]Provider:[/] {entry.Metadata.LlmProvider}
            [bold]Tokens:[/] {entry.Metadata.TokensUsed}

            [bold cyan]Response:[/]
            {Markup.Escape(preview)}
            """))
        {
            Header = new PanelHeader("📊 Weekly Review"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Magenta1)
        };

        AnsiConsole.Write(panel);

        // Show clickable file path
        AnsiConsole.WriteLine();
        if (isTruncated)
        {
            var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();
            string fullPath = Path.Combine(storageOptions.Value.MemoryDirectory, entry.FilePath);
            AnsiConsole.MarkupLine($"[dim]Full entry:[/] [link]{fullPath.EscapeMarkup()}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]Entry saved to: .memory/{CommandNames.ThisWeek}/[/]");
        }
    }
}
