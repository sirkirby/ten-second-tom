using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Represents the complete application configuration
/// Centralized settings providing single source of truth
/// </summary>
public sealed record ConfigurationSettings
{
    /// <summary>
    /// Gets the SSH authentication configuration
    /// </summary>
    public SshConfiguration Ssh { get; init; } = new();

    /// <summary>
    /// Gets the LLM provider configuration
    /// </summary>
    public LlmConfiguration Llm { get; init; } = new();

    /// <summary>
    /// Gets the storage configuration
    /// </summary>
    public StorageConfiguration Storage { get; init; } = new() 
    { 
        MemoryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DirectoryNames.ApplicationRoot)
    };

    /// <summary>
    /// Gets the optional configuration settings
    /// </summary>
    public OptionalConfiguration Optional { get; init; } = new();

    /// <summary>
    /// Gets the timestamp when configuration was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the timestamp when configuration was last modified
    /// </summary>
    public DateTime? LastModifiedAt { get; init; }

    /// <summary>
    /// Gets the configuration schema version
    /// </summary>
    public string ConfigurationVersion { get; init; } = "1.0";

    /// <summary>
    /// Validates that required configuration is present
    /// </summary>
    public bool IsValid()
    {
        // SSH configuration must have key path or agent
        if (string.IsNullOrEmpty(Ssh.KeyPath) && string.IsNullOrEmpty(Ssh.AgentSocketPath))
            return false;

        // LLM provider must be set
        if (!Enum.IsDefined(Llm.Provider))
            return false;

        // API key must be set
        if (string.IsNullOrEmpty(Llm.ApiKey))
            return false;

        // Memory directory must be set
        if (string.IsNullOrEmpty(Storage.MemoryDirectory))
            return false;

        // Retention days must be positive
        if (Optional.RetentionDays <= 0)
            return false;

        return true;
    }

    /// <summary>
    /// Marks the configuration as modified
    /// </summary>
    public ConfigurationSettings MarkAsModified() => this with
    {
        LastModifiedAt = DateTime.UtcNow
    };
}

/// <summary>
/// SSH authentication configuration
/// </summary>
public sealed record SshConfiguration
{
    /// <summary>
    /// Gets the path to the SSH key file (null for agent-based keys)
    /// </summary>
    public string? KeyPath { get; init; }

    /// <summary>
    /// Gets the source of the SSH key
    /// </summary>
    public SshKeySource? KeySource { get; init; }

    /// <summary>
    /// Gets the path to the SSH agent socket
    /// </summary>
    public string? AgentSocketPath { get; init; }

    /// <summary>
    /// Gets a human-readable identifier for the SSH key (e.g., "id_ed25519 (1Password)")
    /// </summary>
    public string? KeyDisplayName { get; init; }
}

/// <summary>
/// LLM provider configuration
/// </summary>
public sealed record LlmConfiguration
{
    /// <summary>
    /// Gets the selected LLM provider
    /// </summary>
    public LlmProvider Provider { get; init; }

    /// <summary>
    /// Gets the API key for the provider
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Gets the model to use
    /// </summary>
    public string? Model { get; init; }
}

/// <summary>
/// Storage configuration
/// </summary>
public sealed record StorageConfiguration
{
    /// <summary>
    /// Gets the directory for storing memories
    /// </summary>
    public required string MemoryDirectory { get; init; }

    /// <summary>
    /// Gets whether to create the directory if it doesn't exist
    /// </summary>
    public bool CreateIfMissing { get; init; } = true;
}

/// <summary>
/// Optional configuration settings
/// </summary>
public sealed record OptionalConfiguration
{
    /// <summary>
    /// Gets the logging level
    /// </summary>
    public Microsoft.Extensions.Logging.LogLevel LogLevel { get; init; } = Microsoft.Extensions.Logging.LogLevel.Information;

    /// <summary>
    /// Gets the number of days to retain memories
    /// </summary>
    public int RetentionDays { get; init; } = 30;

    /// <summary>
    /// Gets whether telemetry is enabled
    /// </summary>
    public bool EnableTelemetry { get; init; } = false;
}
