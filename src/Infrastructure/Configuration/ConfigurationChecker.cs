using System.IO.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Checks whether Ten Second Tom is configured.
/// Provides self-healing capabilities for missing or corrupted configuration.
/// </summary>
public static class ConfigurationChecker
{
    /// <summary>
    /// Determines if the application has required configuration
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>True if configured, false if setup is needed</returns>
    public static bool IsConfigured(IConfiguration configuration, ILogger logger)
    {
        // Check for required configuration keys using standard TenSecondTom:* namespace
        // Note: Either Ssh:KeyPath OR Ssh:KeySource must be present (agents don't need KeyPath)
        string? sshKeyPath = configuration[ConfigurationKeys.SshKeyPathKey];
        string? sshKeySource = configuration[ConfigurationKeys.SshKeySourceKey];
        string? llmProvider = configuration[ConfigurationKeys.LlmProviderKey];
        string? llmApiKey = configuration[ConfigurationKeys.LlmApiKeyKey];
        string? memoryDirectory = configuration[ConfigurationKeys.MemoryDirectoryKey];

        // SSH is configured if either KeyPath is set OR KeySource is set
        bool hasSshConfiguration = !string.IsNullOrWhiteSpace(sshKeyPath) || 
                                  !string.IsNullOrWhiteSpace(sshKeySource);

        bool isConfigured = hasSshConfiguration &&
                           !string.IsNullOrWhiteSpace(llmProvider) &&
                           !string.IsNullOrWhiteSpace(memoryDirectory) &&
                           !string.IsNullOrWhiteSpace(llmApiKey);

        if (!isConfigured)
        {
            logger.LogInformation("Application is not configured. Setup wizard will be launched.");
            
            if (!hasSshConfiguration)
                logger.LogDebug("Missing: SSH configuration (neither TenSecondTom:Ssh:KeyPath nor TenSecondTom:Ssh:KeySource is set)");
            if (string.IsNullOrWhiteSpace(llmProvider))
                logger.LogDebug("Missing: LLM provider (TenSecondTom:Llm:Provider)");
            if (string.IsNullOrWhiteSpace(memoryDirectory))
                logger.LogDebug("Missing: Memory directory (TenSecondTom:MemoryDirectory)");
            if (string.IsNullOrWhiteSpace(llmApiKey))
                logger.LogDebug("Missing: LLM API key (TenSecondTom:Llm:ApiKey)");
        }

        return isConfigured;
    }

    /// <summary>
    /// Validates that the configured model is valid for the provider
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>True if model is valid or not configured, false if invalid</returns>
    public static bool ValidateModel(IConfiguration configuration, ILogger logger)
    {
        string? provider = configuration[ConfigurationKeys.LlmProviderKey];
        string? model = configuration[ConfigurationKeys.LlmModelKey];
        
        // If no model is configured, validation passes (model is optional in some scenarios)
        if (string.IsNullOrWhiteSpace(model))
        {
            logger.LogDebug("No model configured, validation skipped");
            return true;
        }
        
        // If provider is not configured, we can't validate
        if (string.IsNullOrWhiteSpace(provider))
        {
            logger.LogDebug("No provider configured, model validation skipped");
            return true;
        }
        
        // Parse provider enum
        if (!Enum.TryParse<LlmProvider>(provider, out var llmProvider))
        {
            logger.LogError("Invalid LLM provider configured: {Provider}", provider);
            return false;
        }
        
        // Validate model against registry
        bool isValid = ModelRegistry.IsValid(model, llmProvider);
        
        if (!isValid)
        {
            var validModels = ModelRegistry.GetByProvider(llmProvider);
            var validModelsList = string.Join(", ", validModels.Select(m => $"'{m.Id}'"));
            
            logger.LogError(
                "Invalid model '{Model}' configured for provider {Provider}. Valid models: {ValidModels}",
                model, llmProvider, validModelsList);
            
            return false;
        }
        
        logger.LogDebug("Model validation passed: {Model} is valid for {Provider}", model, llmProvider);
        return true;
    }

    /// <summary>
    /// Gets a user-friendly error message when model validation fails
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <returns>Error message string, or null if validation would pass</returns>
    public static string? GetModelValidationError(IConfiguration configuration)
    {
        string? provider = configuration[ConfigurationKeys.LlmProviderKey];
        string? model = configuration[ConfigurationKeys.LlmModelKey];
        
        // If no model is configured, no error
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }
        
