using System;
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Abstractions.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Registers the top-level <c>transcribe</c> CLI command via discovery.
/// Includes model management for STT providers that support it.
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

        var command = new Command("transcribe", "Transcribe audio files or manage STT models");

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
            Description = "Path to a standalone audio file to import (.wav, .mp3, or .flac)."
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

        // Model management options
        var listModelsOption = new Option<bool>("--list-models")
        {
            Description = "List available STT models for the configured provider."
        };
        var downloadModelOption = new Option<string?>("--download-model")
        {
            Description = "Download a specific STT model by ID."
        };
        var providerOption = new Option<string?>("--provider")
        {
            Description = "Override the STT provider for model operations (whisper-cpp, built-in-local)."
        };

        command.Options.Add(noteOption);
        command.Options.Add(recordingOption);
        command.Options.Add(fileOption);
        command.Options.Add(nameOption);
        command.Options.Add(sttOption);
        command.Options.Add(listOption);
        command.Options.Add(forceOption);
        command.Options.Add(listModelsOption);
        command.Options.Add(downloadModelOption);
        command.Options.Add(providerOption);
        command.Options.Add(jsonOutputOption);

        command.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            bool listModels = parseResult.GetValue(listModelsOption);
            string? downloadModel = parseResult.GetValue(downloadModelOption);
            string? providerArg = parseResult.GetValue(providerOption);

            // Handle model management operations first
            if (listModels || !string.IsNullOrEmpty(downloadModel))
            {
                return await HandleModelManagementAsync(
                    serviceProvider,
                    jsonOutput,
                    listModels,
                    downloadModel,
                    providerArg);
            }

            // Standard transcription operations
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

    private static async Task<int> HandleModelManagementAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        bool listModels,
        string? downloadModel,
        string? providerArg)
    {
        var options = serviceProvider.GetRequiredService<IOptions<AudioOptions>>().Value;

        // Determine which provider to use
        string provider = !string.IsNullOrEmpty(providerArg) ? providerArg : options.SttProvider;

        // Validate provider supports model management
        if (provider != SttProviders.WhisperCpp && provider != SttProviders.BuiltInLocal)
        {
            if (jsonOutput)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Provider '{provider}' does not support model management. Use whisper-cpp or built-in-local."
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Provider '{provider}' does not support model management.[/]");
                AnsiConsole.MarkupLine("[dim]Model management is available for whisper-cpp and built-in-local providers.[/]");
            }
            return 1;
        }

        // Get the appropriate model management interface
        ISupportsModelManagement? modelManagement = provider switch
        {
            SttProviders.WhisperCpp => serviceProvider.GetRequiredService<WhisperNetSttProvider>(),
            SttProviders.BuiltInLocal => serviceProvider.GetRequiredService<BuiltInLocalSttProvider>(),
            _ => null
        };

        if (modelManagement == null)
        {
            if (jsonOutput)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Could not initialize model management for provider '{provider}'."
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error: Could not initialize model management for '{provider}'[/]");
            }
            return 1;
        }

        // Handle --list-models
        if (listModels)
        {
            var models = await modelManagement.ListModelsAsync(CancellationToken.None);

            if (jsonOutput)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = true, provider, models }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[cyan]Available STT models for {provider}:[/]");
                foreach (var model in models)
                {
                    AnsiConsole.MarkupLine($"  • [green]{model.EscapeMarkup()}[/]");
                }
            }
            return 0;
        }

        // Handle --download-model
        if (!string.IsNullOrEmpty(downloadModel))
        {
            if (jsonOutput)
            {
                var result = await modelManagement.DownloadModelAsync(downloadModel, cancellationToken: CancellationToken.None);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = result.IsSuccess,
                    provider,
                    model = downloadModel,
                    error = result.IsSuccess ? null : result.Error
                }));
                return result.IsSuccess ? 0 : 1;
            }

            // Show progress bar for interactive download
            Shared.Results.Result? downloadResult = null;
            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var progressTask = ctx.AddTask($"Downloading model '{downloadModel}'", maxValue: 100);

                    downloadResult = await modelManagement.DownloadModelAsync(
                        downloadModel,
                        progress => progressTask.Value = progress,
                        CancellationToken.None);

                    progressTask.Value = 100;
                });

            if (downloadResult?.IsSuccess == true)
            {
                AnsiConsole.MarkupLine($"[green]✓ Model '{downloadModel}' is ready[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Failed to download model: {downloadResult?.Error ?? "Unknown error"}[/]");
                return 1;
            }
        }

        return 0;
    }
}
