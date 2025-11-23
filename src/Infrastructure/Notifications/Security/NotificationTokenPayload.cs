namespace TenSecondTom.Infrastructure.Notifications.Security;

/// <summary>
/// Represents the data contained in a notification action security token.
/// This payload is serialized, signed with HMAC, and embedded in notification callback URLs.
/// </summary>
/// <remarks>
/// Security properties:
/// - Immutable record prevents tampering after creation
/// - Timestamp enables expiration checking
/// - Notification ID and Action ID binding prevents token reuse
/// - Combined with HMAC signature to detect tampering
/// </remarks>
public sealed record NotificationTokenPayload
{
    /// <summary>
    /// Gets the ID of the notification this token is valid for.
    /// Prevents tokens from being reused across different notifications.
    /// </summary>
    public required Guid NotificationId { get; init; }

    /// <summary>
    /// Gets the ID of the action this token authorizes.
    /// Prevents tokens from being used to trigger different actions.
    /// </summary>
    public required string ActionId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this token was created.
    /// Used to validate that the token has not expired.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Creates a new token payload for the specified notification and action.
    /// </summary>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="actionId">The action ID.</param>
    /// <returns>A new token payload with the current UTC timestamp.</returns>
    public static NotificationTokenPayload Create(Guid notificationId, string actionId) => new()
    {
        NotificationId = notificationId,
        ActionId = actionId,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
