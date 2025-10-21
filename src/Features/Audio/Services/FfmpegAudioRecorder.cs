using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// FFmpeg-based audio recorder implementation.
/// Captures audio using system microphone via FFmpeg.
/// Outputs WAV format optimized for whisper.cpp (16kHz, mono, pcm_s16le).
/// </summary>
public sealed class FfmpegAudioRecorder : IAudioRecorder
{
    private readonly AudioConfiguration _config;
    private readonly ILogger<FfmpegAudioRecorder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegAudioRecorder"/> class.
    /// </summary>
    /// <param name="config">Audio configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public FfmpegAudioRecorder(
        IOptions<AudioConfiguration> config,
        ILogger<FfmpegAudioRecorder> logger)
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
    public async Task<Result<AudioRecording>> RecordAsync(
        string outputPath,
        int? maxDurationSeconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var startTime = DateTimeOffset.UtcNow;

        // Determine platform-specific audio input device
        string inputDevice = GetPlatformAudioInput();
        string inputFormat = GetPlatformInputFormat();

        // Build audio filter chain based on configuration
        var filters = new List<string>();

        // Add frequency filters if enabled (recommended for voice)
        if (_config.Recorder.EnableFrequencyFilters)
        {
            filters.Add("highpass=f=80");  // Remove low-frequency rumble
            filters.Add("lowpass=f=8000"); // Remove high-frequency hiss
        }

        // Add volume adjustment
        filters.Add($"volume={_config.Recorder.InputVolume}");

        // Add noise reduction if enabled
        if (_config.Recorder.EnableNoiseReduction)
        {
            filters.Add("anlmdn"); // Adaptive noise reduction
        }

        var audioFilter = string.Join(",", filters);

        var arguments = $"-f {inputFormat} -i {inputDevice} " +
                       $"-af \"{audioFilter}\" " +
                       "-ar 16000 -ac 1 -acodec pcm_s16le " +
                       $"\"{outputPath}\"";

        _logger.LogDebug(
            "Starting FFmpeg recording: {FfmpegPath} {Arguments}",
            _config.Recorder.FfmpegPath,
            arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.Recorder.FfmpegPath,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // FFmpeg not found - provide helpful error message
            _logger.LogError(ex, "FFmpeg not found at path: {FfmpegPath}", _config.Recorder.FfmpegPath);

            var errorMessage = $"FFmpeg not found. Please install FFmpeg to use voice recording.\n\n" +
                             $"Installation instructions:\n" +
                             $"  macOS:   brew install ffmpeg\n" +
                             $"  Linux:   sudo apt install ffmpeg (Ubuntu/Debian) or sudo yum install ffmpeg (RHEL/CentOS)\n" +
                             $"  Windows: Download from https://ffmpeg.org/download.html\n\n" +
                             $"Configured path: {_config.Recorder.FfmpegPath}";

            return Result<AudioRecording>.Failure(errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start FFmpeg process");
            return Result<AudioRecording>.Failure($"Failed to start FFmpeg: {ex.Message}");
        }

        if (process == null)
        {
            return Result<AudioRecording>.Failure("Failed to start FFmpeg process");
        }

        _logger.LogInformation("Recording started. Press Enter to stop recording...");

        // Wait for user to press Enter to stop recording, with optional timeout
        try
        {
            // Read stderr in background to prevent buffer overflow
            var stderrTask = Task.Run(async () =>
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                _logger.LogDebug("FFmpeg stderr: {StdErr}", stderr);
            }, cancellationToken);

            bool shouldContinue = true;
            var recordingStart = DateTimeOffset.UtcNow;
            var lastPromptTime = recordingStart;

            // Use a polling loop to check for Enter key without blocking Console.ReadLine
            while (shouldContinue && !cancellationToken.IsCancellationRequested)
            {
                // Calculate time until next timeout prompt
                var timeElapsedSinceLastPrompt = DateTimeOffset.UtcNow - lastPromptTime;
                var timeUntilPrompt = maxDurationSeconds.HasValue
                    ? TimeSpan.FromSeconds(maxDurationSeconds.Value) - timeElapsedSinceLastPrompt
                    : TimeSpan.MaxValue;

                // Check for Enter key press (non-blocking)
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Enter)
                    {
                        // User pressed Enter to stop
                        shouldContinue = false;
                        break;
                    }
                }

                // Check if timeout reached
                if (maxDurationSeconds.HasValue && timeUntilPrompt <= TimeSpan.Zero)
                {
                    // Timeout reached - prompt user to continue
                    var totalElapsed = (DateTimeOffset.UtcNow - recordingStart).TotalSeconds;
                    Console.WriteLine($"\nRecording limit reached ({totalElapsed:F0}s / {maxDurationSeconds}s interval).");
                    Console.Write("Continue recording? (y/n): ");
                    
                    // Add a timeout to the continuation prompt (30 seconds)
                    var responseTask = Task.Run(() => Console.ReadLine()?.Trim().ToLowerInvariant());
                    var promptTimeout = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    var completedPromptTask = await Task.WhenAny(responseTask, promptTimeout);
                    
                    string? response = null;
                    if (completedPromptTask == responseTask)
                    {
                        response = await responseTask;
                    }
                    else
                    {
                        // User didn't respond within 30 seconds - default to stopping
                        Console.WriteLine("\nNo response received. Stopping recording.");
                        shouldContinue = false;
                        break;
                    }
                    
                    if (response == "y" || response == "yes")
                    {
                        Console.WriteLine("Recording continues. Press Enter to stop.");
                        lastPromptTime = DateTimeOffset.UtcNow; // Reset the interval timer
                        continue;
                    }
                    else
                    {
                        // User chose to stop
                        shouldContinue = false;
                        break;
                    }
                }

                // Small delay to avoid busy waiting
                await Task.Delay(100, cancellationToken);
            }

            // Send 'q' to FFmpeg stdin to gracefully stop
            await process.StandardInput.WriteAsync('q');
            await process.StandardInput.FlushAsync(cancellationToken);

            // Wait for process to exit
            await process.WaitForExitAsync(cancellationToken);
            await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogError("FFmpeg exited with code {ExitCode}", process.ExitCode);
                return Result<AudioRecording>.Failure($"FFmpeg recording failed with exit code {process.ExitCode}");
            }
        }
        catch (OperationCanceledException)
        {
            // Try to kill the process gracefully
            try
            {
                if (!process.HasExited)
                {
                    await process.StandardInput.WriteAsync('q');
                    await process.StandardInput.FlushAsync(CancellationToken.None);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
            }
            catch
            {
                process.Kill();
            }

            throw;
        }

        // Get file info
        if (!File.Exists(outputPath))
        {
            return Result<AudioRecording>.Failure($"Recording file not created: {outputPath}");
        }

        var fileInfo = new FileInfo(outputPath);
        var endTime = DateTimeOffset.UtcNow;
        var duration = endTime - startTime;

        // Calculate duration from file size (16kHz, mono, 16-bit = 32,000 bytes/sec)
        var audioDuration = TimeSpan.FromSeconds(fileInfo.Length / 32000.0);

        var recording = new AudioRecording
        {
            Filename = Path.GetFileName(outputPath),
            FilePath = outputPath,
            Duration = audioDuration,
            SampleRate = 16000,
            Channels = 1,
            Format = AudioFormat.Wav,
            Encoding = "pcm_s16le",
            RecordedAt = startTime,
            FileSizeBytes = fileInfo.Length
        };

        _logger.LogInformation(
            "Recording completed: Duration={Duration}s, Size={SizeBytes} bytes",
            recording.Duration.TotalSeconds,
            recording.FileSizeBytes);

        return Result<AudioRecording>.Success(recording);
    }

    /// <summary>
    /// Gets the platform-specific audio input device identifier for FFmpeg.
    /// </summary>
    /// <returns>Device identifier string for the current platform.</returns>
    /// <remarks>
    /// macOS (avfoundation): Format is [video]:[audio]. Use ":default" for audio-only from default microphone.
    /// Linux (alsa): Use "default" for default ALSA audio device.
    /// Windows (dshow): Use DirectShow device name format.
    /// </remarks>
    private static string GetPlatformAudioInput()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // ":default" = no video (empty before colon), default system audio input
            // This automatically uses the current system default microphone
            return ":default";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "default"; // ALSA default device
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "audio=\"Microphone\""; // Windows DirectShow
        }

        throw new PlatformNotSupportedException("Unsupported platform for audio recording");
    }

    private static string GetPlatformInputFormat()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "avfoundation";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "alsa";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "dshow";
        }

        throw new PlatformNotSupportedException("Unsupported platform for audio recording");
    }
}

