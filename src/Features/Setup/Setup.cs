using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Initiates or re-runs the guided setup wizard to configure Ten Second Tom.
/// </summary>
public static class Setup
{
    /// <summary>
    /// Command to initiate or re-run the guided setup wizard.
    /// </summary>
    public sealed record Command : IRequest<Result<ConfigurationSettings>>
    {
        /// <summary>
        /// Gets whether to force setup even if configuration exists.
        /// </summary>
        public bool Force { get; init; }

        /// <summary>
        /// Gets whether to run in non-interactive mode (use defaults, no prompts).
        /// </summary>
        public bool NonInteractive { get; init; }

        /// <summary>
        /// Gets the existing configuration to use as defaults.
        /// </summary>
        public ConfigurationSettings? ExistingConfiguration { get; init; }
    }

    /// <summary>
    /// Validator for Setup command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // NonInteractive mode requires ExistingConfiguration
            RuleFor(x => x.ExistingConfiguration)
                .NotNull()
                .When(x => x.NonInteractive)
                .WithMessage("ExistingConfiguration must be provided when NonInteractive is true");

            // ExistingConfiguration must be valid if provided
            RuleFor(x => x.ExistingConfiguration)
                .Must(config => config == null || config.IsValid())
                .When(x => x.ExistingConfiguration != null)
                .WithMessage("ExistingConfiguration must be valid if provided");
        }
    }

    /// <summary>
    /// Handler for Setup command (auto-discovered by MediatR).
    /// Orchestrates the complete setup wizard flow.
    /// </summary>
    public sealed class Handler(
        IConfigurationStorageService storageService,
        ISetupWizardUI wizardUI,
        ISshKeyDetectorFactory sshKeyDetectorFactory,
        IStorageProviderFactory storageProviderFactory,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<ConfigurationSettings>>
    {
        public async Task<Result<ConfigurationSettings>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Starting setup wizard (Force: {Force}, NonInteractive: {NonInteractive})",
                    request.Force, request.NonInteractive);

                var progress = SetupProgress.CreateInitial(totalSteps: 8);

                // Check if reconfiguration or first-time setup
                var isReconfiguration = request.ExistingConfiguration != null;
                if (isReconfiguration)
                {
                    wizardUI.ShowStatus("Reconfiguring Ten Second Tom");
                }
                else
                {
                    wizardUI.ShowStatus("Welcome to Ten Second Tom! Let's get you set up.");
                }

                // Step 1: SSH Key Configuration
                wizardUI.ShowStepHeader(1, 8, "SSH Key Configuration");
                wizardUI.ShowStatus("Detecting SSH keys...");

                var sshDetectionResult = await sshKeyDetectorFactory.DetectKeysAsync(
                    TimeSpan.FromSeconds(5),
                    cancellationToken);

                var selectedSshKey = await wizardUI.PromptForSshKeyAsync(
                    sshDetectionResult.DetectedKeys,
                    null,
                    cancellationToken);

                if (selectedSshKey == null && !request.NonInteractive)
                {
                    wizardUI.ShowError("Setup cannot continue without an SSH key.");
                    wizardUI.ShowStatus("Please generate an SSH key or add one to your SSH agent, then run 'tom setup' again.");
                    wizardUI.ShowStatus("Learn more: https://docs.github.com/en/authentication/connecting-to-github-with-ssh");
                    return Result<ConfigurationSettings>.Failure("Setup cancelled: No SSH key selected. Run 'tom setup' after adding an SSH key.");
                }

                // Step 2: LLM Provider Selection
                wizardUI.ShowStepHeader(2, 8, "LLM Provider Selection");
                var selectedProvider = await wizardUI.PromptForLlmProviderAsync(
                    request.ExistingConfiguration?.Llm.Provider,
                    cancellationToken);

                if (!selectedProvider.HasValue)
                {
                    wizardUI.ShowError("Setup cannot continue without selecting an AI provider.");
                    wizardUI.ShowStatus("Please choose OpenAI or Anthropic to continue.");
                    return Result<ConfigurationSettings>.Failure("Setup cancelled: No LLM provider selected. Run 'tom setup' to try again.");
                }

                // Step 2.5: Model Selection (new step)
                wizardUI.ShowStatus("Selecting model for the chosen provider...");
                var selectedModel = await wizardUI.PromptForModelAsync(
                    selectedProvider.Value,
                    request.ExistingConfiguration?.Llm.Model,
                    cancellationToken);

                // If no model selected, use the default for the provider
                string? modelId = selectedModel?.Id;
                if (string.IsNullOrEmpty(modelId))
                {
                    var defaultModel = ModelRegistry.GetDefault(selectedProvider.Value);
                    modelId = defaultModel.Id;
                    wizardUI.ShowStatus($"Using default model: {defaultModel.DisplayName}");
                }

                // Step 3: API Key Configuration
                wizardUI.ShowStepHeader(3, 8, "API Key Configuration");
                var apiKey = await wizardUI.PromptForApiKeyAsync(
                    selectedProvider.Value,
                    request.ExistingConfiguration?.Llm.ApiKey,
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    wizardUI.ShowError("Setup cannot continue without a valid API key.");
                    var providerName = selectedProvider.Value == LlmProvider.OpenAI ? "OpenAI" : "Anthropic";
                    var keyUrl = selectedProvider.Value == LlmProvider.OpenAI
                        ? "https://platform.openai.com/api-keys"
                        : "https://console.anthropic.com/settings/keys";
                    wizardUI.ShowStatus($"Get your {providerName} API key from: {keyUrl}");
                    return Result<ConfigurationSettings>.Failure($"Setup cancelled: No API key provided. Visit {keyUrl} to create an API key, then run 'tom setup' again.");
                }

                // Step 4: Storage Provider Selection
                wizardUI.ShowStepHeader(4, 10, "Storage Provider Selection");
                var availableProviders = storageProviderFactory.GetAvailableProviders();
                var selectedStorageProvider = await wizardUI.PromptForStorageProviderAsync(
                    availableProviders,
                    request.ExistingConfiguration?.Storage.ProviderId,
                    cancellationToken);

                if (selectedStorageProvider == null)
                {
                    wizardUI.ShowWarning("No storage provider selected. Defaulting to 'default' provider.");
                    selectedStorageProvider = availableProviders.FirstOrDefault(p =>
                        p.ProviderId.Equals(StorageProviderIds.Default, StringComparison.OrdinalIgnoreCase));

                    if (selectedStorageProvider == null)
                    {
                        wizardUI.ShowError("Setup cannot continue without a storage provider.");
                        return Result<ConfigurationSettings>.Failure("Setup cancelled: No storage provider available.");
                    }
                }

                // Step 5: Application Root Directory Configuration
                wizardUI.ShowStepHeader(5, 10, "Application Root Directory");
                wizardUI.ShowStatus("This is where config.json and templates/ will be stored.");
                var rootDirectory = await wizardUI.PromptForRootDirectoryAsync(
                    request.ExistingConfiguration?.RootDirectory,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(rootDirectory))
                {
                    rootDirectory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        DirectoryNames.ApplicationRoot);
                }

                // Step 6: Storage Provider Configuration (provider-specific)
                string? providerPath = null;
                string? memorySubdirectory = null;

                if (selectedStorageProvider.ProviderId.Equals(StorageProviderIds.Obsidian, StringComparison.OrdinalIgnoreCase))
                {
                    // Obsidian-specific configuration
                    wizardUI.ShowStepHeader(6, 10, "Obsidian Vault Location");
                    wizardUI.ShowStatus("This is where your memory entries (today, thisweek, recordings) will be stored.");
                    providerPath = await wizardUI.PromptForObsidianVaultPathAsync(
                        request.ExistingConfiguration?.Storage.ProviderPath,
                        cancellationToken);

                    if (string.IsNullOrWhiteSpace(providerPath))
                    {
                        wizardUI.ShowError("Setup cannot continue without a valid Obsidian vault path.");
                        return Result<ConfigurationSettings>.Failure("Setup cancelled: No vault path provided. Run 'tom setup' again.");
                    }

                    // Step 6.5: Obsidian Subdirectory (optional)
                    wizardUI.ShowStatus("Optionally create a subdirectory under the vault for Ten Second Tom entries.");
                    memorySubdirectory = await wizardUI.PromptForSubdirectoryAsync(
                        "Subdirectory name (leave empty for vault root):",
                        request.ExistingConfiguration?.Storage.MemorySubdirectory,
                        cancellationToken);
                }
                else
                {
                    // Default provider uses RootDirectory for both config and storage
                    wizardUI.ShowStepHeader(6, 10, "Default Storage Configuration");
                    wizardUI.ShowStatus("Memory entries will be stored in the application root directory.");
                    // providerPath stays null - provider will use RootDirectory
                }

                // Step 7: Logging Level
                wizardUI.ShowStepHeader(7, 10, "Logging Level");
                var logLevel = await wizardUI.PromptForLogLevelAsync(
                    request.ExistingConfiguration?.Optional.LogLevel,
                    cancellationToken);

                // Step 8: Data Retention
                wizardUI.ShowStepHeader(8, 10, "Data Retention");
                var retentionDays = await wizardUI.PromptForRetentionDaysAsync(
                    request.ExistingConfiguration?.Optional.RetentionDays,
                    cancellationToken);

                // Step 9: Configuration Summary
                wizardUI.ShowStepHeader(9, 10, "Configuration Summary");

                var newConfiguration = new ConfigurationSettings
                {
                    RootDirectory = rootDirectory!,
                    Ssh = new SshConfiguration
                    {
                        KeyPath = selectedSshKey?.FilePath,
                        KeySource = selectedSshKey?.Source,
                        KeyDisplayName = selectedSshKey?.DisplayName,
                        AgentSocketPath = selectedSshKey?.Source != SshKeySource.FileSystem
                            ? GetAgentSocketPath(selectedSshKey?.Source)
                            : null
                    },
                    Llm = new LlmConfiguration
                    {
                        Provider = selectedProvider.Value,
                        ApiKey = apiKey,
                        Model = modelId, // Set the selected or default model
                        MaxInputTokens = selectedProvider.Value == LlmProvider.Anthropic
                            ? LlmConstants.DefaultMaxInputTokensAnthropic
                            : LlmConstants.DefaultMaxInputTokensOpenAI
                    },
                    Storage = new StorageConfiguration
                    {
                        ProviderId = selectedStorageProvider.ProviderId,
                        ProviderPath = providerPath,
                        MemorySubdirectory = memorySubdirectory,
                        CreateIfMissing = true,
                        RetentionPolicy = Shared.Models.RetentionPolicy.Indefinite,
                        AutoPurge = false,
                        MaxFileSizeBytes = null,
                        CompressionEnabled = false
                    },
                    Optional = new OptionalConfiguration
                    {
                        LogLevel = logLevel ?? Microsoft.Extensions.Logging.LogLevel.Information,
                        RetentionDays = retentionDays ?? -1, // -1 means unlimited (never delete)
                        EnableTelemetry = false
                    },
                    Audio = request.ExistingConfiguration?.Audio ?? new AudioConfigurationDisplay(),
                    CreatedAt = request.ExistingConfiguration?.CreatedAt ?? DateTime.UtcNow,
                    LastModifiedAt = isReconfiguration ? DateTime.UtcNow : null,
                    ConfigurationVersion = "1.0"
                };

                var confirmed = await wizardUI.ShowSummaryAndConfirmAsync(newConfiguration, cancellationToken).ConfigureAwait(false);

                if (!confirmed)
                {
                    wizardUI.ShowWarning("Setup cancelled by user. No changes were saved.");
                    wizardUI.ShowStatus("Run 'tom setup' anytime to configure Ten Second Tom.");
                    return Result<ConfigurationSettings>.Failure("Setup cancelled: User chose not to save configuration. Run 'tom setup' to try again.");
                }

                // Step 10: Save Configuration
                wizardUI.ShowStepHeader(10, 10, "Saving Configuration");
                wizardUI.ShowStatus("Saving configuration...");

                var saveResult = await storageService.SaveAsync(newConfiguration, cancellationToken).ConfigureAwait(false);

                if (!saveResult.IsSuccess)
                {
                    wizardUI.ShowError($"Failed to save configuration: {saveResult.Error}");
                    wizardUI.ShowStatus("Your configuration could not be saved. This might be a permissions issue.");
                    wizardUI.ShowStatus("Try running the command again, or check the logs for more details.");
                    return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}. Check file permissions and try 'tom setup' again.");
                }

                wizardUI.ShowSuccess("✓ Setup complete!");
                wizardUI.ShowStatus($"Configuration saved to {saveResult.Value}");
                wizardUI.ShowStatus("You can view your configuration anytime with: tom config --show");
                wizardUI.ShowStatus("To change individual settings, use: tom config --set <setting-name> <value>");

                logger.LogInformation("Setup wizard completed successfully");
                return Result<ConfigurationSettings>.Success(newConfiguration);
            }
            catch (OperationCanceledException)
            {
                wizardUI.ShowWarning("Setup was interrupted. No changes were saved.");
                wizardUI.ShowStatus("Run 'tom setup' anytime to complete configuration.");
                logger.LogWarning("Setup wizard was cancelled");
                return Result<ConfigurationSettings>.Failure("Setup cancelled: Operation was interrupted. Run 'tom setup' to try again.");
            }
            catch (Exception ex)
            {
                wizardUI.ShowError($"An unexpected error occurred: {ex.Message}");
                wizardUI.ShowStatus("Please check the logs for more details, then try running 'tom setup' again.");
                wizardUI.ShowStatus("If the problem persists, please report it at: https://github.com/sirkirby/ten-second-tom/issues");
                logger.LogError(ex, "Setup wizard failed");
                return Result<ConfigurationSettings>.Failure($"Setup failed: {ex.Message}. Check logs and try 'tom setup' again, or report the issue if it persists.");
            }
        }

        /// <summary>
        /// Gets the SSH agent socket path for the specified key source.
        /// Platform-aware: supports macOS, Linux, and Windows.
        /// </summary>
        /// <param name="source">The SSH key source to get the socket path for.</param>
        /// <returns>The socket path, or null if not available or not applicable.</returns>
        private static string? GetAgentSocketPath(SshKeySource? source)
        {
            if (!source.HasValue)
                return null;

            // Convert SshKeySource to SshAgentProvider and use the existing resolver
            // which already handles platform detection and path resolution
            var provider = source.Value switch
            {
                SshKeySource.SystemAgent => SshAgentProvider.System,
                SshKeySource.OnePasswordAgent => SshAgentProvider.OnePassword,
                SshKeySource.SecretiveAgent => SshAgentProvider.Secretive,
                _ => SshAgentProvider.System // Default fallback
            };

            return SshAgentProviderResolver.GetSocketPath(provider);
        }
    }
}

