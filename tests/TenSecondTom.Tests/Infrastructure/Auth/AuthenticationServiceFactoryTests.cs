using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.Infrastructure.Auth;

/// <summary>
/// Tests for AuthenticationServiceFactory implementation.
/// Verifies factory logic for selecting between SSH agent and file-based authentication.
/// </summary>
public sealed class AuthenticationServiceFactoryTests
{
    private readonly Mock<ILogger<SshAgentAuthenticationService>> _mockSshAgentLogger;
    private readonly Mock<ILogger<SshKeyAuthenticationService>> _mockSshKeyLogger;
    private readonly Mock<ISshAgentClient> _mockAgentClient;

    public AuthenticationServiceFactoryTests()
    {
        _mockSshAgentLogger = new Mock<ILogger<SshAgentAuthenticationService>>();
        _mockSshKeyLogger = new Mock<ILogger<SshKeyAuthenticationService>>();
        _mockAgentClient = new Mock<ISshAgentClient>();
    }

    [Fact]
    public async Task Create_WithSystemAgentAndValidKeyPath_ReturnsSshAgentService()
    {
        // Arrange
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-ssh-agent-{Guid.NewGuid()}.sock");
        var tempKeyFile = Path.Combine(Path.GetTempPath(), $"id_ed25519-{Guid.NewGuid()}.pub");

        try
        {
            // Create a temporary socket file
            File.WriteAllText(socketPath, "");

            // Create properly formatted Ed25519 public key in SSH wire format
            var keyType = "ssh-ed25519"u8.ToArray();
            var keyData = new byte[32];
            Array.Fill(keyData, (byte)0x42);

            var publicKey = new byte[4 + keyType.Length + 4 + keyData.Length];
            var offset = 0;

            // Write type length (big-endian)
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = (byte)keyType.Length;

            // Write type string
            Array.Copy(keyType, 0, publicKey, offset, keyType.Length);
            offset += keyType.Length;

            // Write key length (big-endian)
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = (byte)keyData.Length;

            // Write key data
            Array.Copy(keyData, 0, publicKey, offset, keyData.Length);

            var publicKeyBase64 = Convert.ToBase64String(publicKey);

            // Write .pub file format
            File.WriteAllText(tempKeyFile, $"ssh-ed25519 {publicKeyBase64} test@example.com");

            var authOptions = new AuthOptions
            {
                KeySource = SshKeySource.SystemAgent,
                KeyPath = tempKeyFile,
                AgentSocketPath = socketPath
            };

            // Act
            var result = await AuthenticationServiceFactory.CreateAsync(
                authOptions,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert
            result.Should().BeOfType<SshAgentAuthenticationService>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
            if (File.Exists(tempKeyFile))
            {
                File.Delete(tempKeyFile);
            }
        }
    }

    [Fact]
    public async Task Create_WithOnePasswordAgentAndValidKeyPath_ReturnsSshAgentService()
    {
        // Arrange
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-1password-agent-{Guid.NewGuid()}.sock");
        var tempKeyFile = Path.Combine(Path.GetTempPath(), $"id_ed25519-{Guid.NewGuid()}.pub");

        try
        {
            // Create a temporary socket file
            File.WriteAllText(socketPath, "");

            // Create properly formatted Ed25519 public key in SSH wire format
            var keyType = "ssh-ed25519"u8.ToArray();
            var keyData = new byte[32];
            Array.Fill(keyData, (byte)0x42);

            var publicKey = new byte[4 + keyType.Length + 4 + keyData.Length];
            var offset = 0;

            // Write type length (big-endian)
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = (byte)keyType.Length;

            // Write type string
            Array.Copy(keyType, 0, publicKey, offset, keyType.Length);
            offset += keyType.Length;

            // Write key length (big-endian)
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = (byte)keyData.Length;

            // Write key data
            Array.Copy(keyData, 0, publicKey, offset, keyData.Length);

            var publicKeyBase64 = Convert.ToBase64String(publicKey);

            // Write .pub file format
            File.WriteAllText(tempKeyFile, $"ssh-ed25519 {publicKeyBase64} test@example.com");

            var authOptions = new AuthOptions
            {
                KeySource = SshKeySource.OnePasswordAgent,
                KeyPath = tempKeyFile,
                AgentSocketPath = socketPath
            };

            // Act
            var result = await AuthenticationServiceFactory.CreateAsync(
                authOptions,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert
            result.Should().BeOfType<SshAgentAuthenticationService>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempKeyFile))
            {
                File.Delete(tempKeyFile);
            }
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    [Fact]
    public async Task Create_WithAgentButNoKeyPath_UsesDefaultLocationIfAvailable()
    {
        // Arrange
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-agent-{Guid.NewGuid()}.sock");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultKeyExists = File.Exists(Path.Combine(homeDir, ".ssh", "id_ed25519.pub")) ||
                               File.Exists(Path.Combine(homeDir, ".ssh", "id_rsa.pub")) ||
                               File.Exists(Path.Combine(homeDir, ".ssh", "id_ecdsa.pub")) ||
                               File.Exists(Path.Combine(homeDir, ".ssh", "id_dsa.pub"));

        try
        {
            File.WriteAllText(socketPath, "");

            var authOptions = new AuthOptions
            {
                KeySource = SshKeySource.SystemAgent,
                AgentSocketPath = socketPath
                // KeyPath is intentionally not set - should check default locations
            };

            // Act
            var result = await AuthenticationServiceFactory.CreateAsync(
                authOptions,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert - behavior depends on whether default keys exist on the system
            if (defaultKeyExists)
            {
                // Default key found - should use SSH agent authentication
                result.Should().BeOfType<SshAgentAuthenticationService>(
                    "because a default SSH key was found in ~/.ssh/");
            }
            else
            {
                // No default keys - should fall back to file-based authentication
                result.Should().BeOfType<SshKeyAuthenticationService>(
                    "because no default SSH keys exist in ~/.ssh/");
            }
        }
        finally
        {
            // Cleanup
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    [Fact]
    public async Task Create_WithAgentButNoKeyPath_AndDefaultKeyExists_ReturnsSshAgentService()
    {
        // Arrange
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-agent-{Guid.NewGuid()}.sock");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var sshDir = Path.Combine(homeDir, ".ssh");
        var defaultKeyPath = Path.Combine(sshDir, "id_ed25519.pub");
        var tempKeyCreated = false;

        try
        {
            File.WriteAllText(socketPath, "");

            // Only create default key if it doesn't already exist
            // (we don't want to overwrite user's actual SSH key in tests!)
            if (!File.Exists(defaultKeyPath))
            {
                // Ensure .ssh directory exists
                Directory.CreateDirectory(sshDir);

                // Create properly formatted Ed25519 public key in SSH wire format
                var keyType = "ssh-ed25519"u8.ToArray();
                var keyData = new byte[32];
                Array.Fill(keyData, (byte)0x42);

                var publicKey = new byte[4 + keyType.Length + 4 + keyData.Length];
                var offset = 0;

                // Write type length (big-endian)
                publicKey[offset++] = 0;
                publicKey[offset++] = 0;
                publicKey[offset++] = 0;
                publicKey[offset++] = (byte)keyType.Length;

                // Write type string
                Array.Copy(keyType, 0, publicKey, offset, keyType.Length);
                offset += keyType.Length;

                // Write key length (big-endian)
                publicKey[offset++] = 0;
                publicKey[offset++] = 0;
                publicKey[offset++] = 0;
                publicKey[offset++] = (byte)keyData.Length;

                // Write key data
                Array.Copy(keyData, 0, publicKey, offset, keyData.Length);

                var publicKeyBase64 = Convert.ToBase64String(publicKey);

                // Write .pub file format
                File.WriteAllText(defaultKeyPath, $"ssh-ed25519 {publicKeyBase64} test@example.com");
                tempKeyCreated = true;
            }

            var authOptions = new AuthOptions
            {
                KeySource = SshKeySource.OnePasswordAgent,
                AgentSocketPath = socketPath
                // KeyPath not set - should use default location
            };

            // Act
            var result = await AuthenticationServiceFactory.CreateAsync(
                authOptions,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert - should use agent with key from default location
            result.Should().BeOfType<SshAgentAuthenticationService>();
        }
        finally
        {
            // Cleanup - only delete if we created it
            if (tempKeyCreated && File.Exists(defaultKeyPath))
            {
                File.Delete(defaultKeyPath);
            }
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }

    [Fact]
    public async Task Create_WithFileSystemKeySource_ReturnsSshKeyService()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.FileSystem,
            KeyPath = "~/.ssh/id_ed25519"
        };

        // Act
        var result = await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public async Task Create_WithEmptyAgentSocketPath_ReturnsSshKeyService()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.SystemAgent,
            KeyPath = "~/.ssh/id_ed25519",
            AgentSocketPath = ""
        };

        // Act
        var result = await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public async Task Create_WithWhitespaceAgentSocketPath_ReturnsSshKeyService()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.SecretiveAgent,
            KeyPath = "~/.ssh/id_ed25519",
            AgentSocketPath = "   "
        };

        // Act
        var result = await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public async Task Create_WithNullAuthOptions_ThrowsArgumentNullException()
    {
        // Arrange
        AuthOptions authOptions = null!;

        // Act
        var act = async () => await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("authOptions");
    }

    [Fact]
    public async Task Create_WithNullAgentClient_ThrowsArgumentNullException()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.FileSystem,
            KeyPath = "~/.ssh/id_ed25519"
        };

        // Act
        var act = async () => await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            null!,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("agentClient");
    }

    [Fact]
    public async Task Create_WithSecretiveAgentAndPubKeyFile_ReturnsSshAgentService()
    {
        // Arrange
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-secretive-agent-{Guid.NewGuid()}.sock");
        var tempKeyFile = Path.Combine(Path.GetTempPath(), $"id_ed25519-{Guid.NewGuid()}.pub");

        try
        {
            // Create a temporary socket file
            File.WriteAllText(socketPath, "");

            // Create properly formatted Ed25519 public key in SSH wire format
            var keyType = "ssh-ed25519"u8.ToArray();
            var keyData = new byte[32];
            Array.Fill(keyData, (byte)0x42);

            var publicKey = new byte[4 + keyType.Length + 4 + keyData.Length];
            var offset = 0;

            // Write type length (big-endian)
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = (byte)keyType.Length;

            // Write type string
            Array.Copy(keyType, 0, publicKey, offset, keyType.Length);
            offset += keyType.Length;

            // Write key length (big-endian)
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = 0;
            publicKey[offset++] = (byte)keyData.Length;

            // Write key data
            Array.Copy(keyData, 0, publicKey, offset, keyData.Length);

            var publicKeyBase64 = Convert.ToBase64String(publicKey);

            // Write full SSH public key line format
            File.WriteAllText(tempKeyFile, $"ssh-ed25519 {publicKeyBase64} test@example.com");

            var authOptions = new AuthOptions
            {
                KeySource = SshKeySource.SecretiveAgent,
                KeyPath = tempKeyFile,
                AgentSocketPath = socketPath
            };

            // Act
            var result = await AuthenticationServiceFactory.CreateAsync(
                authOptions,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert
            result.Should().BeOfType<SshAgentAuthenticationService>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
            if (File.Exists(tempKeyFile))
            {
                File.Delete(tempKeyFile);
            }
        }
    }

    [Fact]
    public async Task Create_WithNullSshAgentLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.FileSystem,
            KeyPath = "~/.ssh/id_ed25519"
        };

        // Act
        var act = async () => await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            null!,
            _mockSshKeyLogger.Object);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("sshAgentLogger");
    }

