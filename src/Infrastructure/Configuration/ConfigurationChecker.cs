using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Checks whether Ten Second Tom is configured.
/// Provides self-healing capabilities for missing or corrupted configuration.
/// </summary>
public sealed class ConfigurationChecker
{
    private readonly LlmOptions? _llmOptions;
    private readonly AuthOptions? _authOptions;
    private readonly StorageOptions? _storageOptions;
    private readonly ILogger<ConfigurationChecker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationChecker"/> class.
    /// </summary>
    /// <param name="llmOptions">LLM configuration options.</param>
    /// <param name="authOptions">Authentication configuration options.</param>
    /// <param name="storageOptions">Storage configuration options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    public ConfigurationChecker(
        IOptions<LlmOptions>? llmOptions,
        IOptions<AuthOptions>? authOptions,
        IOptions<StorageOptions>? storageOptions,
        ILogger<ConfigurationChecker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Options may be null during initial setup, so we handle this gracefully
        try
        {
            _llmOptions = llmOptions?.Value;
        }
        catch (OptionsValidationException)
        {
            // Options are not yet configured or validation failed
            _llmOptions = null;
        }

        try
        {
            _authOptions = authOptions?.Value;
        }
        catch (OptionsValidationException)
        {
            // Options are not yet configured or validation failed
            _authOptions = null;
        }

