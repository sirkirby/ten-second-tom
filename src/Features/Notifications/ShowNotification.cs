using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Abstractions.Notifications;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Requests;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Notifications;

/// <summary>
/// Infrastructure handler for sending notifications to the user through the configured notification channel.
/// Processes <see cref="SendNotificationRequest"/> from any feature.
/// </summary>
/// <remarks>
/// This feature provides a unified interface for sending notifications across platforms.
/// The notification service handles channel selection, fallback, and platform-specific formatting.
/// Terminal prompts should remain the PRIMARY user interface - notifications are enhancements.
/// </remarks>
public static class ShowNotification
{
    /// <summary>
    /// Validates the SendNotificationRequest.
    /// Auto-discovered by FluentValidation assembly scanning.
    /// </summary>
    public sealed class Validator : AbstractValidator<SendNotificationRequest>
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
    /// Handles the SendNotificationRequest by sending the notification through the notification service.
    /// Auto-discovered by MediatR assembly scanning.
    /// </summary>
    public sealed class Handler(
        INotificationService notificationService,
        ILogger<Handler> logger) : IRequestHandler<SendNotificationRequest, Result>
    {
        /// <summary>
        /// Handles the SendNotificationRequest by creating and sending the notification.
        /// </summary>
        /// <param name="request">The notification request containing display parameters.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success or failure.
        /// Success means the notification was sent (may still fail to display on the OS side).
        /// Failure indicates the notification service rejected the request.
        /// </returns>
        public async Task<Result> Handle(
            SendNotificationRequest request,
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
