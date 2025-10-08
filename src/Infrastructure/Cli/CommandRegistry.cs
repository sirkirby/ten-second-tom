using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.Shell.Services;
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
    private static readonly string[] QuitAliases = ["exit"];

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
        
        // Set handler for root command (when no subcommand specified)
        rootCommand.SetAction((parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            
            // Show logo and help when no subcommand
            Logo.Display(jsonOutput);
            return 0;
        });
        
        rootCommand.Subcommands.Add(BuildTodayCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildThisWeekCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildSearchCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildLoginCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildLogoutCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildShellCommand(serviceProvider));
        rootCommand.Subcommands.Add(BuildHelpCommand(jsonOutputOption));
        rootCommand.Subcommands.Add(BuildVersionCommand(jsonOutputOption));
        return rootCommand;
    }

    private static Command BuildVersionCommand(Option<bool> jsonOutputOption)
    {
        var versionCommand = new Command("version", "Display version information");

        versionCommand.Options.Add(jsonOutputOption);

        versionCommand.SetAction((parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            
            // Simple version output (no logo in shell mode to avoid duplication)
            var version = typeof(Logo).Assembly.GetName().Version;
            var versionString = $"Ten Second Tom v{version?.Major}.{version?.Minor}.{version?.Build ?? 0}";
            
            if (jsonOutput)
            {
                AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { version = versionString }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]{versionString}[/]");
                AnsiConsole.MarkupLine("[dim]Your personal memory assistant[/]");
            }
        });

        return versionCommand;
    }

    private static Command BuildHelpCommand(Option<bool> jsonOutputOption)
    {
        var helpCommand = new Command("help", "Display available commands with descriptions");

        helpCommand.Options.Add(jsonOutputOption);

        helpCommand.SetAction((parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            
            if (jsonOutput)
            {
                // JSON output for help
                var commands = new List<object>
                {
                    new { command = "today", description = "Capture today's reflection with 3-5 prompts", requiresAuth = true },
                    new { command = "thisweek", description = "Generate a weekly review from recent daily entries", requiresAuth = true },
                    new { command = "search", description = "Search memory entries by text query", requiresAuth = true },
                    new { command = "login", description = "Authenticate with SSH key and create a session", requiresAuth = false },
                    new { command = "logout", description = "Log out and invalidate the current session", requiresAuth = true },
                    new { command = "help", description = "Display available commands with descriptions", requiresAuth = false },
                    new { command = "quit", description = "Exit the shell", requiresAuth = false, aliases = QuitAliases },
                    new { command = "version", description = "Display version information", requiresAuth = false }
                };
                
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = true, commands }));
            }
            else
            {
                // Pretty formatted help for human readers
                AnsiConsole.MarkupLine("[bold cyan]Available Commands:[/]");
                AnsiConsole.WriteLine();
                
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]Command[/]"))
                    .AddColumn(new TableColumn("[bold]Description[/]"))
                    .AddColumn(new TableColumn("[bold]Auth Required[/]"));
                
                table.AddRow("[cyan]/today[/]", "Capture today's reflection with 3-5 prompts", "[green]Yes[/]");
                table.AddRow("[cyan]/thisweek[/]", "Generate a weekly review from recent daily entries", "[green]Yes[/]");
                table.AddRow("[cyan]/search[/] [dim]<query>[/]", "Search memory entries by text query", "[green]Yes[/]");
                table.AddRow("[cyan]/login[/]", "Authenticate with SSH key and create a session", "[red]No[/]");
                table.AddRow("[cyan]/logout[/]", "Log out and invalidate the current session", "[green]Yes[/]");
                table.AddRow("[cyan]/help[/]", "Display available commands with descriptions", "[red]No[/]");
                table.AddRow("[cyan]/quit[/] or [cyan]/exit[/]", "Exit the shell", "[red]No[/]");
                table.AddRow("[cyan]/version[/]", "Display version information", "[red]No[/]");
                
                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Tip: Type partial commands (e.g., /to) to see suggestions[/]");
            }
            
            return 0;
        });

        return helpCommand;
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
        todayCommand.Options.Add(jsonOutputOption);

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
        thisWeekCommand.Options.Add(jsonOutputOption);

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
        searchCommand.Options.Add(jsonOutputOption);

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

        // Add the global JSON output option to this command
        loginCommand.Options.Add(jsonOutputOption);

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

        // Add the global JSON output option to this command
        logoutCommand.Options.Add(jsonOutputOption);

        // Set action
        logoutCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<AuthLogoutHandler>();
            await LogoutCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
        });

        return logoutCommand;
    }

    private static Command BuildShellCommand(IServiceProvider serviceProvider)
    {
        var shellCommand = new Command("shell", "Start interactive shell mode");

        shellCommand.SetAction(async (parseResult) =>
        {
            var replLoop = serviceProvider.GetRequiredService<IReplLoop>();
            var exitCode = await replLoop.RunAsync(CancellationToken.None).ConfigureAwait(false);
            return exitCode;
        });

        return shellCommand;
    }
}
