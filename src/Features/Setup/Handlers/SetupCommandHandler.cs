using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup.Handlers;

/// <summary>
/// Handler for SetupCommand
/// Orchestrates the complete setup wizard flow
/// </summary>
public sealed class SetupCommandHandler
{
    private readonly IConfigurationStorageService _storageService;
    private readonly ISetupWizardUI _wizardUI;
    private readonly ISshKeyDetectorFactory _sshKeyDetectorFactory;
    private readonly ILogger<SetupCommandHandler> _logger;

    public SetupCommandHandler(
        IConfigurationStorageService storageService,
        ISetupWizardUI wizardUI,
        ISshKeyDetectorFactory sshKeyDetectorFactory,
        ILogger<SetupCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _wizardUI = wizardUI ?? throw new ArgumentNullException(nameof(wizardUI));
        _sshKeyDetectorFactory = sshKeyDetectorFactory ?? throw new ArgumentNullException(nameof(sshKeyDetectorFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ConfigurationSettings>> Handle(
        SetupCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting setup wizard (Force: {Force}, NonInteractive: {NonInteractive})", 
                command.Force, command.NonInteractive);

            var progress = SetupProgress.CreateInitial(totalSteps: 8);
            
            // Check if reconfiguration or first-time setup
            var isReconfiguration = command.ExistingConfiguration != null;
            if (isReconfiguration)
            {
                _wizardUI.ShowStatus("Reconfiguring Ten Second Tom");
            }
            else
            {
                _wizardUI.ShowStatus("Welcome to Ten Second Tom! Let's get you set up.");
            }

            // Step 1: SSH Key Configuration
            _wizardUI.ShowStepHeader(1, 8, "SSH Key Configuration");
            _wizardUI.ShowStatus("Detecting SSH keys...");
            
            var sshDetectionResult = await _sshKeyDetectorFactory.DetectKeysAsync(
                TimeSpan.FromSeconds(5), 
                cancellationToken);

            var selectedSshKey = await _wizardUI.PromptForSshKeyAsync(
                sshDetectionResult.DetectedKeys,
                null,
                cancellationToken);

            if (selectedSshKey == null && !command.NonInteractive)
            {
                _wizardUI.ShowError("Setup cannot continue without an SSH key.");
                _wizardUI.ShowStatus("Please generate an SSH key or add one to your SSH agent, then run 'tom setup' again.");
                _wizardUI.ShowStatus("Learn more: https://docs.github.com/en/authentication/connecting-to-github-with-ssh");
                return Result<ConfigurationSettings>.Failure("Setup cancelled: No SSH key selected. Run 'tom setup' after adding an SSH key.");
            }

            // Step 2: LLM Provider Selection
            _wizardUI.ShowStepHeader(2, 8, "LLM Provider Selection");
            var selectedProvider = await _wizardUI.PromptForLlmProviderAsync(
                command.ExistingConfiguration?.Llm.Provider,
                cancellationToken);

            if (!selectedProvider.HasValue)
            {
                _wizardUI.ShowError("Setup cannot continue without selecting an AI provider.");
                _wizardUI.ShowStatus("Please choose OpenAI or Anthropic to continue.");
                return Result<ConfigurationSettings>.Failure("Setup cancelled: No LLM provider selected. Run 'tom setup' to try again.");
            }

            // Step 2.5: Model Selection (new step)
            _wizardUI.ShowStatus("Selecting model for the chosen provider...");
            var selectedModel = await _wizardUI.PromptForModelAsync(
                selectedProvider.Value,
                command.ExistingConfiguration?.Llm.Model,
                cancellationToken);

            // If no model selected, use the default for the provider
            string? modelId = selectedModel?.Id;
            if (string.IsNullOrEmpty(modelId))
            {
                var defaultModel = ModelRegistry.GetDefault(selectedProvider.Value);
                modelId = defaultModel.Id;
                _wizardUI.ShowStatus($"Using default model: {defaultModel.DisplayName}");
            }

            // Step 3: API Key Configuration
            _wizardUI.ShowStepHeader(3, 8, "API Key Configuration");
            var apiKey = await _wizardUI.PromptForApiKeyAsync(
                selectedProvider.Value,
                command.ExistingConfiguration?.Llm.ApiKey,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _wizardUI.ShowError("Setup cannot continue without a valid API key.");
                var providerName = selectedProvider.Value == LlmProvider.OpenAI ? "OpenAI" : "Anthropic";
                var keyUrl = selectedProvider.Value == LlmProvider.OpenAI 
                    ? "https://platform.openai.com/api-keys" 
                    : "https://console.anthropic.com/settings/keys";
                _wizardUI.ShowStatus($"Get your {providerName} API key from: {keyUrl}");
                return Result<ConfigurationSettings>.Failure($"Setup cancelled: No API key provided. Visit {keyUrl} to create an API key, then run 'tom setup' again.");
            }

            // Step 4: Memory Directory Configuration
            _wizardUI.ShowStepHeader(4, 8, "Memory Storage Location");
            var memoryDirectory = await _wizardUI.PromptForMemoryDirectoryAsync(
                command.ExistingConfiguration?.RootDirectory,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(memoryDirectory))
            {
                memoryDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    DirectoryNames.ApplicationRoot);
            }

            // Step 5: Logging Level
            _wizardUI.ShowStepHeader(5, 8, "Logging Level");
            var logLevel = await _wizardUI.PromptForLogLevelAsync(
                command.ExistingConfiguration?.Optional.LogLevel,
                cancellationToken);

            // Step 6: Data Retention
            _wizardUI.ShowStepHeader(6, 8, "Data Retention");
            var retentionDays = await _wizardUI.PromptForRetentionDaysAsync(
                command.ExistingConfiguration?.Optional.RetentionDays,
                cancellationToken);

            // Step 7: Configuration Summary
            _wizardUI.ShowStepHeader(7, 8, "Configuration Summary");
            
            var newConfiguration = new ConfigurationSettings
            {
                RootDirectory = memoryDirectory,
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
                    CreateIfMissing = true
                },
                Optional = new OptionalConfiguration
                {
                    LogLevel = logLevel ?? Microsoft.Extensions.Logging.LogLevel.Information,
                    RetentionDays = retentionDays ?? -1, // -1 means unlimited (never delete)
                    EnableTelemetry = false
                },
                Audio = command.ExistingConfiguration?.Audio ?? new AudioConfigurationDisplay(),
                CreatedAt = command.ExistingConfiguration?.CreatedAt ?? DateTime.UtcNow,
                LastModifiedAt = isReconfiguration ? DateTime.UtcNow : null,
                ConfigurationVersion = "1.0"
            };

            var confirmed = await _wizardUI.ShowSummaryAndConfirmAsync(newConfiguration, cancellationToken).ConfigureAwait(false);
            
            if (!confirmed)
            {
                _wizardUI.ShowWarning("Setup cancelled by user. No changes were saved.");
                _wizardUI.ShowStatus("Run 'tom setup' anytime to configure Ten Second Tom.");
                return Result<ConfigurationSettings>.Failure("Setup cancelled: User chose not to save configuration. Run 'tom setup' to try again.");
            }

            // Step 8: Save Configuration
            _wizardUI.ShowStepHeader(8, 8, "Saving Configuration");
            _wizardUI.ShowStatus("Saving configuration...");

            var saveResult = await _storageService.SaveAsync(newConfiguration, cancellationToken).ConfigureAwait(false);
            
            if (!saveResult.IsSuccess)
            {
                _wizardUI.ShowError($"Failed to save configuration: {saveResult.Error}");
                _wizardUI.ShowStatus("Your configuration could not be saved. This might be a permissions issue.");
                _wizardUI.ShowStatus("Try running the command again, or check the logs for more details.");
                return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}. Check file permissions and try 'tom setup' again.");
            }

