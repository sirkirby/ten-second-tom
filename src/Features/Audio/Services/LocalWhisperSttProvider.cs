using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Local whisper.cpp STT provider implementation.
/// Uses whisper.cpp CLI for offline speech-to-text transcription.
/// </summary>
public sealed class LocalWhisperSttProvider : ISttProvider
{
    private readonly AudioOptions _config;
    private readonly ILogger<LocalWhisperSttProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWhisperSttProvider"/> class.
    /// </summary>
    /// <param name="config">Audio configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public LocalWhisperSttProvider(
        IOptions<AudioOptions> config,
        ILogger<LocalWhisperSttProvider> logger)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public SttEngine Engine => SttEngine.Local;

    /// <summary>
    /// Gets the binary path for whisper-cli from configuration.
    /// </summary>
    private string GetBinaryPath() => _config.SttBinaryPath;

    /// <summary>
    /// Gets the model path for whisper model from configuration.
    /// </summary>
    private string? GetModelPath() => _config.SttModel;

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Check if binary exists
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GetBinaryPath(),
                Arguments = "--help",
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

            // Check if model is configured and exists
            var configuredModelPath = GetModelPath();
            if (string.IsNullOrWhiteSpace(configuredModelPath))
            {
                _logger.LogDebug("Local whisper model path not configured");
                return false;
            }

            // Expand user home directory if needed
            var modelPath = configuredModelPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

            if (!File.Exists(modelPath))
            {
                _logger.LogDebug("Local whisper model file not found: {ModelPath}", modelPath);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Local whisper availability check failed");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult>> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioFilePath);

        if (!File.Exists(audioFilePath))
        {
            return Result<TranscriptionResult>.Failure($"Audio file not found: {audioFilePath}");
        }

        var configuredModelPath = GetModelPath();
        if (string.IsNullOrWhiteSpace(configuredModelPath))
        {
            return Result<TranscriptionResult>.Failure("Local whisper model path not configured");
        }

        // Expand user home directory if needed
        var modelPath = configuredModelPath.Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        if (!File.Exists(modelPath))
        {
            return Result<TranscriptionResult>.Failure($"Local whisper model file not found: {modelPath}");
        }

        var startTime = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        // Create temp output prefix
        var tempPrefix = Path.Combine(Path.GetTempPath(), $"whisper-{Guid.NewGuid()}");

        var arguments = $"-m \"{modelPath}\" -f \"{audioFilePath}\" -otxt -of \"{tempPrefix}\"";

        _logger.LogDebug(
            "Starting whisper.cpp transcription: {BinaryPath} {Arguments}",
            GetBinaryPath(),
            arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = GetBinaryPath(),
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return Result<TranscriptionResult>.Failure("Failed to start whisper.cpp process");
        }

        try
        {
            // Read output in background
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            stopwatch.Stop();

            if (process.ExitCode != 0)
            {
                _logger.LogError("whisper.cpp exited with code {ExitCode}: {StdErr}", process.ExitCode, stderr);
                return Result<TranscriptionResult>.Failure($"whisper.cpp transcription failed with exit code {process.ExitCode}");
            }

            _logger.LogDebug("whisper.cpp stdout: {StdOut}", stdout);

            // Read the output text file
            var outputTextFile = $"{tempPrefix}.txt";

            if (!File.Exists(outputTextFile))
            {
                return Result<TranscriptionResult>.Failure($"whisper.cpp did not create expected output file: {outputTextFile}");
            }

            var transcriptText = await File.ReadAllTextAsync(outputTextFile, cancellationToken);

            // Clean up temp file
            try
            {
                File.Delete(outputTextFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temp file: {TempFile}", outputTextFile);
            }

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                return Result<TranscriptionResult>.Failure("whisper.cpp returned empty transcript");
            }

            // Normalize whitespace: replace line breaks and multiple spaces with single space
            // This makes local whisper output match OpenAI's clean single-line format
            transcriptText = System.Text.RegularExpressions.Regex.Replace(transcriptText, @"\s+", " ").Trim();

            // Calculate word count
            var wordCount = transcriptText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

            // Extract model name from path
            var modelName = Path.GetFileName(modelPath);

            var result = new TranscriptionResult
            {
                AudioReference = audioFilePath,
                TranscriptText = transcriptText,
                SttEngine = SttEngine.Local,
                SttModel = modelName,
                ProcessingDuration = stopwatch.Elapsed,
                TranscribedAt = startTime,
                WordCount = wordCount
            };

            _logger.LogInformation(
                "Local whisper transcription completed: Model={Model}, Duration={Duration}s, WordCount={WordCount}",
                modelName,
                result.ProcessingDuration.TotalSeconds,
                wordCount);

            return Result<TranscriptionResult>.Success(result);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // Ignore errors during cleanup
            }

            throw;
        }
    }
}
