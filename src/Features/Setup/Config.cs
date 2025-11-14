using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Views or modifies individual configuration settings.
/// Hybrid command/query for configuration management.
/// </summary>
public static class Config
{
    /// <summary>
    /// Command to view or modify individual configuration settings.
    /// </summary>
    public sealed record Command : IRequest<Result<ConfigurationSettings>>
    {
        /// <summary>
        /// Gets the action to perform (Show, Set, Reset, Validate).
        /// </summary>
        public ConfigAction Action { get; init; } = ConfigAction.Show;

        /// <summary>
        /// Gets the setting name to modify (required for Set action).
        /// Valid names: llm-provider, api-key, memory-directory, ssh-key-path, log-level, retention-days.
        /// Use 'tom config llm' or 'tom config audio' for guided configuration flows.
        /// </summary>
        public string? SettingName { get; init; }

        /// <summary>
        /// Gets the new value for the setting (required for Set action).
        /// </summary>
        public string? SettingValue { get; init; }

        /// <summary>
        /// Gets whether to display last 4 characters of secrets (for Show action).
        /// </summary>
        public bool ShowSecrets { get; init; }
    }

    /// <summary>
    /// Validator for Config command (auto-discovered by FluentValidation).
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        private static readonly string[] ValidSettingNames =
        [
            "llm-provider",
            "api-key",
            "memory-directory",
            "ssh-key-path",
            "log-level",
            "retention-days",
            "llm",
            "audio"
        ];

