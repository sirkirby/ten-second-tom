using System.Text.Json;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Unified configuration storage service that manages all application configuration in appsettings.json.
/// Provides atomic updates with proper error handling and file locking.
/// Environment variables can override any configuration value via .env file or exported environment variables.
/// </summary>
public sealed class ConfigurationStorageService : IConfigurationStorageService, IAppSettingsStorageService, IDisposable
{
    private readonly ILogger<ConfigurationStorageService> _logger;
    private readonly string _appSettingsPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a new instance of ConfigurationStorageService.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configuration">Configuration to read the Memory/app root directory</param>
    /// <param name="appSettingsPath">Optional override path. If null, uses {MemoryDirectory}/config/config.json</param>
    public ConfigurationStorageService(
        ILogger<ConfigurationStorageService> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string? appSettingsPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appSettingsPath = appSettingsPath ?? GetUserConfigPath(configuration);
    }

    /// <summary>
    /// Gets the user configuration file path within the app root (Memory) directory.
    /// This ensures all user data lives under one root, separate from the binary location.
    /// </summary>
    /// <param name="configuration">Configuration to read Memory directory setting</param>
    /// <returns>Path to user configuration file (e.g., ~/ten-second-tom/config/config.json)</returns>
    private static string GetUserConfigPath(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        // Get the Memory directory (app root) from configuration
        // Falls back to ~/ten-second-tom if not configured
        var memoryDir = configuration[ConfigurationKeys.MemoryDirectory];

        if (string.IsNullOrWhiteSpace(memoryDir))
        {
            // First run: Use default location
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            memoryDir = Path.Combine(home, "ten-second-tom");
        }
        else if (memoryDir.StartsWith("~/", StringComparison.Ordinal))
        {
            // Expand ~ to home directory
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            memoryDir = Path.Combine(home, memoryDir[2..]);
        }

        // User config lives in {MemoryDirectory}/config/config.json (not appsettings.json)
        var configDir = Path.Combine(memoryDir, "config");

        // Ensure directory exists
        Directory.CreateDirectory(configDir);

        return Path.Combine(configDir, "config.json");
    }

