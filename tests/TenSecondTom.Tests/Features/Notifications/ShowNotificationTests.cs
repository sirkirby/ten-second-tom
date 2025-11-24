using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Notifications;
using TenSecondTom.Shared.Abstractions.Notifications;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Requests;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Notifications;

/// <summary>
/// Unit tests for <see cref="ShowNotification"/>.
/// Tests notification command validation and handling.
/// </summary>
public sealed class ShowNotificationTests
{
    private readonly ShowNotification.Validator _validator = new();

    #region Validator Tests

    [Fact]
    public void Validator_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Priority: NotificationPriority.Normal,
            TimeoutSeconds: 30,
            Actions: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WithEmptyTitle_FailsValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: string.Empty,
            Message: "Test Message");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Notification title is required");
    }

    [Fact]
    public void Validator_WithNullTitle_FailsValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: null!,
            Message: "Test Message");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validator_WithTooLongTitle_FailsValidation()
    {
        // Arrange
        var longTitle = new string('A', 101); // 101 characters, max is 100
        var command = new SendNotificationRequest(
            Title: longTitle,
            Message: "Test Message");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage("Notification title must not exceed 100 characters");
    }

    [Fact]
    public void Validator_WithMaxLengthTitle_PassesValidation()
    {
        // Arrange
        var maxTitle = new string('A', 100); // Exactly 100 characters
        var command = new SendNotificationRequest(
            Title: maxTitle,
            Message: "Test Message");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validator_WithEmptyMessage_FailsValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: string.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Notification message is required");
    }

    [Fact]
    public void Validator_WithNullMessage_FailsValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: null!);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Message);
    }

    [Fact]
    public void Validator_WithTooLongMessage_FailsValidation()
    {
        // Arrange
        var longMessage = new string('A', 501); // 501 characters, max is 500
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: longMessage);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Message)
            .WithErrorMessage("Notification message must not exceed 500 characters");
    }

    [Fact]
    public void Validator_WithMaxLengthMessage_PassesValidation()
    {
        // Arrange
        var maxMessage = new string('A', 500); // Exactly 500 characters
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: maxMessage);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validator_WithInvalidTimeout_FailsValidation(int invalidTimeout)
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            TimeoutSeconds: invalidTimeout);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeoutSeconds)
            .WithErrorMessage("Timeout must be between 1 and 300 seconds");
    }

    [Fact]
    public void Validator_WithTimeoutTooLarge_FailsValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            TimeoutSeconds: 301); // Max is 300

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TimeoutSeconds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(300)]
    public void Validator_WithValidTimeout_PassesValidation(int validTimeout)
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            TimeoutSeconds: validTimeout);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TimeoutSeconds);
    }

    [Fact]
    public void Validator_WithNullTimeout_PassesValidation()
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            TimeoutSeconds: null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TimeoutSeconds);
    }

    [Theory]
    [InlineData(NotificationPriority.Low)]
    [InlineData(NotificationPriority.Normal)]
    [InlineData(NotificationPriority.High)]
    [InlineData(NotificationPriority.Critical)]
    public void Validator_WithValidPriority_PassesValidation(NotificationPriority priority)
    {
        // Arrange
        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Priority: priority);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Priority);
    }

    [Fact]
    public void Validator_WithTooManyActions_FailsValidation()
    {
        // Arrange
        var tooManyActions = new List<NotificationAction>
        {
            NotificationAction.Create("action1", "Action 1", "cmd1"),
            NotificationAction.Create("action2", "Action 2", "cmd2"),
            NotificationAction.Create("action3", "Action 3", "cmd3"),
            NotificationAction.Create("action4", "Action 4", "cmd4"),
            NotificationAction.Create("action5", "Action 5", "cmd5") // 5 actions, max is 4
        };

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Actions: tooManyActions);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Actions)
            .WithErrorMessage("Maximum of 4 actions are supported");
    }

    [Fact]
    public void Validator_WithMaxActions_PassesValidation()
    {
        // Arrange
        var maxActions = new List<NotificationAction>
        {
            NotificationAction.Create("action1", "Action 1", "cmd1"),
            NotificationAction.Create("action2", "Action 2", "cmd2"),
            NotificationAction.Create("action3", "Action 3", "cmd3"),
            NotificationAction.Create("action4", "Action 4", "cmd4")
        };

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Actions: maxActions);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Actions);
    }

    [Fact]
    public void Validator_WithActionMissingActionId_FailsValidation()
    {
        // Arrange
        var invalidActions = new List<NotificationAction>
        {
            new NotificationAction
            {
                ActionId = string.Empty, // Invalid
                Label = "Continue",
                Command = "record continue"
            }
        };

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Actions: invalidActions);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Actions)
            .WithErrorMessage("All actions must have a valid ActionId");
    }

    [Fact]
    public void Validator_WithActionMissingLabel_FailsValidation()
    {
        // Arrange
        var invalidActions = new List<NotificationAction>
        {
            new NotificationAction
            {
                ActionId = "action1",
                Label = string.Empty, // Invalid
                Command = "record continue"
            }
        };

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Actions: invalidActions);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Actions)
            .WithErrorMessage("All actions must have a valid Label");
    }

    #endregion

    #region Handler Tests

    [Fact]
    public async Task Handler_WithValidCommand_SendsNotification()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();

        mockService
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Priority: NotificationPriority.Normal,
            TimeoutSeconds: 30);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        mockService.Verify(
            x => x.SendAsync(
                It.Is<Notification>(n =>
                    n.Title == "Test Title" &&
                    n.Message == "Test Message" &&
                    n.Priority == NotificationPriority.Normal &&
                    n.TimeoutSeconds == 30),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_WithActions_SendsInteractiveNotification()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();

        mockService
            .Setup(x => x.SendInteractiveAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var actions = new List<NotificationAction>
        {
            NotificationAction.Create("action1", "Continue", "record continue")
        };

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Actions: actions);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        mockService.Verify(
            x => x.SendInteractiveAsync(
                It.Is<Notification>(n => n.Actions.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_WhenServiceUnavailable_ReturnsFailure()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();

        mockService
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("No notification channel available"));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No notification channel available");
    }

    [Fact]
    public async Task Handler_WhenExceptionOccurs_ReturnsFailure()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();

        mockService
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("unexpected error");
    }

    [Fact]
    public async Task Handler_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();
        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        // Act & Assert
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handler_LogsNotificationDetails()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();

        mockService
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message",
            Priority: NotificationPriority.High,
            TimeoutSeconds: 60);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Test Title") &&
                    v.ToString()!.Contains("High") &&
                    v.ToString()!.Contains("60")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_OnSuccess_LogsSuccessMessage()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();
        var notificationId = Guid.NewGuid();

        mockService
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Success(notificationId));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("sent successfully")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_OnFailure_LogsWarningMessage()
    {
        // Arrange
        var mockService = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<ShowNotification.Handler>>();

        mockService
            .Setup(x => x.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("Service unavailable"));

        var handler = new ShowNotification.Handler(mockService.Object, mockLogger.Object);

        var command = new SendNotificationRequest(
            Title: "Test Title",
            Message: "Test Message");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to send notification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
