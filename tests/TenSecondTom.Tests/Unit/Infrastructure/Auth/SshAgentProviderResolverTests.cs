using FluentAssertions;
using TenSecondTom.Infrastructure.Auth;

namespace TenSecondTom.Tests.Unit.Infrastructure.Auth;

/// <summary>
/// Tests for SSH agent provider resolution and auto-detection.
/// Verifies platform-specific socket path resolution and provider detection logic.
/// </summary>
public sealed class SshAgentProviderResolverTests
{
    [Fact]
    public void GetProviderName_WithOnePassword_ReturnsCorrectName()
    {
        // Act
        var name = SshAgentProviderResolver.GetProviderName(SshAgentProvider.OnePassword);

        // Assert
        name.Should().Be("1Password SSH Agent");
    }

    [Fact]
    public void GetProviderName_WithSecretive_ReturnsCorrectName()
    {
        // Act
        var name = SshAgentProviderResolver.GetProviderName(SshAgentProvider.Secretive);

        // Assert
        name.Should().Be("Secretive SSH Agent");
    }

    [Fact]
    public void GetProviderName_WithSystem_ReturnsCorrectName()
    {
        // Act
        var name = SshAgentProviderResolver.GetProviderName(SshAgentProvider.System);

        // Assert
        name.Should().Be("System SSH Agent");
    }

    [Fact]
    public void GetProviderName_WithAuto_ReturnsCorrectName()
    {
        // Act
        var name = SshAgentProviderResolver.GetProviderName(SshAgentProvider.Auto);

        // Assert
        name.Should().Be("Auto-detected SSH Agent");
    }

    [Fact]
    public void GetSocketPath_WithAuto_ReturnsNonNull()
    {
        // Act
        var path = SshAgentProviderResolver.GetSocketPath(SshAgentProvider.Auto);

        // Assert
        // Should return some path (could be 1Password, Secretive, or System)
        path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DetectProvider_WithOnePasswordPath_ReturnsOnePassword()
    {
        // Arrange
        var onePasswordPath = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/Group Containers/2BUA8C4S2C.com.1password/t/agent.sock")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".1password/agent.sock");

        // Act
        var provider = SshAgentProviderResolver.DetectProvider(onePasswordPath);

        // Assert
        provider.Should().Be(SshAgentProvider.OnePassword);
    }

    [Fact]
    public void DetectProvider_WithSecretivePath_ReturnsSecretive()
    {
        // Skip test if not on macOS (Secretive is macOS-only)
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        // Arrange
        var secretivePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library/Containers/com.maxgoedjen.Secretive.SecretAgent/Data/socket.ssh");

        // Act
        var provider = SshAgentProviderResolver.DetectProvider(secretivePath);

        // Assert
        provider.Should().Be(SshAgentProvider.Secretive);
    }

    [Fact]
    public void DetectProvider_WithSystemPath_ReturnsSystem()
    {
        // Arrange
        var systemPath = "/tmp/ssh-agent.sock";

        // Act
        var provider = SshAgentProviderResolver.DetectProvider(systemPath);

        // Assert
        provider.Should().Be(SshAgentProvider.System);
    }

    [Fact]
    public void DetectProvider_WithEmptyPath_ReturnsSystem()
    {
        // Act
        var provider = SshAgentProviderResolver.DetectProvider(string.Empty);

        // Assert
        provider.Should().Be(SshAgentProvider.System);
    }

    [Fact]
    public void GetSocketPath_WithOnePassword_ReturnsCorrectPath()
    {
        // Act
        var path = SshAgentProviderResolver.GetSocketPath(SshAgentProvider.OnePassword);

        // Assert
        if (OperatingSystem.IsMacOS())
        {
            path.Should().Contain("1password");
            path.Should().Contain("agent.sock");
        }
        else if (OperatingSystem.IsLinux())
        {
            path.Should().Be(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".1password/agent.sock"));
        }
        // Windows 1Password uses named pipe, not socket
    }

    [Fact]
    public void GetSocketPath_WithSecretive_OnMacOS_ReturnsPathOrNull()
    {
        // Skip test if not on macOS
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        // Act
        var path = SshAgentProviderResolver.GetSocketPath(SshAgentProvider.Secretive);

        // Assert
        // Path will be null if Secretive is not installed or running
        // If it exists, it should contain the expected strings
        if (path != null)
        {
            path.Should().Contain("Secretive");
            path.Should().Contain("socket.ssh");
        }
        else
        {
            // If null, Secretive is not installed/running - this is valid
            path.Should().BeNull();
        }
    }

    [Fact]
    public void GetSocketPath_WithSecretive_OnNonMacOS_ReturnsNull()
    {
        // Skip test if on macOS
        if (OperatingSystem.IsMacOS())
        {
            return;
        }

        // Act
        var path = SshAgentProviderResolver.GetSocketPath(SshAgentProvider.Secretive);

        // Assert
        path.Should().BeNull();
    }

    [Fact]
    public void GetSocketPath_WithSystem_UsesSSH_AUTH_SOCK()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        try
        {
            var testPath = "/tmp/test-ssh-agent.sock";
            // Create a temporary file to simulate socket
            File.WriteAllText(testPath, "test");

            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", testPath);

            // Act
            var path = SshAgentProviderResolver.GetSocketPath(SshAgentProvider.System);

            // Assert
            path.Should().Be(testPath);

            // Cleanup
            File.Delete(testPath);
        }
        finally
        {
            // Restore original value
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", originalValue);
        }
    }

    [Fact]
    public void GetSocketPath_WithSystem_WhenSSH_AUTH_SOCK_NotSet_ReturnsNull()
    {
        // Arrange
        var originalValue = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        try
        {
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);

            // Act
            var path = SshAgentProviderResolver.GetSocketPath(SshAgentProvider.System);

            // Assert
            path.Should().BeNull();
        }
        finally
        {
            // Restore original value
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", originalValue);
        }
    }
}
