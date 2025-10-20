using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Shared.TextEditing.Services;

namespace TenSecondTom.IntegrationTests.Integration.Cli;

/// <summary>
/// Integration tests for TodayCommandHandler with mocked text editor.
/// Tests the integration between the CLI command and the interactive text editor.
/// </summary>
public sealed class TodayCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithEditorSaved_CreatesEntry()
    {
        // Arrange
        var mockHandler = new Mock<IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockEditor = new Mock<IInteractiveTextEditor>();

        // Setup authentication to succeed
        mockAuthService
            .Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup editor to return saved content for each prompt
        var callCount = 0;
        mockEditor
            .Setup(e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return EditorResult.Saved(
                    $"Answer {callCount}",
                    EditorMetadata.Empty
                );
            });

        // Setup handler to return success
        mockHandler
            .Setup(h => h.Handle(
                It.IsAny<CreateDailyEntryCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateDailyEntryCommand cmd, CancellationToken ct) =>
            {
                var entry = new DailyEntry
                {
                    EntryId = "test-entry-1",
                    Command = "today",
                    Timestamp = DateTimeOffset.UtcNow,
                    EntryNumber = 1,
                    UserInput = cmd.Content,
                    LlmResponse = "Test summary",
                    Metadata = new MemoryEntryMetadata
                    {
                        LlmProvider = "OpenAI",
                        LlmModel = "gpt-4",
                        TokensUsed = 100,
                        ProcessingDuration = TimeSpan.FromSeconds(1),
                        CustomTags = new Dictionary<string, string>()
                    },
                    Summary = new DailySummary
                    {
                        KeyEvents = new List<string> { "Event 1" },
                        Themes = new List<string> { "Theme 1" },
                        TodoItems = new List<TodoItem>(),
                        ImportantPeople = new List<string>(),
                        NotableTasks = new List<string>()
                    }
                };
                return Result<DailyEntry>.Success(entry);
            });

        // Act
        await TodayCommandHandler.ExecuteAsync(
            mockHandler.Object,
            mockAuthService.Object,
            mockEditor.Object,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            jsonOutput: true // Use JSON mode to avoid console output
        );

        // Assert
        mockEditor.Verify(
            e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Should call editor once for content input

        mockHandler.Verify(
            h => h.Handle(
                It.Is<CreateDailyEntryCommand>(cmd =>
                    !string.IsNullOrEmpty(cmd.Content) &&
                    cmd.Content.StartsWith("Answer", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithEditorCancelled_DoesNotCreateEntry()
    {
        // Arrange
        var mockHandler = new Mock<IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockEditor = new Mock<IInteractiveTextEditor>();

        // Setup authentication to succeed
        mockAuthService
            .Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup editor to return cancelled on first prompt
        mockEditor
            .Setup(e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditorResult.Cancelled(EditorMetadata.Empty));

        // Act
        await TodayCommandHandler.ExecuteAsync(
            mockHandler.Object,
            mockAuthService.Object,
            mockEditor.Object,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            jsonOutput: true
        );

        // Assert
        mockEditor.Verify(
            e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Should only call editor once before cancelling

        mockHandler.Verify(
            h => h.Handle(
                It.IsAny<CreateDailyEntryCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never); // Handler should not be called
    }

    [Fact]
    public async Task ExecuteAsync_WithEditorError_DoesNotCreateEntry()
    {
        // Arrange
        var mockHandler = new Mock<IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockEditor = new Mock<IInteractiveTextEditor>();

        // Setup authentication to succeed
        mockAuthService
            .Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup editor to return error
        mockEditor
            .Setup(e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditorResult.Error("Terminal error", EditorMetadata.Empty));

        // Act
        await TodayCommandHandler.ExecuteAsync(
            mockHandler.Object,
            mockAuthService.Object,
            mockEditor.Object,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            jsonOutput: true
        );

        // Assert
        mockEditor.Verify(
            e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Should only call editor once before error

        mockHandler.Verify(
            h => h.Handle(
                It.IsAny<CreateDailyEntryCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never); // Handler should not be called on error
    }

    [Fact]
    public async Task ExecuteAsync_WithMultiLineContent_PreservesContent()
    {
        // Arrange
        var mockHandler = new Mock<IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockEditor = new Mock<IInteractiveTextEditor>();

        mockAuthService
            .Setup(a => a.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Setup editor to return multi-line content
        var multiLineContent = "Line 1\nLine 2\nLine 3";
        mockEditor
            .Setup(e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditorResult.Saved(multiLineContent, EditorMetadata.Empty));

        mockHandler
            .Setup(h => h.Handle(
                It.IsAny<CreateDailyEntryCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateDailyEntryCommand cmd, CancellationToken ct) =>
            {
                var entry = new DailyEntry
                {
                    EntryId = "test-entry",
                    Command = "today",
                    Timestamp = DateTimeOffset.UtcNow,
                    EntryNumber = 1,
                    UserInput = cmd.Content,
                    LlmResponse = "Summary",
                    Metadata = new MemoryEntryMetadata
                    {
                        LlmProvider = "test-provider",
                        LlmModel = "test-model"
                    },
                    Summary = new DailySummary()
                };
                return Result<DailyEntry>.Success(entry);
            });

        // Act
        await TodayCommandHandler.ExecuteAsync(
            mockHandler.Object,
            mockAuthService.Object,
            mockEditor.Object,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            jsonOutput: true
        );

        // Assert
        mockHandler.Verify(
            h => h.Handle(
                It.Is<CreateDailyEntryCommand>(cmd =>
                    cmd.Content.Contains('\n', StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

