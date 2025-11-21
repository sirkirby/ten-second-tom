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

        var templateOption = new Option<string?>("--template") { Description = "Template ID to use. If not provided, interactive selection is used." };
        var noteOption = new Option<string?>("--note") { Description = "Note filename (without extension) to process." };
        var recordingOption = new Option<string?>("--recording") { Description = "Recording filename (without extension) to process." };
        var listTemplatesOption = new Option<bool>("--list-templates") { Description = "List available templates and exit." };

        generateCommand.Options.Add(templateOption);
        generateCommand.Options.Add(noteOption);
        generateCommand.Options.Add(recordingOption);
        generateCommand.Options.Add(listTemplatesOption);
        generateCommand.Options.Add(jsonOutputOption);

        generateCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? templateId = parseResult.GetValue(templateOption);
            string? noteName = parseResult.GetValue(noteOption);
            string? recordingName = parseResult.GetValue(recordingOption);
            bool listTemplates = parseResult.GetValue(listTemplatesOption);

            var exitCode = await GenerateCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                templateId,
                noteName,
                recordingName,
                listTemplates).ConfigureAwait(false);

            return exitCode;
        });

        return generateCommand;
    }
}
