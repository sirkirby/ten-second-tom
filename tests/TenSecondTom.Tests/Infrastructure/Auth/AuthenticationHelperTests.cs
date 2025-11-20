using FluentAssertions;
using Moq;
using AuthService = TenSecondTom.Infrastructure.Auth.IAuthenticationService;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Auth;

/// <summary>
/// Unit tests for AuthenticationHelper.
/// Tests the centralized authentication orchestration logic.
/// </summary>
public sealed class AuthenticationHelperTests
{
    private readonly Mock<AuthService> _authServiceMock;

    public AuthenticationHelperTests()
    {
        _authServiceMock = new Mock<AuthService>();
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenAlreadyAuthenticated_ReturnsSuccess()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await AuthenticationHelper.EnsureAuthenticatedAsync(
            _authServiceMock.Object,
            CommandNames.Today,
            jsonOutput: false,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _authServiceMock.Verify(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenNotAuthenticated_AttemptsAuthentication()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(new UserSession
            {
                SessionId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                SshKeyHash = "test-hash"
            }));

        // Act
        var result = await AuthenticationHelper.EnsureAuthenticatedAsync(
            _authServiceMock.Object,
            CommandNames.Today,
            jsonOutput: false,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _authServiceMock.Verify(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WhenAuthenticationFails_ReturnsFailure()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("Auth failed"));

        // Act
        var result = await AuthenticationHelper.EnsureAuthenticatedAsync(
            _authServiceMock.Object,
            CommandNames.Today,
            jsonOutput: false,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Auth failed");
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WithJsonOutput_ReturnsJsonFormattedError()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _authServiceMock.Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("Auth failed"));

        // Capture console output
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        // Act
        var result = await AuthenticationHelper.EnsureAuthenticatedAsync(
            _authServiceMock.Object,
            CommandNames.Today,
            jsonOutput: true,
            CancellationToken.None);

        Console.SetOut(originalOut);
        var output = writer.ToString();

        // Assert
        result.IsSuccess.Should().BeFalse();
        output.Should().Contain("\"success\":false");
        output.Should().Contain(CommandNames.Today);
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WithException_HandlesGracefully()
    {
        // Arrange
        _authServiceMock.Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection error"));

        // Act
        var result = await AuthenticationHelper.EnsureAuthenticatedAsync(
            _authServiceMock.Object,
            CommandNames.Today,
            jsonOutput: false,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Connection error");
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WithNullAuthService_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await AuthenticationHelper.EnsureAuthenticatedAsync(
            null!,
            CommandNames.Today,
            jsonOutput: false,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnsureAuthenticatedAsync_WithNullCommandName_ThrowsArgumentNullException()
    {
        // Act
        Func<Task> act = async () => await AuthenticationHelper.EnsureAuthenticatedAsync(
            _authServiceMock.Object,
            null!,
            jsonOutput: false,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
