using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using AuthLoginHandler = TenSecondTom.Features.Auth.Handlers.LoginCommandHandler;
using AuthLogoutHandler = TenSecondTom.Features.Auth.Handlers.LogoutCommandHandler;

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
        
        // Add global --output-json option
        var jsonOutputOption = new Option<bool>("--output-json")
        {
            Description = "Output results in JSON format for programmatic consumption"
        };
        
        rootCommand.Options.Add(jsonOutputOption);
        
        rootCommand.Subcommands.Add(BuildTodayCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildThisWeekCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildSearchCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildLoginCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildLogoutCommand(serviceProvider, jsonOutputOption));
        return rootCommand;
    }

    private static Command BuildTodayCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
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
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? provider = parseResult.GetValue(providerOption);
            var handler = serviceProvider.GetRequiredService<CreateDailyEntryHandler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await TodayCommandHandler.ExecuteAsync(handler, authService, provider, jsonOutput).ConfigureAwait(false);
        });

        return todayCommand;
    }

    private static Command BuildThisWeekCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
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
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            DateTimeOffset? fromDate = parseResult.GetValue(fromDateOption);
            DateTimeOffset? toDate = parseResult.GetValue(toDateOption);
            string? provider = parseResult.GetValue(providerOption);

            var handler = serviceProvider.GetRequiredService<CreateWeeklyReviewHandler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await ThisWeekCommandHandler.ExecuteAsync(handler, authService, fromDate, toDate, provider, jsonOutput).ConfigureAwait(false);
        });

        return thisWeekCommand;
    }

    private static Command BuildSearchCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var searchCommand = new Command("search", "Search memory entries by text query");

        // Add required query argument
        var queryArgument = new Argument<string>("query")
        {
            Description = "The text to search for in memory entries"
        };

        // Add options for date range filters
        var fromDateOption = new Option<DateTime?>("--from-date")
        {
            Description = "Start date filter (yyyy-MM-dd). Optional."
        };

        var toDateOption = new Option<DateTime?>("--to-date")
        {
            Description = "End date filter (yyyy-MM-dd). Optional."
        };

        searchCommand.Arguments.Add(queryArgument);
        searchCommand.Options.Add(fromDateOption);
        searchCommand.Options.Add(toDateOption);

        // Set action
        searchCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string query = parseResult.GetValue(queryArgument) ?? string.Empty;
            DateTime? fromDate = parseResult.GetValue(fromDateOption);
            DateTime? toDate = parseResult.GetValue(toDateOption);

            var handler = serviceProvider.GetRequiredService<SearchMemoriesQueryHandler>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
            await SearchCommandHandler.ExecuteAsync(handler, authService, query, fromDate, toDate, jsonOutput).ConfigureAwait(false);
        });

        return searchCommand;
    }

    private static Command BuildLoginCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var loginCommand = new Command("login", "Authenticate with SSH key and create a session");

        // Set action
        loginCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<AuthLoginHandler>();
            await LoginCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
        });

        return loginCommand;
    }

    private static Command BuildLogoutCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var logoutCommand = new Command("logout", "Log out and invalidate the current session");

        // Set action
        logoutCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<AuthLogoutHandler>();
            await LogoutCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
        });

        return logoutCommand;
    }
}
