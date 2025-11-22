using System;
using System.CommandLine;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Registers the top-level <c>transcribe</c> CLI command via discovery.
/// </summary>
public sealed class TranscribeCommandBuilder : ICommandBuilder
{
    /// <inheritdoc />
    public int Priority => 25;

    /// <inheritdoc />
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var command = new Command("transcribe", "Transcribe an existing note/recording WAV file");

        var noteOption = new Option<string?>("--note")
        {
            Description = "Note base name (without extension) to transcribe."
        };
        var recordingOption = new Option<string?>("--recording")
        {
            Description = "Recording base name (without extension) to re-transcribe."
        };
        var fileOption = new Option<string?>("--file")
        {
            Description = "Path to a standalone .wav file to import."
        };
        var nameOption = new Option<string?>("--name")
        {
            Description = "Override the destination recording name (defaults to source)."
        };
        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, openai."
        };
        var listOption = new Option<bool>("--list")
        {
            Description = "List available audio files and exit."
        };
        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing transcript/audio if present."
        };

        command.Options.Add(noteOption);
        command.Options.Add(recordingOption);
        command.Options.Add(fileOption);
        command.Options.Add(nameOption);
        command.Options.Add(sttOption);
        command.Options.Add(listOption);
        command.Options.Add(forceOption);
        command.Options.Add(jsonOutputOption);

        command.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? noteName = parseResult.GetValue(noteOption);
            string? recordingName = parseResult.GetValue(recordingOption);
            string? filePath = parseResult.GetValue(fileOption);
            string? customName = parseResult.GetValue(nameOption);
            string? sttSelection = parseResult.GetValue(sttOption);
            bool listOnly = parseResult.GetValue(listOption);
            bool force = parseResult.GetValue(forceOption);

            return await TranscribeCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                noteName,
                recordingName,
                filePath,
                customName,
                sttSelection,
                listOnly,
                force).ConfigureAwait(false);
        });

        return command;
    }
}
