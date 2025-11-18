using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Features.Audio.Constants;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Builds the 'config audio' subcommand for the Audio feature slice.
/// This keeps all audio-specific CLI knowledge within the Audio feature, following VSA principles.
/// Auto-discovered via assembly scanning of IConfigSubcommandBuilder implementations.
/// </summary>
public sealed class AudioConfigCommandBuilder : IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'config audio' subcommand with all audio-specific options.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured 'audio' subcommand.</returns>
    public Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var audioCommand = new Command("audio", "Configure audio recording and processing settings interactively");
        
        var todayTimeoutOption = new Option<int?>("--today-timeout")
        {
            Description = $"Timeout in seconds for 'today --voice' recording ({AudioConstants.MinTodayTimeoutSeconds}-{AudioConstants.MaxTodayTimeoutSeconds} seconds). When provided, skips the interactive prompt for this setting."
        };

        var recordTimeoutOption = new Option<int?>("--record-timeout")
        {
            Description = $"Timeout in seconds for 'record' command ({AudioConstants.MinRecordTimeoutSeconds}-{AudioConstants.MaxRecordTimeoutSeconds} seconds). When provided, skips the interactive prompt for this setting."
        };

        var inputVolumeOption = new Option<double?>("--input-volume")
        {
            Description = "Input volume multiplier (0.0 to 2.0). Typical values: 0.7-0.8 for dynamic mics, 1.0-1.2 for laptop mics. When provided, skips the interactive prompt for this setting."
        };

        var noiseReductionOption = new Option<bool?>("--noise-reduction")
        {
            Description = "Enable noise reduction during recording (true/false). Recommended for laptop mics, disable for professional mics. When provided, skips the interactive prompt for this setting."
        };

        var frequencyFiltersOption = new Option<bool?>("--frequency-filters")
        {
            Description = "Enable frequency filters during recording (true/false). Removes rumble and hiss. Recommended for most scenarios. When provided, skips the interactive prompt for this setting."
        };

        audioCommand.Options.Add(todayTimeoutOption);
        audioCommand.Options.Add(recordTimeoutOption);
        audioCommand.Options.Add(inputVolumeOption);
        audioCommand.Options.Add(noiseReductionOption);
        audioCommand.Options.Add(frequencyFiltersOption);
        audioCommand.Options.Add(jsonOutputOption);

        audioCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            int? todayTimeout = parseResult.GetValue(todayTimeoutOption);
            int? recordTimeout = parseResult.GetValue(recordTimeoutOption);
            double? inputVolume = parseResult.GetValue(inputVolumeOption);
            bool? noiseReduction = parseResult.GetValue(noiseReductionOption);
            bool? frequencyFilters = parseResult.GetValue(frequencyFiltersOption);

            // Create ConfigureAudio command and send via MediatR
            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var configureAudioCommand = new ConfigureAudio.Command
            {
                TodayTimeoutSeconds = todayTimeout,
                RecordTimeoutSeconds = recordTimeout,
                InputVolume = inputVolume,
                EnableNoiseReduction = noiseReduction,
                EnableFrequencyFilters = frequencyFilters
            };

            var audioResult = await mediator.Send(configureAudioCommand, CancellationToken.None).ConfigureAwait(false);

            if (audioResult.IsSuccess)
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = true,
                        message = "Audio configuration updated successfully"
                    }));
                }
                // Success message already displayed by ConfigureAudio.Handler
                return 0;
            }
            else
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = audioResult.Error }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] {audioResult.Error?.EscapeMarkup() ?? "Audio configuration failed"}");
                }
                return 1;
            }
        });

        return audioCommand;
    }
}

