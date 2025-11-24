using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Notifications.Security;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.Infrastructure.Notifications;

/// <summary>
/// Unit tests for <see cref="NotificationTokenService"/>.
/// Tests token generation, validation, and security features.
/// </summary>
public sealed class NotificationTokenServiceTests
{
    private readonly Mock<ILogger<NotificationTokenService>> _mockLogger;
    private readonly SecurityOptions _securityOptions;
    private readonly NotificationTokenService _tokenService;

    public NotificationTokenServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationTokenService>>();
        _securityOptions = new SecurityOptions
        {
            NotificationSecret = "test-secret-key-with-minimum-16-chars",
            MaxTokenAgeSeconds = 300
        };
        var options = Options.Create(_securityOptions);
        _tokenService = new NotificationTokenService(options, _mockLogger.Object);
    }

    [Fact]
    public void GenerateToken_WithValidInputs_ReturnsSignedToken()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";

        // Act
        var token = _tokenService.GenerateToken(notificationId, actionId);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        token.Should().Contain("."); // Token format: payload.signature
        var parts = token.Split('.');
        parts.Should().HaveCount(2);
        parts[0].Should().NotBeNullOrWhiteSpace(); // Payload
        parts[1].Should().NotBeNullOrWhiteSpace(); // Signature
    }

    [Fact]
    public void ValidateToken_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";
        var token = _tokenService.GenerateToken(notificationId, actionId);

        // Act
        var result = _tokenService.ValidateToken(token, notificationId, actionId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NotificationId.Should().Be(notificationId);
        result.Value.ActionId.Should().Be(actionId);
        result.Value.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ValidateToken_WithExpiredToken_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";

        // Create a service with very short token age
        var shortAgeOptions = new SecurityOptions
        {
            NotificationSecret = "test-secret-key-with-minimum-16-chars",
            MaxTokenAgeSeconds = 0 // Tokens expire immediately
        };
        var shortAgeService = new NotificationTokenService(
            Options.Create(shortAgeOptions),
            _mockLogger.Object);

        var token = shortAgeService.GenerateToken(notificationId, actionId);

        // Wait a tiny bit to ensure token is expired
        Thread.Sleep(100);

        // Act
        var result = shortAgeService.ValidateToken(token, notificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public void ValidateToken_WithTamperedToken_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";
        var token = _tokenService.GenerateToken(notificationId, actionId);

        // Tamper with the token by modifying a character in the payload
        var tamperedToken = token.Replace("A", "B");

        // Act
        var result = _tokenService.ValidateToken(tamperedToken, notificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("signature is invalid");
    }

    [Fact]
    public void ValidateToken_WithMismatchedNotificationId_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var differentNotificationId = Guid.NewGuid();
        var actionId = "test-action";
        var token = _tokenService.GenerateToken(notificationId, actionId);

        // Act
        var result = _tokenService.ValidateToken(token, differentNotificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("notification ID does not match");
    }

    [Fact]
    public void ValidateToken_WithMismatchedActionId_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";
        var differentActionId = "different-action";
        var token = _tokenService.GenerateToken(notificationId, actionId);

        // Act
        var result = _tokenService.ValidateToken(token, notificationId, differentActionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("action ID does not match");
    }

    [Fact]
    public void ValidateToken_WithNullToken_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";

        // Act
        var result = _tokenService.ValidateToken(null!, notificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public void ValidateToken_WithEmptyToken_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";

        // Act
        var result = _tokenService.ValidateToken(string.Empty, notificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public void ValidateToken_WithInvalidTokenFormat_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";
        var invalidToken = "not-a-valid-token"; // Missing the dot separator

        // Act
        var result = _tokenService.ValidateToken(invalidToken, notificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Invalid token format");
    }

    [Fact]
    public void ValidateToken_WithInvalidBase64Encoding_ReturnsFailure()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";
        var invalidToken = "!!!invalid-base64!!!.!!!invalid-base64!!!";

        // Act
        var result = _tokenService.ValidateToken(invalidToken, notificationId, actionId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("encoding");
    }

    [Fact]
    public void GenerateToken_WithDifferentInputs_GeneratesDifferentTokens()
    {
        // Arrange
        var notificationId1 = Guid.NewGuid();
        var notificationId2 = Guid.NewGuid();
        var actionId = "test-action";

        // Act
        var token1 = _tokenService.GenerateToken(notificationId1, actionId);
        var token2 = _tokenService.GenerateToken(notificationId2, actionId);

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateToken_CalledMultipleTimes_GeneratesDifferentTokens()
    {
        // Arrange
        var notificationId = Guid.NewGuid();
        var actionId = "test-action";

        // Act
        var token1 = _tokenService.GenerateToken(notificationId, actionId);
        Thread.Sleep(10); // Ensure different timestamp
        var token2 = _tokenService.GenerateToken(notificationId, actionId);

        // Assert
        // Tokens should be different due to different timestamps
        token1.Should().NotBe(token2);
    }
}
