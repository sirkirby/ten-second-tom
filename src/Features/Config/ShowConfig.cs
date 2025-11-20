using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Config;

/// <summary>
/// Views or modifies individual configuration settings.
/// Hybrid command/query for configuration management.
/// </summary>
public static class ShowConfig
{
    /// <summary>
    /// Command to view or modify individual configuration settings.
    /// </summary>
    public sealed record Command : IRequest<Result<ConfigDisplay>>
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
    /// Validator for ShowConfig command (auto-discovered by FluentValidation).
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
    /// Handler for ShowConfig command (auto-discovered by MediatR).
    /// Manages individual configuration setting updates.
    /// </summary>
    public sealed class Handler(
        IConfigurationSectionStore sectionStore,
        IOptions<AuthOptions> authOptions,
        IOptions<LlmOptions> llmOptions,
        IOptions<StorageOptions> storageOptions,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<ConfigDisplay>>
    {
        public async Task<Result<ConfigDisplay>> Handle(
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
                    ConfigAction.Set => Result<ConfigDisplay>.Failure("Config set is not yet implemented for the new structure. Use 'tom config llm' or 'tom config audio' for interactive configuration."),
                    ConfigAction.Reset => Result<ConfigDisplay>.Failure("Config reset is not yet implemented. Run 'tom setup' to reconfigure."),
                    ConfigAction.Validate => HandleValidate(),
                    _ => Result<ConfigDisplay>.Failure($"Unknown action '{request.Action}'. Valid actions: show, set, validate, reset. Use 'tom config --help' for more information.")
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Config command failed");
                return Result<ConfigDisplay>.Failure($"Configuration operation failed: {ex.Message}. Check logs for details or try 'tom config --help' for usage information.");
            }
        }

        private async Task<Result<ConfigDisplay>> HandleShowAsync(
            Command command,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Displaying current configuration (ShowSecrets: {ShowSecrets})",
                command.ShowSecrets);

            return await BuildConfigDisplayAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<Result<ConfigDisplay>> BuildConfigDisplayAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                // Read SSH config from Auth section (feature ownership)
                var sshResult = await sectionStore.ReadSectionAsync<AuthOptions>(
                    AuthOptions.SectionName,
                    cancellationToken).ConfigureAwait(false);

                // Read LLM config from Llm section (feature ownership)
                var llmResult = await sectionStore.ReadSectionAsync<LlmOptions>(
                    LlmOptions.SectionPath,
                    cancellationToken).ConfigureAwait(false);

                // Read Storage config
                var storageResult = await sectionStore.ReadSectionAsync<StorageSettings>(
                    "TenSecondTom:Storage",
                    cancellationToken).ConfigureAwait(false);

                // Read Optional config
                var optionalResult = await sectionStore.ReadSectionAsync<OptionalConfiguration>(
                    "TenSecondTom:Optional",
                    cancellationToken).ConfigureAwait(false);

                // Read Audio config
                var audioResult = await sectionStore.ReadSectionAsync<AudioConfigurationDisplay>(
                    "TenSecondTom:Audio",
                    cancellationToken).ConfigureAwait(false);

                // Read Configuration metadata
                var metadataResult = await sectionStore.ReadSectionAsync<ConfigurationMetadata>(
                    "TenSecondTom:Configuration",
                    cancellationToken).ConfigureAwait(false);

                // Build display model
                var display = new ConfigDisplay
                {
                    RootDirectory = configuration[ConfigurationKeys.RootDirectoryKey]
                        ?? storageOptions.Value.RootDirectory ?? string.Empty,
                    Ssh = new SshConfiguration
                    {
                        KeyPath = sshResult.Value?.KeyPath ?? string.Empty,
                        KeySource = sshResult.Value?.KeySource ?? SshKeySource.FileSystem,
                        AgentSocketPath = sshResult.Value?.AgentSocketPath ?? string.Empty,
                        KeyDisplayName = sshResult.Value?.KeyDisplayName ?? string.Empty
                    },
                    Llm = new LlmConfiguration
                    {
                        Provider = llmResult.Value?.Provider ?? LlmProvider.OpenAI,
                        ApiKey = llmResult.Value?.ApiKey ?? string.Empty,
                        Model = llmResult.Value?.Model ?? string.Empty,
                        MaxInputTokens = llmResult.Value?.MaxInputTokens ?? 0
                    },
                    Storage = storageResult.Value ?? new StorageSettings(),
                    Optional = optionalResult.Value ?? new OptionalConfiguration(),
                    Audio = audioResult.Value ?? new AudioConfigurationDisplay(),
                    CreatedAt = metadataResult.Value?.CreatedAt ?? DateTime.UtcNow,
                    LastModifiedAt = metadataResult.Value?.LastModifiedAt,
                    ConfigurationVersion = metadataResult.Value?.Version ?? "1.0"
                };

                logger.LogInformation("Built configuration display from feature-owned sections");
                return Result<ConfigDisplay>.Success(display);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to build configuration display");
                return Result<ConfigDisplay>.Failure($"Failed to load configuration: {ex.Message}");
            }
        }

        private Result<ConfigDisplay> HandleValidate()
        {
            // Validate using the Options Pattern values
            bool hasValidSsh = !string.IsNullOrWhiteSpace(authOptions.Value.KeyPath)
                || authOptions.Value.KeySource != default;
            bool hasValidLlm = !string.IsNullOrWhiteSpace(llmOptions.Value.ApiKey)
                && !string.IsNullOrWhiteSpace(llmOptions.Value.Model);
            bool hasValidStorage = !string.IsNullOrWhiteSpace(storageOptions.Value.RootDirectory);

            if (!hasValidSsh || !hasValidLlm || !hasValidStorage)
            {
                return Result<ConfigDisplay>.Failure(
                    "Configuration validation failed: Required fields are missing. Run 'tom setup' to reconfigure.");
            }

            logger.LogInformation("Configuration validation passed");
            // Return a minimal success display
            return Result<ConfigDisplay>.Success(new ConfigDisplay
            {
                RootDirectory = storageOptions.Value.RootDirectory ?? string.Empty
            });
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
