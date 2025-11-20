using System;
using System.CommandLine;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;

namespace TenSecondTom.Features.Search;

/// <summary>
/// Provides the /search command via ICommandBuilder discovery.
/// </summary>
public sealed class SearchCliCommandBuilder : ICommandBuilder
{
    public int Priority => 40;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var searchCommand = new Command("search", "Search memory entries by text query");

        var fromDateOption = new Option<DateTime?>("--from-date") { Description = "Start date filter (yyyy-MM-dd). Optional." };
        var toDateOption = new Option<DateTime?>("--to-date") { Description = "End date filter (yyyy-MM-dd). Optional." };

        searchCommand.Options.Add(fromDateOption);
        searchCommand.Options.Add(toDateOption);
        searchCommand.Options.Add(jsonOutputOption);

        var queryArgument = new Argument<string[]>("query")
        {
            Description = "The text to search for in memory entries",
            Arity = ArgumentArity.ZeroOrMore
        };
        searchCommand.Arguments.Add(queryArgument);

        searchCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string[] queryWords = parseResult.GetValue(queryArgument) ?? [];

            if (queryWords.Length == 0)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search",
                        "Query is required. Usage: search <query> [options]",
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Query is required.");
                    AnsiConsole.MarkupLine("[dim]Usage: search <query> [[--from-date YYYY-MM-DD]] [[--to-date YYYY-MM-DD]] [[--output-json]][/]");
                }
                Environment.ExitCode = 1;
                return;
            }

            if (queryWords.Any(w => w.StartsWith("--", StringComparison.Ordinal)))
            {
                string invalidToken = queryWords.First(w => w.StartsWith("--", StringComparison.Ordinal));
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search",
                        $"Invalid argument '{invalidToken}'. Options must come before the query.",
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid argument '{invalidToken}'. Options must come before the query.");
                }
                Environment.ExitCode = 1;
                return;
            }

            string query = string.Join(' ', queryWords);
            DateTime? fromDate = parseResult.GetValue(fromDateOption);
            DateTime? toDate = parseResult.GetValue(toDateOption);

            var handler = serviceProvider.GetRequiredService<SearchMemories.Handler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            var storageOptions = serviceProvider.GetRequiredService<IOptions<StorageOptions>>();

            await SearchCommandHandler.ExecuteAsync(
                handler,
                authService,
                storageOptions.Value,
                query,
                fromDate,
                toDate,
                jsonOutput).ConfigureAwait(false);
            Environment.ExitCode = 0;
        });

        return searchCommand;
    }
}
