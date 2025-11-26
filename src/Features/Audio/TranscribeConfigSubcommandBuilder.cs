using System.CommandLine;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Abstractions.LocalAi;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using TenSecondTom.Features.Audio.Services;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Builds the 'transcribe config' subcommand for configuring STT and transcription settings.
/// This is used by TranscribeCommandBuilder to provide the 'config' subcommand.
/// </summary>
public sealed class TranscribeConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'transcribe config' command with all transcription-specific options.
    /// </summary>
    public static Command BuildConfigCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        return BuildConfigCommandWithName("config", serviceProvider, jsonOutputOption);
    }

    /// <summary>
    /// Builds a transcribe config command with a custom name.
    /// Used by TranscribeConfigCommandBuilder to create '/config transcribe'.
    /// </summary>
    public static Command BuildConfigCommandWithName(string name, IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var configCommand = new Command(name, "Configure transcription and STT settings");

        var providerOption = new Option<string?>("--stt-provider")
        {
            Description = $"STT provider to use ({SttProviders.WhisperCpp}, {SttProviders.BuiltInLocal}, {SttProviders.OpenAI}). Starts interactive setup for provider-specific settings."
        };

        var modelOption = new Option<string?>("--model")
        {
            Description = "STT model to use (provider-specific). Use 'tom transcribe models list' to see available models."
        };

        var apiKeyOption = new Option<string?>("--api-key")
        {
            Description = "API key for cloud STT providers (e.g., OpenAI)."
        };

        var keepFilesOption = new Option<bool?>("--keep-files")
        {
            Description = "Keep audio files after transcription (true/false). Default: true."
        };

        var listProvidersOption = new Option<bool>("--list-providers")
        {
            Description = "List available STT providers and exit."
        };

        var showOption = new Option<bool>("--show")
        {
            Description = "Show current transcription configuration and exit."
        };

        configCommand.Options.Add(providerOption);
        configCommand.Options.Add(modelOption);
        configCommand.Options.Add(apiKeyOption);
        configCommand.Options.Add(keepFilesOption);
        configCommand.Options.Add(listProvidersOption);
        configCommand.Options.Add(showOption);
        configCommand.Options.Add(jsonOutputOption);

        configCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            bool listProviders = parseResult.GetValue(listProvidersOption);
            bool showConfig = parseResult.GetValue(showOption);
            string? provider = parseResult.GetValue(providerOption);
            string? model = parseResult.GetValue(modelOption);
            string? apiKey = parseResult.GetValue(apiKeyOption);
            bool? keepFiles = parseResult.GetValue(keepFilesOption);

            var mediator = serviceProvider.GetRequiredService<IMediator>();

            if (listProviders)
            {
                return DisplayProviderList(jsonOutput);
            }

            if (showConfig)
            {
                return await DisplayCurrentConfigAsync(mediator, jsonOutput);
            }

            // If any options provided, apply them directly
            if (provider != null || model != null || apiKey != null || keepFiles.HasValue)
            {
                return await ApplyConfigurationAsync(
                    serviceProvider,
                    mediator,
                    provider,
                    model,
                    apiKey,
                    keepFiles,
                    jsonOutput);
            }

            // No options provided - run interactive configuration
            return await RunInteractiveConfigAsync(serviceProvider, mediator, jsonOutput);
        });

        return configCommand;
    }

    private static int DisplayProviderList(bool jsonOutput)
    {
        var providers = new[]
        {
            new { Id = SttProviders.WhisperCpp, Name = "Whisper.NET (Local)", Description = "High-quality local STT using Whisper.NET - no external dependencies" },
            new { Id = SttProviders.BuiltInLocal, Name = "Built-in Local (AI Foundry)", Description = "Local STT using AI Foundry engine" },
            new { Id = SttProviders.OpenAI, Name = "OpenAI Whisper API", Description = "Cloud-based STT using OpenAI's Whisper API (requires API key)" }
        };

        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { success = true, providers }));
            return 0;
        }

        AnsiConsole.MarkupLine("[cyan]Available STT Providers:[/]");
        var table = new Table()
            .AddColumn("Provider ID")
            .AddColumn("Name")
            .AddColumn("Description");

        foreach (var p in providers)
        {
            table.AddRow(p.Id.EscapeMarkup(), p.Name.EscapeMarkup(), p.Description.EscapeMarkup());
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static async Task<int> DisplayCurrentConfigAsync(IMediator mediator, bool jsonOutput)
    {
        var result = await mediator.Send(new GetTranscribeConfiguration.Query(), CancellationToken.None);

        if (!result.IsSuccess || result.Value is null)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = result.Error ?? "Failed to load configuration" }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {result.Error ?? "Failed to load configuration"}");
            }
            return 1;
        }

        var config = result.Value;

        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                stt_provider = config.SttProvider,
                keep_files = config.KeepFiles,
                providers = config.Providers
            }));
            return 0;
        }

        AnsiConsole.MarkupLine("[cyan]Current Transcription Configuration:[/]");
        AnsiConsole.MarkupLine($"  • STT Provider: [green]{config.SttProvider.EscapeMarkup()}[/]");
        AnsiConsole.MarkupLine($"  • Keep Files: [green]{config.KeepFiles}[/]");

        if (config.Providers.Count > 0)
        {
            AnsiConsole.MarkupLine("\n[dim]Provider Settings:[/]");
            foreach (var (providerName, settings) in config.Providers)
            {
                AnsiConsole.MarkupLine($"  [{providerName.EscapeMarkup()}]");
                foreach (var (key, value) in settings)
                {
                    var displayValue = key.Contains("Key", StringComparison.OrdinalIgnoreCase)
                        ? "********"
                        : value;
                    AnsiConsole.MarkupLine($"    • {key.EscapeMarkup()}: [green]{displayValue.EscapeMarkup()}[/]");
                }
            }
        }

        return 0;
    }

    private static async Task<int> ApplyConfigurationAsync(
        IServiceProvider serviceProvider,
        IMediator mediator,
        string? provider,
        string? model,
        string? apiKey,
        bool? keepFiles,
        bool jsonOutput)
    {
        // Load current config
        var currentResult = await mediator.Send(new GetTranscribeConfiguration.Query(), CancellationToken.None);
        if (!currentResult.IsSuccess || currentResult.Value is null)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Failed to load current configuration" }));
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Error:[/] Failed to load current configuration");
            }
            return 1;
        }

        var config = currentResult.Value;

        // Validate provider if specified
        if (provider != null)
        {
            var validProviders = new[] { SttProviders.WhisperCpp, SttProviders.BuiltInLocal, SttProviders.OpenAI };
            if (!validProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Invalid provider '{provider}'. Valid options: {string.Join(", ", validProviders)}" }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error:[/] Invalid provider '{provider}'. Valid options: {string.Join(", ", validProviders)}");
                }
                return 1;
            }

            config.SttProvider = provider;
        }

        // Build updated config with new KeepFiles if specified (KeepFiles is init-only)
        if (keepFiles.HasValue)
        {
            config = new TranscribeOptions
            {
                SttProvider = config.SttProvider,
                KeepFiles = keepFiles.Value,
                Providers = new Dictionary<string, Dictionary<string, string>>(config.Providers)
            };
        }

        // Apply provider-specific settings
        var effectiveProvider = provider ?? config.SttProvider;
        if (model != null)
        {
            config.SetSttProviderConfig(effectiveProvider, "Model", model);
        }

        if (apiKey != null)
        {
            if (effectiveProvider != SttProviders.OpenAI)
            {
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine("[yellow]Warning:[/] API key is only used with OpenAI provider.");
                }
            }
            config.SetSttProviderConfig(effectiveProvider, "ApiKey", apiKey);
        }

        // If switching to a local provider and no model specified, check if we need to prompt for model download
        if (provider != null && (provider == SttProviders.WhisperCpp || provider == SttProviders.BuiltInLocal) && model == null)
        {
            var existingModel = config.GetSttModel(provider);
            if (string.IsNullOrEmpty(existingModel))
            {
                if (!jsonOutput)
                {
                    AnsiConsole.MarkupLine($"[yellow]Note:[/] No model configured for {provider}. Use 'tom transcribe models list' to see available models, then 'tom transcribe config --model <model>' to set one.");
                }
            }
        }

        // Save updated config
        var saveResult = await mediator.Send(new UpdateTranscribeConfiguration.Command(config), CancellationToken.None);

        if (!saveResult.IsSuccess)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = saveResult.Error ?? "Failed to save configuration" }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {saveResult.Error ?? "Failed to save configuration"}");
            }
            return 1;
        }

        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                stt_provider = config.SttProvider,
                keep_files = config.KeepFiles
            }));
        }
        else
        {
            AnsiConsole.MarkupLine("[green]✓[/] Transcription configuration saved successfully");
            if (provider != null)
            {
                AnsiConsole.MarkupLine($"  • STT Provider: {config.SttProvider}");
            }
            if (model != null)
            {
                AnsiConsole.MarkupLine($"  • Model: {model}");
            }
            if (keepFiles.HasValue)
            {
                AnsiConsole.MarkupLine($"  • Keep Files: {config.KeepFiles}");
            }
        }

        return 0;
    }

    private static async Task<int> RunInteractiveConfigAsync(
        IServiceProvider serviceProvider,
        IMediator mediator,
        bool jsonOutput)
    {
        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = false,
                error = "Interactive configuration not available in JSON output mode. Use --stt-provider, --model, --api-key, or --keep-files options."
            }));
            return 1;
        }

        var setupWizard = serviceProvider.GetRequiredService<ISetupWizardUI>();
        var localAiEngine = serviceProvider.GetRequiredService<ILocalAiEngine>();
        var whisperNetModelManager = serviceProvider.GetRequiredService<IWhisperNetModelManager>();

        // Load current config
        var currentResult = await mediator.Send(new GetTranscribeConfiguration.Query(), CancellationToken.None);
        var currentConfig = currentResult.IsSuccess && currentResult.Value != null
            ? currentResult.Value
            : new TranscribeOptions();

        const int totalSteps = 3;

        // Step 1: STT Provider
        setupWizard.ShowStepHeader(1, totalSteps, "Speech-to-Text Provider");
        var sttProvider = await setupWizard.PromptForSttProviderAsync(
            currentConfig.SttProvider,
            CancellationToken.None);

        if (sttProvider == null)
        {
            return 0; // Cancelled
        }

        // Step 2: Provider-specific configuration
        string? sttModel = currentConfig.GetSttModel(sttProvider);
        string? sttApiKey = currentConfig.GetSttApiKey(sttProvider);

        if (sttProvider == SttProviders.BuiltInLocal)
        {
            setupWizard.ShowStepHeader(2, totalSteps, "Local STT Model");
            setupWizard.ShowStatus("Fetching available whisper models from AI Foundry catalog...");

            var availableModels = (await localAiEngine.ListAvailableModelsAsync(CancellationToken.None))
                .Where(m => m.Contains("whisper", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (availableModels.Count == 0)
            {
                setupWizard.ShowWarning("No whisper models found in the AI Foundry catalog.");
                return 1;
            }

            var selectedModel = await setupWizard.PromptForSelectionAsync(
                "Select a whisper model for speech-to-text:",
                availableModels,
                m => m,
                CancellationToken.None);

            if (string.IsNullOrEmpty(selectedModel))
            {
                return 0; // Cancelled
            }

            sttModel = selectedModel;

            // Download model
            Result? downloadResult = null;
            await setupWizard.RunWithProgressAsync(
                $"Downloading whisper model '{sttModel}'...",
                async progress =>
                {
                    downloadResult = await localAiEngine.EnsureModelAvailableAsync(
                        sttModel,
                        progress,
                        CancellationToken.None);
                },
                CancellationToken.None);

            if (downloadResult?.IsSuccess != true)
            {
                setupWizard.ShowError($"Failed to download model: {downloadResult?.Error ?? "Unknown error"}");
                return 1;
            }

            setupWizard.ShowSuccess($"✓ Whisper model '{sttModel}' is ready");
        }
        else if (sttProvider == SttProviders.WhisperCpp)
        {
            setupWizard.ShowStepHeader(2, totalSteps, "Whisper.NET Model");
            setupWizard.ShowStatus("Available Whisper models (powered by Whisper.NET):");

            var availableModels = whisperNetModelManager.ListAvailableModels();
            var downloadedModels = await whisperNetModelManager.ListDownloadedModelsAsync(CancellationToken.None);
            var downloadedIds = downloadedModels.Select(d => d.ModelId).ToHashSet();

            var modelChoices = availableModels
                .Select(m =>
                {
                    var status = downloadedIds.Contains(m.Id) ? " (downloaded)" : "";
                    var recommended = m.Recommended ? " *" : "";
                    return $"{m.Id} ({m.SizeMb} MB){recommended}{status}";
                })
                .ToList();

            var selectedChoice = await setupWizard.PromptForSelectionAsync(
                "Select a whisper model for speech-to-text:",
                modelChoices,
                m => m,
                CancellationToken.None);

            if (string.IsNullOrEmpty(selectedChoice))
            {
                return 0; // Cancelled
            }

            var selectedModelId = selectedChoice.Split(' ')[0];
            sttModel = whisperNetModelManager.GetModelPath(selectedModelId);

            if (sttModel == null)
            {
                setupWizard.ShowStatus($"Downloading model '{selectedModelId}' from Hugging Face...");

                Result<string>? downloadResult = null;
                await setupWizard.RunWithProgressAsync(
                    $"Downloading Whisper model '{selectedModelId}'...",
                    async progress =>
                    {
                        downloadResult = await whisperNetModelManager.DownloadModelAsync(
                            selectedModelId,
                            progress,
                            CancellationToken.None);
                    },
                    CancellationToken.None);

                if (downloadResult == null || !downloadResult.Value.IsSuccess)
                {
                    setupWizard.ShowError($"Failed to download model: {downloadResult?.Error ?? "Unknown error"}");
                    return 1;
                }

                sttModel = downloadResult.Value.Value;
                setupWizard.ShowSuccess($"✓ Model downloaded to {sttModel}");
            }
            else
            {
                setupWizard.ShowSuccess($"✓ Model '{selectedModelId}' is ready at {sttModel}");
            }
        }
        else if (sttProvider == SttProviders.OpenAI)
        {
            setupWizard.ShowStepHeader(2, totalSteps, "OpenAI API Key");
            sttApiKey = await setupWizard.PromptForSttApiKeyAsync(
                sttProvider,
                sttApiKey,
                CancellationToken.None);

            if (sttApiKey == null)
            {
                return 0; // Cancelled
            }
        }
        else
        {
            setupWizard.ShowStepHeader(2, totalSteps, "Provider Settings");
            setupWizard.ShowStatus("Skipped (no additional configuration required)");
        }

        // Step 3: Keep Files
        setupWizard.ShowStepHeader(3, totalSteps, "File Retention");
        var keepFiles = await setupWizard.PromptForBooleanAsync(
            "Keep audio files after transcription?",
            currentConfig.KeepFiles,
            CancellationToken.None);

        if (!keepFiles.HasValue)
        {
            return 0; // Cancelled
        }

        // Build and save updated config
        var updatedConfig = new TranscribeOptions
        {
            SttProvider = sttProvider,
            KeepFiles = keepFiles.Value,
            Providers = new Dictionary<string, Dictionary<string, string>>(
                currentConfig.Providers ?? new Dictionary<string, Dictionary<string, string>>())
        };

        if (!string.IsNullOrEmpty(sttModel))
        {
            updatedConfig.SetSttProviderConfig(sttProvider, "Model", sttModel);
        }

        if (!string.IsNullOrEmpty(sttApiKey))
        {
            updatedConfig.SetSttProviderConfig(sttProvider, "ApiKey", sttApiKey);
        }

        var saveResult = await mediator.Send(
            new UpdateTranscribeConfiguration.Command(updatedConfig),
            CancellationToken.None);

        if (!saveResult.IsSuccess)
        {
            setupWizard.ShowError($"Failed to save configuration: {saveResult.Error}");
            return 1;
        }

        setupWizard.ShowSuccess("✓ Transcription configuration saved successfully");
        var providerDisplay = sttProvider switch
        {
            SttProviders.WhisperCpp => "Whisper.NET (local)",
            SttProviders.BuiltInLocal => "Built-in Local (AI Foundry)",
            SttProviders.OpenAI => "OpenAI Whisper API (cloud)",
            _ => sttProvider
        };
        setupWizard.ShowStatus($"  • STT Provider: {providerDisplay}");
        if (!string.IsNullOrEmpty(sttModel))
        {
            setupWizard.ShowStatus($"  • Model: {sttModel}");
        }
        setupWizard.ShowStatus($"  • Keep Files: {keepFiles.Value}");

        return 0;
    }
}
