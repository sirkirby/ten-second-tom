using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Builds the setup command for the CLI.
/// Implements ICommandBuilder for automatic discovery via assembly scanning.
/// </summary>
public sealed class SetupCommandBuilder : ICommandBuilder
{
    /// <summary>
    /// Priority for command ordering. Setup is a core command.
    /// </summary>
    public int Priority => 10;

    /// <summary>
    /// Builds the setup command.
    /// </summary>
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        return BuildSetupCommand(serviceProvider, jsonOutputOption);
    }

    public static Command BuildSetupCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var setupCommand = new Command("setup", "Run the guided setup wizard to configure Ten Second Tom");

        // Options
        var forceOption = new Option<bool>("--force")
        {
            Description = "Force setup to run even if configuration exists"
        };
        var nonInteractiveOption = new Option<bool>("--non-interactive")
        {
            Description = "Run setup without interactive prompts (fails if input required)"
        };

        setupCommand.Options.Add(forceOption);
        setupCommand.Options.Add(nonInteractiveOption);
        setupCommand.Options.Add(jsonOutputOption);

        setupCommand.SetAction(async (parseResult) =>
        {
            bool force = parseResult.GetValue(forceOption);
            bool nonInteractive = parseResult.GetValue(nonInteractiveOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            var command = new Setup.Command
            {
                Force = force,
                NonInteractive = nonInteractive
            };

            var handler = serviceProvider.GetRequiredService<Setup.Handler>();
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
}
