using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Notifications;
using TenSecondTom.Infrastructure.Notifications.Channels;
using TenSecondTom.Infrastructure.Notifications.Security;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Infrastructure.Notifications;

/// <summary>
/// Unit tests for <see cref="NotificationService"/>.
/// Tests notification routing, channel selection, and state management.
/// </summary>
public sealed class NotificationServiceTests
{
    private readonly Mock<INotificationChannel> _mockChannel;
    private readonly Mock<INotificationTokenService> _mockTokenService;
    private readonly Mock<ILogger<NotificationService>> _mockLogger;
    private readonly NotificationOptions _notificationOptions;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _mockChannel = new Mock<INotificationChannel>();
        _mockTokenService = new Mock<INotificationTokenService>();
        _mockLogger = new Mock<ILogger<NotificationService>>();

        _notificationOptions = new NotificationOptions
        {
            Enabled = true,
            DefaultTimeoutSeconds = 30,
            DefaultPriority = NotificationPriority.Normal,
            SilentFallback = false
        };

        // Setup default channel behavior
        _mockChannel.Setup(x => x.ChannelName).Returns("TestChannel");
        _mockChannel.Setup(x => x.Capabilities).Returns(new NotificationChannelCapabilities
        {
            SupportsInteractivity = true,
            SupportsCustomTimeout = true,
            SupportsCustomIcon = false,
            SupportsGrouping = false,
            MaxActions = 4
        });

        var channels = new[] { _mockChannel.Object };
        var options = Options.Create(_notificationOptions);

