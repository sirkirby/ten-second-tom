using System.CommandLine;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Builds the record command for audio recording with transcription.
/// Implements ICommandBuilder for automatic discovery via assembly scanning.
/// </summary>
public sealed class RecordCommandBuilder : ICommandBuilder
{
    private static readonly System.Text.Json.JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    /// <summary>
    /// Priority for command ordering. Primary command (record audio).
    /// </summary>
    public int Priority => 20;

    /// <summary>
    /// Builds the record command for automatic discovery.
    /// </summary>
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var recordCommand = new Command("record", "Record audio with transcription and save to recording/ directory");

        // Add options
        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, or openai."
        };

        // Recording override options
        var inputVolumeOption = new Option<double?>("--input-volume")
        {
            Description = "Input volume multiplier for this recording (0.0 to 2.0). Overrides configured default."
        };

        var noiseReductionOption = new Option<bool?>("--noise-reduction")
        {
            Description = "Enable/disable noise reduction for this recording. Overrides configured default."
        };

        var frequencyFiltersOption = new Option<bool?>("--frequency-filters")
        {
            Description = "Enable/disable frequency filters for this recording. Overrides configured default."
        };

        // Preprocessing override options
        var removeSilenceOption = new Option<bool?>("--remove-silence")
        {
            Description = "Enable/disable silence removal for this recording. Overrides configured default."
        };

        var silenceThresholdOption = new Option<int?>("--silence-threshold-db")
        {
            Description = "Silence detection threshold in dB for this recording (-60 to -40). Overrides configured default."
        };

        var minSilenceDurationOption = new Option<int?>("--min-silence-duration-ms")
        {
            Description = "Minimum silence duration to remove in milliseconds for this recording (100 to 2000). Overrides configured default."
        };

        recordCommand.Options.Add(sttOption);
        recordCommand.Options.Add(inputVolumeOption);
        recordCommand.Options.Add(noiseReductionOption);
        recordCommand.Options.Add(frequencyFiltersOption);
        recordCommand.Options.Add(removeSilenceOption);
        recordCommand.Options.Add(silenceThresholdOption);
        recordCommand.Options.Add(minSilenceDurationOption);
        recordCommand.Options.Add(jsonOutputOption);

        // Set action
        recordCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? stt = parseResult.GetValue(sttOption);

            // Parse recording overrides
            double? inputVolume = parseResult.GetValue(inputVolumeOption);
            bool? noiseReduction = parseResult.GetValue(noiseReductionOption);
            bool? frequencyFilters = parseResult.GetValue(frequencyFiltersOption);

            // Parse preprocessing overrides
            bool? removeSilence = parseResult.GetValue(removeSilenceOption);
            int? silenceThreshold = parseResult.GetValue(silenceThresholdOption);
            int? minSilenceDuration = parseResult.GetValue(minSilenceDurationOption);

            // Build override objects (only if any values were provided)
            RecordingOverrides? recordingOverrides = null;
            if (inputVolume.HasValue || noiseReduction.HasValue || frequencyFilters.HasValue)
            {
                recordingOverrides = new RecordingOverrides
                {
                    InputVolume = inputVolume,
                    EnableNoiseReduction = noiseReduction,
                    EnableFrequencyFilters = frequencyFilters
                };
            }

            PreprocessingOverrides? preprocessingOverrides = null;
            if (removeSilence.HasValue || silenceThreshold.HasValue || minSilenceDuration.HasValue)
            {
                preprocessingOverrides = new PreprocessingOverrides
                {
                    RemoveSilence = removeSilence,
                    SilenceThresholdDb = silenceThreshold,
                    MinSilenceDurationMs = minSilenceDuration
                };
            }

            var mediator = serviceProvider.GetRequiredService<IMediator>();

            var audioOptionsResult = await mediator.Send(new GetAudioConfiguration.Query(), CancellationToken.None)
                .ConfigureAwait(false);

            if (!audioOptionsResult.IsSuccess || audioOptionsResult.Value is null)
            {
                CommandOutputFormatter.WriteError(
                    audioOptionsResult.Error ?? "Audio configuration is unavailable.",
                    jsonOutput);
                return 1;
            }

            var audioOptions = audioOptionsResult.Value;

            var transcribeOptionsResult = await mediator.Send(new GetTranscribeConfiguration.Query(), CancellationToken.None)
                .ConfigureAwait(false);

            if (!transcribeOptionsResult.IsSuccess || transcribeOptionsResult.Value is null)
            {
                CommandOutputFormatter.WriteError(
                    transcribeOptionsResult.Error ?? "Transcription configuration is unavailable.",
                    jsonOutput);
                return 1;
            }

            var transcribeOptions = transcribeOptionsResult.Value;

            if (!SttSelectionMapper.TryParse(stt, out var sttSelection, out var sttError))
            {
                CommandOutputFormatter.WriteValidationError("STT selection", sttError!, jsonOutput);
                return 1;
            }

            // Authenticate first (record command creates data that will be used by authenticated commands)
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();

            // Show warning if using mock authentication (only in non-JSON mode)
            if (!jsonOutput && authService is MockAuthenticationService)
            {
                CommandOutputFormatter.WriteWarning("Development Mode: Authentication bypassed", jsonOutput);
                AnsiConsole.WriteLine();
            }

            var authResult = await AuthenticationHelper.EnsureAuthenticatedAsync(
                authService,
                CommandNames.Record,
                jsonOutput,
                CancellationToken.None);

            if (!authResult.IsSuccess)
            {
                return 1; // Authentication failed
            }

            // Validate transcription configuration
            var audioValidator = serviceProvider.GetRequiredService<TenSecondTom.Features.Audio.Services.IAudioConfigurationValidator>();
            var audioConfigResult = TenSecondTom.Features.Audio.AudioConfigurationHelper.EnsureAudioConfigured(
                audioValidator,
                transcribeOptions,
                CommandNames.Record,
                jsonOutput);

            if (!audioConfigResult.IsSuccess)
            {
                return 1; // Audio configuration incomplete
            }

            // Get handler
            var handler = serviceProvider.GetService<Record.Handler>();
            if (handler is null)
            {
                CommandOutputFormatter.WriteError(
                    "Record functionality is unavailable - handler not registered in DI container. Ensure AddFeatureAudioServices() was called.",
                    jsonOutput);
                return 1;
            }

            // Display microphone information before recording (unless in JSON mode)
            if (!jsonOutput)
            {
                var audioRecorder = serviceProvider.GetService<TenSecondTom.Features.Audio.Services.IAudioRecorder>();
                if (audioRecorder is not null)
                {
                    var micResult = await audioRecorder.GetDefaultMicrophoneNameAsync(CancellationToken.None);
                    if (micResult.IsSuccess)
                    {
                        AnsiConsole.MarkupLine($"[dim]Microphone: {micResult.Value.EscapeMarkup()}[/]");
                    }
                }
            }

            // Show recording prompt (unless in JSON mode)
            if (!jsonOutput)
            {
                AnsiConsole.MarkupLine("[cyan]🎤 Recording... Press Enter to stop.[/]");
            }

            // Execute command with configured timeout
            var command = new Record.Command
            {
                TranscribeConfig = SttSelectionMapper.BuildTranscribeOptions(sttSelection, transcribeOptions),
                MaxDurationSeconds = audioOptions.Timeouts.RecordSeconds,  // Use configured timeout
                RecordingOverrides = recordingOverrides,
                PreprocessingOverrides = preprocessingOverrides
            };

            var result = await handler.Handle(command, CancellationToken.None);

            if (result.IsSuccess && result.Value is not null)
            {
                var recording = result.Value;

                if (jsonOutput)
                {
                    var output = new
                    {
                        success = true,
                        audio_path = recording.AudioFilePath,
                        transcription_path = recording.TranscriptionFilePath,
                        text = File.ReadAllText(recording.TranscriptionFilePath),
                        duration_seconds = recording.Duration.TotalSeconds,
                        word_count = recording.TranscriptionWordCount,
                        stt_engine = recording.SttEngine.ToString(),
                        stt_model = recording.SttModel,
                        recorded_at = recording.RecordedAt,
                        file_size_bytes = recording.FileSizeBytes
                    };
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(output, SnakeCaseJsonOptions));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Recording complete ({recording.Duration.TotalSeconds:F1}s)");
                    AnsiConsole.MarkupLine($"[green]✓[/] Transcription complete ({recording.TranscriptionWordCount} words, {recording.SttEngine})");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[bold]Transcript:[/]");

                    // Read and display the transcription text
                    var transcriptContent = File.ReadAllText(recording.TranscriptionFilePath);
                    var fullTranscript = TranscriptFormatter.StripFrontmatter(transcriptContent);
                    var (formattedText, wasTruncated, _) = TranscriptFormatter.FormatForDisplay(fullTranscript);

                    AnsiConsole.MarkupLine($"[dim]{formattedText.EscapeMarkup()}[/]");

                    AnsiConsole.WriteLine();
                    if (wasTruncated)
                    {
                        AnsiConsole.MarkupLine($"[dim]Full transcript: {recording.TranscriptionFilePath.EscapeMarkup()}[/]");
                    }
                    AnsiConsole.MarkupLine($"[dim]Audio saved: {recording.AudioFilePath.EscapeMarkup()}[/]");
                }
                return 0;
            }
            else
            {
                CommandOutputFormatter.WriteError(result.Error ?? "Recording failed", jsonOutput);
                return 1;
            }
        });

        return recordCommand;
    }

}
