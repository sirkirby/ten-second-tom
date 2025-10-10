using Spectre.Console;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.Search.Queries;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.OutputFormatters;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// CLI handler for the /search command.
/// Provides interactive search functionality across memory entries.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public CLI command handler")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Top-level CLI handler must catch all exceptions for user-friendly error messages")]
public static class SearchCommandHandler
{
    /// <summary>
    /// Executes the search command with the specified parameters.
    /// </summary>
    /// <param name="handler">The search query handler.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="query">The search query text.</param>
    /// <param name="fromDate">Optional start date filter.</param>
    /// <param name="toDate">Optional end date filter.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ExecuteAsync(
        SearchMemoriesQueryHandler handler,
        IAuthenticationService authService,
        string query,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        bool jsonOutput = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(authService);
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            // Check authentication first
            var isAuthenticated = await authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
            if (!isAuthenticated)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search", 
                        "Authentication required. Please authenticate first.", 
                        DateTimeOffset.UtcNow));
                    return;
                }

                AnsiConsole.MarkupLine("[red]Authentication required. Please authenticate first.[/]");
                
                var authResult = await authService.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
                if (!authResult.IsSuccess)
                {
                    AnsiConsole.MarkupLine($"[red]Authentication failed: {Markup.Escape(authResult.Error ?? "Unknown error")}[/]");
                    return;
                }

                AnsiConsole.MarkupLine("[green]Authentication successful![/]");
            }

            // Execute search
            var searchQuery = new SearchMemoriesQuery(query, fromDate, toDate);
            var result = await handler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search", 
                        result.Error ?? "Unknown error", 
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Search failed: {Markup.Escape(result.Error ?? "Unknown error")}[/]");
                }
                return;
            }

            // Output results based on format
            if (jsonOutput)
            {
                var searchResultData = new SearchResultData
                {
                    Query = query,
                    FromDate = fromDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    ToDate = toDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    ResultCount = result.Value.Count,
                    Results = result.Value
                        .OrderByDescending(e => e.Timestamp)
                        .Select(e => new SearchResultEntry
                        {
                            EntryNumber = e.EntryNumber,
                            Command = e.Command,
                            Timestamp = e.Timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture),
                            FilePath = e.FilePath,
                            UserInput = e.UserInput,
                            LlmResponse = e.LlmResponse
                        })
                        .ToList()
                };

                Console.WriteLine(JsonOutputFormatter.FormatSuccess("search", searchResultData, DateTimeOffset.UtcNow));
            }
            else
            {
                // Display search header
                AnsiConsole.Write(
                    new Rule($"[blue]Searching for:[/] {Markup.Escape(query)}")
                        .RuleStyle("grey")
                        .LeftJustified());

                if (fromDate.HasValue || toDate.HasValue)
                {
                    var dateRangeText = fromDate.HasValue && toDate.HasValue
                        ? $"{fromDate.Value:yyyy-MM-dd} to {toDate.Value:yyyy-MM-dd}"
                        : fromDate.HasValue
                            ? $"from {fromDate.Value:yyyy-MM-dd}"
                            : $"up to {toDate!.Value:yyyy-MM-dd}";
                    
                    AnsiConsole.MarkupLine($"[grey]Date range: {dateRangeText}[/]");
                }

                AnsiConsole.WriteLine();

                // Display results
                if (result.Value.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]No entries found matching '{Markup.Escape(query)}'[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[green]Found {result.Value.Count} result(s)[/]\n");

                // Sort by date (newest first) and display each result
                var sortedResults = result.Value.OrderByDescending(e => e.Timestamp).ToList();
                
                for (int i = 0; i < sortedResults.Count; i++)
                {
                    var entry = sortedResults[i];
                    var entryType = entry.Command == "today" ? "Daily Entry" : "Weekly Review";
                    var dateText = entry.Command == "today" 
                        ? entry.Timestamp.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture)
                        : $"Week of {entry.Timestamp.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture)}";

                    // Create excerpt from user input (first 80 characters)
                    var excerpt = entry.UserInput.Length > 80 
                        ? Markup.Escape(entry.UserInput.Substring(0, 77)) + "..."
                        : Markup.Escape(entry.UserInput);

                    var panel = new Panel($"""
                        [bold]{entryType}[/] | [grey]{dateText}[/] | Entry #{entry.EntryNumber}
                        
                        {excerpt}
                        
                        [grey]→ {Markup.Escape(entry.FilePath)}[/]
                        """)
                        .Border(BoxBorder.Rounded)
                        .BorderColor(Color.Grey)
                        .Header($"[yellow]{i + 1}.[/]", Justify.Left);

                    AnsiConsole.Write(panel);
                    AnsiConsole.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonOutputFormatter.FormatFailure("search", ex.Message, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }
}