        _notificationService = new NotificationService(
            channels,
            _mockTokenService.Object,
            options,
            _mockLogger.Object);
    }

    [Fact]
    public async Task SendAsync_WithAvailableChannel_SendsSuccessfully()
    {
        // Arrange
        var notification = Notification.CreateBasic(
            "Test Title",
            "Test Message",
            NotificationPriority.Normal,
            30);

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        _mockChannel
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(notification.NotificationId));

        // Act
        var result = await _notificationService.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(notification.NotificationId);

        _mockChannel.Verify(
            x => x.SendAsync(notification, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithNoAvailableChannels_ReturnsFailure()
    {
        // Arrange
        var notification = Notification.CreateBasic(
            "Test Title",
            "Test Message");

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("Channel not available"));

        // Act
        var result = await _notificationService.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No notification channel available");

        _mockChannel.Verify(
            x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithSilentFallback_LogsWarningOnFailure()
    {
        // Arrange
        var silentOptions = new NotificationOptions
        {
            Enabled = true,
            SilentFallback = true
        };

        var service = new NotificationService(
            new[] { _mockChannel.Object },
            _mockTokenService.Object,
            Options.Create(silentOptions),
            _mockLogger.Object);

        var notification = Notification.CreateBasic("Test", "Message");

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Failure("Not available"));

        // Act
        var result = await service.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(); // Silent fallback returns success
        result.Value.Should().Be(notification.NotificationId);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No notification channel available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendInteractiveAsync_WithNonInteractiveChannel_SendsBasicNotification()
    {
        // Arrange
        var actions = new List<NotificationAction>
        {
            NotificationAction.Create("action1", "Continue", "record continue")
        };

        var notification = Notification.CreateInteractive(
            "Test Title",
            "Test Message",
            actions);

        // Setup channel as non-interactive
        _mockChannel.Setup(x => x.Capabilities).Returns(new NotificationChannelCapabilities
        {
            SupportsInteractivity = false,
            SupportsCustomTimeout = false,
            SupportsCustomIcon = false,
            SupportsGrouping = false,
            MaxActions = 0
        });

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        _mockChannel
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(notification.NotificationId));

        // Act
        var result = await _notificationService.SendInteractiveAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify that a notification with no actions was sent
        _mockChannel.Verify(
            x => x.SendAsync(
                It.Is<Notification>(n => n.Actions.Count == 0),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendInteractiveAsync_WithInteractiveChannel_AttachesTokens()
    {
        // Arrange
        var actions = new List<NotificationAction>
        {
            NotificationAction.Create("action1", "Continue", "record continue"),
            NotificationAction.Create("action2", "Cancel", "record cancel")
        };

        var notification = Notification.CreateInteractive(
            "Test Title",
            "Test Message",
            actions);

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        _mockChannel
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(notification.NotificationId));

        _mockTokenService
            .Setup(x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns("test-token");

        // Act
        var result = await _notificationService.SendInteractiveAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Verify tokens were generated for each action
        _mockTokenService.Verify(
            x => x.GenerateToken(notification.NotificationId, "action1"),
            Times.Once);
        _mockTokenService.Verify(
            x => x.GenerateToken(notification.NotificationId, "action2"),
            Times.Once);

        // Verify the notification sent has tokens attached
        _mockChannel.Verify(
            x => x.SendAsync(
                It.Is<Notification>(n =>
                    n.Actions.Count == 2 &&
                    n.Actions.All(a => a.Token == "test-token")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsCorrectNotificationState()
    {
        // Arrange
        var notification = Notification.CreateBasic("Test", "Message");

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        _mockChannel
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(notification.NotificationId));

        // Send notification to track its state
        await _notificationService.SendAsync(notification, CancellationToken.None);

        // Act
        var stateResult = await _notificationService.GetStateAsync(
            notification.NotificationId,
            CancellationToken.None);

        // Assert
        stateResult.IsSuccess.Should().BeTrue();
        stateResult.Value.Should().Be(NotificationState.Displayed);
    }

    [Fact]
    public async Task GetStateAsync_WithUnknownNotificationId_ReturnsFailure()
    {
        // Arrange
        var unknownId = Guid.NewGuid();

        // Act
        var result = await _notificationService.GetStateAsync(unknownId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task SendAsync_WithDisabledNotifications_ReturnsFailure()
    {
        // Arrange
        var disabledOptions = new NotificationOptions
        {
            Enabled = false,
            SilentFallback = false
        };

        var service = new NotificationService(
            new[] { _mockChannel.Object },
            _mockTokenService.Object,
            Options.Create(disabledOptions),
            _mockLogger.Object);

        var notification = Notification.CreateBasic("Test", "Message");

        // Act
        var result = await service.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("disabled");

        _mockChannel.Verify(
            x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithDisabledNotificationsAndSilentFallback_ReturnsSuccess()
    {
        // Arrange
        var disabledOptions = new NotificationOptions
        {
            Enabled = false,
            SilentFallback = true
        };

        var service = new NotificationService(
            new[] { _mockChannel.Object },
            _mockTokenService.Object,
            Options.Create(disabledOptions),
            _mockLogger.Object);

        var notification = Notification.CreateBasic("Test", "Message");

        // Act
        var result = await service.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(notification.NotificationId);

        _mockChannel.Verify(
            x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenChannelFails_UpdatesStateToFailed()
    {
        // Arrange
        var notification = Notification.CreateBasic("Test", "Message");

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        _mockChannel
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("Channel send failed"));

        // Act
        var sendResult = await _notificationService.SendAsync(notification, CancellationToken.None);

        // Assert
        sendResult.IsFailure.Should().BeTrue();

        var stateResult = await _notificationService.GetStateAsync(
            notification.NotificationId,
            CancellationToken.None);

        stateResult.IsSuccess.Should().BeTrue();
        stateResult.Value.Should().Be(NotificationState.Failed);
    }

    [Fact]
    public async Task SendInteractiveAsync_WithNoActions_CallsSendAsync()
    {
        // Arrange
        var notification = Notification.CreateBasic("Test", "Message");

        _mockChannel
            .Setup(x => x.IsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        _mockChannel
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(notification.NotificationId));

        // Act
        var result = await _notificationService.SendInteractiveAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // Should send via basic channel, not try to find interactive channel
        _mockChannel.Verify(
            x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Token service should not be called
        _mockTokenService.Verify(
            x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_WithNullChannels_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new NotificationService(
            null!,
            _mockTokenService.Object,
            Options.Create(_notificationOptions),
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("channels");
    }

    [Fact]
    public void Constructor_WithNullTokenService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () => new NotificationService(
            new[] { _mockChannel.Object },
            null!,
            Options.Create(_notificationOptions),
            _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tokenService");
    }

    [Fact]
    public async Task SendAsync_WithNullNotification_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = async () => await _notificationService.SendAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendInteractiveAsync_WithNullNotification_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = async () => await _notificationService.SendInteractiveAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
