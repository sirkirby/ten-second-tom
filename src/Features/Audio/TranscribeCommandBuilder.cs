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
/// Registers the top-level <c>transcribe</c> CLI command with subcommands:
/// <list type="bullet">
///   <item><c>transcribe list</c> - Interactive selection of recordings to transcribe</item>
///   <item><c>transcribe recording [name]</c> - Transcribe a specific recording</item>
///   <item><c>transcribe file &lt;path&gt;</c> - Transcribe external file</item>
///   <item><c>transcribe models list</c> - List available STT models</item>
///   <item><c>transcribe models download &lt;model&gt;</c> - Download an STT model</item>
///   <item><c>transcribe config</c> - Configure transcription settings</item>
/// </list>
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

        var transcribeCommand = new Command("transcribe", "Transcribe audio files or manage STT models");
        transcribeCommand.Options.Add(jsonOutputOption);

        // Add subcommands
        transcribeCommand.Subcommands.Add(BuildListSubcommand(serviceProvider, jsonOutputOption));
        transcribeCommand.Subcommands.Add(BuildRecordingSubcommand(serviceProvider, jsonOutputOption));
        transcribeCommand.Subcommands.Add(BuildFileSubcommand(serviceProvider, jsonOutputOption));
        transcribeCommand.Subcommands.Add(BuildModelsSubcommand(serviceProvider, jsonOutputOption));
        transcribeCommand.Subcommands.Add(BuildConfigSubcommand(serviceProvider, jsonOutputOption));

        return transcribeCommand;
    }

    private static Command BuildListSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var command = new Command("list", "Select a recording interactively to transcribe");

        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, openai."
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing transcript if present."
        };

        command.Options.Add(sttOption);
        command.Options.Add(forceOption);
        command.Options.Add(jsonOutputOption);

        command.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            string? stt = parseResult.GetValue(sttOption);
            bool force = parseResult.GetValue(forceOption);

            // In JSON mode, list all audio files without interactive selection
            if (jsonOutput)
            {
                return await TranscribeCommand.ExecuteAsync(
                    serviceProvider,
                    jsonOutput,
                    noteName: null,
                    recordingName: null,
                    filePath: null,
                    customName: null,
                    sttSelection: null,
                    listOnly: true,
                    forceOverwrite: false).ConfigureAwait(false);
            }

            // Interactive mode: TranscribeCommand.ExecuteAsync will prompt for selection
            // when no source is specified (noteName, recordingName, filePath all null)
            return await TranscribeCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                noteName: null,
                recordingName: null,
                filePath: null,
                customName: null,
                sttSelection: stt,
                listOnly: false,
                forceOverwrite: force).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildRecordingSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var command = new Command("recording", "Transcribe a recording from the library");

        var nameArgument = new Argument<string?>("name")
        {
            Description = "Recording base name (without extension). Prompts for selection if omitted.",
            Arity = ArgumentArity.ZeroOrOne
        };

        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, openai."
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing transcript if present."
        };

        command.Arguments.Add(nameArgument);
        command.Options.Add(sttOption);
        command.Options.Add(forceOption);
        command.Options.Add(jsonOutputOption);

        command.SetAction(async parseResult =>
        {
            string? name = parseResult.GetValue(nameArgument);
            string? stt = parseResult.GetValue(sttOption);
            bool force = parseResult.GetValue(forceOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            return await TranscribeCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                noteName: null,
                recordingName: name,
                filePath: null,
                customName: null,
                sttSelection: stt,
                listOnly: false,
                forceOverwrite: force).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildFileSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var command = new Command("file", "Transcribe an external audio file");

        var pathArgument = new Argument<string>("path")
        {
            Description = "Path to the audio file (.wav, .mp3, or .flac)."
        };

        var nameOption = new Option<string?>("--name")
        {
            Description = "Override the destination recording name (defaults to source filename)."
        };

        var sttOption = new Option<string?>("--stt")
        {
            Description = "STT engine selection: auto (default), local, openai."
        };

        var forceOption = new Option<bool>("--force")
        {
            Description = "Overwrite existing transcript if present."
        };

        command.Arguments.Add(pathArgument);
        command.Options.Add(nameOption);
        command.Options.Add(sttOption);
        command.Options.Add(forceOption);
        command.Options.Add(jsonOutputOption);

        command.SetAction(async parseResult =>
        {
            string path = parseResult.GetValue(pathArgument)!;
            string? name = parseResult.GetValue(nameOption);
            string? stt = parseResult.GetValue(sttOption);
            bool force = parseResult.GetValue(forceOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            return await TranscribeCommand.ExecuteAsync(
                serviceProvider,
                jsonOutput,
                noteName: null,
                recordingName: null,
                filePath: path,
                customName: name,
                sttSelection: stt,
                listOnly: false,
                forceOverwrite: force).ConfigureAwait(false);
        });

        return command;
    }

    private static Command BuildModelsSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var modelsCommand = new Command("models", "List available STT models (use 'download' subcommand to download)");

        var providerOption = new Option<string?>("--provider")
        {
            Description = "Override the STT provider for model operations (whisper-cpp, built-in-local)."
        };

        // Add options to parent command for default action
        modelsCommand.Options.Add(providerOption);
        modelsCommand.Options.Add(jsonOutputOption);

        // Default action: list models when no subcommand specified
        modelsCommand.SetAction(async parseResult =>
        {
            string? providerArg = parseResult.GetValue(providerOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            return await HandleModelListAsync(serviceProvider, jsonOutput, providerArg);
        });

        // Nested: transcribe models download <model>
        var downloadCommand = new Command("download", "Download an STT model");

        var modelArgument = new Argument<string>("model")
        {
            Description = "The model ID to download."
        };

        var downloadProviderOption = new Option<string?>("--provider")
        {
            Description = "Override the STT provider for download (whisper-cpp, built-in-local)."
        };

        downloadCommand.Arguments.Add(modelArgument);
        downloadCommand.Options.Add(downloadProviderOption);
        downloadCommand.Options.Add(jsonOutputOption);

        downloadCommand.SetAction(async parseResult =>
        {
            string model = parseResult.GetValue(modelArgument)!;
            string? providerArg = parseResult.GetValue(downloadProviderOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            return await HandleModelDownloadAsync(serviceProvider, jsonOutput, model, providerArg);
        });

        modelsCommand.Subcommands.Add(downloadCommand);

        return modelsCommand;
    }

    private static Command BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        return TranscribeConfigSubcommandBuilder.BuildConfigCommand(serviceProvider, jsonOutputOption);
    }

    private static async Task<int> HandleModelListAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        string? providerArg)
    {
        var options = serviceProvider.GetRequiredService<IOptions<TranscribeOptions>>().Value;
        string provider = !string.IsNullOrEmpty(providerArg) ? providerArg : options.SttProvider;

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

    private static async Task<int> HandleModelDownloadAsync(
        IServiceProvider serviceProvider,
        bool jsonOutput,
        string downloadModel,
        string? providerArg)
    {
        var options = serviceProvider.GetRequiredService<IOptions<TranscribeOptions>>().Value;
        string provider = !string.IsNullOrEmpty(providerArg) ? providerArg : options.SttProvider;

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
}
