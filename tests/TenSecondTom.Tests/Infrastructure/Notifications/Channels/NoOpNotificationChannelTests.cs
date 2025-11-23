using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Notifications.Channels;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Infrastructure.Notifications.Channels;

/// <summary>
/// Unit tests for <see cref="NoOpNotificationChannel"/>.
/// Tests the no-operation fallback notification channel.
/// </summary>
public sealed class NoOpNotificationChannelTests
{
    private readonly Mock<ILogger<NoOpNotificationChannel>> _mockLogger;
    private readonly NoOpNotificationChannel _channel;

    public NoOpNotificationChannelTests()
    {
        _mockLogger = new Mock<ILogger<NoOpNotificationChannel>>();
        _channel = new NoOpNotificationChannel(_mockLogger.Object);
    }

    [Fact]
    public void ChannelName_ReturnsNoOp()
    {
        // Act
        var channelName = _channel.ChannelName;

        // Assert
        channelName.Should().Be("NoOp");
    }

    [Fact]
    public void Capabilities_DoesNotSupportAnyFeatures()
    {
        // Act
        var capabilities = _channel.Capabilities;

        // Assert
        capabilities.SupportsInteractivity.Should().BeFalse();
        capabilities.SupportsCustomTimeout.Should().BeFalse();
        capabilities.SupportsCustomIcon.Should().BeFalse();
        capabilities.SupportsGrouping.Should().BeFalse();
        capabilities.MaxActions.Should().Be(0);
    }

    [Fact]
    public async Task IsAvailableAsync_AlwaysReturnsTrue()
    {
        // Act
        var result = await _channel.IsAvailableAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_LogsDebugMessage()
    {
        // Act
        await _channel.IsAvailableAsync(CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NoOp channel is available")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_AlwaysReturnsSuccess()
    {
        // Arrange
        var notification = Notification.CreateBasic(
            "Test Title",
            "Test Message",
            NotificationPriority.Normal,
            30);

        // Act
        var result = await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(notification.NotificationId);
    }

    [Fact]
    public async Task SendAsync_LogsAtInformationLevel()
    {
        // Arrange
        var notification = Notification.CreateBasic(
            "Test Title",
            "Test Message",
            NotificationPriority.Normal,
            30);

        // Act
        await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NoOp channel")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_LogsNotificationDetails()
    {
        // Arrange
        var notification = Notification.CreateBasic(
            "Test Title",
            "Test Message",
            NotificationPriority.High,
            60);

        // Act
        await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Test Title") &&
                    v.ToString()!.Contains("Test Message") &&
                    v.ToString()!.Contains("High")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithActions_LogsActionCount()
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

        // Act
        await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("2 actions")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithActions_LogsEachActionAtDebugLevel()
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

        // Act
        await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("action1") &&
                    v.ToString()!.Contains("Continue")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("action2") &&
                    v.ToString()!.Contains("Cancel")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithoutActions_DoesNotLogActionDetails()
    {
        // Arrange
        var notification = Notification.CreateBasic("Test", "Message");

        // Act
        await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("actions")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var notification = Notification.CreateBasic("Test", "Message");
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _channel.SendAsync(notification, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_WithAllPriorityLevels_LogsCorrectly()
    {
        // Arrange & Act & Assert
        foreach (NotificationPriority priority in Enum.GetValues<NotificationPriority>())
        {
            var notification = Notification.CreateBasic(
                "Test",
                "Message",
                priority);

            var result = await _channel.SendAsync(notification, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(priority.ToString())),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            _mockLogger.Reset();
        }
    }

    [Fact]
    public async Task SendAsync_ReturnsNotificationId()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var notification = new Notification
        {
            NotificationId = expectedId,
            Title = "Test",
            Message = "Message",
            Priority = NotificationPriority.Normal,
            State = NotificationState.Pending
        };

        // Act
        var result = await _channel.SendAsync(notification, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedId);
    }
}
