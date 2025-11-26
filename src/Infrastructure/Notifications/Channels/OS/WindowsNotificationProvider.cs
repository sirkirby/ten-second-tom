using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Channels.OS;

/// <summary>
/// Windows notification channel stub (unavailable in current build).
/// This is a runtime stub that indicates Windows notifications are not supported
/// in the current net10.0 single-target build.
/// </summary>
/// <remarks>
/// <para>
/// This stub is registered in DI but will report itself as unavailable when not
/// running on Windows. This design allows for a single binary that gracefully
/// degrades rather than failing at startup.
/// </para>
/// <para>
/// For Windows notification support, see WINDOWS-IMPLEMENTATION.md in the project
/// documentation, which outlines the approach for multi-targeting builds that can
/// provide native Windows Toast notifications.
/// </para>
/// <para>
/// <strong>Implementation Strategy:</strong>
/// </para>
/// <list type="bullet">
/// <item>This class is always compiled (no conditional compilation)</item>
/// <item>Runtime platform detection determines availability</item>
/// <item>Clear error messages guide users to future Windows support</item>
/// <item>No dependencies on Windows-specific packages in this build</item>
/// </list>
/// </remarks>
public sealed class WindowsNotificationProvider : INotificationChannel
{
    private readonly ILogger<WindowsNotificationProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsNotificationProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public WindowsNotificationProvider(ILogger<WindowsNotificationProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public string ChannelName => "OS Native (Windows - Unavailable)";

    /// <inheritdoc/>
    public NotificationChannelCapabilities Capabilities => new()
    {
        SupportsInteractivity = false,
        SupportsCustomTimeout = false,
        SupportsCustomIcon = false,
        SupportsGrouping = false,
        MaxActions = 0
    };

    /// <inheritdoc/>
    public Task<Result<Guid>> SendAsync(Notification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        _logger.LogWarning(
            "Attempted to send Windows notification in unsupported build: NotificationId={NotificationId}",
            notification.NotificationId);

        var errorMessage = "Windows notifications are not available in the current build. " +
                          "For Windows notification support, a multi-targeting build is required. " +
                          "See WINDOWS-IMPLEMENTATION.md for implementation details.";

        return Task.FromResult(Result<Guid>.Failure(errorMessage));
    }

    /// <inheritdoc/>
    public Task<Result<bool>> IsAvailableAsync(CancellationToken cancellationToken)
    {
        // This channel is never available in the net10.0 build
        // Future multi-targeted builds (net10.0-windows) would check:
        // RuntimeInformation.IsOSPlatform(OSPlatform.Windows)

        // Log at Debug level to avoid noise on Windows where notifications aren't yet supported
        // Users can enable verbose logging if they want to see this message
        _logger.LogDebug(
            "Windows notification channel not available: {Reason}",
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Current build is net10.0 (cross-platform). Windows notifications require net10.0-windows build with WinRT support."
                : "Not running on Windows platform");

        return Task.FromResult(Result<bool>.Failure("Windows notifications not supported in current build"));
    }
}
