using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.OutputFormatters;
using TenSecondTom.Shared.Abstractions.UI;

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
        
        // Discover and register feature commands via assembly scanning (VSA pattern)
        var commandBuilders = DiscoverCommandBuilders()
            .OrderBy(b => b.Priority)
            .ToList();

        foreach (var builder in commandBuilders)
        {
            var command = builder.BuildCommand(serviceProvider, jsonOutputOption);
            if (command != null)
            {
                rootCommand.Subcommands.Add(command);
            }
        }

        // Add infrastructure and feature commands (temporarily keeping direct calls until migrated to ICommandBuilder)
        // TODO: Migrate remaining commands to ICommandBuilder pattern
        // Setup command is now auto-discovered via ICommandBuilder pattern
        rootCommand.Subcommands.Add(BuildHelpCommand(jsonOutputOption));
        rootCommand.Subcommands.Add(BuildVersionCommand(jsonOutputOption));
        return rootCommand;
    }

    /// <summary>
    /// Discovers all ICommandBuilder implementations via assembly scanning.
    /// Follows the same pattern as MediatR, FluentValidation, and IConfigSubcommandBuilder auto-discovery.
    /// </summary>
    /// <returns>Collection of discovered command builders.</returns>
    private static IEnumerable<ICommandBuilder> DiscoverCommandBuilders()
    {
        // Scan the main application assembly (same assembly that contains all features)
        // Use the same assembly reference as MediatR/FluentValidation for consistency
        var assembly = typeof(TenSecondTom.Infrastructure.DependencyInjection.ServiceCollectionExtensions).Assembly;

        var builderTypes = assembly.GetTypes()
            .Where(t =>
                typeof(ICommandBuilder).IsAssignableFrom(t) &&
                !t.IsInterface &&
                !t.IsAbstract)
            .ToList();

        foreach (var builderType in builderTypes)
        {
            // Create instance using parameterless constructor (builders are stateless)
            if (Activator.CreateInstance(builderType) is ICommandBuilder builder)
            {
                yield return builder;
            }
        }
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

    // BuildRecordCommand removed - now using ICommandBuilder discovery pattern
    // RecordCommandBuilder implements ICommandBuilder and is discovered via assembly scanning

}

