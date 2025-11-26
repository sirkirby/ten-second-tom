using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Abstractions.LocalAi;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Configures transcription/STT settings interactively.
/// This use case handles all transcription configuration logic, keeping it within the Audio feature slice.
/// </summary>
public static class ConfigureTranscribe
{
    /// <summary>
    /// Command to configure transcription settings.
    /// </summary>
    public sealed record Command : IRequest<Result<TranscribeOptions>>;

    /// <summary>
    /// Handler for ConfigureTranscribe command (auto-discovered by MediatR).
    /// Orchestrates interactive transcription configuration.
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ISetupWizardUI setupWizard,
        ILocalAiEngine localAiEngine,
        IWhisperNetModelManager whisperNetModelManager,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<TranscribeOptions>>
    {
        public async Task<Result<TranscribeOptions>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting transcription configuration");

            // Read current transcribe configuration
            var configResult = await sectionStore.ReadSectionAsync<TranscribeOptions>(
                TranscribeOptions.SectionPath,
                cancellationToken);

            var currentConfig = configResult.IsSuccess && configResult.Value != null
                ? configResult.Value
                : new TranscribeOptions();

            const int totalSteps = 3;

            // Step 1: STT Provider
            setupWizard.ShowStepHeader(1, totalSteps, "Speech-to-Text Provider");
            var sttProvider = await setupWizard.PromptForSttProviderAsync(
                currentConfig.SttProvider,
                cancellationToken);

            if (sttProvider == null)
            {
                return Result<TranscribeOptions>.Failure("Transcription configuration cancelled.");
            }

            // Step 2: Provider-specific configuration
            string? sttModel = currentConfig.GetSttModel(sttProvider);
            string? sttApiKey = currentConfig.GetSttApiKey(sttProvider);

            if (sttProvider == SttProviders.BuiltInLocal)
            {
                setupWizard.ShowStepHeader(2, totalSteps, "Local STT Model");
                setupWizard.ShowStatus("Fetching available whisper models from AI Foundry catalog...");

                var availableModels = (await localAiEngine.ListAvailableModelsAsync(cancellationToken))
                    .Where(m => m.Contains("whisper", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (availableModels.Count == 0)
                {
                    setupWizard.ShowWarning("No whisper models found in the AI Foundry catalog.");
                    return Result<TranscribeOptions>.Failure("No whisper models available for built-in local STT provider.");
                }

                var selectedModel = await setupWizard.PromptForSelectionAsync(
                    "Select a whisper model for speech-to-text:",
                    availableModels,
                    m => m,
                    cancellationToken);

                if (string.IsNullOrEmpty(selectedModel))
                {
                    return Result<TranscribeOptions>.Failure("Model selection cancelled.");
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
                            cancellationToken);
                    },
                    cancellationToken);

                if (downloadResult?.IsSuccess != true)
                {
                    setupWizard.ShowError($"Failed to download model: {downloadResult?.Error ?? "Unknown error"}");
                    return Result<TranscribeOptions>.Failure($"Failed to download whisper model: {downloadResult?.Error ?? "Unknown error"}");
                }

                setupWizard.ShowSuccess($"✓ Whisper model '{sttModel}' is ready");
            }
            else if (sttProvider == SttProviders.WhisperCpp)
            {
                setupWizard.ShowStepHeader(2, totalSteps, "Whisper.NET Model");
                setupWizard.ShowStatus("Available Whisper models (powered by Whisper.NET):");

                var availableModels = whisperNetModelManager.ListAvailableModels();
                var downloadedModels = await whisperNetModelManager.ListDownloadedModelsAsync(cancellationToken);
                var downloadedIds = downloadedModels.Select(d => d.ModelId).ToHashSet();

                var modelChoices = availableModels
                    .Select(m =>
                    {
                        var status = downloadedIds.Contains(m.Id) ? " (downloaded)" : "";
                        var recommended = m.Recommended ? " ★" : "";
                        return $"{m.Id} ({m.SizeMb} MB){recommended}{status}";
                    })
                    .ToList();

                var selectedChoice = await setupWizard.PromptForSelectionAsync(
                    "Select a whisper model for speech-to-text:",
                    modelChoices,
                    m => m,
                    cancellationToken);

                if (string.IsNullOrEmpty(selectedChoice))
                {
                    return Result<TranscribeOptions>.Failure("Model selection cancelled.");
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
                                cancellationToken);
                        },
                        cancellationToken);

                    if (downloadResult == null || !downloadResult.Value.IsSuccess)
                    {
                        setupWizard.ShowError($"Failed to download model: {downloadResult?.Error ?? "Unknown error"}");
                        return Result<TranscribeOptions>.Failure($"Failed to download whisper model: {downloadResult?.Error ?? "Unknown error"}");
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
                    cancellationToken);

                if (sttApiKey == null)
                {
                    return Result<TranscribeOptions>.Failure("Transcription configuration cancelled.");
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
                cancellationToken);

            if (!keepFiles.HasValue)
            {
                return Result<TranscribeOptions>.Failure("Transcription configuration cancelled.");
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

            var saveResult = await sectionStore.WriteSectionAsync(
                TranscribeOptions.SectionPath,
                updatedConfig,
                cancellationToken);

            if (!saveResult.IsSuccess)
            {
                return Result<TranscribeOptions>.Failure($"Failed to save configuration: {saveResult.Error}");
            }

            logger.LogInformation("Transcription configuration updated successfully");

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

            return Result<TranscribeOptions>.Success(updatedConfig);
        }
    }
}