        // If provider is not configured, no error (will be caught by IsConfigured)
        if (string.IsNullOrWhiteSpace(provider))
        {
            return null;
        }
        
        // Parse provider enum
        if (!Enum.TryParse<LlmProvider>(provider, out var llmProvider))
        {
            return $"Invalid LLM provider configured: '{provider}'.";
        }
        
        // Validate model against registry
        bool isValid = ModelRegistry.IsValid(model, llmProvider);
        
        if (!isValid)
        {
            var validModels = ModelRegistry.GetByProvider(llmProvider);
            var validModelsList = string.Join(", ", validModels.Select(m => $"'{m.Id}'"));
            
            return $"Configuration error: Model '{model}' is not valid for provider {llmProvider}.\n" +
                   $"Valid models for {llmProvider}: {validModelsList}\n" +
                   "Run 'tom setup' to reconfigure with a valid model.";
        }

        return null; // Validation passed
    }

    /// <summary>
    /// Performs self-healing checks for templates directory and default templates.
    /// Automatically recreates missing directories and reinstalls default templates if needed.
    /// This method is idempotent and safe to call on every command execution.
    /// </summary>
    /// <param name="configuration">Application configuration</param>
    /// <param name="fileSystem">File system abstraction for testability</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if self-healing was performed, false if no action was needed</returns>
    public static async Task<bool> PerformSelfHealingAsync(
        IConfiguration configuration,
        IFileSystem fileSystem,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);

        cancellationToken.ThrowIfCancellationRequested();

        // Get memory directory using standard .NET configuration
        string memoryDirectory = configuration[ConfigurationKeys.MemoryDirectoryKey] ?? "./.memory";

        string templatesDirectory = fileSystem.Path.Combine(memoryDirectory, "templates");

        bool healingPerformed = false;

        // Check if templates directory exists
        if (!fileSystem.Directory.Exists(templatesDirectory))
        {
            logger.LogWarning(
                "Templates directory not found at {TemplatesDirectory}. Performing self-healing...",
                templatesDirectory);

            try
            {
                // Recreate templates directory
                fileSystem.Directory.CreateDirectory(templatesDirectory);
                logger.LogInformation("Recreated templates directory: {TemplatesDirectory}", templatesDirectory);
                healingPerformed = true;

                // Reinstall default templates
                bool templatesRestored = await RestoreDefaultTemplatesAsync(
                    templatesDirectory,
                    fileSystem,
                    logger,
                    cancellationToken).ConfigureAwait(false);

                if (templatesRestored)
                {
                    logger.LogInformation("Self-healing complete: Templates directory and default templates restored");
                }
                else
                {
                    logger.LogWarning("Self-healing partial: Templates directory created but template restoration encountered issues");
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types - self-healing should be resilient
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Self-healing failed: Unable to recreate templates directory at {TemplatesDirectory}", templatesDirectory);
                // Don't throw - allow app to continue with embedded templates as fallback
            }
        }
        else
        {
            // Directory exists - check if it has templates
            string[] templateFiles = fileSystem.Directory.GetFiles(templatesDirectory, "*.md", SearchOption.TopDirectoryOnly);

            if (templateFiles.Length == 0)
            {
                logger.LogWarning(
                    "Templates directory exists but contains no templates. Restoring defaults...");

                bool templatesRestored = await RestoreDefaultTemplatesAsync(
                    templatesDirectory,
                    fileSystem,
                    logger,
                    cancellationToken).ConfigureAwait(false);

                if (templatesRestored)
                {
                    logger.LogInformation("Self-healing complete: Default templates restored to empty directory");
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
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if restoration was successful, false otherwise</returns>
    private static async Task<bool> RestoreDefaultTemplatesAsync(
        string templatesDirectory,
        IFileSystem fileSystem,
        ILogger logger,
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
                logger.LogInformation(
                    "Restored {Count} default templates to {Directory}",
                    result.Value.TemplatesInstalled,
                    templatesDirectory);
                return true;
            }

            logger.LogWarning(
                "Failed to restore default templates: {Error}",
                result.Error);
            return false;
        }
#pragma warning disable CA1031 // Do not catch general exception types - self-healing should be resilient
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Exception during template restoration");
            return false;
        }
    }
}
