using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Channels.OS;

/// <summary>
/// Provides Unix named pipe (FIFO) listener for IPC communication.
/// Enables the Swift macOS notification helper to send signals back to the CLI
/// when users interact with notification action buttons.
/// </summary>
/// <remarks>
/// This service creates a temporary FIFO pipe using mkfifo, waits for incoming
/// signals with timeout support, and ensures proper cleanup on disposal.
/// Thread-safe for single reader scenarios.
/// </remarks>
public sealed class NamedPipeListener : IDisposable
{
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _disposalCts;
    private string? _pipePath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeListener"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic information.</param>
    public NamedPipeListener(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _disposalCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Gets the absolute path to the created named pipe.
    /// Returns null if the pipe has not been created yet.
    /// </summary>
    public string? PipePath => _pipePath;

    /// <summary>
    /// Creates a Unix FIFO (named pipe) at a unique temporary location.
    /// </summary>
    /// <returns>
    /// A <see cref="Result"/> indicating success or failure with error details.
    /// </returns>
    /// <remarks>
    /// The pipe is created in /tmp with a unique GUID to avoid conflicts.
    /// Format: /tmp/tom-notify-{guid}.pipe
    /// </remarks>
    public Result CreatePipe()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // Generate unique pipe path
            var pipeId = Guid.NewGuid().ToString("N");
            _pipePath = $"/tmp/tom-notify-{pipeId}.pipe";

            _logger.LogDebug("Creating named pipe at {PipePath}", _pipePath);

            // Use mkfifo command to create FIFO
            var startInfo = new ProcessStartInfo
            {
                FileName = "mkfifo",
                Arguments = _pipePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogError("Failed to start mkfifo process");
                return Result.Failure("Failed to start mkfifo process");
            }

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var error = process.StandardError.ReadToEnd();
                _logger.LogError("mkfifo failed with exit code {ExitCode}: {Error}",
                    process.ExitCode, error);
                return Result.Failure($"mkfifo failed: {error}");
            }

            // Verify pipe was created
            if (!File.Exists(_pipePath))
            {
                _logger.LogError("Named pipe was not created at {PipePath}", _pipePath);
                return Result.Failure($"Named pipe was not created at {_pipePath}");
            }

            _logger.LogInformation("Named pipe created successfully at {PipePath}", _pipePath);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating named pipe");
            return Result.Failure($"Unexpected error creating named pipe: {ex.Message}");
        }
    }

    /// <summary>
    /// Waits for a signal to arrive on the named pipe with timeout support.
    /// This operation blocks until data arrives, the timeout expires, or cancellation is requested.
    /// </summary>
    /// <param name="timeoutSeconds">Maximum time to wait for a signal in seconds.</param>
    /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
    /// <returns>
    /// The signal string received from the pipe, or null if the operation timed out,
    /// was cancelled, or encountered an error.
    /// </returns>
    /// <remarks>
    /// This method opens the pipe for reading (blocking until a writer connects),
    /// reads one line of text, and returns it. The pipe remains available for
    /// subsequent reads until disposed.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called before CreatePipe() has been successfully invoked.
    /// </exception>
    public async Task<string?> WaitForSignalAsync(
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(_pipePath))
        {
            throw new InvalidOperationException(
                "Named pipe has not been created. Call CreatePipe() first.");
        }

        if (!File.Exists(_pipePath))
        {
            _logger.LogError("Named pipe no longer exists at {PipePath}", _pipePath);
            return null;
        }

        // Combine cancellation tokens (caller + disposal)
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposalCts.Token);

        // Add timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            linkedCts.Token,
            timeoutCts.Token);

        try
        {
            _logger.LogDebug("Waiting for signal on pipe {PipePath} (timeout: {TimeoutSeconds}s)",
                _pipePath, timeoutSeconds);

            // Open pipe for reading - this blocks until a writer connects
            // Use FileShare.ReadWrite to allow multiple readers if needed
            using var fileStream = new FileStream(
                _pipePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);

            using var reader = new StreamReader(fileStream, Encoding.UTF8);

            // Read one line from the pipe
            var signal = await reader.ReadLineAsync(combinedCts.Token);

            if (string.IsNullOrWhiteSpace(signal))
            {
                _logger.LogWarning("Received empty signal from pipe");
                return null;
            }

            _logger.LogInformation("Received signal from pipe: {Signal}", signal);
            return signal.Trim();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _logger.LogDebug("Pipe read timed out after {TimeoutSeconds}s", timeoutSeconds);
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Pipe read cancelled by caller");
            return null;
        }
        catch (OperationCanceledException) when (_disposalCts.IsCancellationRequested)
        {
            _logger.LogDebug("Pipe read cancelled due to disposal");
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "I/O error reading from named pipe");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading from named pipe");
            return null;
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="NamedPipeListener"/>.
    /// Cancels any pending read operations and deletes the named pipe file.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogDebug("Disposing NamedPipeListener");

        // Cancel any pending reads
        _disposalCts.Cancel();

        // Delete the pipe file if it exists
        if (!string.IsNullOrEmpty(_pipePath) && File.Exists(_pipePath))
        {
            try
            {
                File.Delete(_pipePath);
                _logger.LogDebug("Deleted named pipe at {PipePath}", _pipePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete named pipe at {PipePath}", _pipePath);
            }
        }

        _disposalCts.Dispose();
        _disposed = true;
    }
}
