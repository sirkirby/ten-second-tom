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
        // Check for required configuration keys
        string? sshKeyPath = configuration["TenSecondTom:Auth:PublicKeyPath"];
        string? llmProvider = configuration["TenSecondTom:LlmProvider"];
        string? memoryDirectory = configuration["TenSecondTom:MemoryDirectory"];
        
        // Check for API keys based on provider
        string? apiKey = llmProvider?.ToLowerInvariant() switch
        {
            "openai" => configuration["OPENAI_API_KEY"] ?? 
                       Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
            "anthropic" => configuration["ANTHROPIC_API_KEY"] ?? 
                          Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"),
            _ => null
        };

        bool isConfigured = !string.IsNullOrWhiteSpace(sshKeyPath) &&
                           !string.IsNullOrWhiteSpace(llmProvider) &&
                           !string.IsNullOrWhiteSpace(memoryDirectory) &&
                           !string.IsNullOrWhiteSpace(apiKey);

        if (!isConfigured)
        {
            logger.LogInformation("Application is not configured. Setup wizard will be launched.");
            
            if (string.IsNullOrWhiteSpace(sshKeyPath))
                logger.LogDebug("Missing: SSH key path");
            if (string.IsNullOrWhiteSpace(llmProvider))
                logger.LogDebug("Missing: LLM provider");
            if (string.IsNullOrWhiteSpace(memoryDirectory))
                logger.LogDebug("Missing: Memory directory");
            if (string.IsNullOrWhiteSpace(apiKey))
                logger.LogDebug("Missing: API key for provider {Provider}", llmProvider ?? "unknown");
        }

        return isConfigured;
    }
}