        try
        {
            _storageOptions = storageOptions?.Value;
        }
        catch (OptionsValidationException)
        {
            // Options are not yet configured or validation failed
            _storageOptions = null;
        }
    }

    /// <summary>
    /// Determines if the application has required configuration
    /// </summary>
    /// <returns>True if configured, false if setup is needed</returns>
    public bool IsConfigured()
    {
        // Check for required configuration using Options Pattern
        // Note: Either Ssh:KeyPath OR Ssh:KeySource must be present (agents don't need KeyPath)
        bool hasSshKeyPath = !string.IsNullOrWhiteSpace(_authOptions?.KeyPath);
        bool hasSshKeySource = _authOptions?.KeySource != null;

        bool hasSshConfiguration = hasSshKeyPath || hasSshKeySource;

        bool hasLlmProvider = _llmOptions?.Provider != null;
        bool hasLlmApiKey = !string.IsNullOrWhiteSpace(_llmOptions?.ApiKey);
        bool hasMemoryDirectory = !string.IsNullOrWhiteSpace(_storageOptions?.MemoryDirectory);

        bool isConfigured = hasSshConfiguration &&
                           hasLlmProvider &&
                           hasMemoryDirectory &&
                           hasLlmApiKey;

        if (!isConfigured)
        {
            _logger.LogInformation("Application is not configured. Setup wizard will be launched.");

            if (!hasSshConfiguration)
                _logger.LogDebug("Missing: SSH configuration (neither TenSecondTom:Ssh:KeyPath nor TenSecondTom:Ssh:KeySource is set)");
            if (!hasLlmProvider)
                _logger.LogDebug("Missing: LLM provider (TenSecondTom:Llm:Provider)");
            if (!hasMemoryDirectory)
                _logger.LogDebug("Missing: Memory directory (TenSecondTom:MemoryDirectory)");
            if (!hasLlmApiKey)
                _logger.LogDebug("Missing: LLM API key (TenSecondTom:Llm:ApiKey)");
        }

        return isConfigured;
    }

    /// <summary>
    /// Validates that the configured model is valid for the provider
    /// </summary>
    /// <returns>True if model is valid or not configured, false if invalid</returns>
    public bool ValidateModel()
    {
        var provider = _llmOptions?.Provider;
        var model = _llmOptions?.Model;

        // If no model is configured, validation passes (model is optional in some scenarios)
        if (string.IsNullOrWhiteSpace(model))
        {
            _logger.LogDebug("No model configured, validation skipped");
            return true;
        }

        // If provider is not configured, we can't validate
        if (provider == null)
        {
            _logger.LogDebug("No provider configured, model validation skipped");
            return true;
        }

        // Validate model against registry
        bool isValid = ModelRegistry.IsValid(model, provider.Value);

        if (!isValid)
        {
            var validModels = ModelRegistry.GetByProvider(provider.Value);
            var validModelsList = string.Join(", ", validModels.Select(m => $"'{m.Id}'"));

            _logger.LogError(
                "Invalid model '{Model}' configured for provider {Provider}. Valid models: {ValidModels}",
                model, provider, validModelsList);

            return false;
        }

        _logger.LogDebug("Model validation passed: {Model} is valid for {Provider}", model, provider);
        return true;
    }

    /// <summary>
    /// Gets a user-friendly error message when model validation fails
    /// </summary>
    /// <returns>Error message string, or null if validation would pass</returns>
    public string? GetModelValidationError()
    {
        var provider = _llmOptions?.Provider;
        var model = _llmOptions?.Model;

        // If no model is configured, no error
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        // If provider is not configured, no error (will be caught by IsConfigured)
        if (provider == null)
        {
            return null;
        }

        // Validate model against registry
        bool isValid = ModelRegistry.IsValid(model, provider.Value);

        if (!isValid)
        {
            var validModels = ModelRegistry.GetByProvider(provider.Value);
            var validModelsList = string.Join(", ", validModels.Select(m => $"'{m.Id}'"));

            return $"Configuration error: Model '{model}' is not valid for provider {provider}.\n" +
                   $"Valid models for {provider}: {validModelsList}\n" +
                   "Run 'tom setup' to reconfigure with a valid model.";
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Performs self-healing checks for templates directory and default templates.
    /// Automatically recreates missing directories and reinstalls default templates if needed.
    /// This method is idempotent and safe to call on every command execution.
    /// </summary>
    /// <param name="fileSystem">File system abstraction for testability</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if self-healing was performed, false if no action was needed</returns>
    public async Task<bool> PerformSelfHealingAsync(
        IFileSystem fileSystem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        cancellationToken.ThrowIfCancellationRequested();

        // Get memory directory from options
        string memoryDirectory = _storageOptions?.MemoryDirectory ?? "./.memory";

        string templatesDirectory = fileSystem.Path.Combine(memoryDirectory, "templates");

        bool healingPerformed = false;

        // Check if templates directory exists
        if (!fileSystem.Directory.Exists(templatesDirectory))
        {
            _logger.LogWarning(
                "Templates directory not found at {TemplatesDirectory}. Performing self-healing...",
                templatesDirectory);

            try
            {
                // Recreate templates directory
                fileSystem.Directory.CreateDirectory(templatesDirectory);
                _logger.LogInformation("Recreated templates directory: {TemplatesDirectory}", templatesDirectory);
                healingPerformed = true;

                // Reinstall default templates
                bool templatesRestored = await RestoreDefaultTemplatesAsync(
                    templatesDirectory,
                    fileSystem,
                    cancellationToken).ConfigureAwait(false);

                if (templatesRestored)
                {
                    _logger.LogInformation("Self-healing complete: Templates directory and default templates restored");
                }
                else
                {
                    _logger.LogWarning("Self-healing partial: Templates directory created but template restoration encountered issues");
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - self-healing should be resilient
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogError(ex, "Self-healing failed: Unable to recreate templates directory at {TemplatesDirectory}", templatesDirectory);
                // Don't throw - allow app to continue with embedded templates as fallback
            }
        }
        else
        {
            // Directory exists - check if it has templates
            string[] templateFiles = fileSystem.Directory.GetFiles(templatesDirectory, "*.md", SearchOption.TopDirectoryOnly);

            if (templateFiles.Length == 0)
            {
                _logger.LogWarning(
                    "Templates directory exists but contains no templates. Restoring defaults...");

                bool templatesRestored = await RestoreDefaultTemplatesAsync(
                    templatesDirectory,
                    fileSystem,
                    cancellationToken).ConfigureAwait(false);

                if (templatesRestored)
                {
                    _logger.LogInformation("Self-healing complete: Default templates restored to empty directory");
                    healingPerformed = true;
                }
            }
        }

        return healingPerformed;
    }

    /// <summary>
    /// Restores default templates to the specified directory.
    /// </summary>
    /// <param name="templatesDirectory">Target directory for templates</param>
    /// <param name="fileSystem">File system abstraction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if restoration was successful, false otherwise</returns>
    private async Task<bool> RestoreDefaultTemplatesAsync(
        string templatesDirectory,
        IFileSystem fileSystem,
        CancellationToken cancellationToken)
    {
        try
        {
            // Create a temporary logger for the handler that logs through the main logger
            using var loggerFactory = LoggerFactory.Create(builder => { });
            var handlerLogger = loggerFactory.CreateLogger<InstallDefaultTemplatesHandler>();

            var handler = new InstallDefaultTemplatesHandler(fileSystem, handlerLogger);

            var command = new InstallDefaultTemplatesCommand
            {
                TargetDirectory = templatesDirectory,
                OverwriteExisting = false // Don't overwrite any existing customizations
            };

            var result = await handler.Handle(command, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Restored {Count} default templates to {Directory}",
                    result.Value.TemplatesInstalled,
                    templatesDirectory);
                return true;
            }

            _logger.LogWarning(
                "Failed to restore default templates: {Error}",
                result.Error);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types - self-healing should be resilient
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Exception during template restoration");
            return false;
        }
    }
}
