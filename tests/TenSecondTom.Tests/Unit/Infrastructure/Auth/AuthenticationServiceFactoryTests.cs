using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Auth;

namespace TenSecondTom.Tests.Unit.Infrastructure.Auth;

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
    public void Create_WithSshAgentAvailableAndPublicKeyConfigured_ReturnsSshAgentService()
    {
        // Arrange
        // Create a temporary socket file that File.Exists() can verify
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-ssh-agent-{Guid.NewGuid()}.sock");
        File.WriteAllText(socketPath, ""); // Create empty file to simulate socket
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", socketPath);
        
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
        
        var configData = new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKey"] = publicKeyBase64,
            ["TenSecondTom:Auth:SshAgentProvider"] = "System" // Use System provider to check SSH_AUTH_SOCK
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshAgentAuthenticationService>();
        
        // Cleanup
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }
    }

    [Fact]
    public void Create_WithSshAgentAvailableAndPublicKeyPathConfigured_ReturnsSshAgentService()
    {
        // Arrange
        // Create a temporary socket file that File.Exists() can verify
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-ssh-agent-{Guid.NewGuid()}.sock");
        File.WriteAllText(socketPath, ""); // Create empty file to simulate socket
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", socketPath);
        
        // Create a temporary SSH public key file
        var tempFile = Path.GetTempFileName();
        try
        {
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
            
            // Write .pub file format: "ssh-ed25519 base64data comment"
            File.WriteAllText(tempFile, $"ssh-ed25519 {publicKeyBase64} test@example.com");
            
            var configData = new Dictionary<string, string?>
            {
                ["TenSecondTom:Auth:PublicKeyPath"] = tempFile,
                ["TenSecondTom:Auth:SshAgentProvider"] = "System" // Use System provider to check SSH_AUTH_SOCK
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            // Act
            var result = AuthenticationServiceFactory.Create(
                configuration,
                _mockAgentClient.Object,
                _mockSshAgentLogger.Object,
                _mockSshKeyLogger.Object);

            // Assert
            result.Should().BeOfType<SshAgentAuthenticationService>();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
            Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
        }
    }

    [Fact]
    public void Create_WithSshAgentAvailableButNoPublicKeyConfigured_ReturnsSshKeyService()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", "/tmp/ssh-agent.sock");
        
        var configData = new Dictionary<string, string?>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
        
        // Cleanup
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
    }

    [Fact]
    public void Create_WithoutSshAgent_ReturnsSshKeyService()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
        
        var configData = new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKey"] = "AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKeyData"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
    }

    [Fact]
    public void Create_WithEmptySshAuthSock_ReturnsSshKeyService()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", "");
        
        var configData = new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKey"] = "AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKeyData"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
        
        // Cleanup
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
    }

    [Fact]
    public void Create_WithWhitespaceSshAuthSock_ReturnsSshKeyService()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", "   ");
        
        var configData = new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKey"] = "AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKeyData"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshKeyAuthenticationService>();
        
        // Cleanup
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
    }

    [Fact]
    public void Create_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Arrange
        IConfiguration configuration = null!;

        // Act
        var act = () => AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Create_WithNullAgentClient_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => AuthenticationServiceFactory.Create(
            configuration,
            null!,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("agentClient");
    }

    [Fact]
    public void Create_WithFullSshPublicKeyLine_ReturnsSshAgentService()
    {
        // Arrange
        // Create a temporary socket file that File.Exists() can verify
        var socketPath = Path.Combine(Path.GetTempPath(), $"test-ssh-agent-{Guid.NewGuid()}.sock");
        File.WriteAllText(socketPath, ""); // Create empty file to simulate socket
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", socketPath);
        
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
        
        // Full SSH public key line format: "ssh-ed25519 base64data comment"
        var fullKeyLine = $"ssh-ed25519 {publicKeyBase64} test@example.com";
        
        var configData = new Dictionary<string, string?>
        {
            ["TenSecondTom:Auth:PublicKey"] = fullKeyLine,
            ["TenSecondTom:Auth:SshAgentProvider"] = "System" // Use System provider to check SSH_AUTH_SOCK
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Act
        var result = AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            _mockSshKeyLogger.Object);

        // Assert
        result.Should().BeOfType<SshAgentAuthenticationService>();
        
        // Cleanup
        Environment.SetEnvironmentVariable("SSH_AUTH_SOCK", null);
        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }
    }

    [Fact]
    public void Create_WithNullSshAgentLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            null!,
            _mockSshKeyLogger.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sshAgentLogger");
    }

    [Fact]
    public void Create_WithNullSshKeyLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();

        // Act
        var act = () => AuthenticationServiceFactory.Create(
            configuration,
            _mockAgentClient.Object,
            _mockSshAgentLogger.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sshKeyLogger");
    }
}
