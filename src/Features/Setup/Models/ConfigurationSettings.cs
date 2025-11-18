using System.Text.Json;
using System.Text.Json.Serialization;
using TenSecondTom.Features.Audio.Constants;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Setup.Models;

/// <summary>
/// Represents the complete application configuration
/// Centralized settings providing single source of truth
/// </summary>
public sealed record ConfigurationSettings
{
    /// <summary>
    /// Gets or sets the application root directory where config, memories, and other app data is stored.
    /// This is the base directory for all application data, not storage-specific.
    /// Future storage providers (database, cloud, etc.) will still use this for local config.
    /// </summary>
    /// <remarks>
    /// Default: ~/ten-second-tom
    /// Can be overridden via:
    /// - Environment variable: TenSecondTom__RootDirectory
    /// - config.json: TenSecondTom:RootDirectory
    /// </remarks>
    public string RootDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        DirectoryNames.ApplicationRoot);

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
    public StorageConfiguration Storage { get; init; } = new();

    /// <summary>
    /// Gets the optional configuration settings
    /// </summary>
    public OptionalConfiguration Optional { get; init; } = new();

    /// <summary>
    /// Gets the audio recording and preprocessing configuration
    /// </summary>
    public AudioConfigurationDisplay Audio { get; init; } = new();

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
        if (!Enum.IsDefined<LlmProvider>(Llm.Provider))
            return false;

        // API key must be set
        if (string.IsNullOrEmpty(Llm.ApiKey))
            return false;

        // Root directory must be set
        if (string.IsNullOrEmpty(RootDirectory))
            return false;

        // Retention days must be positive or -1 (unlimited)
        if (Optional.RetentionDays <= 0 && Optional.RetentionDays != -1)
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
    /// Gets the model to use for chat/text generation
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Gets the maximum number of input tokens to send to the LLM.
    /// If null, uses provider-specific defaults (50K for OpenAI, 80K for Anthropic).
    /// This limit helps control costs and ensures inputs fit within context windows.
    /// </summary>
    public int? MaxInputTokens { get; init; }
}

/// <summary>
/// Storage configuration.
/// Defines storage provider selection and settings.
/// The RootDirectory (where config and templates live) is at ConfigurationSettings root level.
/// </summary>
public sealed record StorageConfiguration
{
    /// <summary>
    /// Gets the storage provider ID (e.g., "default", "obsidian").
    /// Default: "default"
    /// </summary>
    public string ProviderId { get; init; } = StorageProviderIds.Default;

    /// <summary>
    /// Gets the provider-specific storage path (e.g., Obsidian vault path).
    /// For default provider: not used (uses RootDirectory for both config and storage)
    /// For Obsidian provider: the vault root path where memory entries are stored
    /// Default: null (use RootDirectory)
    /// </summary>
    public string? ProviderPath { get; init; }

    /// <summary>
    /// Gets the optional subdirectory name for memory entries within the provider path.
    /// When set, memory entries are stored under {ProviderPath}/{MemorySubdirectory}/
    /// Default: null (store directly in provider path root)
    /// Primarily used by Obsidian provider to isolate TST entries in a vault subdirectory.
    /// </summary>
    public string? MemorySubdirectory { get; init; }

    /// <summary>
    /// Gets whether to create directories if they don't exist
    /// </summary>
    public bool CreateIfMissing { get; init; } = true;

    /// <summary>
    /// Gets the retention policy for memory entries.
    /// Default: Indefinite (never auto-purge)
    /// </summary>
    public Shared.Models.RetentionPolicy RetentionPolicy { get; init; } = Shared.Models.RetentionPolicy.Indefinite;

    /// <summary>
    /// Gets whether to automatically purge entries based on retention policy.
    /// Default: false (manual purging only)
    /// </summary>
    public bool AutoPurge { get; init; }

    /// <summary>
    /// Gets the optional maximum file size in bytes for memory entries.
    /// If null, no size limit is enforced.
    /// </summary>
    public long? MaxFileSizeBytes { get; init; }

    /// <summary>
    /// Gets whether to compress memory entries.
    /// Default: false (no compression)
    /// </summary>
    public bool CompressionEnabled { get; init; }
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

/// <summary>
/// Audio recording and preprocessing configuration (display model for config show)
/// </summary>
public sealed record AudioConfigurationDisplay
{
    /// <summary>
    /// Gets the speech-to-text provider
    /// </summary>
    public string SttProvider { get; init; } = SttProviders.WhisperCpp;

    /// <summary>
    /// Gets the API key for the STT provider (masked for display)
    /// </summary>
    public string? SttApiKey { get; init; }

    /// <summary>
    /// Gets whether fallback to a secondary STT provider is enabled
    /// </summary>
    public bool SttFallbackEnabled { get; init; }

    /// <summary>
    /// Gets the fallback STT provider (e.g., openai)
    /// </summary>
    public string? SttFallbackProvider { get; init; }

    /// <summary>
    /// Gets the API key for the fallback STT provider (masked for display)
    /// </summary>
    public string? SttFallbackApiKey { get; init; }

    /// <summary>
    /// Gets whether to keep audio files after transcription
    /// </summary>
    public bool KeepFiles { get; init; } = true;

    /// <summary>
    /// Gets the audio recorder configuration
    /// </summary>
    public RecorderConfigurationDisplay Recorder { get; init; } = new();

    /// <summary>
    /// Gets the audio preprocessing configuration
    /// </summary>
    public PreprocessingConfigurationDisplay Preprocessing { get; init; } = new();
}

/// <summary>
/// Audio recorder configuration (display model for config show)
/// </summary>
public sealed record RecorderConfigurationDisplay
{
    /// <summary>
    /// Gets the input volume multiplier (0.0 to 2.0)
    /// </summary>
    public double InputVolume { get; init; } = 1.0;

    /// <summary>
    /// Gets whether noise reduction is enabled during recording
    /// </summary>
    public bool EnableNoiseReduction { get; init; } = true;

    /// <summary>
    /// Gets whether frequency filters are enabled during recording
    /// </summary>
    public bool EnableFrequencyFilters { get; init; } = true;
}

/// <summary>
/// Audio preprocessing configuration (display model for config show)
/// </summary>
public sealed record PreprocessingConfigurationDisplay
{
    /// <summary>
    /// Gets whether silence removal is enabled
    /// </summary>
    public bool RemoveSilence { get; init; } = true;

    /// <summary>
    /// Gets the silence detection threshold in decibels
    /// </summary>
    public int SilenceThresholdDb { get; init; } = -50;

    /// <summary>
    /// Gets the minimum silence duration to remove in milliseconds
    /// </summary>
    public int MinimumSilenceDurationMs { get; init; } = 500;
}

/// <summary>
/// Root configuration file wrapper (config.json).
/// Preserves non-TenSecondTom sections (e.g., Serilog) when saving/loading configuration.
/// The TenSecondTom section is stored as a raw JsonElement and mapped to/from ConfigurationSettings.
/// </summary>
public sealed class ConfigurationRoot
{
    /// <summary>
    /// The TenSecondTom configuration section as raw JSON.
    /// Mapped to/from ConfigurationSettings by ConfigurationStorageService.
    /// </summary>
    [JsonPropertyName("TenSecondTom")]
    public JsonElement TenSecondTom { get; set; }

    /// <summary>
    /// Extension data to preserve other configuration sections (like Serilog, Logging, etc.)
    /// when roundtripping configuration through save/load cycles.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
