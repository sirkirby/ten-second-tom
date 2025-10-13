using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Validation;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup.Handlers;

/// <summary>
/// Handler for ConfigCommand
/// Manages individual configuration setting updates
/// </summary>
public sealed class ConfigCommandHandler
{
    private readonly IConfigurationStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ISetupWizardUI _setupWizard;
    private readonly IEnumerable<IApiKeyValidator> _apiKeyValidators;
    private readonly ILogger<ConfigCommandHandler> _logger;

    public ConfigCommandHandler(
        IConfigurationStorageService storageService,
        IConfiguration configuration,
        ISetupWizardUI setupWizard,
        IEnumerable<IApiKeyValidator> apiKeyValidators,
        ILogger<ConfigCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _setupWizard = setupWizard ?? throw new ArgumentNullException(nameof(setupWizard));
        _apiKeyValidators = apiKeyValidators ?? throw new ArgumentNullException(nameof(apiKeyValidators));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<ConfigurationSettings>> Handle(
        ConfigCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing config command: {Action} {Setting}", 
                command.Action, command.SettingName ?? "N/A");

            return command.Action switch
            {
                ConfigAction.Show => await HandleShowAsync(command, cancellationToken),
                ConfigAction.Set => await HandleSetAsync(command, cancellationToken),
                ConfigAction.Reset => await HandleResetAsync(cancellationToken),
                ConfigAction.Validate => await HandleValidateAsync(cancellationToken),
                _ => Result<ConfigurationSettings>.Failure($"Unknown action '{command.Action}'. Valid actions: show, set, validate, reset. Use 'tom config --help' for more information.")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Config command failed");
            return Result<ConfigurationSettings>.Failure($"Configuration operation failed: {ex.Message}. Check logs for details or try 'tom config --help' for usage information.");
        }
    }

    private async Task<Result<ConfigurationSettings>> HandleShowAsync(
        ConfigCommand command,
        CancellationToken cancellationToken)
    {
        // Load base configuration from user secrets
        var loadResult = await _storageService.LoadAsync(cancellationToken);
        
        if (!loadResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to configure Ten Second Tom.");
        }

        var config = loadResult.Value!;

        // Apply environment variable overrides from IConfiguration
        // This shows the effective configuration that will be used at runtime
        string? envProvider = _configuration["Llm:Provider"];
        string? envApiKey = _configuration["Llm:ApiKey"];
        string? envModel = _configuration["Llm:Model"];

        // If environment variables are set, they override user secrets
        if (!string.IsNullOrWhiteSpace(envProvider) || 
            !string.IsNullOrWhiteSpace(envApiKey) || 
            !string.IsNullOrWhiteSpace(envModel))
        {
            config = config with
            {
                Llm = config.Llm with
                {
                    Provider = Enum.TryParse<LlmProvider>(envProvider, true, out var provider) 
                        ? provider 
                        : config.Llm.Provider,
                    ApiKey = envApiKey ?? config.Llm.ApiKey,
                    Model = envModel ?? config.Llm.Model
                }
            };

            _logger.LogDebug("Configuration overrides applied from environment variables");
        }

        _logger.LogInformation("Displaying current configuration (ShowSecrets: {ShowSecrets})", 
            command.ShowSecrets);

        return Result<ConfigurationSettings>.Success(config);
    }

