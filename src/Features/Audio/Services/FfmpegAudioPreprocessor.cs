using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// FFmpeg-based audio preprocessor implementation.
/// Applies audio preprocessing filters such as silence removal using FFmpeg.
/// </summary>
public sealed class FfmpegAudioPreprocessor : IAudioPreprocessor
{
    private readonly AudioConfiguration _config;
    private readonly ILogger<FfmpegAudioPreprocessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegAudioPreprocessor"/> class.
    /// </summary>
    /// <param name="config">Audio configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public FfmpegAudioPreprocessor(
        IOptions<AudioConfiguration> config,
        ILogger<FfmpegAudioPreprocessor> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _config.Recorder.FfmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "FFmpeg availability check failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<PreprocessingResult>> PreprocessAsync(
        string audioFilePath,
        bool replaceOriginal = true,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(audioFilePath))
        {
            return Result<PreprocessingResult>.Failure("Audio file path cannot be null or empty");
        }

        if (!File.Exists(audioFilePath))
        {
            return Result<PreprocessingResult>.Failure($"Audio file not found: {audioFilePath}");
        }

        // If silence removal is disabled, return success without processing
        if (!_config.Preprocessing.RemoveSilence)
        {
            var fileInfo = new FileInfo(audioFilePath);
            var noDuration = CalculateAudioDuration(fileInfo.Length);

            var noProcessingResult = new PreprocessingResult
            {
                ProcessedFilePath = audioFilePath,
                OriginalSizeBytes = fileInfo.Length,
                ProcessedSizeBytes = fileInfo.Length,
                OriginalDuration = noDuration,
                ProcessedDuration = noDuration,
                ProcessingTime = TimeSpan.Zero
            };

            _logger.LogDebug("Silence removal disabled, skipping preprocessing");
            return Result<PreprocessingResult>.Success(noProcessingResult);
        }

        var stopwatch = Stopwatch.StartNew();

        // Get original file info
        var originalFileInfo = new FileInfo(audioFilePath);
        var originalSize = originalFileInfo.Length;
        var originalDuration = CalculateAudioDuration(originalSize);

        // Determine output path
        string outputPath;
        if (replaceOriginal)
        {
            // Create temp file, will replace original later
            outputPath = Path.Combine(
                Path.GetDirectoryName(audioFilePath)!,
                $"{Path.GetFileNameWithoutExtension(audioFilePath)}_temp{Path.GetExtension(audioFilePath)}");
        }
        else
        {
            // Create new file with _processed suffix
            outputPath = Path.Combine(
                Path.GetDirectoryName(audioFilePath)!,
                $"{Path.GetFileNameWithoutExtension(audioFilePath)}_processed{Path.GetExtension(audioFilePath)}");
        }

        // Build FFmpeg silenceremove filter arguments
        var filterArgs = BuildSilenceRemoveFilter();

        var arguments = $"-i \"{audioFilePath}\" -af \"{filterArgs}\" -ar 16000 -ac 1 -acodec pcm_s16le \"{outputPath}\" -y";

        _logger.LogDebug(
            "Starting audio preprocessing: {FfmpegPath} {Arguments}",
            _config.Recorder.FfmpegPath,
            arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.Recorder.FfmpegPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return Result<PreprocessingResult>.Failure("Failed to start FFmpeg process");
        }

        try
        {
            // Read stderr to capture FFmpeg progress
            var stderrTask = Task.Run(async () =>
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogDebug("FFmpeg preprocessing stderr: {StdErr}", stderr);
            }, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("FFmpeg preprocessing failed with exit code {ExitCode}", process.ExitCode);
                
                // Clean up temp file if exists
                if (File.Exists(outputPath))
                {
                    try { File.Delete(outputPath); } catch { }
                }

                return Result<PreprocessingResult>.Failure($"FFmpeg preprocessing failed with exit code {process.ExitCode}");
            }
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                
                // Clean up temp file
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            throw;
        }

        stopwatch.Stop();

        // Verify output file was created
        if (!File.Exists(outputPath))
        {
            return Result<PreprocessingResult>.Failure($"FFmpeg did not create output file: {outputPath}");
        }

        // Get processed file info
        var processedFileInfo = new FileInfo(outputPath);
        var processedSize = processedFileInfo.Length;
        var processedDuration = CalculateAudioDuration(processedSize);

        // If replaceOriginal, replace the original file with processed version
        string finalPath = audioFilePath;
        if (replaceOriginal)
        {
            try
            {
                File.Delete(audioFilePath);
                File.Move(outputPath, audioFilePath);
                finalPath = audioFilePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replace original file with processed version");
                
                // Clean up temp file
                try { File.Delete(outputPath); } catch { }
                
                return Result<PreprocessingResult>.Failure($"Failed to replace original file: {ex.Message}");
            }
        }
        else
        {
            finalPath = outputPath;
        }

        var result = new PreprocessingResult
        {
            ProcessedFilePath = finalPath,
            OriginalSizeBytes = originalSize,
            ProcessedSizeBytes = processedSize,
            OriginalDuration = originalDuration,
            ProcessedDuration = processedDuration,
            ProcessingTime = stopwatch.Elapsed
        };

        _logger.LogInformation(
            "Audio preprocessing completed: OriginalDuration={OriginalDuration}s, ProcessedDuration={ProcessedDuration}s, " +
            "DurationReduction={DurationReduction:F1}%, SizeReduction={SizeReduction:F1}%, ProcessingTime={ProcessingTime}s",
            result.OriginalDuration.TotalSeconds,
            result.ProcessedDuration.TotalSeconds,
            result.DurationReductionPercent,
            result.SizeReductionPercent,
            result.ProcessingTime.TotalSeconds);

        return Result<PreprocessingResult>.Success(result);
    }

    /// <summary>
    /// Builds the FFmpeg silenceremove filter string based on configuration.
    /// </summary>
    /// <returns>FFmpeg audio filter string.</returns>
    private string BuildSilenceRemoveFilter()
    {
        var threshold = _config.Preprocessing.SilenceThresholdDb;
        var minSilenceDuration = _config.Preprocessing.MinimumSilenceDurationMs / 1000.0; // Convert to seconds

        // Comprehensive silence removal strategy using RMS detection:
        // RMS (Root Mean Square) is better than peak for detecting true silence because:
        // - Ignores brief clicks/pops that peak detection catches
        // - Better represents the average audio energy level
        // - More accurate for speech vs. silence detection
        //
        // We need TWO passes to handle both leading/trailing AND internal silence:
        //
        // Pass 1: Remove leading and trailing silence
        // - start_periods=1: Removes silence at the beginning
        // - stop_periods=-1: Removes all trailing silence
        // - detection=rms: Use RMS for better noise immunity
        //
        // Pass 2: Compress internal silence gaps
        // - start_periods=0: Don't skip any periods (process all audio)
        // - stop_periods=-50: Process many internal periods (aggressive compression)
        // - window=0: No sliding window (process immediately)
        //   This effectively compresses long silence gaps down to the minimum duration
        
        var pass1 = $"silenceremove=start_periods=1:start_duration={minSilenceDuration}:start_threshold={threshold}dB:" +
                   $"stop_periods=-1:stop_duration={minSilenceDuration}:stop_threshold={threshold}dB:detection=rms";
        
        var pass2 = $"silenceremove=start_periods=0:stop_periods=-50:stop_duration={minSilenceDuration}:stop_threshold={threshold}dB:detection=rms:window=0";
        
        return $"{pass1},{pass2}";
    }

    /// <summary>
    /// Calculates approximate audio duration from file size.
    /// Assumes 16kHz, mono, 16-bit PCM (32,000 bytes per second).
    /// </summary>
    /// <param name="fileSizeBytes">File size in bytes.</param>
    /// <returns>Approximate duration.</returns>
    private static TimeSpan CalculateAudioDuration(long fileSizeBytes)
    {
        // 16kHz * 1 channel * 2 bytes/sample = 32,000 bytes/second
        const int bytesPerSecond = 32000;
        
        // Subtract WAV header (44 bytes) if present
        var dataSize = fileSizeBytes > 44 ? fileSizeBytes - 44 : fileSizeBytes;
        
        var seconds = dataSize / (double)bytesPerSecond;
        return TimeSpan.FromSeconds(Math.Max(0, seconds));
    }
}

