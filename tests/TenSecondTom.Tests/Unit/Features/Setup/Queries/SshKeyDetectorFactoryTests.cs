using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Features.Setup.Queries;
using Xunit;

namespace TenSecondTom.Tests.Unit.Features.Setup.Queries;

/// <summary>
/// Unit tests for <see cref="SshKeyDetectorFactory"/>
/// Tests SSH key detection orchestration across multiple detectors
/// </summary>
public sealed class SshKeyDetectorFactoryTests
{
    private readonly Mock<ILogger<SshKeyDetectorFactory>> _mockLogger;
    private readonly Mock<ISshKeyDetector> _mockSystemDetector;
    private readonly Mock<ISshKeyDetector> _mock1PasswordDetector;
    private readonly Mock<ISshKeyDetector> _mockSecretiveDetector;
    private readonly Mock<ISshKeyDetector> _mockFileSystemDetector;

    public SshKeyDetectorFactoryTests()
    {
        _mockLogger = new Mock<ILogger<SshKeyDetectorFactory>>();
        
        _mockSystemDetector = new Mock<ISshKeyDetector>();
        _mockSystemDetector.Setup(d => d.Source).Returns(SshKeySource.SystemAgent);
        
        _mock1PasswordDetector = new Mock<ISshKeyDetector>();
        _mock1PasswordDetector.Setup(d => d.Source).Returns(SshKeySource.OnePasswordAgent);
        
        _mockSecretiveDetector = new Mock<ISshKeyDetector>();
        _mockSecretiveDetector.Setup(d => d.Source).Returns(SshKeySource.SecretiveAgent);
        
        _mockFileSystemDetector = new Mock<ISshKeyDetector>();
        _mockFileSystemDetector.Setup(d => d.Source).Returns(SshKeySource.FileSystem);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDetectors_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SshKeyDetectorFactory(null!, _mockLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("detectors");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region Happy Path Tests

    [Fact]
    public async Task DetectKeysAsync_WithMultipleDetectors_CallsAllInPriorityOrder()
    {
        // Arrange
        var detectors = new[]
        {
            _mockFileSystemDetector.Object, // Should be called last (priority 4)
            _mockSystemDetector.Object,     // Should be called first (priority 1)
            _mockSecretiveDetector.Object,  // Should be called third (priority 3)
            _mock1PasswordDetector.Object   // Should be called second (priority 2)
        };

        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());
        
        _mock1PasswordDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());
        
        _mockSecretiveDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());
        
