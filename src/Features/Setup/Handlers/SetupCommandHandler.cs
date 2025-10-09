using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Queries;
using TenSecondTom.Infrastructure.Configuration;
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
                return Result<ConfigurationSettings>.Failure("Setup was cancelled: No SSH key selected");
            }

            // Step 2: LLM Provider Selection
            _wizardUI.ShowStepHeader(2, 8, "LLM Provider Selection");
            var selectedProvider = await _wizardUI.PromptForLlmProviderAsync(
                command.ExistingConfiguration?.Llm.Provider,
                cancellationToken);

            if (!selectedProvider.HasValue)
            {
                return Result<ConfigurationSettings>.Failure("Setup was cancelled: No LLM provider selected");
            }

            // Step 3: API Key Configuration
            _wizardUI.ShowStepHeader(3, 8, "API Key Configuration");
            var apiKey = await _wizardUI.PromptForApiKeyAsync(
                selectedProvider.Value,
                command.ExistingConfiguration?.Llm.ApiKey,
                cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Result<ConfigurationSettings>.Failure("Setup was cancelled: No API key provided");
            }

            // Step 4: Memory Directory Configuration
            _wizardUI.ShowStepHeader(4, 8, "Memory Storage Location");
            var memoryDirectory = await _wizardUI.PromptForMemoryDirectoryAsync(
                command.ExistingConfiguration?.Storage.MemoryDirectory,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(memoryDirectory))
            {
                memoryDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".memory", "ten-second-tom");
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
                    Model = null // Will be set by default based on provider
                },
                Storage = new StorageConfiguration
                {
                    MemoryDirectory = memoryDirectory,
                    CreateIfMissing = true
                },
                Optional = new OptionalConfiguration
                {
                    LogLevel = logLevel ?? Microsoft.Extensions.Logging.LogLevel.Information,
                    RetentionDays = retentionDays ?? -1, // -1 means unlimited (never delete)
                    EnableTelemetry = false
                },
                CreatedAt = command.ExistingConfiguration?.CreatedAt ?? DateTime.UtcNow,
                LastModifiedAt = isReconfiguration ? DateTime.UtcNow : null,
                ConfigurationVersion = "1.0"
            };

            var confirmed = await _wizardUI.ShowSummaryAndConfirmAsync(newConfiguration, cancellationToken).ConfigureAwait(false);
            
            if (!confirmed)
            {
                return Result<ConfigurationSettings>.Failure("Setup was cancelled by user");
            }

            // Step 8: Save Configuration
            _wizardUI.ShowStepHeader(8, 8, "Saving Configuration");
            _wizardUI.ShowStatus("Saving configuration...");

            var saveResult = await _storageService.SaveAsync(newConfiguration, cancellationToken).ConfigureAwait(false);
            
            if (!saveResult.IsSuccess)
            {
                _wizardUI.ShowError($"Failed to save configuration: {saveResult.Error}");
                return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}");
            }

            _wizardUI.ShowSuccess("✓ Setup complete!");
            _wizardUI.ShowStatus($"Configuration saved to {saveResult.Value}");

            _logger.LogInformation("Setup wizard completed successfully");
            return Result<ConfigurationSettings>.Success(newConfiguration);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Setup wizard was cancelled");
            return Result<ConfigurationSettings>.Failure("Setup was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Setup wizard failed");
            return Result<ConfigurationSettings>.Failure($"Setup failed: {ex.Message}");
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
