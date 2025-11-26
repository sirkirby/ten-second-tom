using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Builds the top-level <c>auth</c> CLI command with subcommands:
/// <list type="bullet">
///   <item><c>auth login</c> - Authenticate with SSH key</item>
///   <item><c>auth logout</c> - Log out and invalidate session</item>
///   <item><c>auth config</c> - Configure SSH authentication settings</item>
/// </list>
/// </summary>
public sealed class AuthCommandBuilder : ICommandBuilder
{
    /// <inheritdoc />
    public int Priority => 75; // Before login (80) and logout (81)

    /// <inheritdoc />
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var authCommand = new Command("auth", "Authentication management commands");
        authCommand.Options.Add(jsonOutputOption);

        // Add subcommands
        authCommand.Subcommands.Add(BuildLoginSubcommand(serviceProvider, jsonOutputOption));
        authCommand.Subcommands.Add(BuildLogoutSubcommand(serviceProvider, jsonOutputOption));
        authCommand.Subcommands.Add(BuildConfigSubcommand(serviceProvider, jsonOutputOption));

        return authCommand;
    }

    private static Command BuildLoginSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var loginCommand = new Command("login", "Authenticate with SSH key and create a session");
        loginCommand.Options.Add(jsonOutputOption);

        loginCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<Login.Handler>();
            await LoginCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
            return 0;
        });

        return loginCommand;
    }

    private static Command BuildLogoutSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var logoutCommand = new Command("logout", "Log out and invalidate the current session");
        logoutCommand.Options.Add(jsonOutputOption);

        logoutCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<Logout.Handler>();
            await LogoutCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
            return 0;
        });

        return logoutCommand;
    }

    private static Command BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        // Delegate to the existing AuthConfigCommandBuilder
        var configBuilder = serviceProvider.GetService<AuthConfigCommandBuilder>();
        if (configBuilder is not null)
        {
            var configCommand = configBuilder.BuildConfigSubcommand(serviceProvider, jsonOutputOption);
            if (configCommand is not null)
            {
                // Rename from "auth" to "config" since we're nesting under auth parent
                var renamedCommand = new Command("config", configCommand.Description);
                foreach (var option in configCommand.Options)
                {
                    renamedCommand.Options.Add(option);
                }
                // Copy the action by creating a handler that delegates
                renamedCommand.SetAction(parseResult =>
                {
                    // The action is already set on configCommand, but we need to forward to it
                    // Since we can't easily copy the action, let's just inline the logic
                    return ExecuteConfigAsync(serviceProvider, parseResult.GetValue(jsonOutputOption));
                });
                return renamedCommand;
            }
        }

        // Fallback placeholder
        var placeholder = new Command("config", "Configure SSH authentication settings");
        placeholder.SetAction(_ =>
        {
            AnsiConsole.MarkupLine("[yellow]Auth config not available.[/]");
            return Task.FromResult(1);
        });
        return placeholder;
    }

    private static async Task<int> ExecuteConfigAsync(IServiceProvider serviceProvider, bool jsonOutput)
    {
        var mediator = serviceProvider.GetRequiredService<MediatR.IMediator>();
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
    }
}
