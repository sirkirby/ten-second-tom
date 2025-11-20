using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Builds the 'config auth' subcommand for the Auth feature slice so it can participate
/// in the setup/config flows without other features taking a direct dependency on Auth.
/// Auto-discovered via assembly scanning of IConfigSubcommandBuilder implementations.
/// </summary>
public sealed class AuthConfigCommandBuilder : IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'config auth' subcommand.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured 'auth' subcommand.</returns>
    public Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var authCommand = new Command("auth", "Configure SSH authentication settings interactively");
        authCommand.Options.Add(jsonOutputOption);

        authCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            // Create ConfigureSsh command and send via MediatR
            // Force=true because user explicitly called /config auth to reconfigure
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var configureSshCommand = new ConfigureSsh.Command
            {
                DetectionTimeout = TimeSpan.FromSeconds(5),
                Force = true
            };

            var result = await mediator.Send(configureSshCommand, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    var config = result.Value!;
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = true,
                        keySource = config.KeySource.ToString(),
                        keyPath = config.KeyPath,
                        keyDisplayName = config.KeyDisplayName
                    }));
                }
                // Success message already displayed by ConfigureSsh.Handler
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
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error?.EscapeMarkup() ?? "SSH authentication configuration failed"}");
                }
                return 1;
            }
        });

        return authCommand;
    }
}
