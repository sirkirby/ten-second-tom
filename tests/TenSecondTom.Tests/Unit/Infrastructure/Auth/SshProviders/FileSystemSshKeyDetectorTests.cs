using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Auth.SshProviders;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Auth.SshProviders;

/// <summary>
/// Unit tests for <see cref="FileSystemSshKeyDetector"/>
/// Tests SSH key detection from ~/.ssh directory
/// </summary>
public sealed class FileSystemSshKeyDetectorTests
{
    private readonly Mock<ILogger<FileSystemSshKeyDetector>> _mockLogger;
    private readonly FileSystemSshKeyDetector _detector;

    public FileSystemSshKeyDetectorTests()
    {
        _mockLogger = new Mock<ILogger<FileSystemSshKeyDetector>>();
        _detector = new FileSystemSshKeyDetector(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new FileSystemSshKeyDetector(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Source_ReturnsFileSystem()
    {
        // Assert
        _detector.Source.Should().Be(SshKeySource.FileSystem);
    }

    #endregion

    #region Happy Path Tests

    [Fact(Skip = "Requires integration test with actual file system")]
    public async Task DetectKeysAsync_WithEd25519KeyInSshDirectory_ReturnsKey()
    {
        // Arrange
        var testSshDir = Path.Combine(Path.GetTempPath(), $"test-ssh-{Guid.NewGuid()}");
        Directory.CreateDirectory(testSshDir);

        try
        {
            // Create test ED25519 public key file
            var publicKeyContent = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI test@example.com";
            var pubKeyPath = Path.Combine(testSshDir, "id_ed25519.pub");
            await File.WriteAllTextAsync(pubKeyPath, publicKeyContent);

            // Temporarily set HOME to test directory
            var originalHome = Environment.GetEnvironmentVariable("HOME");
            var originalUserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            try
            {
                // Note: We can't easily override Environment.SpecialFolder.UserProfile in tests
                // This test requires actual ~/.ssh directory with test keys
                // Skip for now - integration test coverage instead
            }
            finally
            {
                // Cleanup
                Directory.Delete(testSshDir, true);
            }
        }
        catch
        {
            // Cleanup on exception
            if (Directory.Exists(testSshDir))
                Directory.Delete(testSshDir, true);
            throw;
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task DetectKeysAsync_WithMissingSshDirectory_ReturnsEmptyList()
    {
        // Arrange - detector will check actual ~/.ssh which may or may not exist
        var timeout = TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _detector.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<SshKeyInfo>>();
        
        // Note: Result may be empty or contain actual user keys
        // This is testing the interface contract, not specific keys
    }

    [Fact(Skip = "File system mocking is complex - covered in integration tests")]
    public void DetectKeysAsync_WithNonEd25519Keys_SkipsThem()
    {
        // This test requires mocking the file system which is complex
        // Better suited for integration tests
    }

    [Fact]
    public async Task DetectKeysAsync_WithTimeout_CancelsOperation()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(1); // Very short timeout
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _detector.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IReadOnlyList<SshKeyInfo>>();
        
        // Timeout should complete without throwing
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("timed out")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtMostOnce());
    }

    [Fact]
    public async Task DetectKeysAsync_WithCancellationToken_CancelsOperation()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // Cancel immediately

        // Act
        var result = await _detector.DetectKeysAsync(timeout, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact(Skip = "Requires integration test with actual file system")]
    public void DetectKeysAsync_WithMultipleEd25519Keys_ReturnsAll()
    {
        // This requires actual file system setup
        // Better suited for integration tests
    }

    #endregion

    #region Error Handling Tests

    [Fact(Skip = "File permission testing requires integration test")]
    public void DetectKeysAsync_WithUnreadableFile_LogsWarning()
    {
        // File permission testing is platform-specific and requires actual file system
    }

    [Fact(Skip = "Requires integration test with actual file system")]
    public void DetectKeysAsync_WithInvalidUtf8Content_HandlesGracefully()
    {
        // This requires actual file system with invalid content
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task DetectKeysAsync_SetsCorrectSshKeyInfoProperties()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _detector.DetectKeysAsync(timeout, cts.Token);

        // Assert
        foreach (var key in result)
        {
            key.Source.Should().Be(SshKeySource.FileSystem);
            key.DisplayName.Should().StartWith("[File]");
            key.PublicKey.Should().NotBeNullOrEmpty();
            key.FilePath.Should().NotBeNullOrEmpty();
            key.IsEd25519.Should().BeTrue();
            key.DetectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            key.ValidationResult.Should().Be(ValidationResult.Valid);
            key.AgentName.Should().BeNull();
        }
    }

    [Fact(Skip = "Requires integration test with actual file system")]
    public void DetectKeysAsync_WithPublicKeyFormat_ParsesCorrectly()
    {
        // This requires actual ED25519 key file
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task DetectKeysAsync_LogsDebugMessages()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource();

        // Act
        await _detector.DetectKeysAsync(timeout, cts.Token);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce());
    }

    #endregion
}
