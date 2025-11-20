using System.Text.Json;
using Microsoft.Extensions.Logging;
using TenSecondTom.Infrastructure.Configuration.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Generic JSON configuration storage service with NO feature knowledge.
/// Provides thread-safe, atomic read/write of configuration sections.
/// </summary>
/// <remarks>
/// This implementation is intentionally feature-agnostic and operates purely
/// on JSON section paths and generic types. It has zero knowledge of domain models
/// like AudioConfiguration, SshConfiguration, etc.
///
/// Key features:
/// - Thread-safe: SemaphoreSlim ensures safe concurrent access
/// - Atomic writes: Temp file + File.Move prevents corruption
/// - Section preservation: Writing one section preserves all others
/// - Nested path support: "TenSecondTom:Audio:Recorder" navigates nested structure
/// - Default handling: Returns new T() if section doesn't exist
/// </remarks>
public sealed class ConfigurationSectionStore : IConfigurationSectionStore, IDisposable
{
    private readonly ILogger<ConfigurationSectionStore> _logger;
    private readonly string _configPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Gets the absolute path to the configuration file being managed.
    /// </summary>
    public string ConfigurationPath => _configPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// Creates a new configuration section store.
    /// </summary>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="configuration">Microsoft configuration to resolve config path.</param>
    /// <param name="configPath">Optional override for config file path (primarily for testing).</param>
    public ConfigurationSectionStore(
        ILogger<ConfigurationSectionStore> logger,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string? configPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configPath = configPath ?? GetUserConfigPath(configuration);
    }

    /// <inheritdoc />
    public async Task<Result<T>> ReadSectionAsync<T>(
        string sectionPath,
        CancellationToken cancellationToken = default)
        where T : new()
    {
        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return Result<T>.Failure("Section path cannot be null or empty");
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Reading configuration section: {SectionPath}", sectionPath);

            if (!File.Exists(_configPath))
            {
                _logger.LogDebug("Config file not found, returning default instance for {Type}", typeof(T).Name);
                return Result<T>.Success(new T());
            }

            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            using var document = JsonDocument.Parse(json);

            // Navigate to the section using the path segments
            var segments = sectionPath.Split(':');
            JsonElement current = document.RootElement;

            foreach (var segment in segments)
            {
                if (!current.TryGetProperty(segment, out var next))
                {
                    // Section doesn't exist, return default instance
                    _logger.LogDebug("Section {SectionPath} not found, returning default instance", sectionPath);
                    return Result<T>.Success(new T());
                }
                current = next;
            }

            // Deserialize the section to the requested type
            var section = JsonSerializer.Deserialize<T>(current.GetRawText(), JsonOptions);

            if (section == null)
            {
                _logger.LogDebug("Section {SectionPath} deserialized to null, returning default instance", sectionPath);
                return Result<T>.Success(new T());
            }

            _logger.LogDebug("Successfully read section {SectionPath} as {Type}", sectionPath, typeof(T).Name);
            return Result<T>.Success(section);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in config file while reading {SectionPath}", sectionPath);
            return Result<T>.Failure($"Invalid JSON in configuration file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read configuration section {SectionPath}", sectionPath);
            return Result<T>.Failure($"Failed to read configuration section: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> WriteSectionAsync<T>(
        string sectionPath,
        T config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sectionPath))
        {
            return Result<string>.Failure("Section path cannot be null or empty");
        }

        ArgumentNullException.ThrowIfNull(config);

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Writing configuration section: {SectionPath}", sectionPath);

