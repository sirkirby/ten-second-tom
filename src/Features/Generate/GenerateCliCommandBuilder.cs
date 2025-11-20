using System;
using System.CommandLine;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// Provides the /generate command via ICommandBuilder discovery.
/// </summary>
public sealed class GenerateCliCommandBuilder : ICommandBuilder
{
    public int Priority => 45;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var generateCommand = new Command("generate", "Generate output from a recording using a prompt template");

        var templateOption = new Option<string?>("--template") { Description = "Template name for non-interactive execution. Automatically selects most recent recording." };

        generateCommand.Options.Add(templateOption);
        generateCommand.Options.Add(jsonOutputOption);

        generateCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? templateName = parseResult.GetValue(templateOption);

            var exitCode = await GenerateCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                templateName).ConfigureAwait(false);

            return exitCode;
        });

        return generateCommand;
    }
}