    private async Task<Result<ConfigurationSettings>> HandleSetAsync(
        ConfigCommand command,
        CancellationToken cancellationToken)
    {
        // Validate command
        if (string.IsNullOrWhiteSpace(command.SettingName))
        {
            return Result<ConfigurationSettings>.Failure("Setting name is required. Example: tom config --set llm-provider OpenAI");
        }

        // Special handling for "llm" - interactive configuration
        if (command.SettingName.Equals("llm", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleInteractiveLlmConfigurationAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(command.SettingValue))
        {
            return Result<ConfigurationSettings>.Failure("Setting value is required. Example: tom config --set llm-provider OpenAI");
        }

        // Load current configuration
        var loadResult = await _storageService.LoadAsync(cancellationToken);
        
        if (!loadResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to create initial configuration, then use 'tom config --set' to update settings.");
        }

        var currentConfig = loadResult.Value!;
        
        // Update the specified setting
        var updateResult = await UpdateSettingAsync(
            currentConfig,
            command.SettingName.ToLowerInvariant(),
            command.SettingValue,
            cancellationToken);

        if (!updateResult.IsSuccess)
        {
            return updateResult;
        }

        var updatedConfig = updateResult.Value!.MarkAsModified();

        // Save updated configuration
        var saveResult = await _storageService.SaveAsync(updatedConfig, cancellationToken).ConfigureAwait(false);
        
        if (!saveResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}. Changes were not applied. Try again or check file permissions.");
        }

        _logger.LogInformation("Configuration setting '{Setting}' updated successfully", command.SettingName);
        return Result<ConfigurationSettings>.Success(updatedConfig);
    }

    private async Task<Result<ConfigurationSettings>> UpdateSettingAsync(
        ConfigurationSettings currentConfig,
        string settingName,
        string settingValue,
        CancellationToken cancellationToken)
    {
        return settingName switch
        {
            "llm-provider" => await UpdateLlmProviderAsync(currentConfig, settingValue, cancellationToken).ConfigureAwait(false),
            "api-key" => UpdateApiKey(currentConfig, settingValue),
            "memory-directory" => UpdateMemoryDirectory(currentConfig, settingValue),
            "ssh-key-path" => UpdateSshKeyPath(currentConfig, settingValue),
            "log-level" => UpdateLogLevel(currentConfig, settingValue),
            "retention-days" => UpdateRetentionDays(currentConfig, settingValue),
            _ => Result<ConfigurationSettings>.Failure($"Unknown setting '{settingName}'. Valid settings: llm-provider, api-key, memory-directory, ssh-key-path, log-level, retention-days. Use 'tom config --help' for examples.")
        };
    }

    private static async Task<Result<ConfigurationSettings>> UpdateLlmProviderAsync(
        ConfigurationSettings currentConfig,
        string value,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<LlmProvider>(value, ignoreCase: true, out var provider))
        {
            return Result<ConfigurationSettings>.Failure($"Invalid LLM provider '{value}'. Valid values: OpenAI, Anthropic. Example: tom config --set llm-provider OpenAI");
        }

        // Await a completed task to satisfy async method
        await Task.CompletedTask.ConfigureAwait(false);

        var newConfig = currentConfig with
        {
            Llm = currentConfig.Llm with { Provider = provider }
        };

