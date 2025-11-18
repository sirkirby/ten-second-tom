using TenSecondTom.Features.Audio.Constants;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Builds the record command for audio recording with transcription.
/// Separated from CommandRegistry to improve maintainability.
/// </summary>
internal static class RecordCommandBuilder
{
    private static readonly System.Text.Json.JsonSerializerOptions SnakeCaseJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static Command BuildRecordCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var recordCommand = new Command("record", "Record audio with transcription and save to recording/ directory");

        // Add options
        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, or openai."
        };

        recordCommand.Options.Add(sttOption);
        recordCommand.Options.Add(jsonOutputOption);

        // Set action
        recordCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? stt = parseResult.GetValue(sttOption);

            // Get AudioConfiguration to read default SttProvider and timeout
            var audioConfig = serviceProvider.GetService<IOptions<AudioConfiguration>>()?.Value
                ?? new AudioConfiguration();

            // Parse STT selection - use configured default or fall back to Auto
            var sttSelection = SttSelection.Auto;

            // If no --stt flag provided, use configuration default
            if (string.IsNullOrWhiteSpace(stt))
            {
                if (audioConfig.SttProvider is not null)
                {
                    if (!Enum.TryParse<SttSelection>(audioConfig.SttProvider, ignoreCase: true, out sttSelection))
                    {
                        // Invalid config value, fall back to Auto
                        sttSelection = SttSelection.Auto;
                    }
                }
            }
            else
            {
                // --stt flag provided, parse and validate it
                if (!Enum.TryParse<SttSelection>(stt, ignoreCase: true, out sttSelection))
                {
                    CommandOutputFormatter.WriteValidationError(
                        "STT selection",
                        $"Invalid value: {stt}. Valid options: auto, local, openai",
                        jsonOutput);

                    if (!jsonOutput)
                    {
                        CommandOutputFormatter.WriteInfo("Valid options: auto, local, openai", jsonOutput);
                    }
                    return 1;
                }
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

            // Validate audio configuration
            var audioValidator = serviceProvider.GetRequiredService<TenSecondTom.Features.Audio.Services.IAudioConfigurationValidator>();
            var audioConfigResult = TenSecondTom.Features.Audio.AudioConfigurationHelper.EnsureAudioConfigured(
                audioValidator,
                audioConfig,
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
                AudioConfig = ConvertSttSelectionToConfig(sttSelection, audioConfig),
                MaxDurationSeconds = audioConfig.Timeouts.RecordSeconds  // Use configured timeout
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

    /// <summary>
    /// Converts CLI SttSelection intent to AudioConfiguration.
    /// Maps user-friendly CLI options (auto/local/openai) to the proper configuration.
    /// </summary>
    /// <param name="selection">The STT selection from CLI (auto, local, or openai)</param>
    /// <param name="baseConfig">The base audio configuration from appsettings</param>
    /// <returns>AudioConfiguration with appropriate provider and fallback settings</returns>
    private static AudioConfiguration ConvertSttSelectionToConfig(SttSelection selection, AudioConfiguration baseConfig)
    {
        return selection switch
        {
            // Auto: Try local provider first, fallback to OpenAI cloud if enabled
            SttSelection.Auto => new AudioConfiguration
            {
                SttProvider = baseConfig.SttProvider,
                SttBinaryPath = baseConfig.SttBinaryPath,
                SttModel = baseConfig.SttModel,
                SttApiKey = baseConfig.SttApiKey,
                SttFallbackEnabled = true,
                SttFallbackProvider = baseConfig.SttFallbackProvider,
                SttFallbackBinaryPath = baseConfig.SttFallbackBinaryPath,
                SttFallbackModel = baseConfig.SttFallbackModel,
                SttFallbackApiKey = baseConfig.SttFallbackApiKey,
                Recorder = baseConfig.Recorder,
                KeepFiles = baseConfig.KeepFiles,
                Preprocessing = baseConfig.Preprocessing,
                Timeouts = baseConfig.Timeouts
            },

            // Local: Use only the configured local provider (whisper.cpp, ollama, etc.) - no fallback
            SttSelection.Local => new AudioConfiguration
            {
                SttProvider = baseConfig.SttProvider,
                SttBinaryPath = baseConfig.SttBinaryPath,
                SttModel = baseConfig.SttModel,
                SttApiKey = baseConfig.SttApiKey,
                SttFallbackEnabled = false,
                Recorder = baseConfig.Recorder,
                KeepFiles = baseConfig.KeepFiles,
                Preprocessing = baseConfig.Preprocessing,
                Timeouts = baseConfig.Timeouts
            },

            // OpenAI: Force OpenAI provider, no fallback
            SttSelection.OpenAI => new AudioConfiguration
            {
                SttProvider = SttProviders.OpenAI,
                SttBinaryPath = baseConfig.SttBinaryPath,
                SttModel = baseConfig.SttModel,
                SttApiKey = baseConfig.SttApiKey,
                SttFallbackEnabled = false,
                Recorder = baseConfig.Recorder,
                KeepFiles = baseConfig.KeepFiles,
                Preprocessing = baseConfig.Preprocessing,
                Timeouts = baseConfig.Timeouts
            },

            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection,
                $"Unsupported STT selection: {selection}")
        };
    }
}
