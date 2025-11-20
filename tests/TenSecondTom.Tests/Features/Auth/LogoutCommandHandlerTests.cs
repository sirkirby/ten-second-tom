using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Auth;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Auth;

/// <summary>
/// Unit tests for <see cref="Logout.Handler"/>.
/// Tests the logout command handler's ability to invalidate user sessions.
/// </summary>
public sealed class LogoutCommandHandlerTests
{
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<ILogger<Logout.Handler>> _mockLogger;
    private readonly Logout.Handler _handler;

    public LogoutCommandHandlerTests()
    {
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockLogger = new Mock<ILogger<Logout.Handler>>();
        _handler = new Logout.Handler(_mockAuthService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithActiveSession_LogsOutSuccessfully()
    {
        // Arrange
        var command = new Logout.Command();
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _mockAuthService.Verify(x => x.LogoutAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNoActiveSession_ReturnsError()
    {
        // Arrange
        var command = new Logout.Command();
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("No active session to logout."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No active session to logout.");
        _mockAuthService.Verify(x => x.LogoutAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAuthServiceFails_ReturnsError()
    {
        // Arrange
        var command = new Logout.Command();
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("Session file could not be deleted."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Session file could not be deleted");
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        // Arrange
        var command = new Logout.Command();
        using var cancellationTokenSource = new CancellationTokenSource();
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        // Act
        await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        _mockAuthService.Verify(
            x => x.LogoutAsync(cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsLogoutAttempt()
    {
        // Arrange
        var command = new Logout.Command();
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Logging out user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsSuccessfulLogout()
    {
        // Arrange
        var command = new Logout.Command();
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User logged out successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsFailedLogout()
    {
        // Arrange
        var command = new Logout.Command();
        var errorMessage = "No active session to logout.";
        _mockAuthService
            .Setup(x => x.LogoutAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure(errorMessage));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Logout failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
