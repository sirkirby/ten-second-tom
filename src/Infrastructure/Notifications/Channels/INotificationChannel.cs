using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Channels;

/// <summary>
/// Represents a channel through which notifications can be sent (OS native, Slack, etc.).
/// Each channel implementation handles the specifics of displaying notifications on that platform.
/// </summary>
/// <remarks>
/// Channel implementations should:
/// - Check their capabilities before attempting to send notifications
/// - Return clear error messages when notifications fail
/// - Handle platform-specific edge cases gracefully
/// - Log detailed diagnostic information for troubleshooting
/// </remarks>
public interface INotificationChannel
{
    /// <summary>
    /// Gets the name of this notification channel (e.g., "OSNative", "Slack").
    /// Used for logging and diagnostics.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Gets the capabilities of this notification channel.
    /// Used to determine if this channel can handle a given notification.
    /// </summary>
    NotificationChannelCapabilities Capabilities { get; }

    /// <summary>
    /// Sends a notification through this channel.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result indicating success (with notification ID) or failure (with error message).
    /// </returns>
    /// <remarks>
    /// Implementations should:
    /// 1. Validate that the notification is compatible with the channel's capabilities
    /// 2. Transform the notification into the channel's required format
    /// 3. Send the notification through the channel's API/system
    /// 4. Return success with the notification ID or failure with a clear error message
    /// </remarks>
    Task<Result<Guid>> SendAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if this channel is available and properly configured.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result indicating success (channel is available) or failure (channel is not available, with reason).
    /// </returns>
    /// <remarks>
    /// This should perform lightweight checks (e.g., configuration validation, OS compatibility).
    /// It should NOT make network calls or perform expensive operations.
    /// </remarks>
    Task<Result<bool>> IsAvailableAsync(CancellationToken cancellationToken);
}