    [Fact]
    public async Task Create_WithNullSshKeyLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.ManualPath,
            KeyPath = "~/.ssh/custom_key"
        };

        // Act
        var act = async () => await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("sshKeyLogger");
    }

    [Fact]
    public async Task Create_WithManualPathKeySource_ReturnsSshKeyService()
    {
        // Arrange
        var authOptions = new AuthOptions
        {
            KeySource = SshKeySource.ManualPath,
            KeyPath = "/custom/path/to/id_ed25519"
        };

        // Act
        var result = await AuthenticationServiceFactory.CreateAsync(
            authOptions,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public async Task Create_WithAgentAndNonExistentKeyFile_FallsBackToDefaultLocations()
    {
        // Arrange
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-agent-{Guid.NewGuid()}.sock");
        var nonExistentKeyPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.pub");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var defaultKeyExists = File.Exists(Path.Combine(homeDir, ".ssh", "id_ed25519.pub")) ||
                               File.Exists(Path.Combine(homeDir, ".ssh", "id_rsa.pub")) ||
                               File.Exists(Path.Combine(homeDir, ".ssh", "id_ecdsa.pub")) ||
                               File.Exists(Path.Combine(homeDir, ".ssh", "id_dsa.pub"));

        try
        {
            File.WriteAllText(socketPath, "");

            var authOptions = new AuthOptions
            {
                KeySource = SshKeySource.SystemAgent,
                KeyPath = nonExistentKeyPath,
                AgentSocketPath = socketPath
            };

            // Act
            var result = await AuthenticationServiceFactory.CreateAsync(
                authOptions,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert - when explicit KeyPath doesn't exist, falls back to default locations
            if (defaultKeyExists)
            {
                // Default key found - should use SSH agent authentication
                result.Should().BeOfType<SshAgentAuthenticationService>(
                    "because the explicit KeyPath doesn't exist but a default SSH key was found in ~/.ssh/");
            }
            else
            {
                // No default keys - should fall back to file-based authentication
                result.Should().BeOfType<SshKeyAuthenticationService>(
                    "because neither the explicit KeyPath nor default SSH keys exist");
            }
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }
}
