using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Registry for all CLI commands in the application.
/// Builds the root command with all subcommands configured.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API for CLI commands")]
public static class CommandRegistry
{
    /// <summary>
    /// Builds and configures the root command with all subcommands.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <returns>Configured root command.</returns>
    public static RootCommand BuildRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("Ten Second Tom - Personal Memory Assistant");
        rootCommand.Subcommands.Add(BuildTodayCommand(serviceProvider));
        rootCommand.Subcommands.Add(BuildThisWeekCommand(serviceProvider));
        return rootCommand;
    }

    private static Command BuildTodayCommand(IServiceProvider serviceProvider)
    {
        var todayCommand = new Command("today", "Capture today's reflection with 3-5 prompts");

        // Add options for LLM provider override
        var providerOption = new Option<string?>("--provider")
        {
            Description = "LLM provider to use (OpenAI or Anthropic). Defaults to configured provider."
        };
        
        todayCommand.Options.Add(providerOption);

        // Set action
        todayCommand.SetAction(async (parseResult) =>
        {
            string? provider = parseResult.GetValue(providerOption);
            var handler = serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await TodayCommandHandler.ExecuteAsync(handler, authService, provider).ConfigureAwait(false);
        });

        return todayCommand;
    }

    private static Command BuildThisWeekCommand(IServiceProvider serviceProvider)
    {
        var thisWeekCommand = new Command("thisweek", "Generate a weekly review from recent daily entries");

        // Add options for custom date range
        var fromDateOption = new Option<DateTimeOffset?>("--from-date")
        {
            Description = "Start date for custom range (yyyy-MM-dd). Must be used with --to-date."
        };

        var toDateOption = new Option<DateTimeOffset?>("--to-date")
        {
            Description = "End date for custom range (yyyy-MM-dd). Must be used with --from-date."
        };

        // Add option for LLM provider override
        var providerOption = new Option<string?>("--provider")
        {
            Description = "LLM provider to use (OpenAI or Anthropic). Defaults to configured provider."
        };

        thisWeekCommand.Options.Add(fromDateOption);
        thisWeekCommand.Options.Add(toDateOption);
        thisWeekCommand.Options.Add(providerOption);

        // Set action
        thisWeekCommand.SetAction(async (parseResult) =>
        {
            DateTimeOffset? fromDate = parseResult.GetValue(fromDateOption);
            DateTimeOffset? toDate = parseResult.GetValue(toDateOption);
            string? provider = parseResult.GetValue(providerOption);

            var handler = serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await ThisWeekCommandHandler.ExecuteAsync(handler, authService, fromDate, toDate, provider).ConfigureAwait(false);
        });

        return thisWeekCommand;
    }
}
