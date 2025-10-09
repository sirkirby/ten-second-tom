using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Infrastructure.Auth.SshProviders;
using Xunit;

namespace TenSecondTom.Tests.Unit.Infrastructure.Auth.SshProviders;

/// <summary>
/// Unit tests for <see cref="SystemSshAgentDetector"/>
/// Tests SSH key detection from system SSH agent (ssh-agent)
/// </summary>
public sealed class SystemSshAgentDetectorTests
{
    private readonly Mock<ILogger<SystemSshAgentDetector>> _mockLogger;
    private readonly SystemSshAgentDetector _detector;

    public SystemSshAgentDetectorTests()
    {
        _mockLogger = new Mock<ILogger<SystemSshAgentDetector>>();
        _detector = new SystemSshAgentDetector(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SystemSshAgentDetector(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Source_ReturnsSystemAgent()
    {
        // Assert
        _detector.Source.Should().Be(SshKeySource.SystemAgent);
    }

    #endregion

    #region Happy Path Tests

    [Fact(Skip = "Requires actual SSH agent running with ED25519 keys - integration test")]
    public void DetectKeysAsync_WithSshAgentRunning_ReturnsKeys()
    {
        // This test requires:
        // 1. SSH_AUTH_SOCK environment variable set
        // 2. ssh-agent actually running
        // 3. ED25519 keys loaded in agent
        // Better suited for integration tests
    }

    [Fact]
    public async Task DetectKeysAsync_WithMissingSshAuthSock_ReturnsEmptyList()
    {
        // Arrange
        var originalSshAuthSock = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        
        try
        {
            // Clear SSH_AUTH_SOCK to simulate no agent
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
            
            var timeout = TimeSpan.FromSeconds(5);
            using var cts = new CancellationTokenSource();

            // Act
            var result = await _detector.DetectKeysAsync(timeout, cts.Token);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("socket not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once());
        }
        finally
        {
            // Restore original SSH_AUTH_SOCK
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", originalSshAuthSock);
        }
    }

    #endregion

    #region Edge Case Tests

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
        
        // May timeout or complete normally depending on system
        // Should not throw
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

    [Fact(Skip = "Requires process mocking - complex, better as integration test")]
    public void DetectKeysAsync_WithSshAddNotInstalled_HandlesGracefully()
    {
        // This test would require mocking System.Diagnostics.Process
        // Better suited for integration tests with actual environment
    }

    [Fact(Skip = "Requires actual SSH agent with non-ED25519 keys - integration test")]
    public void DetectKeysAsync_WithOnlyNonEd25519Keys_ReturnsEmptyList()
    {
        // This test requires an actual SSH agent with RSA/ECDSA keys only
        // Better suited for integration tests
    }

    [Fact(Skip = "Requires actual SSH agent with mixed keys - integration test")]
    public void DetectKeysAsync_WithMixedKeyTypes_ReturnsOnlyEd25519Keys()
    {
        // This test requires an actual SSH agent with both ED25519 and other key types
        // Better suited for integration tests
    }

    #endregion

    #region Platform-Specific Tests

    [Fact(Skip = "Platform-specific - Windows only")]
    public void DetectKeysAsync_OnWindows_UsesNamedPipe()
    {
        // This test would need to run only on Windows
        // Should verify that \\.\pipe\openssh-ssh-agent is used
        // Better as platform-specific integration test
    }

    [Fact(Skip = "Platform-specific - Unix/macOS only")]
    public void DetectKeysAsync_OnUnix_UsesSshAuthSock()
    {
        // This test would need to run only on Unix/macOS
        // Should verify SSH_AUTH_SOCK environment variable is used
        // Better as platform-specific integration test
    }

    #endregion

    #region Error Handling Tests

    [Fact(Skip = "Requires process error simulation - integration test")]
    public void DetectKeysAsync_WithSshAddError_LogsWarningAndReturnsEmpty()
    {
        // This test would require simulating ssh-add failure
        // Better suited for integration tests
    }

    [Fact(Skip = "Requires actual SSH agent with invalid output - integration test")]
    public void DetectKeysAsync_WithMalformedOutput_HandlesGracefully()
    {
        // This test requires actual SSH agent returning malformed data
        // Better suited for integration tests
    }

    #endregion

    #region Validation Tests

    [Fact(Skip = "Requires actual SSH agent - integration test")]
    public void DetectKeysAsync_SetsCorrectSshKeyInfoProperties()
    {
        // This test requires an actual SSH agent with ED25519 keys
        // Would verify:
        // - Source = SystemAgent
        // - DisplayName starts with "[System Agent]"
        // - AgentName = "ssh-agent"
        // - IsEd25519 = true
        // - PublicKey starts with "ssh-ed25519"
        // - ValidationResult = Valid
        // - DetectedAt is recent
        // - FilePath is null
        // Better suited for integration tests
    }

    [Fact(Skip = "Requires actual SSH agent with commented keys - integration test")]
    public void DetectKeysAsync_WithKeyComments_UsesCommentsInDisplayName()
    {
        // This test requires actual SSH agent with keys that have comments
        // Better suited for integration tests
    }

    [Fact(Skip = "Requires actual SSH agent with uncommented keys - integration test")]
    public void DetectKeysAsync_WithoutKeyComments_UsesDefaultDisplayName()
    {
        // This test requires actual SSH agent with keys without comments
        // Better suited for integration tests
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

    [Fact(Skip = "Requires process error simulation - integration test")]
    public void DetectKeysAsync_OnProcessError_LogsWarning()
    {
        // This test would require simulating ssh-add process failure
        // Better suited for integration tests
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task DetectKeysAsync_CompletesWithinTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        using var cts = new CancellationTokenSource();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await _detector.DetectKeysAsync(timeout, cts.Token);

        // Assert
        stopwatch.Stop();
        stopwatch.Elapsed.Should().BeLessThan(timeout + TimeSpan.FromSeconds(1)); // Allow small overhead
    }

    #endregion
}