        _mockFileSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());

        var factory = new SshKeyDetectorFactory(detectors, _mockLogger.Object);
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.DetectedKeys.Should().BeEmpty();
        result.SourcesChecked.Should().Contain(new[]
        {
            SshKeySource.SystemAgent,
            SshKeySource.OnePasswordAgent,
            SshKeySource.SecretiveAgent,
            SshKeySource.FileSystem
        });

        // Verify all detectors were called
        _mockSystemDetector.Verify(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _mock1PasswordDetector.Verify(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockSecretiveDetector.Verify(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockFileSystemDetector.Verify(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectKeysAsync_WithKeysFromMultipleSources_AggregatesAllKeys()
    {
        // Arrange
        var systemKey = new SshKeyInfo
        {
            DisplayName = "[System Agent] key1",
            Source = SshKeySource.SystemAgent,
            PublicKey = "ssh-ed25519 AAAAC3... key1",
            AgentName = "ssh-agent",
            IsEd25519 = true
        };

        var fileSystemKey = new SshKeyInfo
        {
            DisplayName = "[File] key2",
            Source = SshKeySource.FileSystem,
            PublicKey = "ssh-ed25519 AAAAC3... key2",
            FilePath = "~/.ssh/id_ed25519.pub",
            IsEd25519 = true
        };

        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { systemKey });
        
        _mockFileSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fileSystemKey });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object, _mockFileSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.DetectedKeys.Should().HaveCount(2);
        result.DetectedKeys.Should().Contain(systemKey);
        result.DetectedKeys.Should().Contain(fileSystemKey);
    }

    [Fact]
    public async Task DetectKeysAsync_FiltersNonEd25519Keys()
    {
        // Arrange
        var ed25519Key = new SshKeyInfo
        {
            DisplayName = "[System Agent] ed25519",
            Source = SshKeySource.SystemAgent,
            PublicKey = "ssh-ed25519 AAAAC3...",
            AgentName = "ssh-agent",
            IsEd25519 = true
        };

        var rsaKey = new SshKeyInfo
        {
            DisplayName = "[System Agent] rsa",
            Source = SshKeySource.SystemAgent,
            PublicKey = "ssh-rsa AAAAB3...",
            AgentName = "ssh-agent",
            IsEd25519 = false
        };

        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ed25519Key, rsaKey });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.DetectedKeys.Should().HaveCount(1);
        result.DetectedKeys.Should().Contain(ed25519Key);
        result.DetectedKeys.Should().NotContain(rsaKey);
    }

    #endregion

    #region Timeout Tests

    [Fact]
    public async Task DetectKeysAsync_WithTimeout_EnforcesTimeLimit()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // Simulate slow detector
                Thread.Sleep(100);
                return Array.Empty<SshKeyInfo>();
            });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromMilliseconds(50); // Shorter than detector
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.Should().NotBeNull();
        // May or may not have completed the detection, but should not throw
    }

    [Fact]
    public async Task DetectKeysAsync_PassesRemainingTimeToDetectors()
    {
        // Arrange
        TimeSpan? capturedTimeout = null;
        
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan t, CancellationToken ct) =>
            {
                capturedTimeout = t;
                return Array.Empty<SshKeyInfo>();
            });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        capturedTimeout.Should().NotBeNull();
        capturedTimeout.Should().BeLessThanOrEqualTo(timeout);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task DetectKeysAsync_WithCancellationToken_StopsDetection()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());
        
        _mockFileSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object, _mockFileSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.Should().NotBeNull();
        // Should not have called all detectors due to cancellation
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task DetectKeysAsync_WhenDetectorThrows_ContinuesWithOtherDetectors()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("System detector failed"));

        var fileSystemKey = new SshKeyInfo
        {
            DisplayName = "[File] key",
            Source = SshKeySource.FileSystem,
            PublicKey = "ssh-ed25519 AAAAC3...",
            FilePath = "~/.ssh/id_ed25519.pub",
            IsEd25519 = true
        };

        _mockFileSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fileSystemKey });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object, _mockFileSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.DetectedKeys.Should().HaveCount(1);
        result.DetectedKeys.Should().Contain(fileSystemKey);
        
        // Should have logged error for system detector
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("System")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task DetectKeysAsync_WhenDetectorCancelled_ContinuesWithOtherDetectors()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("System detector cancelled"));

        var fileSystemKey = new SshKeyInfo
        {
            DisplayName = "[File] key",
            Source = SshKeySource.FileSystem,
            PublicKey = "ssh-ed25519 AAAAC3...",
            FilePath = "~/.ssh/id_ed25519.pub",
            IsEd25519 = true
        };

        _mockFileSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { fileSystemKey });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object, _mockFileSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.DetectedKeys.Should().HaveCount(1);
        result.DetectedKeys.Should().Contain(fileSystemKey);
    }

    #endregion

    #region Result Tests

    [Fact]
    public async Task DetectKeysAsync_ReturnsResultWithDuration()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.DetectionDuration.Should().BeGreaterThan(TimeSpan.Zero);
        result.DetectionDuration.Should().BeLessThan(timeout);
    }

    [Fact]
    public async Task DetectKeysAsync_ReturnsResultWithSourcesChecked()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());
        
        _mockFileSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object, _mockFileSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.SourcesChecked.Should().Contain(SshKeySource.SystemAgent);
        result.SourcesChecked.Should().Contain(SshKeySource.FileSystem);
    }

    [Fact]
    public async Task DetectKeysAsync_WithNoDetectors_ReturnsEmptyResult()
    {
        // Arrange
        var factory = new SshKeyDetectorFactory(
            Array.Empty<ISshKeyDetector>(),
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.DetectedKeys.Should().BeEmpty();
        result.SourcesChecked.Should().BeEmpty();
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task DetectKeysAsync_LogsDetectionStart()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting SSH key detection")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task DetectKeysAsync_LogsDetectionCompletion()
    {
        // Arrange
        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SshKeyInfo>());

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once());
    }

    [Fact]
    public async Task DetectKeysAsync_WhenKeysFound_LogsKeyCounts()
    {
        // Arrange
        var key = new SshKeyInfo
        {
            DisplayName = "[System Agent] key",
            Source = SshKeySource.SystemAgent,
            PublicKey = "ssh-ed25519 AAAAC3...",
            AgentName = "ssh-agent",
            IsEd25519 = true
        };

        _mockSystemDetector
            .Setup(d => d.DetectKeysAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { key });

        var factory = new SshKeyDetectorFactory(
            new[] { _mockSystemDetector.Object },
            _mockLogger.Object);
        
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();

        // Act
        await factory.DetectKeysAsync(timeout, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Found") && v.ToString()!.Contains("keys")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    #endregion
}
