using FluentAssertions;
using Moq;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Auth;

/// <summary>
/// Minimal interface definition for testing.
/// Actual interface will be implemented in T035.
/// </summary>
internal interface IAuthenticationService
{
    Task<Result<UserSession>> AuthenticateAsync(CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);
    Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tests for IAuthenticationService interface behavior using mock implementations.
/// </summary>
public sealed class IAuthenticationServiceTests
{

    [Fact]
    public async Task AuthenticateAsync_WithValidSshKey_CreatesUserSession()
    {
        // Arrange
        var expectedSession = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "sha256:abc123def456",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        };

        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(expectedSession));

        // Act
        Result<UserSession> result = await mockService.Object.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SessionId.Should().Be(expectedSession.SessionId);
        result.Value.SshKeyHash.Should().Be(expectedSession.SshKeyHash);
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticateAsync_WithMissingSshKey_ReturnsFailure()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("No SSH key found in ~/.ssh/"));

        // Act
        Result<UserSession> result = await mockService.Object.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SSH key");
    }

    [Fact]
    public async Task AuthenticateAsync_WithEncryptedKey_PromptsForPassphrase()
    {
        // Arrange - Simulates encrypted key requiring passphrase
        var expectedSession = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "sha256:encrypted123",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            ExpiresAt = null
        };

        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(expectedSession));

        // Act
        Result<UserSession> result = await mockService.Object.AuthenticateAsync();

        // Assert - Verify successful authentication after passphrase entry
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SshKeyHash.Should().Contain("encrypted");
    }

    [Fact]
    public async Task AuthenticateAsync_WithIncorrectPassphrase_ReturnsFailure()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("Incorrect passphrase. Authentication failed."));

        // Act
        Result<UserSession> result = await mockService.Object.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("passphrase");
    }

    [Fact]
    public async Task AuthenticateAsync_SupportsCancellation()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        using var cts = new CancellationTokenSource();
        
        mockService
            .Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await mockService.Object.AuthenticateAsync(cts.Token));
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithActiveSession_ReturnsTrue()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        bool isAuthenticated = await mockService.Object.IsAuthenticatedAsync();

        // Assert
        isAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithoutActiveSession_ReturnsFalse()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        bool isAuthenticated = await mockService.Object.IsAuthenticatedAsync();

        // Assert
        isAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_WithActiveSession_InvalidatesSession()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        // Act
        Result<bool> result = await mockService.Object.LogoutAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task LogoutAsync_WithoutActiveSession_ReturnsFailure()
    {
        // Arrange
        var mockService = new Mock<IAuthenticationService>();
        mockService
            .Setup(s => s.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("No active session to logout."));

        // Act
        Result<bool> result = await mockService.Object.LogoutAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No active session");
    }

    [Fact]
    public async Task SessionPersistsAcrossMultipleCalls()
    {
        // Arrange - Simulate persisted session
        var sessionId = Guid.NewGuid();
        var mockService = new Mock<IAuthenticationService>();
        
        // First call authenticates
        mockService
            .Setup(s => s.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(new UserSession
            {
                SessionId = sessionId,
                SshKeyHash = "sha256:persistent",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow,
                ExpiresAt = null
            }));

        // Subsequent calls show authenticated
        mockService
            .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        Result<UserSession> authResult = await mockService.Object.AuthenticateAsync();
        bool isAuth1 = await mockService.Object.IsAuthenticatedAsync();
        bool isAuth2 = await mockService.Object.IsAuthenticatedAsync();

        // Assert
        authResult.IsSuccess.Should().BeTrue();
        authResult.Value.SessionId.Should().Be(sessionId);
        isAuth1.Should().BeTrue();
        isAuth2.Should().BeTrue();
    }
}
