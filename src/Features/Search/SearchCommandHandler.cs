using Spectre.Console;
using TenSecondTom.Features.Search.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;

namespace TenSecondTom.Features.Search;

/// <summary>
/// CLI handler for the /search command.
/// Provides interactive search functionality across memory entries.
/// </summary>
public static class SearchCommandHandler
{
    /// <summary>
    /// Executes the search command with the specified parameters.
    /// </summary>
    /// <param name="handler">The search query handler.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="storageOptions">Storage options for accessing memory directory.</param>
    /// <param name="query">The search query text.</param>
    /// <param name="fromDate">Optional start date filter.</param>
    /// <param name="toDate">Optional end date filter.</param>
    /// <param name="jsonOutput">Whether to output results in JSON format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task ExecuteAsync(
        SearchMemories.Handler handler,
        IAuthenticationService authService,
        StorageOptions storageOptions,
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
            var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
                authService,
                CommandNames.Search,
                jsonOutput,
                cancellationToken).ConfigureAwait(false);

            if (!authResult.IsSuccess)
            {
                return;
            }

            // Execute search
            var searchQuery = new SearchMemories.Query(query, fromDate, toDate);
            var result = await handler.Handle(searchQuery, cancellationToken).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Search,
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

                Console.WriteLine(JsonOutputFormatter.FormatSuccess(CommandNames.Search, searchResultData, DateTimeOffset.UtcNow));
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

                    AnsiConsole.MarkupLine($"[grey]Date range: {dateRangeText.EscapeMarkup()}[/]");
                }

                AnsiConsole.WriteLine();

                // Display results
                if (result.Value.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]No entries found matching '{Markup.Escape(query)}'[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"[green]Found {result.Value.Count} result(s)[/]\n");

                // Get the effective storage directory for absolute path display
                var storageBaseDir = storageOptions.GetEffectiveStorageDirectory();

                // Sort by date (newest first) and display each result
                var sortedResults = result.Value.OrderByDescending(e => e.Timestamp).ToList();

                for (int i = 0; i < sortedResults.Count; i++)
                {
                    var entry = sortedResults[i];
                    var entryType = entry.Command switch
                    {
                        CommandNames.Today => "Daily Entry",
                        CommandNames.ThisWeek => "Weekly Review",
                        CommandNames.Generate => "Generated Output",
                        _ => entry.Command
                    };

                    var dateText = entry.Command switch
                    {
                        CommandNames.Today => entry.Timestamp.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture),
                        CommandNames.ThisWeek => $"Week of {entry.Timestamp.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture)}",
                        CommandNames.Generate => entry.Timestamp.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture),
                        _ => entry.Timestamp.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture)
                    };

                    // Create excerpt from user input or LLM response (first 80 characters)
                    var contentToExcerpt = !string.IsNullOrWhiteSpace(entry.UserInput)
                        ? entry.UserInput
                        : entry.LlmResponse ?? string.Empty;

                    var excerpt = contentToExcerpt.Length > 80
                        ? Markup.Escape(contentToExcerpt.Substring(0, 77)) + "..."
                        : Markup.Escape(contentToExcerpt);

                    // Build full absolute path for display
                    string fullPath = Path.Combine(storageBaseDir, entry.FilePath);

                    var panel = new Panel($"""
                        [bold]{entryType}[/] | [grey]{dateText}[/] | Entry #{entry.EntryNumber}

                        {excerpt}

                        [grey]→ {Markup.Escape(fullPath)}[/]
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
                Console.WriteLine(JsonOutputFormatter.FormatFailure(CommandNames.Search, ex.Message, DateTimeOffset.UtcNow));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
            }
        }
    }
}
