using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Provides persistent storage for command history using JSON file.
/// History is stored at {MemoryDirectory}/data/history.json.
/// </summary>
public sealed class HistoryStore : IHistoryStore, IDisposable
{
    private readonly ILogger<HistoryStore> _logger;
    private readonly string _historyPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public HistoryStore(ILogger<HistoryStore> logger, IConfiguration configuration)
    {
        _logger = logger;
        _historyPath = GetHistoryFilePath(configuration);
    }

    /// <inheritdoc/>
    public async Task<Result<List<CommandHistoryEntry>>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_historyPath))
            {
                _logger.LogDebug("History file not found at {Path}, returning empty history", _historyPath);
                return Result<List<CommandHistoryEntry>>.Success([]);
            }

            var json = await File.ReadAllTextAsync(_historyPath, cancellationToken);
            var entries = JsonSerializer.Deserialize<List<CommandHistoryEntry>>(json, JsonOptions);

            _logger.LogDebug("Loaded {Count} history entries from {Path}", entries?.Count ?? 0, _historyPath);
            return Result<List<CommandHistoryEntry>>.Success(entries ?? []);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in history file, returning empty history");
            return Result<List<CommandHistoryEntry>>.Success([]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history from {Path}", _historyPath);
            return Result<List<CommandHistoryEntry>>.Failure($"Failed to load history: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string>> SaveAsync(IReadOnlyList<CommandHistoryEntry> entries, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(_historyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write atomically: temp file + move
            var tempPath = _historyPath + ".tmp";
            var json = JsonSerializer.Serialize(entries, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, _historyPath, overwrite: true);

            _logger.LogDebug("Saved {Count} history entries to {Path}", entries.Count, _historyPath);
            return Result<string>.Success(_historyPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save history to {Path}", _historyPath);
            return Result<string>.Failure($"Failed to save history: {ex.Message}");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    public string GetHistoryPath() => _historyPath;

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

    /// <summary>
    /// Gets the history file path from configuration.
    /// </summary>
    private static string GetHistoryFilePath(IConfiguration configuration)
    {
        var memoryDir = configuration[ConfigurationKeys.RootDirectoryKey];

        if (string.IsNullOrWhiteSpace(memoryDir))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            memoryDir = Path.Combine(home, DirectoryNames.ApplicationRoot);
        }
        else if (memoryDir.StartsWith("~/", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            memoryDir = Path.Combine(home, memoryDir[2..]);
        }
        else if (!Path.IsPathRooted(memoryDir))
        {
            memoryDir = Path.GetFullPath(memoryDir, Directory.GetCurrentDirectory());
        }

        // History lives at {MemoryDirectory}/data/history.json
        var dataDir = Path.Combine(memoryDir, "data");
        return Path.Combine(dataDir, "history.json");
    }
}
