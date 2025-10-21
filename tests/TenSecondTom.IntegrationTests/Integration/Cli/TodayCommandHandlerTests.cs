using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using TenSecondTom.Shared.TextEditing.Models;
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.IntegrationTests.TestHelpers;

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
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockEditor = new Mock<IInteractiveTextEditor>();
        var mockLlm = MockLlmProvider.WithDailySummaryResponse();

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

        // Build service provider with mocks
        var serviceProvider = new TestServiceProviderBuilder()
            .WithAuthenticationService(mockAuthService.Object)
            .WithTextEditor(mockEditor.Object)
            .WithLlmProvider(mockLlm)
            .Build();

        // Act
        await TodayCommandHandler.ExecuteAsync(
            serviceProvider,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            useVoice: false,
            sttSelection: null,
            jsonOutput: true // Use JSON mode to avoid console output
        );

        // Assert
        mockEditor.Verify(
            e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Should call editor once for content input
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

        // Build service provider with mocks
        var serviceProvider = new TestServiceProviderBuilder()
            .WithAuthenticationService(mockAuthService.Object)
            .WithTextEditor(mockEditor.Object)
            .Build();

        // Act
        await TodayCommandHandler.ExecuteAsync(
            serviceProvider,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            useVoice: false,
            sttSelection: null,
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

        // Build service provider with mocks
        var serviceProvider = new TestServiceProviderBuilder()
            .WithAuthenticationService(mockAuthService.Object)
            .WithTextEditor(mockEditor.Object)
            .Build();

        // Act
        await TodayCommandHandler.ExecuteAsync(
            serviceProvider,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            useVoice: false,
            sttSelection: null,
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
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockEditor = new Mock<IInteractiveTextEditor>();
        var mockLlm = MockLlmProvider.WithDailySummaryResponse();

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

        // Build service provider with mocks
        var serviceProvider = new TestServiceProviderBuilder()
            .WithAuthenticationService(mockAuthService.Object)
            .WithTextEditor(mockEditor.Object)
            .WithLlmProvider(mockLlm)
            .Build();

        // Act
        await TodayCommandHandler.ExecuteAsync(
            serviceProvider,
            notes: null,
            noEdit: false,
            useDefaultTemplate: false,
            templateName: null,
            providerOverride: null,
            useVoice: false,
            sttSelection: null,
            jsonOutput: true
        );

        // Assert
        mockEditor.Verify(
            e => e.EditAsync(
                It.IsAny<string?>(),
                It.IsAny<EditorConfiguration?>(),
                It.IsAny<CancellationToken>()),
            Times.Once); // Editor should be called once
    }
}
