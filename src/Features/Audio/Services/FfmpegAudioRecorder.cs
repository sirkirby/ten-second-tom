using System.Diagnostics;
using System.Runtime.InteropServices;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// FFmpeg-based audio recorder implementation.
/// Captures audio using system microphone via FFmpeg.
/// Outputs WAV format optimized for whisper.cpp (16kHz, mono, pcm_s16le).
/// </summary>
public sealed class FfmpegAudioRecorder : IAudioRecorder
{
    private readonly AudioOptions _config;
    private readonly ILogger<FfmpegAudioRecorder> _logger;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfmpegAudioRecorder"/> class.
    /// </summary>
    /// <param name="config">Audio configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="mediator">Mediator for cross-feature communication.</param>
    public FfmpegAudioRecorder(
        IOptions<AudioOptions> config,
        ILogger<FfmpegAudioRecorder> logger,
        IMediator mediator)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
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
    public async Task<Result<string>> GetDefaultMicrophoneNameAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await GetMacOSDefaultMicrophoneAsync(cancellationToken);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await GetWindowsDefaultMicrophoneAsync(cancellationToken);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await GetLinuxDefaultMicrophoneAsync(cancellationToken);
            }

            return Result<string>.Failure("Unsupported platform for microphone detection");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to detect default microphone");
            return Result<string>.Failure($"Unable to detect microphone: {ex.Message}");
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

                    // Send notification as enhancement (non-blocking, fire-and-forget)
                    // NOTE: Terminal prompt is PRIMARY - notification is secondary enhancement
                    // Notification failures must NOT impact recording functionality
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var durationMinutes = maxDurationSeconds.Value / 60;
                            var durationLabel = durationMinutes > 0
                                ? $"{durationMinutes}-minute"
                                : $"{maxDurationSeconds}-second";

                            var notificationCommand = new Features.Notifications.ShowNotification.Command(
                                Title: "Recording Session Expiring",
                                Message: $"Your {durationLabel} session has ended. Continue recording? (Press Enter in terminal)",
                                Priority: NotificationPriority.High,
                                TimeoutSeconds: 30,
                                Actions: null); // macOS does not support interactive notification buttons

                            var result = await _mediator.Send(notificationCommand, CancellationToken.None);

                            if (!result.IsSuccess)
                            {
                                _logger.LogWarning(
                                    "Failed to send recording expiration notification: {Error}",
                                    result.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Graceful degradation - log but don't fail recording
                            _logger.LogWarning(
                                ex,
                                "Unexpected error sending recording expiration notification. Recording continues normally.");
                        }
                    }, CancellationToken.None);

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
    /// Gets the default microphone name on macOS by querying system preferences.
    /// </summary>
    private async Task<Result<string>> GetMacOSDefaultMicrophoneAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Try to get the actual default input device name from macOS system_profiler
        var systemProfilerInfo = new ProcessStartInfo
        {
            FileName = "system_profiler",
            Arguments = "SPAudioDataType -json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var systemProcess = Process.Start(systemProfilerInfo);
            if (systemProcess != null)
            {
                var output = await systemProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                await systemProcess.WaitForExitAsync(cancellationToken);

                // Parse JSON output to find the default input device
                // system_profiler returns JSON with nested structure
                if (!string.IsNullOrWhiteSpace(output))
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(output);
                    if (jsonDoc.RootElement.TryGetProperty("SPAudioDataType", out var audioDataArray))
                    {
                        // Get first element which contains _items array
                        var firstElement = audioDataArray.EnumerateArray().FirstOrDefault();
                        if (firstElement.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                            firstElement.TryGetProperty("_items", out var items))
                        {
                            // Iterate through devices in _items to find default input
                            foreach (var device in items.EnumerateArray())
                            {
                                if (device.TryGetProperty("coreaudio_default_audio_input_device", out var isDefault) &&
                                    isDefault.GetString() == "spaudio_yes" &&
                                    device.TryGetProperty("_name", out var name))
                                {
                                    var deviceName = name.GetString();
                                    if (!string.IsNullOrWhiteSpace(deviceName))
                                    {
                                        return Result<string>.Success(deviceName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Let cancellation propagate
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query system_profiler for default microphone, falling back to generic message");
        }

        // Fallback: Since we use :default for recording, just indicate that
        return Result<string>.Success("System Default Input Device");
    }

    /// <summary>
    /// Gets the default microphone name on Windows using FFmpeg's dshow.
    /// </summary>
    private async Task<Result<string>> GetWindowsDefaultMicrophoneAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = _config.Recorder.FfmpegPath,
            Arguments = "-list_devices true -f dshow -i dummy",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return Result<string>.Failure("Failed to start FFmpeg for device listing");
        }

        // FFmpeg outputs device list to stderr
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        // Parse stderr for audio devices
        // Example output:
        // [dshow @ 0x...] DirectShow audio devices
        // [dshow @ 0x...] "Microphone (Realtek High Definition Audio)"

        var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var audioDevicesStarted = false;

        foreach (var line in lines)
        {
            if (line.Contains("DirectShow audio devices"))
            {
                audioDevicesStarted = true;
                continue;
            }

            if (audioDevicesStarted && line.Contains('"'))
            {
                // Extract device name between quotes
                var match = System.Text.RegularExpressions.Regex.Match(line, "\"([^\"]+)\"");
                if (match.Success)
                {
                    return Result<string>.Success(match.Groups[1].Value);
                }
            }
        }

        // If we couldn't parse a specific device, return a generic name
        return Result<string>.Success("Default Microphone");
    }

    /// <summary>
    /// Gets the default microphone name on Linux using arecord.
    /// </summary>
    private static async Task<Result<string>> GetLinuxDefaultMicrophoneAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "arecord",
                Arguments = "-l",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<string>.Success("Default ALSA Microphone");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            // Parse output for first capture device
            // Example output:
            // card 0: PCH [HDA Intel PCH], device 0: ALC269VC Analog [ALC269VC Analog]

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("card") && line.Contains(':'))
                {
                    // Extract device name after the colon
                    var parts = line.Split(':', 2);
                    if (parts.Length > 1)
                    {
                        var deviceName = parts[1].Trim();
                        // Remove the part after the comma if present
                        var commaIndex = deviceName.IndexOf(',');
                        if (commaIndex > 0)
                        {
                            deviceName = deviceName[..commaIndex].Trim();
                        }
                        // Remove text in brackets
                        deviceName = System.Text.RegularExpressions.Regex.Replace(deviceName, @"\s*\[.*?\]", "").Trim();

                        if (!string.IsNullOrWhiteSpace(deviceName))
                        {
                            return Result<string>.Success(deviceName);
                        }
                    }
                }
            }

            return Result<string>.Success("Default ALSA Microphone");
        }
        catch (OperationCanceledException)
        {
            // Let cancellation propagate
            throw;
        }
        catch
        {
            // If arecord is not available, return generic name
            return Result<string>.Success("Default ALSA Microphone");
        }
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

