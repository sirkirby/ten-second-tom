using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Features.Setup.Services;

/// <summary>
/// Factory for creating Setup.Command instances with proper existing configuration loading.
/// Centralizes the logic to prevent duplication and bugs from inconsistent implementations.
/// </summary>
public sealed class SetupCommandFactory
{
    private readonly IConfigurationStorageService _storageService;
    private readonly ILogger<SetupCommandFactory> _logger;

    public SetupCommandFactory(
        IConfigurationStorageService storageService,
        ILogger<SetupCommandFactory> logger)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a Setup.Command with existing configuration loaded if available.
    /// This ensures all callers (CLI, Bootstrapper, etc.) consistently preserve existing settings.
    /// </summary>
    /// <param name="force">Force setup to run even if configuration exists.</param>
    /// <param name="nonInteractive">Run setup in non-interactive mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Setup.Command with ExistingConfiguration properly loaded.</returns>
    public async Task<Setup.Command> CreateAsync(
        bool force = false,
        bool nonInteractive = false,
        CancellationToken cancellationToken = default)
    {
        var existingConfig = await LoadExistingConfigurationAsync(cancellationToken);
        
        return new Setup.Command
        {
            Force = force,
            NonInteractive = nonInteractive,
            ExistingConfiguration = existingConfig
        };
    }

    /// <summary>
    /// Loads existing configuration if it exists and contains real (non-default) values.
    /// </summary>
    private async Task<ConfigurationSettings?> LoadExistingConfigurationAsync(CancellationToken cancellationToken)
    {
        var loadResult = await _storageService.LoadAsync(cancellationToken).ConfigureAwait(false);
        
        if (!loadResult.IsSuccess || loadResult.Value == null)
        {
            _logger.LogDebug("No existing configuration found");
            return null;
        }

        var config = loadResult.Value;
        
        // Check if it's a real config (not just defaults from a missing file)
        // We check for ANY non-default values to preserve (SSH, LLM, or Audio config)
        bool hasRealConfig = !string.IsNullOrEmpty(config.Ssh.KeyPath) ||
                            !string.IsNullOrEmpty(config.Ssh.AgentSocketPath) ||
                            !string.IsNullOrEmpty(config.Llm.ApiKey) ||
                            !string.IsNullOrEmpty(config.Audio.SttApiKey) ||
                            config.Audio.SttProvider != "whisper-cpp"; // Non-default STT provider

        if (hasRealConfig)
        {
            _logger.LogDebug("Loaded existing configuration for reconfiguration (valid: {IsValid})", 
                config.IsValid());
            return config;
        }

        _logger.LogDebug("Config file contains only defaults, treating as first-time setup");
        return null;
    }
}

