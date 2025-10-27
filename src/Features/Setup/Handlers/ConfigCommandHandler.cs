using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Commands;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Validation;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
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
    private readonly IAppSettingsStorageService _appSettingsStorage;
    private readonly ILogger<ConfigCommandHandler> _logger;

    public ConfigCommandHandler(
        IConfigurationStorageService storageService,
        IConfiguration configuration,
        ISetupWizardUI setupWizard,
        IEnumerable<IApiKeyValidator> apiKeyValidators,
        IAppSettingsStorageService appSettingsStorage,
        ILogger<ConfigCommandHandler> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _setupWizard = setupWizard ?? throw new ArgumentNullException(nameof(setupWizard));
        _apiKeyValidators = apiKeyValidators ?? throw new ArgumentNullException(nameof(apiKeyValidators));
        _appSettingsStorage = appSettingsStorage ?? throw new ArgumentNullException(nameof(appSettingsStorage));
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
        string? envSshKeyPath = _configuration[ConfigurationKeys.SshKeyPathKey];
        string? envSshKeySource = _configuration[ConfigurationKeys.SshKeySourceKey];
        string? envSshAgentSocketPath = _configuration[ConfigurationKeys.SshAgentSocketPathKey];
        string? envProvider = _configuration[ConfigurationKeys.LlmProviderKey];
        string? envApiKey = _configuration[ConfigurationKeys.LlmApiKeyKey];
        string? envModel = _configuration[ConfigurationKeys.LlmModelKey];
        string? envMemoryDir = _configuration[ConfigurationKeys.MemoryDirectoryKey];
        string? envCreateIfMissing = _configuration["TenSecondTom:CreateIfMissing"];
        string? envLogLevel = _configuration["TenSecondTom:Optional:LogLevel"];
        string? envRetentionDays = _configuration["TenSecondTom:Optional:RetentionDays"];
        string? envEnableTelemetry = _configuration["TenSecondTom:Optional:EnableTelemetry"];
        
        // Audio configuration from environment
        string? envSttProvider = _configuration["TenSecondTom:Audio:SttProvider"];
        string? envSttApiKey = _configuration["TenSecondTom:Audio:SttApiKey"];
        string? envSttFallbackEnabled = _configuration["TenSecondTom:Audio:SttFallbackEnabled"];
        string? envKeepFiles = _configuration["TenSecondTom:Audio:KeepFiles"];
        string? envInputVolume = _configuration[ConfigurationKeys.AudioRecorderInputVolumeKey];
        string? envNoiseReduction = _configuration[ConfigurationKeys.AudioRecorderEnableNoiseReductionKey];
        string? envFreqFilters = _configuration[ConfigurationKeys.AudioRecorderEnableFrequencyFiltersKey];
        string? envRemoveSilence = _configuration[ConfigurationKeys.AudioPreprocessingRemoveSilenceKey];
        string? envSilenceThreshold = _configuration[ConfigurationKeys.AudioPreprocessingSilenceThresholdDbKey];
        string? envMinSilenceDuration = _configuration[ConfigurationKeys.AudioPreprocessingMinimumSilenceDurationMsKey];

        // If environment variables are set, they override user secrets
        bool hasSshOverrides = !string.IsNullOrWhiteSpace(envSshKeyPath) ||
                              !string.IsNullOrWhiteSpace(envSshKeySource) ||
                              !string.IsNullOrWhiteSpace(envSshAgentSocketPath);
        bool hasLlmOverrides = !string.IsNullOrWhiteSpace(envProvider) ||
                              !string.IsNullOrWhiteSpace(envApiKey) ||
                              !string.IsNullOrWhiteSpace(envModel);
        bool hasStorageOverrides = !string.IsNullOrWhiteSpace(envMemoryDir) ||
                                   !string.IsNullOrWhiteSpace(envCreateIfMissing);
        bool hasOptionalOverrides = !string.IsNullOrWhiteSpace(envLogLevel) ||
                                    !string.IsNullOrWhiteSpace(envRetentionDays) ||
                                    !string.IsNullOrWhiteSpace(envEnableTelemetry);
        bool hasAudioOverrides = !string.IsNullOrWhiteSpace(envSttProvider) ||
                                !string.IsNullOrWhiteSpace(envSttApiKey) ||
                                !string.IsNullOrWhiteSpace(envSttFallbackEnabled) ||
                                !string.IsNullOrWhiteSpace(envKeepFiles) ||
                                !string.IsNullOrWhiteSpace(envInputVolume) ||
                                !string.IsNullOrWhiteSpace(envNoiseReduction) ||
                                !string.IsNullOrWhiteSpace(envFreqFilters) ||
                                !string.IsNullOrWhiteSpace(envRemoveSilence) ||
                                !string.IsNullOrWhiteSpace(envSilenceThreshold) ||
                                !string.IsNullOrWhiteSpace(envMinSilenceDuration);

        // Load audio configuration from IConfiguration (includes appsettings.json and env vars)
        var audioConfig = new AudioConfigurationDisplay
        {
            SttProvider = _configuration["TenSecondTom:Audio:SttProvider"] ?? SttProviders.WhisperCpp,
            SttApiKey = _configuration["TenSecondTom:Audio:SttApiKey"],
            SttFallbackEnabled = bool.TryParse(_configuration["TenSecondTom:Audio:SttFallbackEnabled"], out var fallback) ? fallback : false,
            SttFallbackProvider = _configuration["TenSecondTom:Audio:SttFallbackProvider"],
            SttFallbackApiKey = _configuration["TenSecondTom:Audio:SttFallbackApiKey"],
            KeepFiles = bool.TryParse(_configuration["TenSecondTom:Audio:KeepFiles"], out var keepFiles) ? keepFiles : true,
            Recorder = new RecorderConfigurationDisplay
            {
                InputVolume = double.TryParse(_configuration[ConfigurationKeys.AudioRecorderInputVolumeKey], out var inputVolume) ? inputVolume : 1.0,
                EnableNoiseReduction = bool.TryParse(_configuration[ConfigurationKeys.AudioRecorderEnableNoiseReductionKey], out var noiseReduction) ? noiseReduction : true,
                EnableFrequencyFilters = bool.TryParse(_configuration[ConfigurationKeys.AudioRecorderEnableFrequencyFiltersKey], out var freqFilters) ? freqFilters : true
            },
            Preprocessing = new PreprocessingConfigurationDisplay
            {
                RemoveSilence = bool.TryParse(_configuration[ConfigurationKeys.AudioPreprocessingRemoveSilenceKey], out var removeSilence) ? removeSilence : true,
                SilenceThresholdDb = int.TryParse(_configuration[ConfigurationKeys.AudioPreprocessingSilenceThresholdDbKey], out var silenceThreshold) ? silenceThreshold : -50,
                MinimumSilenceDurationMs = int.TryParse(_configuration[ConfigurationKeys.AudioPreprocessingMinimumSilenceDurationMsKey], out var minSilenceDuration) ? minSilenceDuration : 500
            }
        };

        if (hasSshOverrides || hasLlmOverrides || hasStorageOverrides || hasOptionalOverrides || hasAudioOverrides)
        {
            config = config with
            {
                Ssh = config.Ssh with
                {
                    KeyPath = envSshKeyPath ?? config.Ssh.KeyPath,
                    KeySource = Enum.TryParse<SshKeySource>(envSshKeySource, true, out var keySource)
                        ? keySource
                        : config.Ssh.KeySource,
                    AgentSocketPath = envSshAgentSocketPath ?? config.Ssh.AgentSocketPath
                },
                Llm = config.Llm with
                {
                    Provider = Enum.TryParse<LlmProvider>(envProvider, true, out var provider)
                        ? provider
                        : config.Llm.Provider,
                    ApiKey = envApiKey ?? config.Llm.ApiKey,
                    Model = envModel ?? config.Llm.Model
                },
                Storage = config.Storage with
                {
                    MemoryDirectory = envMemoryDir ?? config.Storage.MemoryDirectory,
                    CreateIfMissing = bool.TryParse(envCreateIfMissing, out var createIfMissing)
                        ? createIfMissing
                        : config.Storage.CreateIfMissing
                },
                Optional = config.Optional with
                {
                    LogLevel = Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(envLogLevel, true, out var logLevel)
                        ? logLevel
                        : config.Optional.LogLevel,
                    RetentionDays = int.TryParse(envRetentionDays, out var retentionDays)
                        ? retentionDays
                        : config.Optional.RetentionDays,
                    EnableTelemetry = bool.TryParse(envEnableTelemetry, out var enableTelemetry)
                        ? enableTelemetry
                        : config.Optional.EnableTelemetry
                },
                Audio = audioConfig
            };

            _logger.LogDebug("Configuration overrides applied from environment variables");
        }
        else
        {
            // No overrides, but still need to populate audio config
            config = config with
            {
                Audio = audioConfig
            };
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

        // Special handling for "audio" - interactive configuration
        if (command.SettingName.Equals("audio", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleInteractiveAudioConfigurationAsync(cancellationToken);
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
            Llm = currentConfig.Llm with
            {
                Provider = provider,
                MaxInputTokens = provider == LlmProvider.Anthropic
                    ? LlmConstants.DefaultMaxInputTokensAnthropic
                    : LlmConstants.DefaultMaxInputTokensOpenAI
            }
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
                ApiKey = apiKey,
                MaxInputTokens = selectedProvider.Value == LlmProvider.Anthropic
                    ? LlmConstants.DefaultMaxInputTokensAnthropic
                    : LlmConstants.DefaultMaxInputTokensOpenAI
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

    private async Task<Result<ConfigurationSettings>> HandleInteractiveAudioConfigurationAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting interactive audio configuration");

        // Load current audio configuration from appsettings.json
        var loadResult = await _appSettingsStorage.LoadAudioConfigurationAsync(cancellationToken);

        if (!loadResult.IsSuccess)
        {
            _setupWizard.ShowWarning("Could not load current audio configuration. Using defaults.");
        }

        var currentAudio = loadResult.IsSuccess ? loadResult.Value! : new AudioConfiguration();

        const int totalSteps = 9;

        // Step 1: Input Volume
        _setupWizard.ShowStepHeader(1, totalSteps, "Input Volume");
        var inputVolume = await _setupWizard.PromptForInputVolumeAsync(
            currentAudio.Recorder.InputVolume,
            cancellationToken);

        if (!inputVolume.HasValue)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Step 2: Noise Reduction
        _setupWizard.ShowStepHeader(2, totalSteps, "Noise Reduction");
        var noiseReduction = await _setupWizard.PromptForBooleanAsync(
            "Enable noise reduction during recording?",
            currentAudio.Recorder.EnableNoiseReduction,
            cancellationToken);

        if (!noiseReduction.HasValue)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Step 3: Frequency Filters
        _setupWizard.ShowStepHeader(3, totalSteps, "Frequency Filters");
        var frequencyFilters = await _setupWizard.PromptForBooleanAsync(
            "Enable frequency filters during recording?",
            currentAudio.Recorder.EnableFrequencyFilters,
            cancellationToken);

        if (!frequencyFilters.HasValue)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Step 4: Silence Removal
        _setupWizard.ShowStepHeader(4, totalSteps, "Silence Removal");
        var removeSilence = await _setupWizard.PromptForBooleanAsync(
            "Remove silence from recordings during preprocessing?",
            currentAudio.Preprocessing.RemoveSilence,
            cancellationToken);

        if (!removeSilence.HasValue)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Step 5: Silence Threshold (only if silence removal enabled)
        int silenceThresholdDb = currentAudio.Preprocessing.SilenceThresholdDb;
        if (removeSilence.Value)
        {
            _setupWizard.ShowStepHeader(5, totalSteps, "Silence Detection Threshold");
            var threshold = await _setupWizard.PromptForIntAsync(
                "Silence threshold in decibels (-60 to -40):",
                currentAudio.Preprocessing.SilenceThresholdDb,
                -60,
                -40,
                cancellationToken);

            if (!threshold.HasValue)
            {
                return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
            }
            silenceThresholdDb = threshold.Value;
        }
        else
        {
            _setupWizard.ShowStepHeader(5, totalSteps, "Silence Detection Threshold");
            _setupWizard.ShowStatus("Skipped (silence removal disabled)");
        }

        // Step 6: Minimum Silence Duration (only if silence removal enabled)
        int minSilenceDurationMs = currentAudio.Preprocessing.MinimumSilenceDurationMs;
        if (removeSilence.Value)
        {
            _setupWizard.ShowStepHeader(6, totalSteps, "Minimum Silence Duration");
            var duration = await _setupWizard.PromptForIntAsync(
                "Minimum silence duration to remove (ms, 100-2000):",
                currentAudio.Preprocessing.MinimumSilenceDurationMs,
                100,
                2000,
                cancellationToken);

            if (!duration.HasValue)
            {
                return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
            }
            minSilenceDurationMs = duration.Value;
        }
        else
        {
            _setupWizard.ShowStepHeader(6, totalSteps, "Minimum Silence Duration");
            _setupWizard.ShowStatus("Skipped (silence removal disabled)");
        }

        // Step 7: Speech-to-Text Provider
        _setupWizard.ShowStepHeader(7, totalSteps, "Speech-to-Text Provider");
        var sttProvider = await _setupWizard.PromptForSttProviderAsync(
            currentAudio.SttProvider,
            cancellationToken);

        if (sttProvider == null)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Step 7a: STT API Key (if provider requires it)
        string? sttApiKey = currentAudio.SttApiKey;
        if (SttProviders.RequiresApiKey(sttProvider))
        {
            sttApiKey = await _setupWizard.PromptForSttApiKeyAsync(
                sttProvider,
                currentAudio.SttApiKey,
                cancellationToken);

            if (sttApiKey == null)
            {
                return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
            }
        }

        // Step 7b: STT Fallback Provider
        bool sttFallbackEnabled = currentAudio.SttFallbackEnabled;
        string? sttFallbackProvider = currentAudio.SttFallbackProvider;
        string? sttFallbackApiKey = currentAudio.SttFallbackApiKey;

        if (SttProviders.SupportsFallback(sttProvider))
        {
            var fallback = await _setupWizard.PromptForSttFallbackAsync(
                currentAudio.SttFallbackEnabled,
                cancellationToken);

            if (!fallback.HasValue)
            {
                return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
            }

            sttFallbackEnabled = fallback.Value;

            // If fallback is enabled, prompt for provider and API key
            if (sttFallbackEnabled)
            {
                // Prompt for fallback provider
                var fallbackProvider = await _setupWizard.PromptForSttFallbackProviderAsync(
                    currentAudio.SttFallbackProvider,
                    cancellationToken);

                if (fallbackProvider == null)
                {
                    return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
                }

                sttFallbackProvider = fallbackProvider;

                // Prompt for fallback API key
                _setupWizard.ShowStatus($"Fallback provider '{fallbackProvider}' requires an API key.");

                var fallbackApiKey = await _setupWizard.PromptForSttApiKeyAsync(
                    fallbackProvider,
                    currentAudio.SttFallbackApiKey,
                    cancellationToken);

                if (fallbackApiKey == null)
                {
                    return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
                }

                sttFallbackApiKey = fallbackApiKey;
            }
            else
            {
                // Fallback is disabled, clear the provider and API key
                sttFallbackProvider = null;
                sttFallbackApiKey = null;
            }
        }

        // Step 8: Today Voice Timeout
        _setupWizard.ShowStepHeader(8, totalSteps, "Today Voice Recording Timeout");
        _setupWizard.ShowStatus("When this duration is reached, you'll be prompted to continue or finish recording.");
        var todayTimeout = await _setupWizard.PromptForIntAsync(
            "Time before prompting to continue 'today --voice' (seconds, 30-600):",
            currentAudio.Timeouts.TodaySeconds,
            30,
            600,
            cancellationToken);

        if (!todayTimeout.HasValue)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Step 9: Record Command Timeout
        _setupWizard.ShowStepHeader(9, totalSteps, "Record Command Timeout");
        _setupWizard.ShowStatus("When this duration is reached, you'll be prompted to continue or finish recording.");
        var recordTimeout = await _setupWizard.PromptForIntAsync(
            "Time before prompting to continue 'record' (seconds, 60-1800):",
            currentAudio.Timeouts.RecordSeconds,
            60,
            1800,
            cancellationToken);

        if (!recordTimeout.HasValue)
        {
            return Result<ConfigurationSettings>.Failure("Audio configuration cancelled. No changes were made.");
        }

        // Build updated audio configuration
        var updatedAudio = new AudioConfiguration
        {
            SttProvider = sttProvider,
            SttApiKey = sttApiKey,
            SttFallbackEnabled = sttFallbackEnabled,
            SttFallbackProvider = sttFallbackProvider,
            SttFallbackBinaryPath = currentAudio.SttFallbackBinaryPath, // Not modified interactively
            SttFallbackModel = currentAudio.SttFallbackModel, // Not modified interactively
            SttFallbackApiKey = sttFallbackApiKey,
            KeepFiles = currentAudio.KeepFiles, // Not modified interactively
            Recorder = new RecorderConfiguration
            {
                FfmpegPath = currentAudio.Recorder.FfmpegPath, // Not modified interactively
                InputVolume = inputVolume.Value,
                EnableNoiseReduction = noiseReduction.Value,
                EnableFrequencyFilters = frequencyFilters.Value
            },
            SttBinaryPath = currentAudio.SttBinaryPath, // Not modified interactively
            SttModel = currentAudio.SttModel, // Not modified interactively
            Preprocessing = new PreprocessingConfiguration
            {
                RemoveSilence = removeSilence.Value,
                SilenceThresholdDb = silenceThresholdDb,
                MinimumSilenceDurationMs = minSilenceDurationMs
            },
            Timeouts = new RecordingTimeoutsConfiguration
            {
                TodaySeconds = todayTimeout.Value,
                RecordSeconds = recordTimeout.Value
            }
        };

        // Save to appsettings.json
        var saveResult = await _appSettingsStorage.SaveAudioConfigurationAsync(updatedAudio, cancellationToken);

        if (!saveResult.IsSuccess)
        {
            return Result<ConfigurationSettings>.Failure($"Failed to save audio configuration: {saveResult.Error}. Changes were not applied.");
        }

        _logger.LogInformation("Audio configuration updated successfully");

        // Display success message with summary
        _setupWizard.ShowSuccess("✓ Audio configuration saved successfully");
        _setupWizard.ShowStatus($"  • Input volume: {inputVolume.Value:F1}");
        _setupWizard.ShowStatus($"  • Noise reduction: {(noiseReduction.Value ? "Enabled" : "Disabled")}");
        _setupWizard.ShowStatus($"  • Frequency filters: {(frequencyFilters.Value ? "Enabled" : "Disabled")}");
        _setupWizard.ShowStatus($"  • Silence removal: {(removeSilence.Value ? "Enabled" : "Disabled")}");
        if (removeSilence.Value)
        {
            _setupWizard.ShowStatus($"  • Silence threshold: {silenceThresholdDb} dB");
            _setupWizard.ShowStatus($"  • Min silence duration: {minSilenceDurationMs} ms");
        }
        var sttProviderDisplay = sttProvider == SttProviders.WhisperCpp ? "whisper.cpp (local)" : "OpenAI Whisper API (cloud)";
        _setupWizard.ShowStatus($"  • STT provider: {sttProviderDisplay}");
        if (sttFallbackEnabled)
        {
            _setupWizard.ShowStatus($"  • STT fallback provider: Enabled ({sttFallbackProvider})");
        }
        _setupWizard.ShowStatus($"  • Today timeout: {todayTimeout.Value}s");
        _setupWizard.ShowStatus($"  • Record timeout: {recordTimeout.Value}s");

        // Return current configuration (audio config is stored separately in appsettings.json)
        var configLoadResult = await _storageService.LoadAsync(cancellationToken);
        return configLoadResult.IsSuccess
            ? Result<ConfigurationSettings>.Success(configLoadResult.Value!)
            : Result<ConfigurationSettings>.Failure("Audio configuration saved, but could not reload main configuration.");
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
