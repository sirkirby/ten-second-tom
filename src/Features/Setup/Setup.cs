using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Abstractions.LocalAi;
using TenSecondTom.Features.Audio;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Initiates or re-runs the guided setup wizard to configure Ten Second Tom.
/// </summary>
public static class Setup
{
    /// <summary>
    /// Command to initiate or re-run the guided setup wizard.
    /// </summary>
    public sealed record Command : IRequest<Result<SetupResult>>
    {
        /// <summary>
        /// Gets whether to force setup even if configuration exists.
        /// </summary>
        public bool Force { get; init; }

        /// <summary>
        /// Gets whether to suppress interactive prompts when running setup.
        /// </summary>
        public bool NonInteractive { get; init; }
    }

    /// <summary>
    /// Validator for Setup command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // Minimal validation - Command is simple now
            RuleFor(x => x.Force).NotNull();
        }
    }

    /// <summary>
    /// Handler for Setup command (auto-discovered by MediatR).
    /// Orchestrates the complete setup wizard flow using MediatR to delegate
    /// feature-specific configuration to their respective slices (Auth, LLM, Storage).
    /// Setup ONLY owns infrastructure configuration (Optional, Audio, Configuration metadata).
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        ISetupWizardUI wizardUI,
        IMediator mediator,
        ILogger<Handler> logger,
        IOptions<StorageOptions> storageOptions,
        ILocalAiEngine localAiEngine)
        : IRequestHandler<Command, Result<SetupResult>>
    {
        private readonly IOptions<StorageOptions> _storageOptions = storageOptions;

        public async Task<Result<SetupResult>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation(
                    "Starting setup wizard (Force: {Force}, NonInteractive: {NonInteractive})",
                    request.Force,
                    request.NonInteractive);

                wizardUI.ShowStatus("Welcome to Ten Second Tom! Let's get you set up.");

                // Step 1: SSH Key Configuration (delegated to Auth feature via MediatR)
                // Force=false for idempotent setup wizard behavior (skip if already configured)
                wizardUI.ShowStepHeader(1, 6, "SSH Authentication Configuration");
                var sshConfigResult = await mediator.Send(new Auth.ConfigureSsh.Command
                {
                    DetectionTimeout = TimeSpan.FromSeconds(5),
                    Force = false
                }, cancellationToken);

                if (!sshConfigResult.IsSuccess)
                {
                    logger.LogWarning("SSH configuration failed: {Error}", sshConfigResult.Error);
                    wizardUI.ShowError($"SSH configuration failed: {sshConfigResult.Error}");
                    wizardUI.ShowStatus("Please ensure you have an SSH key configured, then run 'tom setup' again.");
                    return Result<SetupResult>.Failure($"Setup cancelled: SSH configuration failed. {sshConfigResult.Error}");
                }

                // Step 2: LLM Provider Configuration (delegated to LLM feature via MediatR)
                // Force=false for idempotent setup wizard behavior (skip if already configured)
                wizardUI.ShowStepHeader(2, 6, "LLM Provider Configuration");
                var llmConfigResult = await mediator.Send(new Llm.ConfigureLlm.Command { Force = false }, cancellationToken);

                if (!llmConfigResult.IsSuccess)
                {
                    logger.LogWarning("LLM configuration failed: {Error}", llmConfigResult.Error);
                    wizardUI.ShowError($"LLM configuration failed: {llmConfigResult.Error}");
                    wizardUI.ShowStatus("Please ensure you have a valid LLM provider and API key, then run 'tom setup' again.");
                    return Result<SetupResult>.Failure($"Setup cancelled: LLM configuration failed. {llmConfigResult.Error}");
                }

                // Step 2b: Local LLM Verification (if applicable)
                await mediator.Send(new Llm.SetupLocalLlm.Command(), cancellationToken);

                // Step 3: Audio Configuration (delegated to Audio feature via MediatR)
                wizardUI.ShowStepHeader(3, 6, "Audio Configuration");
                var audioConfigResult = await mediator.Send(new Audio.ConfigureAudio.Command(), cancellationToken);

                if (!audioConfigResult.IsSuccess)
                {
                    logger.LogWarning("Audio configuration failed: {Error}", audioConfigResult.Error);
                    wizardUI.ShowError($"Audio configuration failed: {audioConfigResult.Error}");
                    return Result<SetupResult>.Failure($"Setup cancelled: Audio configuration failed. {audioConfigResult.Error}");
                }

                // Step 3b: Pre-warm Local LLM Model if selected
                if (llmConfigResult.Value.Provider == LlmProvider.BuiltInLocal)
                {
                    wizardUI.ShowStatus("Initializing local AI engine and checking models...");

                    var modelId = llmConfigResult.Value.Model ?? "phi-3.5-mini-instruct";
                    wizardUI.ShowStatus($"Ensuring LLM model '{modelId}' is available...");
                    var llmModelResult = await localAiEngine.EnsureModelAvailableAsync(modelId, cancellationToken: cancellationToken);
                    if (!llmModelResult.IsSuccess)
                    {
                        wizardUI.ShowWarning($"Failed to download LLM model: {llmModelResult.Error}");
                        wizardUI.ShowStatus("You can download the model later with 'tom llm --download-model'");
                    }
                    else
                    {
                        wizardUI.ShowSuccess($"LLM model '{modelId}' is ready");
                    }
                }

                // Step 4: Transcription/STT Configuration (delegated to Audio feature via MediatR)
                wizardUI.ShowStepHeader(4, 6, "Transcription Configuration");
                var transcribeConfigResult = await mediator.Send(new Audio.ConfigureTranscribe.Command(), cancellationToken);

                if (!transcribeConfigResult.IsSuccess)
                {
                    logger.LogWarning("Transcription configuration failed: {Error}", transcribeConfigResult.Error);
                    wizardUI.ShowError($"Transcription configuration failed: {transcribeConfigResult.Error}");
                    return Result<SetupResult>.Failure($"Setup cancelled: Transcription configuration failed. {transcribeConfigResult.Error}");
                }

                // Step 5: Storage Configuration (delegated to Storage feature via MediatR)
                // Force=false for idempotent setup wizard behavior (skip if already configured)
                wizardUI.ShowStepHeader(5, 6, "Storage Configuration");
                var storageOptionsSnapshot = _storageOptions.Value;
                var storageSectionResult = await sectionStore.ReadSectionAsync<StorageSettings>(
                    StorageOptions.SectionName,
                    cancellationToken).ConfigureAwait(false);

                var existingStorage = storageSectionResult.Value ?? new StorageSettings();
                var existingRootDirectory = storageOptionsSnapshot.RootDirectory
                    ?? Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        DirectoryNames.ApplicationRoot);

                var storageConfigResult = await mediator.Send(new Storage.ConfigureStorage.Command
                {
                    ExistingRootDirectory = existingRootDirectory,
                    ExistingStorage = existingStorage,
                    Force = false
                }, cancellationToken);

                if (!storageConfigResult.IsSuccess)
                {
                    logger.LogWarning("Storage configuration failed: {Error}", storageConfigResult.Error);
                    wizardUI.ShowError($"Storage configuration failed: {storageConfigResult.Error}");
                    wizardUI.ShowStatus("Please ensure storage is properly configured, then run 'tom setup' again.");
                    return Result<SetupResult>.Failure($"Setup cancelled: Storage configuration failed. {storageConfigResult.Error}");
                }

                // Use default values for logging and retention
                var logLevel = Microsoft.Extensions.Logging.LogLevel.Information;
                var retentionDays = 30; // Default retention days from OptionalConfiguration

                // Step 6: Configuration Summary
                wizardUI.ShowStepHeader(6, 6, "Configuration Summary");

                // Build summary for display
                var summary = new SetupSummary(
                    SshKeyDisplay: sshConfigResult.Value?.KeyDisplayName ?? sshConfigResult.Value?.KeyPath ?? "Not set",
                    LlmProvider: llmConfigResult.Value?.Provider.ToString() ?? "Unknown",
                    ApiKey: llmConfigResult.Value?.ApiKey ?? "",
                    RootDirectory: storageConfigResult.Value?.RootDirectory ?? "Unknown",
                    LogLevel: logLevel.ToString(),
                    RetentionDays: retentionDays
                );

                var confirmed = await wizardUI.ShowSummaryAndConfirmAsync(summary, cancellationToken).ConfigureAwait(false);

                if (!confirmed)
                {
                    wizardUI.ShowWarning("Setup cancelled by user. No changes were saved.");
                    wizardUI.ShowStatus("Run 'tom setup' anytime to configure Ten Second Tom.");
                    return Result<SetupResult>.Failure("Setup cancelled: User chose not to save configuration. Run 'tom setup' to try again.");
                }

                // Saving Configuration
                // NOTE: Feature slices have already written their sections via MediatR:
                // - Auth feature wrote TenSecondTom:Auth
                // - LLM feature wrote TenSecondTom:Llm
                // - Storage feature wrote TenSecondTom:RootDirectory and TenSecondTom:Storage
                // Setup only writes remaining infrastructure sections (Optional, Audio, Configuration).
                wizardUI.ShowStatus("Saving infrastructure configuration...");

                var metadata = new ConfigurationMetadata
                {
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = null,
                    Version = "1.0"
                };

                // Write ONLY remaining infrastructure sections (VSA-compliant: Setup owns these)
                var sections = new Dictionary<string, object>
                {
                    ["TenSecondTom:Optional"] = new OptionalConfiguration
                    {
                        LogLevel = logLevel,
                        RetentionDays = retentionDays,
                        EnableTelemetry = false
                    },
                    ["TenSecondTom:Configuration"] = metadata
                };

                var saveResult = await sectionStore.WriteMultipleSectionsAsync(sections, cancellationToken).ConfigureAwait(false);

                if (!saveResult.IsSuccess)
                {
                    wizardUI.ShowError($"Failed to save configuration: {saveResult.Error}");
                    wizardUI.ShowStatus("Your configuration could not be saved. This might be a permissions issue.");
                    wizardUI.ShowStatus("Try running the command again, or check the logs for more details.");
                    return Result<SetupResult>.Failure($"Failed to save configuration: {saveResult.Error}. Check file permissions and try 'tom setup' again.");
                }

                wizardUI.ShowSuccess("✓ Setup complete!");
                wizardUI.ShowStatus($"Configuration saved to {saveResult.Value}");
                wizardUI.ShowStatus("You can view your configuration anytime with: tom config --show");
                wizardUI.ShowStatus("To change individual settings, use: tom config --set <setting-name> <value>");

                logger.LogInformation("Setup wizard completed successfully");

                var result = new SetupResult(
                    Message: "Setup complete! Configuration has been saved.",
                    ConfigPath: saveResult.Value ?? ""
                );

                return Result<SetupResult>.Success(result);
            }
            catch (OperationCanceledException)
            {
                wizardUI.ShowWarning("Setup was interrupted. No changes were saved.");
                wizardUI.ShowStatus("Run 'tom setup' anytime to complete configuration.");
                logger.LogWarning("Setup wizard was cancelled");
                return Result<SetupResult>.Failure("Setup cancelled: Operation was interrupted. Run 'tom setup' to try again.");
            }
            catch (Exception ex)
            {
                wizardUI.ShowError($"An unexpected error occurred: {ex.Message}");
                wizardUI.ShowStatus("Please check the logs for more details, then try running 'tom setup' again.");
                wizardUI.ShowStatus("If the problem persists, please report it at: https://github.com/sirkirby/ten-second-tom/issues");
                logger.LogError(ex, "Setup wizard failed");
                return Result<SetupResult>.Failure($"Setup failed: {ex.Message}. Check logs and try 'tom setup' again, or report the issue if it persists.");
            }
        }

        // Helper methods for SSH agent resolution removed - now handled by Auth.ConfigureSsh feature
    }
}