            _wizardUI.ShowSuccess("✓ Setup complete!");
            _wizardUI.ShowStatus($"Configuration saved to {saveResult.Value}");
            _wizardUI.ShowStatus("You can view your configuration anytime with: tom config --show");
            _wizardUI.ShowStatus("To change individual settings, use: tom config --set <setting-name> <value>");

            _logger.LogInformation("Setup wizard completed successfully");
            return Result<ConfigurationSettings>.Success(newConfiguration);
        }
        catch (OperationCanceledException)
        {
            _wizardUI.ShowWarning("Setup was interrupted. No changes were saved.");
            _wizardUI.ShowStatus("Run 'tom setup' anytime to complete configuration.");
            _logger.LogWarning("Setup wizard was cancelled");
            return Result<ConfigurationSettings>.Failure("Setup cancelled: Operation was interrupted. Run 'tom setup' to try again.");
        }
        catch (Exception ex)
        {
            _wizardUI.ShowError($"An unexpected error occurred: {ex.Message}");
            _wizardUI.ShowStatus("Please check the logs for more details, then try running 'tom setup' again.");
            _wizardUI.ShowStatus("If the problem persists, please report it at: https://github.com/sirkirby/ten-second-tom/issues");
            _logger.LogError(ex, "Setup wizard failed");
            return Result<ConfigurationSettings>.Failure($"Setup failed: {ex.Message}. Check logs and try 'tom setup' again, or report the issue if it persists.");
        }
    }

    private static string? GetAgentSocketPath(SshKeySource? source)
    {
        if (!source.HasValue)
            return null;

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return source.Value switch
        {
            SshKeySource.OnePasswordAgent => Path.Combine(homeDir, 
                "Library/Group Containers/2BUA8C4S2C.com.1password/t/agent.sock"),
            SshKeySource.SecretiveAgent => Path.Combine(homeDir, 
                "Library/Containers/com.maxgoedjen.Secretive.SecretAgent/Data/socket.ssh"),
            SshKeySource.SystemAgent => Environment.GetEnvironmentVariable("SSH_AUTH_SOCK"),
            _ => null
        };
    }
}
