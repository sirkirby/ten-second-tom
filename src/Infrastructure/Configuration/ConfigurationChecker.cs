using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
}