            // Load existing config root or create new
            Dictionary<string, JsonElement> rootDict;
            if (File.Exists(_configPath))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(_configPath, cancellationToken);
                    rootDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingJson, JsonOptions)
                        ?? new Dictionary<string, JsonElement>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Existing config.json is invalid, creating new file");
                    rootDict = new Dictionary<string, JsonElement>();
                }
            }
            else
            {
                rootDict = new Dictionary<string, JsonElement>();
            }

            // Navigate and set the section value
            var segments = sectionPath.Split(':');
            SetNestedValue(rootDict, segments, config, JsonOptions);

            // Write atomically: temp file + move
            var tempPath = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(rootDict, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            File.Move(tempPath, _configPath, overwrite: true);

            _logger.LogInformation("Successfully wrote section {SectionPath} to {Path}", sectionPath, _configPath);
            return Result<string>.Success(_configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write configuration section {SectionPath}", sectionPath);
            return Result<string>.Failure($"Failed to write configuration section: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<JsonDocument>> ReadFullConfigAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Reading full configuration from {Path}", _configPath);

            if (!File.Exists(_configPath))
            {
                _logger.LogDebug("Config file not found, returning empty JSON document");
                return Result<JsonDocument>.Success(JsonDocument.Parse("{}"));
            }

            var json = await File.ReadAllTextAsync(_configPath, cancellationToken);
            var document = JsonDocument.Parse(json);

            _logger.LogDebug("Successfully read full configuration");
            return Result<JsonDocument>.Success(document);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in config file");
            return Result<JsonDocument>.Failure($"Invalid JSON in configuration file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read full configuration");
            return Result<JsonDocument>.Failure($"Failed to read configuration: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> WriteMultipleSectionsAsync(
        Dictionary<string, object> sections,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sections);

        if (sections.Count == 0)
        {
            return Result<string>.Failure("No sections provided to write");
        }

        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Writing {Count} configuration sections atomically", sections.Count);

            // Load existing config root or create new
            Dictionary<string, JsonElement> rootDict;
            if (File.Exists(_configPath))
            {
                try
                {
                    var existingJson = await File.ReadAllTextAsync(_configPath, cancellationToken);
                    rootDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existingJson, JsonOptions)
                        ?? new Dictionary<string, JsonElement>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Existing config.json is invalid, creating new file");
                    rootDict = new Dictionary<string, JsonElement>();
                }
            }
            else
            {
                rootDict = new Dictionary<string, JsonElement>();
            }

            // Apply all section updates to the in-memory dictionary
            foreach (var (sectionPath, config) in sections)
            {
                if (string.IsNullOrWhiteSpace(sectionPath))
                {
                    return Result<string>.Failure($"Section path cannot be null or empty");
                }

                var segments = sectionPath.Split(':');
                SetNestedValue(rootDict, segments, config, JsonOptions);

                _logger.LogDebug("Prepared section {SectionPath} for atomic write", sectionPath);
            }

            // Write atomically: all sections in one operation
            var tempPath = _configPath + ".tmp";
            var json = JsonSerializer.Serialize(rootDict, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            File.Move(tempPath, _configPath, overwrite: true);

            _logger.LogInformation("Successfully wrote {Count} sections atomically to {Path}",
                sections.Count, _configPath);
            return Result<string>.Success(_configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write multiple configuration sections atomically");
            return Result<string>.Failure($"Failed to write configuration sections: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public string GetConfigPath() => _configPath;

    /// <summary>
    /// Disposes the semaphore used for file locking.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _fileLock.Dispose();
            _disposed = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Private Helper Methods
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the user configuration file path from the configuration system.
    /// Ensures the config directory exists.
    /// </summary>
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

    /// <summary>
    /// Recursively navigates and sets a nested value in a dictionary structure.
    /// Creates intermediate dictionaries as needed.
    /// </summary>
    /// <param name="root">Root dictionary to modify.</param>
    /// <param name="segments">Array of path segments (e.g., ["TenSecondTom", "Audio"]).</param>
    /// <param name="value">Value to set at the final segment.</param>
    /// <param name="options">JSON serialization options.</param>
    private static void SetNestedValue(
        Dictionary<string, JsonElement> root,
        string[] segments,
        object value,
        JsonSerializerOptions options)
    {
        if (segments.Length == 0)
        {
            throw new ArgumentException("Segments array cannot be empty", nameof(segments));
        }

        // Navigate to the parent of the final segment, creating structure as needed
        // We maintain a chain of dictionaries to update after modifications are complete
        var dictionaryChain = new List<(Dictionary<string, JsonElement> dict, string segment)>();
        var current = root;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];

            Dictionary<string, JsonElement> nested;
            if (current.TryGetValue(segment, out var existingElement))
            {
                // Deserialize existing element to dictionary and continue navigation
                nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    existingElement.GetRawText(), options) ?? new Dictionary<string, JsonElement>();
            }
            else
            {
                // Create new intermediate dictionary
                nested = new Dictionary<string, JsonElement>();
            }

            // Track this dictionary and segment for later update
            dictionaryChain.Add((current, segment));
            current = nested;
        }

        // Set the final value in the deepest nested dictionary
        var finalSegment = segments[^1];
        current[finalSegment] = JsonSerializer.SerializeToElement(value, options);

        // Now work backwards through the chain, updating each parent with the modified child
        // This ensures modifications are propagated up to the root
        for (int i = dictionaryChain.Count - 1; i >= 0; i--)
        {
            var (parentDict, segment) = dictionaryChain[i];
            parentDict[segment] = JsonSerializer.SerializeToElement(current, options);
            current = parentDict;
        }
    }
}