        return Result<ConfigurationSettings>.Success(newConfig);
    }

    private Result<ConfigurationSettings> UpdateApiKey(
        ConfigurationSettings currentConfig,
        string value)
    {
        // Validate API key format for current provider
        var validator = _apiKeyValidators.FirstOrDefault(v => v.Provider == currentConfig.Llm.Provider);
        
        if (validator != null)
        {
            var validationResult = validator.ValidateFormatAsync(value).Result;
            if (!validationResult.IsValid)
            {
                var providerName = currentConfig.Llm.Provider == LlmProvider.OpenAI ? "OpenAI" : "Anthropic";
                var keyUrl = currentConfig.Llm.Provider == LlmProvider.OpenAI 
                    ? "https://platform.openai.com/api-keys" 
                    : "https://console.anthropic.com/settings/keys";
                return Result<ConfigurationSettings>.Failure($"Invalid {providerName} API key format: {validationResult.ErrorMessage}. Get a valid key from {keyUrl}");
            }
        }

        var newConfig = currentConfig with
        {
            Llm = currentConfig.Llm with { ApiKey = value }
        };

        return Result<ConfigurationSettings>.Success(newConfig);
    }

    private static Result<ConfigurationSettings> UpdateMemoryDirectory(
        ConfigurationSettings currentConfig,
        string value)
    {
        try
        {
            var fullPath = Path.GetFullPath(value);
            
            var newConfig = currentConfig with
            {
                Storage = currentConfig.Storage with { MemoryDirectory = fullPath }
            };

            return Result<ConfigurationSettings>.Success(newConfig);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationSettings>.Failure($"Invalid directory path '{value}': {ex.Message}. Provide an absolute path or relative path like './memory'.");
        }
    }

    private static Result<ConfigurationSettings> UpdateSshKeyPath(
        ConfigurationSettings currentConfig,
        string value)
    {
        try
        {
            var expandedPath = value.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            var fullPath = Path.GetFullPath(expandedPath);

            if (!File.Exists(fullPath))
            {
                return Result<ConfigurationSettings>.Failure($"SSH key file not found: {fullPath}. Verify the file exists and the path is correct. Example: ~/.ssh/id_ed25519.pub");
            }

            var newConfig = currentConfig with
            {
                Ssh = currentConfig.Ssh with 
                { 
                    KeyPath = fullPath,
                    KeySource = SshKeySource.ManualPath
                }
            };

            return Result<ConfigurationSettings>.Success(newConfig);
        }
        catch (Exception ex)
        {
            return Result<ConfigurationSettings>.Failure($"Invalid SSH key path '{value}': {ex.Message}. Provide a valid path like ~/.ssh/id_ed25519.pub");
        }
    }

    private static Result<ConfigurationSettings> UpdateLogLevel(
        ConfigurationSettings currentConfig,
        string value)
    {
        if (!Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(value, ignoreCase: true, out var logLevel))
        {
            return Result<ConfigurationSettings>.Failure($"Invalid log level '{value}'. Valid values: Debug, Information, Warning, Error. Example: tom config --set log-level Information");
        }

        var newConfig = currentConfig with
        {
            Optional = currentConfig.Optional with { LogLevel = logLevel }
        };

        return Result<ConfigurationSettings>.Success(newConfig);
    }

    private static Result<ConfigurationSettings> UpdateRetentionDays(
        ConfigurationSettings currentConfig,
        string value)
    {
        if (!int.TryParse(value, out var days) || days <= 0)
        {
            return Result<ConfigurationSettings>.Failure($"Invalid retention days '{value}'. Must be a positive integer. Example: tom config --set retention-days 30");
        }

        var newConfig = currentConfig with
        {
            Optional = currentConfig.Optional with { RetentionDays = days }
        };

        return Result<ConfigurationSettings>.Success(newConfig);
    }

    /// <summary>
    /// Handles interactive LLM configuration via 'tom config llm'
    /// Prompts for provider and model selection, updates configuration
    /// </summary>
    private async Task<Result<ConfigurationSettings>> HandleInteractiveLlmConfigurationAsync(
        CancellationToken cancellationToken)
    {
        // Load current configuration
        var loadResult = await _storageService.LoadAsync(cancellationToken);
        
        if (!loadResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to create initial configuration, then use 'tom config llm' to update LLM settings.");
        }

        var currentConfig = loadResult.Value!;

        _logger.LogInformation("Starting interactive LLM configuration");
        
        // Determine total steps (3 if provider changes, 2 if same provider)
        bool willChangeProvider = false;
        
        // Step 1: Prompt for LLM provider
        _setupWizard.ShowStepHeader(1, 3, "LLM Provider Selection");
        var selectedProvider = await _setupWizard.PromptForLlmProviderAsync(
            currentConfig.Llm.Provider,
            cancellationToken);

        if (!selectedProvider.HasValue)
        {
            _logger.LogInformation("LLM configuration cancelled by user");
            return Result<ConfigurationSettings>.Failure("LLM configuration cancelled. No changes were made.");
        }

        willChangeProvider = selectedProvider.Value != currentConfig.Llm.Provider;
        int totalSteps = willChangeProvider ? 3 : 2;

        // Step 2: Prompt for model selection
        _setupWizard.ShowStepHeader(2, totalSteps, "Model Selection");
        
        // Pass current model only if staying with same provider
        var currentModelId = selectedProvider.Value == currentConfig.Llm.Provider 
            ? currentConfig.Llm.Model 
            : null;

        var selectedModel = await _setupWizard.PromptForModelAsync(
            selectedProvider.Value,
            currentModelId,
            cancellationToken);

        if (selectedModel == null)
        {
            _logger.LogInformation("Model selection cancelled by user");
            return Result<ConfigurationSettings>.Failure("Model selection cancelled. No changes were made.");
        }

        // Step 3: If provider changed, prompt for new API key
        string? apiKey = currentConfig.Llm.ApiKey;
        bool providerChanged = selectedProvider.Value != currentConfig.Llm.Provider;
        
        if (providerChanged)
        {
            _setupWizard.ShowStepHeader(3, 3, "API Key Configuration");
            _setupWizard.ShowWarning($"Provider changed from {currentConfig.Llm.Provider} to {selectedProvider.Value}. A new API key is required.");
            
            var newApiKey = await _setupWizard.PromptForApiKeyAsync(
                selectedProvider.Value,
                null, // Don't show current key from different provider
                cancellationToken);

            if (string.IsNullOrWhiteSpace(newApiKey))
            {
                _logger.LogInformation("API key entry cancelled by user");
                return Result<ConfigurationSettings>.Failure("API key is required when changing providers. Configuration not updated.");
            }

            // Validate the API key format
            var validator = _apiKeyValidators.FirstOrDefault(v => v.Provider == selectedProvider.Value);
            if (validator != null)
            {
                var validationResult = await validator.ValidateFormatAsync(newApiKey);
                if (!validationResult.IsValid)
                {
                    return Result<ConfigurationSettings>.Failure($"Invalid API key format: {validationResult.ErrorMessage}");
                }
            }

            apiKey = newApiKey;
        }

        // Update configuration
        var updatedConfig = currentConfig with
        {
            Llm = currentConfig.Llm with 
            { 
                Provider = selectedProvider.Value,
                Model = selectedModel.Id,
                ApiKey = apiKey
            }
        };

        var markedConfig = updatedConfig.MarkAsModified();

        // Save updated configuration
        var saveResult = await _storageService.SaveAsync(markedConfig, cancellationToken).ConfigureAwait(false);
        
        if (!saveResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}. Changes were not applied. Try again or check file permissions.");
        }

        _logger.LogInformation(
            "LLM configuration updated successfully: Provider={Provider}, Model={Model}", 
            selectedProvider.Value, 
            selectedModel.Id);

        // Display success message
        var providerName = selectedProvider.Value == LlmProvider.OpenAI ? "OpenAI" : "Anthropic";
        _setupWizard.ShowSuccess($"✓ LLM configuration updated: {providerName} - {selectedModel.DisplayName} [{selectedModel.CostTier}]");

        return Result<ConfigurationSettings>.Success(markedConfig);
    }

    private Task<Result<ConfigurationSettings>> HandleResetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reset configuration not yet implemented");
        return Task.FromResult(Result<ConfigurationSettings>.Failure("Reset configuration is not yet implemented. To reconfigure, run 'tom setup' to walk through all settings again."));
    }

    private async Task<Result<ConfigurationSettings>> HandleValidateAsync(CancellationToken cancellationToken)
    {
        var loadResult = await _storageService.LoadAsync(cancellationToken);
        
        if (!loadResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to create a configuration.");
        }

        var config = loadResult.Value!;

        if (!config.IsValid())
        {
            return Result<ConfigurationSettings>.Failure("Configuration validation failed: Required fields are missing or invalid. Run 'tom setup' to reconfigure.");
        }

        _logger.LogInformation("Configuration validation passed");
        return Result<ConfigurationSettings>.Success(config);
    }
}
