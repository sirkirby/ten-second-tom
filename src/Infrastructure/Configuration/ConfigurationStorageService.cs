using System.Text.Json;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Unified configuration storage service using idiomatic C# JSON serialization.
/// Manages configuration in config.json with atomic updates and proper error handling.
/// Environment variables can override any configuration value.
/// </summary>
public sealed class ConfigurationStorageService : IConfigurationStorageService, IAppSettingsStorageService, IDisposable
{
    private readonly ILogger<ConfigurationStorageService> _logger;
    private readonly string _configPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Default PascalCase naming matches .NET configuration system
        PropertyNameCaseInsensitive = true
    };

    public ConfigurationStorageService(
        ILogger<ConfigurationStorageService> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string? configPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configPath = configPath ?? GetUserConfigPath(configuration);
    }

    private static string GetUserConfigPath(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var path = ConfigurationHelpers.GetUserConfigPath(configuration);
        
        // Ensure the config directory exists
        var configDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        return path;
    }

    public async Task<Result<string>> SaveAsync(
        ConfigurationSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Saving configuration to {Path}", _configPath);

            // Load existing root configuration (preserves other sections like Serilog)
            ConfigurationRoot root;
            if (File.Exists(_configPath))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(_configPath, cancellationToken);
                    root = JsonSerializer.Deserialize<ConfigurationRoot>(existingJson, JsonOptions) ?? new ConfigurationRoot();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Existing config.json is invalid, creating new file");
                    root = new ConfigurationRoot();
                }
            }
            else
            {
                root = new ConfigurationRoot();
            }

            // Serialize settings to JsonElement for the TenSecondTom section
            root.TenSecondTom = SerializeToJsonElement(settings);

            // Serialize and save atomically
            var tempPath = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(root, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            File.Move(tempPath, _configPath, overwrite: true);

            _logger.LogInformation("Configuration saved successfully to {Path}", _configPath);
            return Result<string>.Success(_configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            return Result<string>.Failure($"Config.SaveFailed: {ex.Message}");
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
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation("Config file not found, returning defaults");
                return Result<ConfigurationSettings>.Success(CreateDefaultConfiguration());
            }

            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            var root = JsonSerializer.Deserialize<ConfigurationRoot>(json, JsonOptions);

            if (root == null || root.TenSecondTom.ValueKind == JsonValueKind.Null || root.TenSecondTom.ValueKind == JsonValueKind.Undefined)
            {
                _logger.LogDebug("TenSecondTom section not found, returning defaults");
                return Result<ConfigurationSettings>.Success(CreateDefaultConfiguration());
            }

            // Deserialize the JsonElement to ConfigurationSettings, handling the structural differences
            var settings = DeserializeFromJsonElement(root.TenSecondTom);

            _logger.LogDebug("Configuration loaded successfully from {Path}", _configPath);
            return Result<ConfigurationSettings>.Success(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            return Result<ConfigurationSettings>.Failure($"Config.LoadFailed: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public string GetStorageLocation() => _configPath;

    public async Task<Result<string>> SaveAudioConfigurationAsync(
        AudioConfiguration audioConfig,
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Saving audio configuration to {Path}", _configPath);

            // Load existing root configuration
            ConfigurationRoot root;
            if (File.Exists(_configPath))
            {
                var existingJson = await File.ReadAllTextAsync(_configPath, cancellationToken);
                root = JsonSerializer.Deserialize<ConfigurationRoot>(existingJson, JsonOptions) ?? new ConfigurationRoot();
            }
            else
            {
                root = new ConfigurationRoot();
            }

            // If no TenSecondTom section exists, create minimal structure
            if (root.TenSecondTom.ValueKind == JsonValueKind.Null || root.TenSecondTom.ValueKind == JsonValueKind.Undefined)
            {
                root.TenSecondTom = JsonSerializer.SerializeToElement(new { Audio = audioConfig }, JsonOptions);
            }
            else
            {
                // Merge audio config into existing TenSecondTom section
                var tenSecondTomDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    root.TenSecondTom.GetRawText(), JsonOptions) ?? new Dictionary<string, JsonElement>();
                
                tenSecondTomDict["Audio"] = JsonSerializer.SerializeToElement(audioConfig, JsonOptions);
                root.TenSecondTom = JsonSerializer.SerializeToElement(tenSecondTomDict, JsonOptions);
            }

            // Save atomically
            var tempPath = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(root, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, _configPath, overwrite: true);

            _logger.LogInformation("Audio configuration saved successfully");
            return Result<string>.Success(_configPath);
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
            if (!File.Exists(_configPath))
            {
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            var root = JsonSerializer.Deserialize<ConfigurationRoot>(json, JsonOptions);

            if (root == null || root.TenSecondTom.ValueKind == JsonValueKind.Null || root.TenSecondTom.ValueKind == JsonValueKind.Undefined)
            {
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            // Try to extract Audio section from TenSecondTom
            if (!root.TenSecondTom.TryGetProperty("Audio", out var audioElement))
            {
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            var audioConfig = JsonSerializer.Deserialize<AudioConfiguration>(audioElement.GetRawText(), JsonOptions);

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

    // ═══════════════════════════════════════════════════════════════
    // Private Serialization Methods
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Serializes ConfigurationSettings to a JsonElement for the TenSecondTom section.
    /// Handles structural differences between domain model and JSON format.
    /// </summary>
    private static JsonElement SerializeToJsonElement(ConfigurationSettings settings)
    {
        var audioDefaults = new AudioConfiguration();
        
        // Build the JSON structure with all required sections
        var tenSecondTomSection = new
        {
            MemoryDirectory = settings.Storage.MemoryDirectory,
            Ssh = settings.Ssh,
            Llm = settings.Llm,
            Optional = settings.Optional,
            Audio = audioDefaults,
            Auth = new
            {
                SshAgentProvider = SshConstants.DefaultAgentProvider,
                PublicKeyPath = settings.Ssh.KeyPath
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
            Configuration = new
            {
                CreatedAt = settings.CreatedAt.ToString("O"),
                LastModifiedAt = settings.LastModifiedAt?.ToString("O"),
                Version = settings.ConfigurationVersion
            }
        };

        return JsonSerializer.SerializeToElement(tenSecondTomSection, JsonOptions);
    }

    /// <summary>
    /// Deserializes a JsonElement from the TenSecondTom section to ConfigurationSettings.
    /// Handles structural differences and provides defaults for missing values.
    /// </summary>
    private static ConfigurationSettings DeserializeFromJsonElement(JsonElement tenSecondTomSection)
    {
        // Extract core configuration sections
        var sshConfig = tenSecondTomSection.TryGetProperty("Ssh", out var sshElement)
            ? JsonSerializer.Deserialize<SshConfiguration>(sshElement.GetRawText(), JsonOptions) ?? new SshConfiguration()
            : new SshConfiguration();

        var llmConfig = tenSecondTomSection.TryGetProperty("Llm", out var llmElement)
            ? JsonSerializer.Deserialize<LlmConfiguration>(llmElement.GetRawText(), JsonOptions) ?? new LlmConfiguration()
            : new LlmConfiguration();

        var optionalConfig = tenSecondTomSection.TryGetProperty("Optional", out var optionalElement)
            ? JsonSerializer.Deserialize<OptionalConfiguration>(optionalElement.GetRawText(), JsonOptions) ?? new OptionalConfiguration()
            : new OptionalConfiguration();

        // Extract MemoryDirectory (can be at root or in Storage section for backwards compatibility)
        string? memoryDirectory = null;
        if (tenSecondTomSection.TryGetProperty("MemoryDirectory", out var memoryDirElement))
        {
            memoryDirectory = memoryDirElement.GetString();
        }
        else if (tenSecondTomSection.TryGetProperty("Storage", out var storageElement) 
                 && storageElement.TryGetProperty("MemoryDirectory", out var storageMemDirElement))
        {
            memoryDirectory = storageMemDirElement.GetString();
        }

        // Extract metadata
        DateTime createdAt = DateTime.UtcNow;
        DateTime? lastModifiedAt = null;
        string version = "1.0";

        if (tenSecondTomSection.TryGetProperty("Configuration", out var configElement))
        {
            if (configElement.TryGetProperty("CreatedAt", out var createdElement) 
                && DateTime.TryParse(createdElement.GetString(), out var parsedCreated))
            {
                createdAt = parsedCreated;
            }
            
            if (configElement.TryGetProperty("LastModifiedAt", out var modifiedElement) 
                && DateTime.TryParse(modifiedElement.GetString(), out var modified))
            {
                lastModifiedAt = modified;
            }
            
            if (configElement.TryGetProperty("Version", out var versionElement))
            {
                version = versionElement.GetString() ?? "1.0";
            }
        }

        return new ConfigurationSettings
        {
            Ssh = sshConfig,
            Llm = llmConfig,
            Storage = new StorageConfiguration
            {
                MemoryDirectory = memoryDirectory 
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DirectoryNames.ApplicationRoot)
            },
            Optional = optionalConfig,
            CreatedAt = createdAt,
            LastModifiedAt = lastModifiedAt,
            ConfigurationVersion = version
        };
    }

    private static ConfigurationSettings CreateDefaultConfiguration()
    {
        return new ConfigurationSettings
        {
            Ssh = new SshConfiguration(),
            Llm = new LlmConfiguration
            {
                Provider = LlmProvider.OpenAI,
                MaxInputTokens = LlmConstants.DefaultMaxInputTokensOpenAI
            },
            Storage = new StorageConfiguration
            {
                MemoryDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    DirectoryNames.ApplicationRoot)
            },
            Optional = new OptionalConfiguration(),
            CreatedAt = DateTime.UtcNow,
            ConfigurationVersion = "1.0"
        };
    }
}

