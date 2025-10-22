using System.Text.Json;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Service for managing appsettings.json file updates.
/// Provides atomic updates with proper error handling and file locking.
/// </summary>
public sealed class AppSettingsStorageService : IAppSettingsStorageService, IDisposable
{
    private readonly ILogger<AppSettingsStorageService> _logger;
    private readonly string _appSettingsPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private bool _disposed;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettingsStorageService(
        ILogger<AppSettingsStorageService> logger,
        string? appSettingsPath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _appSettingsPath = appSettingsPath ?? Path.Combine(AppContext.BaseDirectory, "appsettings.json");
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

            // Preserve existing TenSecondTom section
            if (existingDoc != null && existingDoc.RootElement.TryGetProperty(ConfigurationKeys.Root, out var tenSecondTomSection))
            {
                root[ConfigurationKeys.Root] = JsonSerializer.Deserialize<Dictionary<string, object>>(tenSecondTomSection.GetRawText())
                    ?? new Dictionary<string, object>();
            }
            else
            {
                root[ConfigurationKeys.Root] = new Dictionary<string, object>();
            }

            // Update audio section
            var tenSecondTom = (Dictionary<string, object>)root[ConfigurationKeys.Root];
            tenSecondTom["Audio"] = new
            {
                PreferredStt = audioConfig.PreferredStt,
                KeepFiles = audioConfig.KeepFiles,
                Recorder = new
                {
                    FfmpegPath = audioConfig.Recorder.FfmpegPath,
                    InputVolume = audioConfig.Recorder.InputVolume,
                    EnableNoiseReduction = audioConfig.Recorder.EnableNoiseReduction,
                    EnableFrequencyFilters = audioConfig.Recorder.EnableFrequencyFilters
                },
                LocalWhisper = new
                {
                    BinaryPath = audioConfig.LocalWhisper.BinaryPath,
                    ModelPath = audioConfig.LocalWhisper.ModelPath
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
                _logger.LogInformation("appsettings.json not found, returning default configuration");
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            var json = await File.ReadAllTextAsync(_appSettingsPath, cancellationToken);
            using var doc = JsonDocument.Parse(json);

            // Navigate to TenSecondTom:Audio section
            if (!doc.RootElement.TryGetProperty(ConfigurationKeys.Root, out var tenSecondTomSection))
            {
                return Result<AudioConfiguration>.Success(new AudioConfiguration());
            }

            if (!tenSecondTomSection.TryGetProperty("Audio", out var audioSection))
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
}
