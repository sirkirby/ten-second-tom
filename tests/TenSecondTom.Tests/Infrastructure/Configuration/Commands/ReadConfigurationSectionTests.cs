using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Configuration.Commands;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Infrastructure.Configuration.Commands;

/// <summary>
/// Unit tests for ReadConfigurationSection CQRS query.
/// </summary>
public sealed class ReadConfigurationSectionTests
{
    public sealed class TestConfig
    {
        public string SttProvider { get; init; } = string.Empty;
        public string? ApiKey { get; init; }
    }

    private readonly Mock<IConfigurationSectionStore> _mockSectionStore;
    private readonly Mock<ILogger<ReadConfigurationSection<TestConfig>.Handler>> _mockLogger;
    private readonly ReadConfigurationSection<TestConfig>.Handler _handler;

    public ReadConfigurationSectionTests()
    {
        _mockSectionStore = new Mock<IConfigurationSectionStore>();
        _mockLogger = new Mock<ILogger<ReadConfigurationSection<TestConfig>.Handler>>();
        _handler = new ReadConfigurationSection<TestConfig>.Handler(_mockSectionStore.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidQuery_CallsSectionStore()
    {
        // Arrange
        var sectionPath = "TenSecondTom:Audio";
        var query = new ReadConfigurationSection<TestConfig>.Query(sectionPath);

        var expectedConfig = new TestConfig { SttProvider = "openai", ApiKey = "test-key" };
        _mockSectionStore
            .Setup(x => x.ReadSectionAsync<TestConfig>(sectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TestConfig>.Success(expectedConfig));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expectedConfig);

        _mockSectionStore.Verify(
            x => x.ReadSectionAsync<TestConfig>(sectionPath, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenSectionDoesNotExist_ReturnsDefaultInstance()
    {
        // Arrange
        var sectionPath = "TenSecondTom:NonExistent";
        var query = new ReadConfigurationSection<TestConfig>.Query(sectionPath);

        var defaultConfig = new TestConfig();
        _mockSectionStore
            .Setup(x => x.ReadSectionAsync<TestConfig>(sectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TestConfig>.Success(defaultConfig));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.SttProvider.Should().Be(string.Empty); // Default value
    }

    [Fact]
    public async Task Handle_WhenStoreReturnsFailure_ReturnsFailure()
    {
        // Arrange
        var sectionPath = "TenSecondTom:Audio";
        var query = new ReadConfigurationSection<TestConfig>.Query(sectionPath);

        var errorMessage = "Invalid JSON in configuration file";
        _mockSectionStore
            .Setup(x => x.ReadSectionAsync<TestConfig>(sectionPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TestConfig>.Failure(errorMessage));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(errorMessage);
    }

    [Fact]
    public async Task Handle_WhenStoreThrowsException_ReturnsFailure()
    {
        // Arrange
        var sectionPath = "TenSecondTom:Audio";
        var query = new ReadConfigurationSection<TestConfig>.Query(sectionPath);

        _mockSectionStore
            .Setup(x => x.ReadSectionAsync<TestConfig>(sectionPath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to read configuration");
    }

    [Fact]
    public void Validator_WithEmptySectionPath_Fails()
    {
        // Arrange
        var validator = new ReadConfigurationSection<TestConfig>.Validator();
        var query = new ReadConfigurationSection<TestConfig>.Query(string.Empty);

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(query.SectionPath));
    }

    [Fact]
    public void Validator_WithValidQuery_Passes()
    {
        // Arrange
        var validator = new ReadConfigurationSection<TestConfig>.Validator();
        var query = new ReadConfigurationSection<TestConfig>.Query("TenSecondTom:Audio");

        // Act
        var result = validator.Validate(query);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
