using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Channels.OS;

/// <summary>
/// macOS notification channel using a native Swift sidecar.
/// </summary>
public sealed class MacOsNotificationProvider : INotificationChannel
{
    private readonly ILogger<MacOsNotificationProvider> _logger;
    private readonly string _notifierPath;

    public MacOsNotificationProvider(
        ILogger<MacOsNotificationProvider> logger,
        IOptions<NotificationOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        var extensionPath = ResolveExtensionPath(options.Value);
        _notifierPath = Path.Combine(extensionPath, "Contents", "MacOS", "notifier");
    }

    /// <inheritdoc/>
    public string ChannelName => "OS Native (macOS Sidecar)";

    /// <inheritdoc/>
    public NotificationChannelCapabilities Capabilities => new()
    {
        SupportsInteractivity = true,
        SupportsCustomTimeout = false,
        SupportsCustomIcon = false,
        SupportsGrouping = true,
        MaxActions = 4
    };

    /// <inheritdoc/>
    public async Task<Result<Guid>> SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Result<Guid>.Failure("Not running on macOS");
        }

        if (!File.Exists(_notifierPath))
        {
            _logger.LogError("Native notifier binary not found at {Path}", _notifierPath);
            return Result<Guid>.Failure("Native notifier binary not found");
        }

        try
        {
            var payload = new
            {
                id = notification.NotificationId.ToString(),
                title = notification.Title,
                message = notification.Message,
                group = notification.GroupKey,
                actions = notification.Actions.Select(a => new { id = a.ActionId, label = a.Label }).ToArray(),
                pipePath = notification.PipePath  // For IPC with notification actions
            };

            var json = JsonSerializer.Serialize(payload);

            _logger.LogDebug("Launching notifier with payload: {Json}", json);

            var startInfo = new ProcessStartInfo
            {
                FileName = _notifierPath,
                Arguments = $"\"{json.Replace("\"", "\\\"")}\"", // Simple escaping for CLI arg
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Launch fire-and-forget for now, or listen for output if we want to handle actions immediately
            // For this implementation, we'll just send it. 
            // Handling actions requires a persistent listener or a different architecture (e.g. named pipes).
            // Given the CLI nature, we'll start it and let it run.

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<Guid>.Failure("Failed to start notifier process");
            }

            // Read stderr for immediate errors
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            // Wait a short bit to see if it crashes immediately
            await Task.WhenAny(process.WaitForExitAsync(cancellationToken), Task.Delay(500, cancellationToken));

            if (process.HasExited && process.ExitCode != 0)
            {
                var error = await stderrTask;
                _logger.LogError("Notifier exited with code {Code}: {Error}", process.ExitCode, error);
                return Result<Guid>.Failure($"Notifier failed: {error}");
            }

            _logger.LogInformation("Native macOS notification sent: {NotificationId}", notification.NotificationId);
            return Result<Guid>.Success(notification.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send native macOS notification");
            return Result<Guid>.Failure($"Native macOS notification failed: {ex.Message}");
        }
    }

    private string ResolveExtensionPath(NotificationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ExtensionDirectory))
        {
            var overridePath = Path.GetFullPath(options.ExtensionDirectory);
            if (Directory.Exists(overridePath))
            {
                _logger.LogDebug("Using configured macOS extension directory override: {Path}", overridePath);
                return overridePath;
            }

            _logger.LogWarning(
                "Configured macOS extension directory '{Path}' was not found. Falling back to automatic discovery.",
                overridePath);
        }

        // Try relative to executable first (dev/direct install)
        var extensionPath = Path.Combine(AppContext.BaseDirectory, "TenSecondTom.Extensions.MacOS.app");

        // For Homebrew: executable in bin/, extension in prefix/
        if (!Directory.Exists(extensionPath) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Get Cellar path ../TenSecondTom.Extensions.MacOS.app
            var executableDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var cellarPath = Path.GetFullPath(Path.Combine(executableDir, "..", "TenSecondTom.Extensions.MacOS.app"));

            if (Directory.Exists(cellarPath))
            {
                _logger.LogDebug("Using Homebrew macOS extension directory: {Path}", cellarPath);
                return cellarPath;
            }
        }

        return extensionPath;
    }

    /// <inheritdoc/>
    public Task<Result<bool>> IsAvailableAsync(CancellationToken cancellationToken)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return Task.FromResult(Result<bool>.Failure("Not running on macOS"));
        }

        if (!File.Exists(_notifierPath))
        {
            return Task.FromResult(Result<bool>.Failure("Native notifier binary not found"));
        }

        return Task.FromResult(Result<bool>.Success(true));
    }
}
