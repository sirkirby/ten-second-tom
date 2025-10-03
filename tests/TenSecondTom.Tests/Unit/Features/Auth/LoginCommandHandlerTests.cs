using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Auth.Commands;
using TenSecondTom.Features.Auth.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Features.Auth;

/// <summary>
/// Unit tests for <see cref="LoginCommandHandler"/>.
/// Tests the login command handler's ability to authenticate users.
/// </summary>
public sealed class LoginCommandHandlerTests
{
    private readonly Mock<IAuthenticationService> _mockAuthService;
    private readonly Mock<ILogger<LoginCommandHandler>> _mockLogger;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _mockAuthService = new Mock<IAuthenticationService>();
        _mockLogger = new Mock<ILogger<LoginCommandHandler>>();
        _handler = new LoginCommandHandler(_mockAuthService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCredentials_AuthenticatesSuccessfully()
    {
        // Arrange
        var command = new LoginCommand();
        var expectedSession = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "test-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(expectedSession));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedSession);
        _mockAuthService.Verify(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlreadyAuthenticated_ReturnsExistingSession()
    {
        // Arrange
        var command = new LoginCommand();
        var existingSession = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "existing-hash",
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            LastAccessedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _mockAuthService
            .Setup(x => x.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(existingSession));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingSession);
    }

    [Fact]
    public async Task Handle_WithMissingSshKey_ReturnsError()
    {
        // Arrange
        var command = new LoginCommand();
        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("No SSH key found in ~/.ssh/"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No SSH key found");
    }

    [Fact]
    public async Task Handle_WithIncorrectPassphrase_ReturnsError()
    {
        // Arrange
        var command = new LoginCommand();
        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("Incorrect passphrase. 2 attempts remaining."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Incorrect passphrase");
    }

    [Fact]
    public async Task Handle_WhenAuthenticationFails_ReturnsError()
    {
        // Arrange
        var command = new LoginCommand();
        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure("Authentication failed after 3 attempts."));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Authentication failed");
    }

    [Fact]
    public async Task Handle_PropagatesCancellationToken()
    {
        // Arrange
        var command = new LoginCommand();
        using var cancellationTokenSource = new CancellationTokenSource();
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "test-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(session));

        // Act
        await _handler.Handle(command, cancellationTokenSource.Token);

        // Assert
        _mockAuthService.Verify(
            x => x.AuthenticateAsync(cancellationTokenSource.Token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsLoginAttempt()
    {
        // Arrange
        var command = new LoginCommand();
        var session = new UserSession
        {
            SessionId = Guid.NewGuid(),
            SshKeyHash = "test-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(session));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempting to authenticate user")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsSuccessfulLogin()
    {
        // Arrange
        var command = new LoginCommand();
        var sessionId = Guid.NewGuid();
        var session = new UserSession
        {
            SessionId = sessionId,
            SshKeyHash = "test-hash",
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Success(session));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User authenticated successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsFailedLogin()
    {
        // Arrange
        var command = new LoginCommand();
        var errorMessage = "Authentication failed after 3 attempts.";
        _mockAuthService
            .Setup(x => x.AuthenticateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UserSession>.Failure(errorMessage));

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Authentication failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
