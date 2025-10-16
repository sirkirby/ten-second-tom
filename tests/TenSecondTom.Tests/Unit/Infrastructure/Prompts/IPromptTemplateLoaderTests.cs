using FluentAssertions;
using Moq;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Prompts;

/// <summary>
/// Unit tests for IPromptTemplateLoader interface contract.
/// Tests define expected behavior using mock implementations.
/// </summary>
public sealed class IPromptTemplateLoaderTests
{
    private readonly Mock<IPromptTemplateLoader> _mockLoader;

    public IPromptTemplateLoaderTests()
    {
        _mockLoader = new Mock<IPromptTemplateLoader>();
    }

    [Fact]
    public async Task LoadTemplateAsync_WithValidTemplateId_ReturnsSuccessResult()
    {
        // Arrange
        const string templateId = "daily-summary";
        PromptTemplate expectedTemplate = new()
        {
            TemplateId = templateId,
            Content = "Test template with {{USER_INPUT}} variable",
            TemplateType = TemplateType.Daily,
            Description = "Daily summary template",
            Source = TemplateSource.Embedded
        };

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(expectedTemplate));

        // Act
        Result<PromptTemplate> result = await _mockLoader.Object.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TemplateId.Should().Be(templateId);
        result.Value.Content.Should().Contain("{{USER_INPUT}}");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithEmbeddedResource_LoadsTemplate()
    {
        // Arrange
        const string templateId = "weekly-review";
        PromptTemplate embeddedTemplate = new()
        {
            TemplateId = templateId,
            Content = "Embedded template content {{START_DATE}} to {{END_DATE}}",
            TemplateType = TemplateType.Weekly,
            Source = TemplateSource.Embedded
        };

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(embeddedTemplate));

        // Act
        Result<PromptTemplate> result = await _mockLoader.Object.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Contain("{{START_DATE}}");
        result.Value.Content.Should().Contain("{{END_DATE}}");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithUserOverride_PreferenceOverEmbedded()
    {
        // Arrange
        const string templateId = "daily-summary";
        PromptTemplate userTemplate = new()
        {
            TemplateId = templateId,
            Content = "User override template {{CUSTOM_VAR}}",
            TemplateType = TemplateType.Daily,
            Description = "User customized version",
            Source = TemplateSource.FileSystem
        };

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Success(userTemplate));

        // Act
        Result<PromptTemplate> result = await _mockLoader.Object.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Contain("User override");
        result.Value.Description.Should().Be("User customized version");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithMissingTemplate_ReturnsFailureResult()
    {
        // Arrange
        const string templateId = "non-existent-template";
        const string errorMessage = $"Template '{templateId}' not found";

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Failure(errorMessage));

        // Act
        Result<PromptTemplate> result = await _mockLoader.Object.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain(templateId);
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task LoadTemplateAsync_SupportsCancellation()
    {
        // Arrange
        const string templateId = "daily-summary";
        using CancellationTokenSource cts = new();

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task> act = async () => await _mockLoader.Object.LoadTemplateAsync(
            templateId,
            cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoadTemplateAsync_WithInvalidTemplateId_ReturnsFailureResult(string? invalidId)
    {
        // Arrange
        const string errorMessage = "Template ID cannot be null or empty";

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(invalidId!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Failure(errorMessage));

        // Act
        Result<PromptTemplate> result = await _mockLoader.Object.LoadTemplateAsync(
            invalidId!,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Template ID");
    }

    [Fact]
    public async Task LoadTemplateAsync_WithIOError_ReturnsFailureResult()
    {
        // Arrange
        const string templateId = "daily-summary";
        const string errorMessage = "Failed to read template file: Permission denied";

        _mockLoader
            .Setup(l => l.LoadTemplateAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PromptTemplate>.Failure(errorMessage));

        // Act
        Result<PromptTemplate> result = await _mockLoader.Object.LoadTemplateAsync(
            templateId,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to read template file");
    }
}
