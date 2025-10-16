using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Checks whether Ten Second Tom is configured
/// Used to trigger first-run setup wizard
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
        // Check for required configuration keys (matching UserSecretsStorageService format)
        // Keys are stored as: Ssh:KeyPath, Llm:Provider, Llm:ApiKey, Storage:MemoryDirectory
        // Note: Either Ssh:KeyPath OR Ssh:KeySource must be present (agents don't need KeyPath)
        string? sshKeyPath = configuration["Ssh:KeyPath"];
        string? sshKeySource = configuration["Ssh:KeySource"];
        string? llmProvider = configuration["Llm:Provider"];
        string? llmApiKey = configuration["Llm:ApiKey"];
        string? memoryDirectory = configuration["Storage:MemoryDirectory"];

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
                logger.LogDebug("Missing: SSH configuration (neither Ssh:KeyPath nor Ssh:KeySource is set)");
            if (string.IsNullOrWhiteSpace(llmProvider))
                logger.LogDebug("Missing: LLM provider (Llm:Provider)");
            if (string.IsNullOrWhiteSpace(memoryDirectory))
                logger.LogDebug("Missing: Memory directory (Storage:MemoryDirectory)");
            if (string.IsNullOrWhiteSpace(llmApiKey))
                logger.LogDebug("Missing: LLM API key (Llm:ApiKey)");
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
        string? provider = configuration["Llm:Provider"];
        string? model = configuration["Llm:Model"];
        
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
        string? provider = configuration["Llm:Provider"];
        string? model = configuration["Llm:Model"];
        
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
}
