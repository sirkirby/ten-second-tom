using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Notifications;

/// <summary>
/// Handles user actions triggered from interactive notifications.
/// Routes notification callbacks to appropriate feature handlers.
/// </summary>
/// <remarks>
/// This feature serves as the entry point for notification action callbacks.
/// When a user clicks an action button in a notification (Windows only, macOS does not support this),
/// the notification system invokes this handler with the notification and action IDs.
///
/// Currently acts as a placeholder for future Windows interactive notification support.
/// macOS notifications do not support interactive action buttons.
/// </remarks>
public static class HandleNotificationAction
{
    /// <summary>
    /// Command to handle a user action triggered from a notification.
    /// </summary>
    /// <param name="NotificationId">The ID of the notification that triggered the action.</param>
    /// <param name="ActionId">The ID of the specific action that was clicked.</param>
    public sealed record Command(
        Guid NotificationId,
        string ActionId) : IRequest<Result>;

    /// <summary>
    /// Validates the HandleNotificationAction command.
    /// Auto-discovered by FluentValidation assembly scanning.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Validator"/> class.
        /// </summary>
        public Validator()
        {
            RuleFor(x => x.NotificationId)
                .NotEmpty()
                .WithMessage("NotificationId is required");

            RuleFor(x => x.ActionId)
                .NotEmpty()
                .WithMessage("ActionId is required")
                .MaximumLength(100)
                .WithMessage("ActionId must not exceed 100 characters");
        }
    }

    /// <summary>
    /// Handles notification action callbacks by routing to appropriate feature handlers.
    /// Auto-discovered by MediatR assembly scanning.
    /// </summary>
    /// <remarks>
    /// This is a placeholder implementation for future Windows interactive notification support.
    /// Currently, it only logs the action and returns success.
    ///
    /// Future enhancements:
    /// - Parse ActionId to determine target feature
    /// - Route to appropriate feature handler (e.g., record continuation, template selection)
    /// - Validate security tokens
    /// - Track action history
    /// </remarks>
    public sealed class Handler(
        ILogger<Handler> logger) : IRequestHandler<Command, Result>
    {
        /// <summary>
        /// Handles the notification action callback.
        /// </summary>
        /// <param name="request">The action command containing notification and action IDs.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>
        /// A <see cref="Result"/> indicating success or failure.
        /// Success means the action was recognized and processed.
        /// Failure indicates the action could not be handled.
        /// </returns>
        public Task<Result> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            logger.LogInformation(
                "Notification action received: NotificationId={NotificationId}, ActionId={ActionId}",
                request.NotificationId,
                request.ActionId);

            // TODO: Parse ActionId and route to appropriate feature handler
            // Example ActionId formats:
            //   - "record.continue" -> Route to Record feature to extend session
            //   - "template.select.daily" -> Route to Template feature to apply template
            //   - "note.save" -> Route to Note feature to save draft

            try
            {
                // Placeholder: Log the action for future implementation
                logger.LogWarning(
                    "Notification action not implemented yet. " +
                    "ActionId='{ActionId}' will be processed when Windows interactive support is added.",
                    request.ActionId);

                // For now, simply acknowledge receipt
                logger.LogInformation(
                    "Notification action acknowledged: NotificationId={NotificationId}, ActionId={ActionId}",
                    request.NotificationId,
                    request.ActionId);

                return Task.FromResult(Result.Success());
            }
            catch (Exception ex)
            {
                const string errorMessage = "An unexpected error occurred while handling the notification action.";
                logger.LogError(ex, "{ErrorMessage} ActionId={ActionId}", errorMessage, request.ActionId);
                return Task.FromResult(Result.Failure(errorMessage));
            }
        }
    }
}
