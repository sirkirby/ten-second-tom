using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Adds the /logout command via ICommandBuilder discovery.
/// </summary>
public sealed class LogoutCliCommandBuilder : ICommandBuilder
{
    public int Priority => 81;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var logoutCommand = new Command("logout", "Log out and invalidate the current session");
        logoutCommand.Options.Add(jsonOutputOption);

        logoutCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<Logout.Handler>();
            await LogoutCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
        });

        return logoutCommand;
    }
}
