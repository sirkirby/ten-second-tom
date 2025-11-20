using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Shared.Options;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Configuration.Commands;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Infrastructure.Configuration.Commands;

/// <summary>
/// Unit tests for UpdateConfigurationSection CQRS command.
/// </summary>
public sealed class UpdateConfigurationSectionTests
{
    private readonly Mock<IConfigurationSectionStore> _mockSectionStore;
    private readonly Mock<ILogger<UpdateConfigurationSection.Handler>> _mockLogger;
    private readonly UpdateConfigurationSection.Handler _handler;

    public UpdateConfigurationSectionTests()
    {
        _mockSectionStore = new Mock<IConfigurationSectionStore>();
        _mockLogger = new Mock<ILogger<UpdateConfigurationSection.Handler>>();
        _handler = new UpdateConfigurationSection.Handler(_mockSectionStore.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsSectionStore()
    {
        // Arrange
        var sectionPath = "TenSecondTom:Audio";
        var config = new { SttProvider = "openai", ApiKey = "test-key" };
        var command = new UpdateConfigurationSection.Command(sectionPath, config);

        var expectedPath = "/path/to/config.json";
        _mockSectionStore
            .Setup(x => x.WriteSectionAsync(sectionPath, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(expectedPath));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expectedPath);

        _mockSectionStore.Verify(
            x => x.WriteSectionAsync(sectionPath, It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStoreReturnsFailure_ReturnsFailure()
    {
        // Arrange
        var sectionPath = "TenSecondTom:Audio";
        var config = new { SttProvider = "openai" };
        var command = new UpdateConfigurationSection.Command(sectionPath, config);

        var errorMessage = "Failed to write configuration";
        _mockSectionStore
            .Setup(x => x.WriteSectionAsync(sectionPath, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Failure(errorMessage));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(errorMessage);
    }

    [Fact]
    public async Task Handle_WhenStoreThrowsException_ReturnsFailure()
    {
        // Arrange
        var sectionPath = "TenSecondTom:Audio";
        var config = new { SttProvider = "openai" };
        var command = new UpdateConfigurationSection.Command(sectionPath, config);

        _mockSectionStore
            .Setup(x => x.WriteSectionAsync(sectionPath, It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to update configuration");
    }

    [Fact]
    public void Validator_WithEmptySectionPath_Fails()
    {
        // Arrange
        var validator = new UpdateConfigurationSection.Validator();
        var command = new UpdateConfigurationSection.Command(string.Empty, new { });

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.SectionPath));
    }

    [Fact]
    public void Validator_WithNullConfiguration_Fails()
    {
        // Arrange
        var validator = new UpdateConfigurationSection.Validator();
        var command = new UpdateConfigurationSection.Command("TenSecondTom:Audio", null!);

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Configuration));
    }

    [Fact]
    public void Validator_WithValidCommand_Passes()
    {
        // Arrange
        var validator = new UpdateConfigurationSection.Validator();
        var command = new UpdateConfigurationSection.Command(
            "TenSecondTom:Audio",
            new { SttProvider = "openai" });

        // Act
        var result = validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
