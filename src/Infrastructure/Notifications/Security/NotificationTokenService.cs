using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Notifications.Security;

/// <summary>
/// Implementation of <see cref="INotificationTokenService"/> using HMAC-SHA256 for token signing.
/// </summary>
/// <remarks>
/// Token format: Base64(JsonPayload + "." + Base64(HMAC-SHA256(JsonPayload)))
/// The HMAC signature ensures that tokens cannot be tampered with.
/// The timestamp in the payload enables expiration checking.
/// </remarks>
public sealed class NotificationTokenService(
    IOptions<SecurityOptions> securityOptions,
    ILogger<NotificationTokenService> logger) : INotificationTokenService
{
    private readonly SecurityOptions _securityOptions = securityOptions.Value;

    /// <inheritdoc/>
    public string GenerateToken(Guid notificationId, string actionId)
    {
        var payload = NotificationTokenPayload.Create(notificationId, actionId);
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        // Generate HMAC signature
        var signature = GenerateSignature(payloadBytes);

        // Combine payload and signature
        var token = $"{Convert.ToBase64String(payloadBytes)}.{Convert.ToBase64String(signature)}";

        logger.LogDebug(
            "Generated token for notification {NotificationId}, action {ActionId}",
            notificationId,
            actionId);

        return token;
    }

    /// <inheritdoc/>
    public Result<NotificationTokenPayload> ValidateToken(
        string token,
        Guid expectedNotificationId,
        string expectedActionId)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<NotificationTokenPayload>.Failure("Token is null or empty.");
        }

        // Split token into payload and signature
        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            logger.LogWarning("Invalid token format: expected 2 parts, got {Count}", parts.Length);
            return Result<NotificationTokenPayload>.Failure("Invalid token format.");
        }

        byte[] payloadBytes;
        byte[] signatureBytes;

        try
        {
            payloadBytes = Convert.FromBase64String(parts[0]);
            signatureBytes = Convert.FromBase64String(parts[1]);
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "Failed to decode base64 token");
            return Result<NotificationTokenPayload>.Failure("Invalid token encoding.");
        }

        // Verify HMAC signature
        var expectedSignature = GenerateSignature(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(signatureBytes, expectedSignature))
        {
            logger.LogWarning(
                "Token signature verification failed for notification {NotificationId}",
                expectedNotificationId);
            return Result<NotificationTokenPayload>.Failure("Token signature is invalid (possible tampering detected).");
        }

        // Deserialize payload
        NotificationTokenPayload payload;
        try
        {
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            payload = JsonSerializer.Deserialize<NotificationTokenPayload>(payloadJson)
                ?? throw new JsonException("Deserialized payload is null");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize token payload");
            return Result<NotificationTokenPayload>.Failure("Invalid token payload format.");
        }

        // Validate token age
        var tokenAge = DateTimeOffset.UtcNow - payload.CreatedAt;
        if (tokenAge.TotalSeconds > _securityOptions.MaxTokenAgeSeconds)
        {
            logger.LogWarning(
                "Token expired: age {TokenAgeSeconds}s exceeds maximum {MaxAgeSeconds}s",
                tokenAge.TotalSeconds,
                _securityOptions.MaxTokenAgeSeconds);
            return Result<NotificationTokenPayload>.Failure(
                $"Token has expired (age: {tokenAge.TotalSeconds:F0}s, max: {_securityOptions.MaxTokenAgeSeconds}s).");
        }

        // Validate notification ID
        if (payload.NotificationId != expectedNotificationId)
        {
            logger.LogWarning(
                "Token notification ID mismatch: expected {Expected}, got {Actual}",
                expectedNotificationId,
                payload.NotificationId);
            return Result<NotificationTokenPayload>.Failure("Token notification ID does not match.");
        }

        // Validate action ID
        if (payload.ActionId != expectedActionId)
        {
            logger.LogWarning(
                "Token action ID mismatch: expected {Expected}, got {Actual}",
                expectedActionId,
                payload.ActionId);
            return Result<NotificationTokenPayload>.Failure("Token action ID does not match.");
        }

        logger.LogDebug(
            "Successfully validated token for notification {NotificationId}, action {ActionId}",
            expectedNotificationId,
            expectedActionId);

        return Result<NotificationTokenPayload>.Success(payload);
    }

    private byte[] GenerateSignature(byte[] payloadBytes)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_securityOptions.NotificationSecret);
        return HMACSHA256.HashData(keyBytes, payloadBytes);
    }
}
