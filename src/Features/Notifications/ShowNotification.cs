using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.Notifications;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Notifications;

/// <summary>
/// Sends a notification to the user through the configured notification channel.
/// Supports both simple text notifications and interactive notifications with action buttons.
/// </summary>
/// <remarks>
/// This feature provides a unified interface for sending notifications across platforms.
/// The notification service handles channel selection, fallback, and platform-specific formatting.
/// Terminal prompts should remain the PRIMARY user interface - notifications are enhancements.
/// </remarks>
public static class ShowNotification
{
    /// <summary>
    /// Command to display a notification to the user.
    /// </summary>
    /// <param name="Title">The notification title (heading). Required. Max 100 characters.</param>
    /// <param name="Message">The notification body text. Required. Max 500 characters.</param>
    /// <param name="Priority">The urgency level (Low, Normal, High, Critical). Defaults to Normal.</param>
    /// <param name="TimeoutSeconds">Auto-dismiss timeout in seconds. Null means no timeout. Range: 1-300 seconds.</param>
    /// <param name="Actions">Optional interactive action buttons. macOS does not support interactive buttons.</param>
    public sealed record Command(
        string Title,
        string Message,
        NotificationPriority Priority = NotificationPriority.Normal,
        int? TimeoutSeconds = null,
        IReadOnlyList<NotificationAction>? Actions = null) : IRequest<Result>;

    /// <summary>
    /// Validates the ShowNotification command.
    /// Auto-discovered by FluentValidation assembly scanning.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        private const int MaxTitleLength = 100;
        private const int MaxMessageLength = 500;
        private const int MinTimeout = 1;
        private const int MaxTimeout = 300; // 5 minutes

        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator()
        {
            RuleFor(x => x.Title)
                .NotEmpty()
                .WithMessage("Notification title is required")
                .MaximumLength(MaxTitleLength)
                .WithMessage($"Notification title must not exceed {MaxTitleLength} characters");

            RuleFor(x => x.Message)
                .NotEmpty()
                .WithMessage("Notification message is required")
                .MaximumLength(MaxMessageLength)
                .WithMessage($"Notification message must not exceed {MaxMessageLength} characters");

            RuleFor(x => x.Priority)
                .IsInEnum()
                .WithMessage("Priority must be a valid NotificationPriority value");

            RuleFor(x => x.TimeoutSeconds)
                .InclusiveBetween(MinTimeout, MaxTimeout)
                .When(x => x.TimeoutSeconds.HasValue)
                .WithMessage($"Timeout must be between {MinTimeout} and {MaxTimeout} seconds");

            RuleFor(x => x.Actions)
                .Must(actions => actions == null || actions.Count <= 4)
                .WithMessage("Maximum of 4 actions are supported")
                .Must(actions => actions == null || actions.All(a => !string.IsNullOrWhiteSpace(a.ActionId)))
                .WithMessage("All actions must have a valid ActionId")
                .Must(actions => actions == null || actions.All(a => !string.IsNullOrWhiteSpace(a.Label)))
                .WithMessage("All actions must have a valid Label");
        }
    }

    /// <summary>
    /// Handles the ShowNotification command by sending the notification through the notification service.
    /// Auto-discovered by MediatR assembly scanning.
    /// </summary>
    public sealed class Handler(
        INotificationService notificationService,
        ILogger<Handler> logger) : IRequestHandler<Command, Result>
    {
        /// <summary>
        /// Handles the ShowNotification command by creating and sending the notification.
        /// </summary>
        /// <param name="request">The notification command containing display parameters.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success or failure.
        /// Success means the notification was sent (may still fail to display on the OS side).
        /// Failure indicates the notification service rejected the request.
        /// </returns>
        public async Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            logger.LogInformation(
                "Sending notification: Title='{Title}', Priority={Priority}, Timeout={Timeout}s, HasActions={HasActions}",
                request.Title,
                request.Priority,
                request.TimeoutSeconds,
                request.Actions?.Count > 0);

            try
            {
                // Create notification model
                var notification = new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    Title = request.Title,
                    Message = request.Message,
                    Priority = request.Priority,
                    TimeoutSeconds = request.TimeoutSeconds,
                    Actions = request.Actions ?? [],
                    State = NotificationState.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                // Send notification (interactive vs non-interactive)
                Result<Guid> sendResult;
                if (request.Actions?.Count > 0)
                {
                    logger.LogDebug(
                        "Sending interactive notification with {ActionCount} actions",
                        request.Actions.Count);
                    sendResult = await notificationService.SendInteractiveAsync(notification, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    logger.LogDebug("Sending basic notification");
                    sendResult = await notificationService.SendAsync(notification, cancellationToken)
                        .ConfigureAwait(false);
                }

                // Check result
                if (sendResult.IsSuccess)
                {
                    logger.LogInformation(
                        "Notification sent successfully: NotificationId={NotificationId}",
                        sendResult.Value);
                    return Result.Success();
                }

                logger.LogWarning(
                    "Failed to send notification: {Error}",
                    sendResult.Error);
                return Result.Failure(sendResult.Error ?? "Failed to send notification");
            }
            catch (Exception ex)
            {
                const string errorMessage = "An unexpected error occurred while sending the notification.";
                logger.LogError(ex, errorMessage);
                return Result.Failure(errorMessage);
            }
        }
    }
}
