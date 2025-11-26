using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Features.Audio.Constants;
using TenSecondTom.Infrastructure.Cli;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Builds the top-level <c>audio</c> CLI command for audio configuration and management.
/// <list type="bullet">
///   <item><c>audio config</c> - Configure audio recording settings</item>
/// </list>
/// Future subcommands: mic (microphone selection), test (audio testing).
/// Note: Use <c>/record</c> for recording with transcription.
/// </summary>
public sealed class AudioCommandBuilder : ICommandBuilder
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    /// <inheritdoc />
    public int Priority => 15;

    /// <inheritdoc />
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var audioCommand = new Command("audio", "Audio configuration and management");
        audioCommand.Options.Add(jsonOutputOption);

        // Default action: show current audio config
        audioCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var mediator = serviceProvider.GetRequiredService<IMediator>();

            var configResult = await mediator.Send(new GetAudioConfiguration.Query(), CancellationToken.None);

            if (!configResult.IsSuccess || configResult.Value is null)
            {
                CommandOutputFormatter.WriteError(
                    configResult.Error ?? "Failed to get audio configuration",
                    jsonOutput);
                return 1;
            }

            var config = configResult.Value;

            if (jsonOutput)
            {
                var json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = true,
                    timeouts = new
                    {
                        today_seconds = config.Timeouts.TodaySeconds,
                        record_seconds = config.Timeouts.RecordSeconds
                    },
                    recorder = new
                    {
                        input_volume = config.Recorder.InputVolume,
                        noise_reduction = config.Recorder.EnableNoiseReduction,
                        frequency_filters = config.Recorder.EnableFrequencyFilters
                    }
                }, JsonOptions);
                Console.WriteLine(json);
            }
            else
            {
                AnsiConsole.MarkupLine("[bold cyan]Audio Configuration[/]");
                AnsiConsole.WriteLine();

                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Setting")
                    .AddColumn("Value");

                table.AddRow("Today Timeout", $"{config.Timeouts.TodaySeconds} seconds");
                table.AddRow("Record Timeout", $"{config.Timeouts.RecordSeconds} seconds");
                table.AddRow("Input Volume", $"{config.Recorder.InputVolume:F1}x");
                table.AddRow("Noise Reduction", config.Recorder.EnableNoiseReduction ? "Enabled" : "Disabled");
                table.AddRow("Frequency Filters", config.Recorder.EnableFrequencyFilters ? "Enabled" : "Disabled");

                AnsiConsole.Write(table);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Use 'tom audio config' to change settings[/]");
            }

            return 0;
        });

        // Add config subcommand
        audioCommand.Subcommands.Add(BuildConfigSubcommand(serviceProvider, jsonOutputOption));

        return audioCommand;
    }

    private static Command BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var configCommand = new Command("config", "Configure audio recording settings interactively");

        var recordTimeoutOption = new Option<int?>("--record-timeout")
        {
            Description = $"Recording timeout in seconds ({AudioConstants.MinRecordTimeoutSeconds}-{AudioConstants.MaxRecordTimeoutSeconds} seconds)."
        };

        var inputVolumeOption = new Option<double?>("--input-volume")
        {
            Description = "Input volume multiplier (0.0 to 2.0). Typical: 0.7-0.8 for dynamic mics, 1.0-1.2 for laptop mics."
        };

        var noiseReductionOption = new Option<bool?>("--noise-reduction")
        {
            Description = "Enable noise reduction during recording (true/false)."
        };

        var frequencyFiltersOption = new Option<bool?>("--frequency-filters")
        {
            Description = "Enable frequency filters during recording (true/false)."
        };

        configCommand.Options.Add(recordTimeoutOption);
        configCommand.Options.Add(inputVolumeOption);
        configCommand.Options.Add(noiseReductionOption);
        configCommand.Options.Add(frequencyFiltersOption);
        configCommand.Options.Add(jsonOutputOption);

        configCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            int? recordTimeout = parseResult.GetValue(recordTimeoutOption);
            double? inputVolume = parseResult.GetValue(inputVolumeOption);
            bool? noiseReduction = parseResult.GetValue(noiseReductionOption);
            bool? frequencyFilters = parseResult.GetValue(frequencyFiltersOption);

            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var command = new ConfigureAudio.Command
            {
                RecordTimeoutSeconds = recordTimeout,
                InputVolume = inputVolume,
                EnableNoiseReduction = noiseReduction,
                EnableFrequencyFilters = frequencyFilters
            };

            var result = await mediator.Send(command, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = true,
                        message = "Audio configuration updated successfully"
                    }, JsonOptions));
                }
                return 0;
            }
            else
            {
                CommandOutputFormatter.WriteError(
                    result.Error ?? "Audio configuration failed",
                    jsonOutput);
                return 1;
            }
        });

        return configCommand;
    }
}
