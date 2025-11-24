using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Abstractions.Notifications;

/// <summary>
/// Service for sending notifications to users through various channels.
/// Supports both simple text notifications and interactive notifications with action buttons.
/// </summary>
/// <remarks>
/// This is the primary interface for features to send notifications.
/// The service automatically handles:
/// - Channel selection and routing
/// - Security token generation for interactive actions
/// - Fallback when notifications fail (if configured)
/// - Logging and error handling
/// </remarks>
public interface INotificationService
{
    /// <summary>
    /// Sends a basic text notification to the user.
    /// </summary>
    /// <param name="notification">The notification to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result indicating success or failure. Success includes the notification ID.
    /// Failure includes an error message describing what went wrong.
    /// </returns>
    Task<Result<Guid>> SendAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an interactive notification with action buttons to the user.
    /// Actions are automatically secured with tokens to prevent tampering.
    /// </summary>
    /// <param name="notification">The interactive notification to send (must have Actions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result indicating success or failure. Success includes the notification ID.
    /// Failure includes an error message describing what went wrong.
    /// </returns>
    /// <remarks>
    /// The notification's actions will be automatically secured with HMAC tokens before sending.
    /// When the user clicks an action, the callback URL will include this token for validation.
    /// </remarks>
    Task<Result<Guid>> SendInteractiveAsync(Notification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the current state of a notification.
    /// </summary>
    /// <param name="notificationId">The ID of the notification to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result containing the notification's current state, or a failure if the notification doesn't exist.
    /// </returns>
    Task<Result<NotificationState>> GetStateAsync(Guid notificationId, CancellationToken cancellationToken);
}
