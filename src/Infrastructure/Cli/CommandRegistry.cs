using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.OutputFormatters;
using AuthLoginHandler = TenSecondTom.Features.Auth.Handlers.LoginCommandHandler;
using AuthLogoutHandler = TenSecondTom.Features.Auth.Handlers.LogoutCommandHandler;

namespace TenSecondTom.Infrastructure.Cli;

/// <summary>
/// Registry for all CLI commands in the application.
/// Builds the root command with all subcommands configured.
/// </summary>
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
        rootCommand.Subcommands.Add(BuildRecordCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildGenerateCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildLoginCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildLogoutCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildSetupCommand(serviceProvider, jsonOutputOption));
        rootCommand.Subcommands.Add(BuildConfigCommand(serviceProvider, jsonOutputOption));
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
            
            // Get full semantic version including pre-release labels (e.g., "1.1.0-beta.1")
            var assembly = typeof(Logo).Assembly;
            var informationalVersion = assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault() as System.Reflection.AssemblyInformationalVersionAttribute;
            
            // Use informational version if available (supports semver), otherwise fall back to assembly version
            string version = informationalVersion?.InformationalVersion 
                ?? assembly.GetName().Version?.ToString(3) 
                ?? "0.0.0-dev";
            
            var versionString = $"Ten Second Tom v{version}";
            
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
                // JSON output for help - read from CommandMetadata.CommandCatalog
                var commands = CommandMetadata.CommandCatalog
                    .Select(cmd => new
                    {
                        command = cmd.Name.TrimStart('/'), // Remove leading slash for JSON
                        description = cmd.HelpText,
                        requiresAuth = cmd.RequiresAuthentication,
                        aliases = cmd.Aliases?.Select(a => a.TrimStart('/')).ToArray()
                    })
                    .ToList();

                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = true, commands }));
            }
            else
            {
                // Pretty formatted help for human readers - read from CommandMetadata.CommandCatalog
                AnsiConsole.MarkupLine("[bold cyan]Available Commands:[/]");
                AnsiConsole.WriteLine();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn(new TableColumn("[bold]Command[/]"))
                    .AddColumn(new TableColumn("[bold]Description[/]"))
                    .AddColumn(new TableColumn("[bold]Auth Required[/]"));

                foreach (var cmd in CommandMetadata.CommandCatalog)
                {
                    // Format command name with aliases
                    string commandDisplay = $"[cyan]{cmd.Name}[/]";
                    if (cmd.Aliases?.Length > 0)
                    {
                        commandDisplay += $" or {string.Join(" or ", cmd.Aliases.Select(a => $"[cyan]{a}[/]"))}";
                    }

                    // Add special argument hint for search command
                    if (cmd.Name == "/search")
                    {
                        commandDisplay += " [dim]<query>[/]";
                    }

                    // Format auth requirement with color
                    string authDisplay = cmd.RequiresAuthentication ? "[green]Yes[/]" : "[red]No[/]";

                    table.AddRow(commandDisplay, cmd.HelpText, authDisplay);
                }

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

        // Add argument for notes
        var notesArgument = new Argument<string?>("notes")
        {
            Description = "Notes for today. If omitted, opens interactive editor.",
            Arity = ArgumentArity.ZeroOrOne
        };

        // Add options
        var noEditOption = new Option<bool>("--no-edit")
        {
            Description = "Skip interactive editor and use notes from command line argument."
        };

        var useDefaultTemplateOption = new Option<bool>("--use-default-template")
        {
            Description = "Automatically use default template (no prompt)."
        };

        var templateOption = new Option<string?>("--template")
        {
            Description = "Use specific template by name (without .md extension)."
        };

        var providerOption = new Option<string?>("--provider")
        {
            Description = "LLM provider to use (OpenAI or Anthropic). Defaults to configured provider."
        };

        var voiceOption = new Option<bool>("--voice")
        {
            Description = "Capture notes using voice recording instead of text input."
        };

        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, or openai. Only used with --voice."
        };

        // Add argument and options to command
        todayCommand.Arguments.Add(notesArgument);
        todayCommand.Options.Add(noEditOption);
        todayCommand.Options.Add(useDefaultTemplateOption);
        todayCommand.Options.Add(templateOption);
        todayCommand.Options.Add(providerOption);
        todayCommand.Options.Add(voiceOption);
        todayCommand.Options.Add(sttOption);
        todayCommand.Options.Add(jsonOutputOption);

        // Set action
        todayCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? provider = parseResult.GetValue(providerOption);
            string? notes = parseResult.GetValue(notesArgument);
            bool noEdit = parseResult.GetValue(noEditOption);
            bool useDefaultTemplate = parseResult.GetValue(useDefaultTemplateOption);
            string? templateName = parseResult.GetValue(templateOption);
            bool useVoice = parseResult.GetValue(voiceOption);
            string? stt = parseResult.GetValue(sttOption);

            await TodayCommandHandler.ExecuteAsync(
                serviceProvider,
                notes,
                noEdit,
                useDefaultTemplate,
                templateName,
                provider,
                useVoice,
                stt,
                jsonOutput).ConfigureAwait(false);
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

        // Add options for date range filters FIRST (before arguments)
        var fromDateOption = new Option<DateTime?>("--from-date")
        {
            Description = "Start date filter (yyyy-MM-dd). Optional."
        };

        var toDateOption = new Option<DateTime?>("--to-date")
        {
            Description = "End date filter (yyyy-MM-dd). Optional."
        };

        searchCommand.Options.Add(fromDateOption);
        searchCommand.Options.Add(toDateOption);
        searchCommand.Options.Add(jsonOutputOption);

        // Add required query argument AFTER options - allow multiple words without quotes
        // Using ZeroOrMore to allow options to be recognized, then require at least one word in handler
        var queryArgument = new Argument<string[]>("query")
        {
            Description = "The text to search for in memory entries",
            Arity = ArgumentArity.ZeroOrMore
        };

        searchCommand.Arguments.Add(queryArgument);

        // Set action (void) - use Environment.ExitCode to communicate failure
        searchCommand.SetAction((parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string[] queryWords = parseResult.GetValue(queryArgument) ?? [];
            
            // Validate that at least one query word was provided
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
                Environment.ExitCode = 1; // failure exit code
                return;
            }

            // Treat any token starting with '--' (that wasn't parsed as an option) as invalid argument usage
            if (queryWords.Any(w => w.StartsWith("--", StringComparison.Ordinal)))
            {
                string invalidToken = queryWords.First(w => w.StartsWith("--", StringComparison.Ordinal));
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search",
                        $"Invalid search query token '{invalidToken}'. Options must precede the query.",
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Invalid argument:[/] '{invalidToken.EscapeMarkup()}' cannot appear in query text. Specify options before the query.");
                    AnsiConsole.MarkupLine("[dim]Usage: search [[--from-date YYYY-MM-DD]] [[--to-date YYYY-MM-DD]] <query words>[/]");
                }
                Environment.ExitCode = 1; // failure exit code
                return;
            }
            
            string query = string.Join(" ", queryWords); // Join multiple words into single query
            DateTime? fromDate = parseResult.GetValue(fromDateOption);
            DateTime? toDate = parseResult.GetValue(toDateOption);

            // Resolve required services. If not registered (e.g., minimal custom test host) fail gracefully.
            var handler = serviceProvider.GetService<SearchMemoriesQueryHandler>();
            if (handler is null)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search",
                        "Search functionality is unavailable - handler not registered in DI container.",
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Search unavailable:[/] handler not registered. Ensure AddTenSecondTomServices() was called.");
                }
                return;
            }

            var authService = serviceProvider.GetService<IAuthenticationService>();
            if (authService is null)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search",
                        "Authentication service not registered - cannot verify session.",
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Authentication unavailable:[/] service not registered. Ensure AddTenSecondTomServices() was called.");
                }
                return;
            }

            var configuration = serviceProvider.GetService<IConfiguration>();
            if (configuration is null)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonOutputFormatter.FormatFailure("search",
                        "Configuration service not registered - cannot resolve file paths.",
                        DateTimeOffset.UtcNow));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Configuration unavailable:[/] service not registered.");
                }
                return;
            }

            SearchCommandHandler.ExecuteAsync(handler, authService, configuration, query, fromDate, toDate, jsonOutput)
                .GetAwaiter().GetResult();
            Environment.ExitCode = 0; // success
        });

        return searchCommand;
    }

    private static Command BuildRecordCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        return RecordCommandBuilder.BuildRecordCommand(serviceProvider, jsonOutputOption);
    }

    private static Command BuildGenerateCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var generateCommand = new Command("generate", "Generate output from a recording using a prompt template");

        // T041: Add --template option with -t alias for non-interactive template selection
        var templateOption = new Option<string?>("--template", "-t")
        {
            Description = "Template name for non-interactive execution. Automatically selects most recent recording."
        };

        generateCommand.Options.Add(templateOption);
        generateCommand.Options.Add(jsonOutputOption);

        generateCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? templateName = parseResult.GetValue(templateOption);

            var exitCode = await TenSecondTom.Features.Generate.GenerateCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                templateName);

            return exitCode;
        });

        return generateCommand;
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

    private static Command BuildSetupCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var setupCommand = new Command("setup", "Run the guided setup wizard to configure Ten Second Tom");

        // Options
        var forceOption = new Option<bool>("--force")
        {
            Description = "Force setup to run even if configuration exists"
        };
        var nonInteractiveOption = new Option<bool>("--non-interactive")
        {
            Description = "Run setup in non-interactive mode (requires existing configuration)"
        };

        setupCommand.Options.Add(forceOption);
        setupCommand.Options.Add(nonInteractiveOption);
        setupCommand.Options.Add(jsonOutputOption);

        setupCommand.SetAction(async (parseResult) =>
        {
            bool force = parseResult.GetValue(forceOption);
            bool nonInteractive = parseResult.GetValue(nonInteractiveOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            var handler = serviceProvider.GetRequiredService<SetupCommandHandler>();
            
            var command = new SetupCommand
            {
                Force = force,
                NonInteractive = nonInteractive,
                ExistingConfiguration = null
            };

            var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                CommandOutputFormatter.WriteSuccess("Setup completed successfully!", jsonOutput);
                return 0;
            }
            else
            {
                CommandOutputFormatter.WriteError($"Setup failed: {result.Error}", jsonOutput);
                return 1;
            }
        });

        return setupCommand;
    }

    private static Command BuildConfigCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        return ConfigCommandBuilder.BuildConfigCommand(serviceProvider, jsonOutputOption);
    }
}

