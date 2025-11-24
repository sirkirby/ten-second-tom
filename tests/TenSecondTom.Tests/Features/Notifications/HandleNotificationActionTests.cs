using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Notifications;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Notifications;

/// <summary>
/// Unit tests for <see cref="HandleNotificationAction"/>.
/// Tests notification action callback validation and handling.
/// </summary>
public sealed class HandleNotificationActionTests
{
    private readonly HandleNotificationAction.Validator _validator = new();

    #region Validator Tests

    [Fact]
    public void Validator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: "test-action");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WithEmptyNotificationId_FailsValidation()
    {
        // Arrange
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.Empty,
            ActionId: "test-action");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NotificationId)
            .WithErrorMessage("NotificationId is required");
    }

    [Fact]
    public void Validator_WithEmptyActionId_FailsValidation()
    {
        // Arrange
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ActionId)
            .WithErrorMessage("ActionId is required");
    }

    [Fact]
    public void Validator_WithNullActionId_FailsValidation()
    {
        // Arrange
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: null!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ActionId);
    }

    [Fact]
    public void Validator_WithWhitespaceActionId_FailsValidation()
    {
        // Arrange
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: "   ");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ActionId)
            .WithErrorMessage("ActionId is required");
    }

    [Fact]
    public void Validator_WithTooLongActionId_FailsValidation()
    {
        // Arrange
        var longActionId = new string('A', 101); // 101 characters, max is 100
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: longActionId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ActionId)
            .WithErrorMessage("ActionId must not exceed 100 characters");
    }

    [Fact]
    public void Validator_WithMaxLengthActionId_PassesValidation()
    {
        // Arrange
        var maxActionId = new string('A', 100); // Exactly 100 characters
        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: maxActionId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ActionId);
    }

    #endregion

    #region Handler Tests

    [Fact]
    public async Task Handler_WithValidAction_LogsAction()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        var notificationId = Guid.NewGuid();
        var command = new HandleNotificationAction.Command(
            NotificationId: notificationId,
            ActionId: "test-action");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Notification action received") &&
                    v.ToString()!.Contains(notificationId.ToString()) &&
                    v.ToString()!.Contains("test-action")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_LogsPlaceholderWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: "test-action");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("not implemented yet")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_LogsAcknowledgement()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        var notificationId = Guid.NewGuid();
        var command = new HandleNotificationAction.Command(
            NotificationId: notificationId,
            ActionId: "test-action");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("acknowledged")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        // Act & Assert
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handler_ReturnsSuccess()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: "test-action");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_WithDifferentActionIds_LogsCorrectly()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        var actionIds = new[] { "record.continue", "template.select.daily", "note.save" };

        foreach (var actionId in actionIds)
        {
            mockLogger.Reset();

            var command = new HandleNotificationAction.Command(
                NotificationId: Guid.NewGuid(),
                ActionId: actionId);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();

            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(actionId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }
    }

    [Fact]
    public async Task Handler_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<HandleNotificationAction.Handler>>();
        var handler = new HandleNotificationAction.Handler(mockLogger.Object);

        var command = new HandleNotificationAction.Command(
            NotificationId: Guid.NewGuid(),
            ActionId: "test-action");

        using var cts = new CancellationTokenSource();

        // Act
        var result = await handler.Handle(command, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