        public Validator()
        {
            // SettingName is required for Set action
            RuleFor(x => x.SettingName)
                .NotEmpty()
                .When(x => x.Action == ConfigAction.Set)
                .WithMessage("SettingName is required for Set action");

            // SettingName must be valid if provided
            RuleFor(x => x.SettingName)
                .Must(name => string.IsNullOrWhiteSpace(name) || ValidSettingNames.Contains(name.ToLowerInvariant()))
                .When(x => !string.IsNullOrWhiteSpace(x.SettingName))
                .WithMessage($"SettingName must be one of: {string.Join(", ", ValidSettingNames)}");

            // SettingValue is required for Set action EXCEPT interactive shortcuts ("llm", "audio")
            RuleFor(x => x.SettingValue)
                .NotEmpty()
                .When(x =>
                    x.Action == ConfigAction.Set &&
                    // If SettingName is null/whitespace, let the SettingName rule handle it
                    !string.IsNullOrWhiteSpace(x.SettingName) &&
                    !IsInteractiveShortcut(x.SettingName!))
                .WithMessage("SettingValue is required for Set action");

            // ShowSecrets only valid for Show action
            RuleFor(x => x.ShowSecrets)
                .Equal(false)
                .When(x => x.Action != ConfigAction.Show)
                .WithMessage("ShowSecrets is only valid for Show action");
        }

    }

    /// <summary>
    /// Handler for Config command (auto-discovered by MediatR).
    /// Manages individual configuration setting updates.
    /// </summary>
    public sealed class Handler(
        IConfigurationStorageService storageService,
        IOptionsMonitor<ConfigurationSettings> configMonitor,
        IEnumerable<IApiKeyValidator> apiKeyValidators,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<ConfigurationSettings>>
    {
        public async Task<Result<ConfigurationSettings>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Processing config command: {Action} {Setting}",
                    request.Action, request.SettingName ?? "N/A");

                return request.Action switch
                {
                    ConfigAction.Show => await HandleShowAsync(request, cancellationToken),
                    ConfigAction.Set => await HandleSetAsync(request, cancellationToken),
                    ConfigAction.Reset => await HandleResetAsync(cancellationToken),
                    ConfigAction.Validate => await HandleValidateAsync(cancellationToken),
                    _ => Result<ConfigurationSettings>.Failure($"Unknown action '{request.Action}'. Valid actions: show, set, validate, reset. Use 'tom config --help' for more information.")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Config command failed");
                return Result<ConfigurationSettings>.Failure($"Configuration operation failed: {ex.Message}. Check logs for details or try 'tom config --help' for usage information.");
            }
        }

        private Task<Result<ConfigurationSettings>> HandleShowAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Displaying current configuration (ShowSecrets: {ShowSecrets})",
                command.ShowSecrets);

            return GetStoredConfigurationAsync(cancellationToken);
        }

        private async Task<Result<ConfigurationSettings>> GetStoredConfigurationAsync(
            CancellationToken cancellationToken)
        {
            var loadResult = await storageService.LoadAsync(cancellationToken).ConfigureAwait(false);

            if (!loadResult.IsSuccess || loadResult.Value is null)
            {
                var error = loadResult.Error ?? "Configuration could not be loaded. Run 'tom setup' to create it.";
                return Result<ConfigurationSettings>.Failure(error);
            }

            logger.LogInformation("Loaded configuration from storage for display");
            return Result<ConfigurationSettings>.Success(loadResult.Value);
        }

        private async Task<Result<ConfigurationSettings>> HandleSetAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            // Validate command
            if (string.IsNullOrWhiteSpace(command.SettingName))
            {
                return Result<ConfigurationSettings>.Failure("Setting name is required. Example: tom config --set llm-provider OpenAI");
            }

            var normalizedSettingName = command.SettingName.ToLowerInvariant();

            // Interactive subcommands live in their own slices; provide guidance rather than delegating.
            if (IsInteractiveShortcut(normalizedSettingName))
            {
                return Result<ConfigurationSettings>.Failure(GetInteractiveSettingMessage(normalizedSettingName));
            }

            if (string.IsNullOrWhiteSpace(command.SettingValue))
            {
                return Result<ConfigurationSettings>.Failure("Setting value is required. Example: tom config --set llm-provider OpenAI");
            }

            // Load current configuration
            var loadResult = await storageService.LoadAsync(cancellationToken);

            if (!loadResult.IsSuccess)
            {
                return Result<ConfigurationSettings>.Failure("No configuration found. Run 'tom setup' first to create initial configuration, then use 'tom config --set' to update settings.");
            }

            var currentConfig = loadResult.Value!;

            // Update the specified setting
            var updateResult = await UpdateSettingAsync(
                currentConfig,
                normalizedSettingName,
                command.SettingValue,
                cancellationToken);

            if (!updateResult.IsSuccess)
            {
                return updateResult;
            }

            var updatedConfig = updateResult.Value!.MarkAsModified();

            // Save updated configuration
            var saveResult = await storageService.SaveAsync(updatedConfig, cancellationToken).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                return Result<ConfigurationSettings>.Failure($"Failed to save configuration: {saveResult.Error}. Changes were not applied. Try again or check file permissions.");
            }

            logger.LogInformation("Configuration setting '{Setting}' updated successfully", command.SettingName);
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
            var validator = apiKeyValidators.FirstOrDefault(v => v.Provider == currentConfig.Llm.Provider);

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
                    RootDirectory = fullPath
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
            logger.LogInformation("Reset configuration not yet implemented");
            return Task.FromResult(Result<ConfigurationSettings>.Failure("Reset configuration is not yet implemented. To reconfigure, run 'tom setup' to walk through all settings again."));
        }

        private Task<Result<ConfigurationSettings>> HandleValidateAsync(CancellationToken cancellationToken)
        {
            // Get the effective runtime configuration (includes env var overrides)
            // We validate what the app will actually use, not just what's in config.json
            var config = configMonitor.CurrentValue;

            if (!config.IsValid())
            {
                return Task.FromResult(Result<ConfigurationSettings>.Failure(
                    "Configuration validation failed: Required fields are missing or invalid. Run 'tom setup' to reconfigure."));
            }

            logger.LogInformation("Configuration validation passed");
            return Task.FromResult(Result<ConfigurationSettings>.Success(config));
        }
    }

    private static bool IsInteractiveShortcut(string settingName)
        => settingName.Equals("llm", StringComparison.OrdinalIgnoreCase)
           || settingName.Equals("audio", StringComparison.OrdinalIgnoreCase);

    private static string GetInteractiveSettingMessage(string settingName)
    {
        return settingName.Equals("llm", StringComparison.OrdinalIgnoreCase)
            ? "Use 'tom config llm' to configure LLM provider and model interactively."
            : "Use 'tom config audio' to configure audio settings interactively.";
    }
}