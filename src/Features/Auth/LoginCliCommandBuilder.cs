using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Adds the legacy /login command via the ICommandBuilder discovery pattern.
/// Keeps authentication concerns within the Auth feature slice.
/// </summary>
public sealed class LoginCliCommandBuilder : ICommandBuilder
{
    public int Priority => 80; // Management commands range

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var loginCommand = new Command("login", "Authenticate with SSH key and create a session");
        loginCommand.Options.Add(jsonOutputOption);

        loginCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var handler = serviceProvider.GetRequiredService<Login.Handler>();
            await LoginCommandHandler.ExecuteAsync(handler, jsonOutput).ConfigureAwait(false);
        });

        return loginCommand;
    }
}
