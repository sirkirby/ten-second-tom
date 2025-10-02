using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Today.Handlers;

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
            await TodayCommandHandler.ExecuteAsync(handler, provider).ConfigureAwait(false);
        });

        return todayCommand;
    }
}
