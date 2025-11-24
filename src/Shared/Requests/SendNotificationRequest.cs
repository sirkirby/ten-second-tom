using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Shared.Requests;

/// <summary>
/// Infrastructure request to send a notification to the user through the configured notification channel.
/// </summary>
/// <remarks>
/// This is a SHARED infrastructure request, not a feature-specific command.
/// Multiple features legitimately need to send notifications (Audio, Generate, etc.).
/// The request is processed by the Notifications feature infrastructure handler.
///
/// This pattern is appropriate for infrastructure concerns that:
/// 1. Have no domain-specific business logic
/// 2. Are used by multiple features
/// 3. Represent cross-cutting capabilities (logging, notifications, metrics)
///
/// Terminal prompts remain the PRIMARY user interface - notifications are enhancements.
/// </remarks>
/// <param name="Title">The notification title (heading). Required. Max 100 characters.</param>
/// <param name="Message">The notification body text. Required. Max 500 characters.</param>
/// <param name="Priority">The urgency level (Low, Normal, High, Critical). Defaults to Normal.</param>
/// <param name="TimeoutSeconds">Auto-dismiss timeout in seconds. Null means no timeout. Range: 1-300 seconds.</param>
/// <param name="Actions">Optional interactive action buttons. macOS does not support interactive buttons.</param>
public sealed record SendNotificationRequest(
    string Title,
    string Message,
    NotificationPriority Priority = NotificationPriority.Normal,
    int? TimeoutSeconds = null,
    IReadOnlyList<NotificationAction>? Actions = null) : IRequest<Result>;
