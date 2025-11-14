using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Builds the 'config llm' subcommand for the Setup feature slice.
/// This keeps all LLM configuration CLI knowledge within the Setup feature, following VSA principles.
/// Auto-discovered via assembly scanning of IConfigSubcommandBuilder implementations.
/// </summary>
public sealed class LlmConfigCommandBuilder : IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'config llm' subcommand.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured 'llm' subcommand.</returns>
    public Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var llmCommand = new Command("llm", "Configure LLM provider and model interactively");
        llmCommand.Options.Add(jsonOutputOption);

        llmCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            // Create ConfigureLlm command and send via MediatR
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var configureLlmCommand = new ConfigureLlm.Command();

            var result = await mediator.Send(configureLlmCommand, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    var config = result.Value!;
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = true,
                        provider = config.Llm.Provider.ToString(),
                        model = config.Llm.Model
                    }));
                }
                // Success message already displayed by ConfigureLlm.Handler
                return 0;
            }
            else
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = result.Error }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error?.EscapeMarkup() ?? "LLM configuration failed"}");
                }
                return 1;
            }
        });

        return llmCommand;
    }
}

