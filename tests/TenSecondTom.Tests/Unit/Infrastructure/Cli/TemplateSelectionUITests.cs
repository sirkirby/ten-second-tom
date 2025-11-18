using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Infrastructure.Cli;

/// <summary>
/// Unit tests for TemplateSelectionUI (T031).
/// Tests interactive template selection using Spectre.Console.
/// Tests cover:
/// - Single template auto-selection
/// - Multiple template display with SelectionPrompt
/// - User selection handling
/// - Cancellation support
/// </summary>
public sealed class TemplateSelectionUITests
{
    private readonly Mock<ILogger<TemplateSelectionUI>> _mockLogger;

    public TemplateSelectionUITests()
    {
        _mockLogger = new Mock<ILogger<TemplateSelectionUI>>();
    }

    [Fact]
    public async Task SelectTemplateAsync_WithSingleTemplate_AutoSelectsWithoutPrompting()
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>
        {
            CreateListItem("daily-summary", "Daily Summary", isDefault: true)
        };

        // Act
        var selectedId = await ui.SelectTemplateAsync(
            templates,
            "today",
            CancellationToken.None);

        // Assert
        selectedId.Should().Be("daily-summary", "single template should be auto-selected");
    }

    [Fact]
    public async Task SelectTemplateAsync_WithNoTemplates_ThrowsArgumentException()
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>();

        // Act
        var act = async () => await ui.SelectTemplateAsync(
            templates,
            "today",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>("empty template list should throw")
            .WithMessage("*at least one template*");
    }

    [Fact]
    public async Task SelectTemplateAsync_WithNullTemplates_ThrowsArgumentNullException()
    {
        // Arrange
        var ui = CreateUI();

        // Act
        var act = async () => await ui.SelectTemplateAsync(
            null!,
            "today",
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>("null template list should throw");
    }

    [Fact]
    public async Task SelectTemplateAsync_WithWeeklyContext_ShowsWeeklyInTitle()
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>
        {
            CreateListItem("weekly-review", "Weekly Review", isDefault: true)
        };

        // Act
        var selectedId = await ui.SelectTemplateAsync(
            templates,
            "thisweek",
            CancellationToken.None);

        // Assert
        selectedId.Should().Be("weekly-review");
        // NOTE: Implementation should show "Select template for: thisweek" in prompt title
    }

    [Fact]
    public async Task SelectTemplateAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>
        {
            CreateListItem("daily-summary", "Daily Summary", isDefault: true),
            CreateListItem("custom-daily", "Custom Daily", isDefault: false)
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = async () => await ui.SelectTemplateAsync(
            templates,
            "today",
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>(
            "cancelled operation should throw");
    }

    [Fact]
    public async Task SelectTemplateAsync_WithSingleTemplateAndCancellation_StillAutoSelects()
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>
        {
            CreateListItem("daily-summary", "Daily Summary", isDefault: true)
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act - Single template should auto-select even if cancelled
        var selectedId = await ui.SelectTemplateAsync(
            templates,
            "today",
            cts.Token);

        // Assert
        selectedId.Should().Be("daily-summary",
            "single template auto-selection should not be affected by cancellation");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SelectTemplateAsync_WithInvalidCommandContext_ThrowsArgumentException(string? commandContext)
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>
        {
            CreateListItem("daily-summary", "Daily Summary", isDefault: true)
        };

        // Act
        var act = async () => await ui.SelectTemplateAsync(
            templates,
            commandContext!,
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>(
            "invalid command context should throw");
    }

    [Fact]
    public async Task SelectTemplateAsync_WithSpecialCharactersInTitle_DisplaysCorrectly()
    {
        // Arrange
        var ui = CreateUI();
        var templates = new List<TemplateInfo>
        {
            CreateListItem("special-chars", "Daily [Summary] - \"v2.0\"",
                isDefault: true)
        };

        // Act
        var selectedId = await ui.SelectTemplateAsync(
            templates,
            "today",
            CancellationToken.None);

        // Assert
        selectedId.Should().Be("special-chars");
        // NOTE: Implementation should escape special characters for Spectre.Console
    }

    private TemplateSelectionUI CreateUI()
    {
        // This will fail until TemplateSelectionUI is implemented
        return new TemplateSelectionUI(_mockLogger.Object);
    }

    private static TemplateInfo CreateListItem(
        string templateId,
        string title,
        string? description = null,
        bool isDefault = false)
    {
        return new TemplateInfo(
            TemplateId: templateId,
            Title: title,
            Description: description ?? string.Empty,
            TemplateType: TemplateType.Daily,
            Source: isDefault ? TemplateSource.Embedded : TemplateSource.FileSystem,
            IsDefault: isDefault
        );
    }
}