    public async Task<Result<string>> SaveAsync(
        ConfigurationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Saving configuration to {Path}", _appSettingsPath);

            // Load existing JSON or create new
            JsonDocument? existingDoc = null;
            if (File.Exists(_appSettingsPath))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
                    existingDoc = JsonDocument.Parse(existingJson);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Existing appsettings.json is invalid, creating new file");
                }
            }

            // Build new JSON structure
            var root = new Dictionary<string, object>();

            // Preserve all existing sections except TenSecondTom
            if (existingDoc != null)
            {
                foreach (var property in existingDoc.RootElement.EnumerateObject())
                {
                    if (property.Name != ConfigurationKeys.Root)
                    {
                        // Preserve non-TenSecondTom sections (e.g., Serilog)
                        root[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText(), JsonOptions)
                            ?? new object();
                    }
                }
            }

            // Build TenSecondTom configuration section
            // Use AudioConfiguration defaults to get audio settings
            var audioDefaults = new AudioConfiguration();

            root[ConfigurationKeys.Root] = new
            {
                MemoryDirectory = settings.Storage.MemoryDirectory,
                Auth = new
                {
                    SshAgentProvider = SshConstants.DefaultAgentProvider,
                    PublicKeyPath = settings.Ssh.KeyPath
                },
                Ssh = new
                {
                    KeyPath = settings.Ssh.KeyPath,
                    KeySource = settings.Ssh.KeySource?.ToString(),
                    AgentSocketPath = settings.Ssh.AgentSocketPath,
                    KeyDisplayName = settings.Ssh.KeyDisplayName
                },
                Llm = new
                {
                    Provider = settings.Llm.Provider.ToString(),
                    ApiKey = settings.Llm.ApiKey,
                    Model = settings.Llm.Model,
                    MaxInputTokens = settings.Llm.MaxInputTokens
                },
                Audio = new
                {
                    SttProvider = audioDefaults.SttProvider,
                    SttBinaryPath = audioDefaults.SttBinaryPath,
                    SttModel = audioDefaults.SttModel,
                    SttApiKey = audioDefaults.SttApiKey,
                    SttFallbackEnabled = audioDefaults.SttFallbackEnabled,
                    SttFallbackProvider = audioDefaults.SttFallbackProvider,
                    SttFallbackBinaryPath = audioDefaults.SttFallbackBinaryPath,
                    SttFallbackModel = audioDefaults.SttFallbackModel,
                    SttFallbackApiKey = audioDefaults.SttFallbackApiKey,
                    KeepFiles = audioDefaults.KeepFiles,
                    Recorder = new
                    {
                        FfmpegPath = audioDefaults.Recorder.FfmpegPath,
                        InputVolume = audioDefaults.Recorder.InputVolume,
                        EnableNoiseReduction = audioDefaults.Recorder.EnableNoiseReduction,
                        EnableFrequencyFilters = audioDefaults.Recorder.EnableFrequencyFilters
                    },
                    Preprocessing = new
                    {
                        RemoveSilence = audioDefaults.Preprocessing.RemoveSilence,
                        SilenceThresholdDb = audioDefaults.Preprocessing.SilenceThresholdDb,
                        MinimumSilenceDurationMs = audioDefaults.Preprocessing.MinimumSilenceDurationMs
                    },
                    Timeouts = new
                    {
                        TodaySeconds = audioDefaults.Timeouts.TodaySeconds,
                        RecordSeconds = audioDefaults.Timeouts.RecordSeconds
                    }
                },
                DataRetention = new
                {
                    DefaultPolicy = DataRetentionConstants.DefaultPolicy,
                    AutoPurgeEnabled = DataRetentionConstants.DefaultAutoPurgeEnabled
                },
                Setup = new
                {
                    SshKeyDetectionTimeoutSeconds = SetupWizardConstants.Timeouts.SshKeyDetectionSeconds,
                    ApiValidationTimeoutSeconds = SetupWizardConstants.Timeouts.ApiValidationSeconds,
                    TotalSetupTimeoutSeconds = SetupWizardConstants.Timeouts.TotalSetupSeconds
                },
                Optional = new
                {
                    LogLevel = settings.Optional.LogLevel.ToString(),
                    RetentionDays = settings.Optional.RetentionDays,
                    EnableTelemetry = settings.Optional.EnableTelemetry
                },
                Configuration = new
                {
                    CreatedAt = settings.CreatedAt.ToString("O"),
                    LastModifiedAt = settings.LastModifiedAt?.ToString("O"),
                    Version = settings.ConfigurationVersion
                }
            };

            // Write atomically (temp file + rename)
            var tempPath = _appSettingsPath + ".tmp";
            var json = JsonSerializer.Serialize(root, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            // Atomic replace
            File.Move(tempPath, _appSettingsPath, overwrite: true);

            _logger.LogInformation("Configuration saved successfully to {Path}", _appSettingsPath);
            return Result<string>.Success(_appSettingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            return Result<string>.Failure($"Config.SaveFailed: Failed to save configuration: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Result<ConfigurationSettings>> LoadAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_appSettingsPath))
            {
                _logger.LogInformation("appsettings.json not found, returning default configuration");
                return Result<ConfigurationSettings>.Success(CreateDefaultConfiguration());
            }

            var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // Navigate to TenSecondTom section
            if (!doc.RootElement.TryGetProperty(ConfigurationKeys.Root, out var tenSecondTomSection))
            {
                _logger.LogDebug("TenSecondTom configuration section not found, returning defaults");
                return Result<ConfigurationSettings>.Success(CreateDefaultConfiguration());
            }

            // Parse configuration sections
            var settings = ParseConfigurationFromJson(tenSecondTomSection);

            _logger.LogDebug("Configuration loaded successfully from {Path}", _appSettingsPath);
            return Result<ConfigurationSettings>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            return Result<ConfigurationSettings>.Failure($"Config.LoadFailed: Failed to load configuration: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public string GetStorageLocation()
    {
        return _appSettingsPath;
    }

    public async Task<Result<string>> SaveAudioConfigurationAsync(
        AudioConfiguration audioConfig,
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Saving audio configuration to {Path}", _appSettingsPath);

            // Load existing JSON or create new
            JsonDocument? existingDoc = null;
            if (File.Exists(_appSettingsPath))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
                    existingDoc = JsonDocument.Parse(existingJson);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Existing appsettings.json is invalid, creating new file");
                }
            }

            // Build new JSON structure
            var root = new Dictionary<string, object>();

            // Preserve all existing sections
            if (existingDoc != null)
            {
                foreach (var property in existingDoc.RootElement.EnumerateObject())
                {
                    root[property.Name] = JsonSerializer.Deserialize<object>(property.Value.GetRawText(), JsonOptions)
                        ?? new object();
                }
            }

            // Get or create TenSecondTom section
            if (!root.TryGetValue(ConfigurationKeys.Root, out var tenSecondTomObj))
            {
                tenSecondTomObj = new Dictionary<string, object>();
                root[ConfigurationKeys.Root] = tenSecondTomObj;
            }

            // Update audio section
            var tenSecondTom = tenSecondTomObj as Dictionary<string, object>
                ?? JsonSerializer.Deserialize<Dictionary<string, object>>(
                    JsonSerializer.Serialize(tenSecondTomObj, JsonOptions), JsonOptions)
                ?? new Dictionary<string, object>();

            tenSecondTom["Audio"] = new
            {
                SttProvider = audioConfig.SttProvider,
                SttBinaryPath = audioConfig.SttBinaryPath,
                SttModel = audioConfig.SttModel,
                SttApiKey = audioConfig.SttApiKey,
                SttFallbackEnabled = audioConfig.SttFallbackEnabled,
                SttFallbackProvider = audioConfig.SttFallbackProvider,
                SttFallbackBinaryPath = audioConfig.SttFallbackBinaryPath,
                SttFallbackModel = audioConfig.SttFallbackModel,
                SttFallbackApiKey = audioConfig.SttFallbackApiKey,
                KeepFiles = audioConfig.KeepFiles,
                Recorder = new
                {
                    FfmpegPath = audioConfig.Recorder.FfmpegPath,
                    InputVolume = audioConfig.Recorder.InputVolume,
                    EnableNoiseReduction = audioConfig.Recorder.EnableNoiseReduction,
                    EnableFrequencyFilters = audioConfig.Recorder.EnableFrequencyFilters
                },
                Preprocessing = new
                {
                    RemoveSilence = audioConfig.Preprocessing.RemoveSilence,
                    SilenceThresholdDb = audioConfig.Preprocessing.SilenceThresholdDb,
                    MinimumSilenceDurationMs = audioConfig.Preprocessing.MinimumSilenceDurationMs
                },
                Timeouts = new
                {
                    TodaySeconds = audioConfig.Timeouts.TodaySeconds,
                    RecordSeconds = audioConfig.Timeouts.RecordSeconds
                }
            };

            root[ConfigurationKeys.Root] = tenSecondTom;

            // Write atomically (temp file + rename)
            var tempPath = _appSettingsPath + ".tmp";
            var json = JsonSerializer.Serialize(root, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            // Atomic replace
            File.Move(tempPath, _appSettingsPath, overwrite: true);

            _logger.LogInformation("Audio configuration saved successfully");
            return Result<string>.Success(_appSettingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audio configuration");
            return Result<string>.Failure($"Failed to save audio configuration: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Result<AudioConfiguration>> LoadAudioConfigurationAsync(
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_appSettingsPath))
            {
                _logger.LogInformation("appsettings.json not found, returning default audio configuration");
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // Navigate to TenSecondTom:Audio section
            if (!doc.RootElement.TryGetProperty(ConfigurationKeys.Root, out var tenSecondTomSection))
            {
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            if (!tenSecondTomSection.TryGetProperty("audio", out var audioSection))
            {
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            // Deserialize audio configuration
            var audioConfig = JsonSerializer.Deserialize<AudioConfiguration>(audioSection.GetRawText(), JsonOptions);

            return Result<AudioConfiguration>.Success(audioConfig ?? new AudioConfiguration());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load audio configuration");
            return Result<AudioConfiguration>.Failure($"Failed to load audio configuration: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _fileLock.Dispose();
            _disposed = true;
        }
    }

    private static ConfigurationSettings CreateDefaultConfiguration()
    {
        return new ConfigurationSettings
        {
            Ssh = new SshConfiguration { KeyPath = null },
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                ApiKey = null,
                MaxInputTokens = LlmConstants.DefaultMaxInputTokensOpenAI
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".memory",
                    "ten-second-tom"),
                CreateIfMissing = true
            },
            Optional = new OptionalConfiguration
            {
                LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = 30,
                EnableTelemetry = false
            },
            CreatedAt = DateTime.UtcNow,
            ConfigurationVersion = "1.0"
        };
    }

    private static ConfigurationSettings ParseConfigurationFromJson(JsonElement tenSecondTomSection)
    {
        // Parse Ssh section
        var sshConfig = new SshConfiguration { KeyPath = null };
        if (tenSecondTomSection.TryGetProperty("ssh", out var sshSection))
        {
            sshConfig = new SshConfiguration
            {
                KeyPath = TryGetStringProperty(sshSection, "keyPath"),
                KeySource = TryGetEnumProperty<SshKeySource>(sshSection, "keySource"),
                AgentSocketPath = TryGetStringProperty(sshSection, "agentSocketPath"),
                KeyDisplayName = TryGetStringProperty(sshSection, "keyDisplayName")
            };
        }

        // Parse Llm section
        var llmConfig = new LlmConfiguration
        {
            Provider = LlmProvider.OpenAI,
            ApiKey = null,
            MaxInputTokens = LlmConstants.DefaultMaxInputTokensOpenAI
        };
        if (tenSecondTomSection.TryGetProperty("llm", out var llmSection))
        {
            var provider = TryGetEnumProperty<LlmProvider>(llmSection, "provider") ?? LlmProvider.OpenAI;
            var maxInputTokens = TryGetIntProperty(llmSection, "maxInputTokens");

            // Use provider-specific default if not explicitly set
            if (!maxInputTokens.HasValue)
            {
                maxInputTokens = provider == LlmProvider.Anthropic
                    ? LlmConstants.DefaultMaxInputTokensAnthropic
                    : LlmConstants.DefaultMaxInputTokensOpenAI;
            }

            llmConfig = new LlmConfiguration
            {
                Provider = provider,
                ApiKey = TryGetStringProperty(llmSection, "apiKey"),
                Model = TryGetStringProperty(llmSection, "model"),
                MaxInputTokens = maxInputTokens
            };
        }

        // Parse Storage section (supports both new format at root and legacy "storage" section)
        var storageConfig = new StorageConfiguration
        {
            MemoryDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".memory",
                "ten-second-tom"),
            CreateIfMissing = true
        };

        // Try new format first (MemoryDirectory at root level)
        var memoryDirectory = TryGetStringProperty(tenSecondTomSection, "memoryDirectory");

        // Fall back to legacy "storage" section for backwards compatibility
        if (string.IsNullOrWhiteSpace(memoryDirectory) && tenSecondTomSection.TryGetProperty("storage", out var storageSection))
        {
            memoryDirectory = TryGetStringProperty(storageSection, "memoryDirectory");
        }

        storageConfig = new StorageConfiguration
        {
            MemoryDirectory = memoryDirectory ?? storageConfig.MemoryDirectory,
            CreateIfMissing = true // Always true for now
        };

        // Parse Optional section
        var optionalConfig = new OptionalConfiguration
        {
            LogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
            RetentionDays = 30,
            EnableTelemetry = false
        };
        if (tenSecondTomSection.TryGetProperty("optional", out var optionalSection))
        {
            optionalConfig = new OptionalConfiguration
            {
                LogLevel = TryGetEnumProperty<Microsoft.Extensions.Logging.LogLevel>(optionalSection, "logLevel")
                    ?? Microsoft.Extensions.Logging.LogLevel.Information,
                RetentionDays = TryGetIntProperty(optionalSection, "retentionDays") ?? 30,
                EnableTelemetry = TryGetBoolProperty(optionalSection, "enableTelemetry") ?? false
            };
        }

        // Parse Configuration metadata
        var createdAt = DateTime.UtcNow;
        DateTime? lastModifiedAt = null;
        var version = "1.0";

        if (tenSecondTomSection.TryGetProperty("configuration", out var configSection))
        {
            if (configSection.TryGetProperty("createdAt", out var createdAtElement))
            {
                if (DateTime.TryParse(createdAtElement.GetString(), out var parsedCreated))
                {
                    createdAt = parsedCreated;
                }
            }

            if (configSection.TryGetProperty("lastModifiedAt", out var modifiedAtElement))
            {
                var modifiedStr = modifiedAtElement.GetString();
                if (!string.IsNullOrEmpty(modifiedStr) && DateTime.TryParse(modifiedStr, out var parsedModified))
                {
                    lastModifiedAt = parsedModified;
                }
            }

            version = TryGetStringProperty(configSection, "version") ?? "1.0";
        }

        return new ConfigurationSettings
        {
            Ssh = sshConfig,
            Llm = llmConfig,
            Storage = storageConfig,
            Optional = optionalConfig,
            CreatedAt = createdAt,
            LastModifiedAt = lastModifiedAt,
            ConfigurationVersion = version
        };
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
        }
        return null;
    }

    private static TEnum? TryGetEnumProperty<TEnum>(JsonElement element, string propertyName) where TEnum : struct, Enum
    {
        var stringValue = TryGetStringProperty(element, propertyName);
        if (!string.IsNullOrEmpty(stringValue) && Enum.TryParse<TEnum>(stringValue, ignoreCase: true, out var result))
        {
            return result;
        }
        return null;
    }

    private static int? TryGetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var intValue))
            {
                return intValue;
            }
        }
        return null;
    }

    private static bool? TryGetBoolProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return null;
    }
}
