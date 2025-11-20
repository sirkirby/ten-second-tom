using System;
using System.CommandLine;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Shell.Services;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Shell;

/// <summary>
/// Provides the /shell command via ICommandBuilder discovery.
/// </summary>
public sealed class ShellCliCommandBuilder : ICommandBuilder
{
    public int Priority => 90;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var shellCommand = new Command("shell", "Start interactive shell mode");

        shellCommand.SetAction(async _ =>
        {
            var replLoop = serviceProvider.GetRequiredService<IReplLoop>();
            var exitCode = await replLoop.RunAsync(CancellationToken.None).ConfigureAwait(false);
            return exitCode;
        });

        return shellCommand;
    }
}
