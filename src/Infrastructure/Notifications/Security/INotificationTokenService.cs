using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Security;

/// <summary>
/// Service for generating and validating secure tokens for notification actions.
/// Tokens prevent tampering with notification callback URLs and ensure authenticity.
/// </summary>
/// <remarks>
/// Token security features:
/// - HMAC-SHA256 signatures to prevent tampering
/// - Timestamp validation to prevent replay attacks
/// - Notification ID binding to prevent token reuse across notifications
/// - Action ID binding to prevent action substitution
/// </remarks>
public interface INotificationTokenService
{
    /// <summary>
    /// Generates a secure token for a notification action.
    /// </summary>
    /// <param name="notificationId">The ID of the notification.</param>
    /// <param name="actionId">The ID of the action.</param>
    /// <returns>
    /// A base64-encoded token containing the payload and HMAC signature.
    /// This token should be included in the callback URL for the action.
    /// </returns>
    /// <remarks>
    /// The token includes:
    /// - Notification ID: Binds token to specific notification
    /// - Action ID: Binds token to specific action
    /// - Timestamp: Enables expiration checking
    /// - HMAC signature: Prevents tampering
    /// </remarks>
    string GenerateToken(Guid notificationId, string actionId);

    /// <summary>
    /// Validates a token from a notification action callback.
    /// </summary>
    /// <param name="token">The base64-encoded token from the callback URL.</param>
    /// <param name="expectedNotificationId">The expected notification ID.</param>
    /// <param name="expectedActionId">The expected action ID.</param>
    /// <returns>
    /// A result indicating success (token is valid) or failure (token is invalid, with reason).
    /// </returns>
    /// <remarks>
    /// Validation checks:
    /// 1. Token format is valid base64
    /// 2. Token payload can be deserialized
    /// 3. HMAC signature is valid (prevents tampering)
    /// 4. Token has not expired (prevents replay attacks)
    /// 5. Notification ID matches expected value
    /// 6. Action ID matches expected value
    /// </remarks>
    Result<NotificationTokenPayload> ValidateToken(string token, Guid expectedNotificationId, string expectedActionId);
}
