using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Channels;

/// <summary>
/// A no-operation notification channel that silently succeeds without sending notifications.
/// Used as a fallback when no real notification channels are available or configured.
/// </summary>
/// <remarks>
/// This channel is useful for:
/// - Development environments where notifications aren't needed
/// - CI/CD pipelines where notification systems aren't available
/// - Silent fallback mode when SilentFallback is enabled in NotificationOptions
/// - Testing scenarios where actual notifications would be disruptive
///
/// The channel logs all notification attempts but doesn't display them to the user.
/// </remarks>
public sealed class NoOpNotificationChannel(ILogger<NoOpNotificationChannel> logger) : INotificationChannel
{
    /// <inheritdoc/>
    public string ChannelName => "NoOp";

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
        logger.LogInformation(
            "NoOp channel: Would send notification {NotificationId} - Title: \"{Title}\", Message: \"{Message}\", Priority: {Priority}",
            notification.NotificationId,
            notification.Title,
            notification.Message,
            notification.Priority);

        if (notification.Actions.Count > 0)
        {
            logger.LogInformation(
                "NoOp channel: Notification has {ActionCount} actions (will not be displayed)",
                notification.Actions.Count);

            foreach (var action in notification.Actions)
            {
                logger.LogDebug(
                    "NoOp channel: Action {ActionId} - Label: \"{Label}\", Command: \"{Command}\"",
                    action.ActionId,
                    action.Label,
                    action.Command);
            }
        }

        // Return success with the notification's ID
        return Task.FromResult(Result<Guid>.Success(notification.NotificationId));
    }

    /// <inheritdoc/>
    public Task<Result<bool>> IsAvailableAsync(CancellationToken cancellationToken)
    {
        // NoOp channel is always available
        logger.LogDebug("NoOp channel is available (always returns true)");
        return Task.FromResult(Result<bool>.Success(true));
    }
}
