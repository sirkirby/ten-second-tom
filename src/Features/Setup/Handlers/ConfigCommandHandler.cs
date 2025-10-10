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
    private readonly IEnumerable<IApiKeyValidator> _apiKeyValidators;
    private readonly ILogger<ConfigCommandHandler> _logger;

    public ConfigCommandHandler(
        IConfigurationStorageService storageService,
        IEnumerable<IApiKeyValidator> apiKeyValidators,
        ILogger<ConfigCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
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
        var loadResult = await _storageService.LoadAsync(cancellationToken);
        
        if (!loadResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to configure Ten Second Tom.");
        }

        _logger.LogInformation("Displaying current configuration (ShowSecrets: {ShowSecrets})", 
            command.ShowSecrets);

        return loadResult;
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
