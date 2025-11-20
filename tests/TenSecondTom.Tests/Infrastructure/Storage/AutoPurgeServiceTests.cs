using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using MoqRange = Moq.Range;

namespace TenSecondTom.Tests.Infrastructure.Storage;

/// <summary>
/// Unit tests for AutoPurgeService.
/// Tests automatic deletion of entries older than configured retention period.
/// </summary>
public sealed class AutoPurgeServiceTests
{
    private readonly Mock<IMemoryStorageProvider> _mockStorage;
    private readonly Mock<ILogger<AutoPurgeService>> _mockLogger;

    public AutoPurgeServiceTests()
    {
        _mockStorage = new Mock<IMemoryStorageProvider>();
        _mockLogger = new Mock<ILogger<AutoPurgeService>>();
    }

    [Fact]
    public async Task PurgeAsync_WhenDisabled_SkipsPurge()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = false,
            RetentionPolicy = RetentionPolicy.Days30
        };

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(0);
        result.Value.WasSkipped.Should().BeTrue();

        // Verify storage was not accessed
        _mockStorage.Verify(
            s => s.PurgeExpiredEntriesAsync(It.IsAny<RetentionPolicy>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_WhenIndefiniteRetention_SkipsPurge()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.Indefinite
        };

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(0);
        result.Value.WasSkipped.Should().BeTrue();

        // Verify storage was not accessed
        _mockStorage.Verify(
            s => s.PurgeExpiredEntriesAsync(It.IsAny<RetentionPolicy>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_WithDays30Policy_CalculatesCorrectCutoffDate()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.Days30
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(
                RetentionPolicy.Days30,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(10));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(10);
        result.Value.WasSkipped.Should().BeFalse();
        result.Value.RetentionPolicy.Should().Be(RetentionPolicy.Days30);
        result.Value.CutoffDate.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeAsync_WithDays90Policy_CalculatesCorrectCutoffDate()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.Days90
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(
                RetentionPolicy.Days90,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(25));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(25);
        result.Value.WasSkipped.Should().BeFalse();
        result.Value.RetentionPolicy.Should().Be(RetentionPolicy.Days90);
        result.Value.CutoffDate.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeAsync_WithOneYearPolicy_CalculatesCorrectCutoffDate()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.OneYear
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(
                RetentionPolicy.OneYear,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(100));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(100);
        result.Value.WasSkipped.Should().BeFalse();
        result.Value.RetentionPolicy.Should().Be(RetentionPolicy.OneYear);
        result.Value.CutoffDate.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeAsync_WithTwoYearsPolicy_CalculatesCorrectCutoffDate()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.TwoYears
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(
                RetentionPolicy.TwoYears,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(200));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(200);
        result.Value.WasSkipped.Should().BeFalse();
        result.Value.RetentionPolicy.Should().Be(RetentionPolicy.TwoYears);
        result.Value.CutoffDate.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeAsync_WhenNoEntriesExpired_ReturnsZero()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.Days30
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(RetentionPolicy.Days30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(0));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(0);
        result.Value.WasSkipped.Should().BeFalse();
    }

    [Fact]
    public async Task PurgeAsync_WhenStorageFails_ReturnsFailure()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.Days30
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(RetentionPolicy.Days30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Failure("Storage error"));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Storage error");
    }

    [Fact]
    public async Task PurgeAsync_LogsPurgeSummary()
    {
        // Arrange
        var config = new StorageConfiguration
        {
            RootDirectory = ".memory",
            ProviderId = "default",
            AutoPurge = true,
            RetentionPolicy = RetentionPolicy.Days30
        };

        _mockStorage
            .Setup(s => s.PurgeExpiredEntriesAsync(RetentionPolicy.Days30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<int>.Success(42));

        var service = new AutoPurgeService(_mockStorage.Object, config, _mockLogger.Object);

        // Act
        Result<AutoPurgeResult> result = await service.PurgeAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EntriesDeleted.Should().Be(42);

        // Verify logging occurred (we can't verify exact message easily with current mock setup,
        // but we can verify that logger was called)
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.AtLeastOnce);
    }
}
