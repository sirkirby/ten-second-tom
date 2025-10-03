using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NSec.Cryptography;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Auth;

/// <summary>
/// Tests for SshAgentAuthenticationService implementation.
/// Tests verify SSH agent protocol communication, challenge-response authentication,
/// and signature verification for secure authentication without accessing private key files.
/// </summary>
public sealed class SshAgentAuthenticationServiceTests
{
    private readonly Mock<ILogger<SshAgentAuthenticationService>> _mockLogger;
    private readonly Mock<ISshAgentClient> _mockAgentClient;
    private readonly byte[] _testPublicKey;
    private readonly Key _testPrivateKey;
    private byte[]? _lastChallenge;

    public SshAgentAuthenticationServiceTests()
    {
        _mockLogger = new Mock<ILogger<SshAgentAuthenticationService>>();
        _mockAgentClient = new Mock<ISshAgentClient>();

        // Generate a real Ed25519 key pair for testing
        var algorithm = SignatureAlgorithm.Ed25519;
        _testPrivateKey = Key.Create(algorithm, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        var publicKeyBytes = _testPrivateKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

        // Create properly formatted SSH Ed25519 public key
        // Format: 4 bytes (type length) + "ssh-ed25519" + 4 bytes (key length) + 32 bytes (key data)
        var keyType = "ssh-ed25519"u8.ToArray();

        _testPublicKey = new byte[4 + keyType.Length + 4 + publicKeyBytes.Length];
        var offset = 0;

        // Write type length (big-endian)
        _testPublicKey[offset++] = 0;
        _testPublicKey[offset++] = 0;
        _testPublicKey[offset++] = 0;
        _testPublicKey[offset++] = (byte)keyType.Length;

        // Write type string
        Array.Copy(keyType, 0, _testPublicKey, offset, keyType.Length);
        offset += keyType.Length;

        // Write key length (big-endian)
        _testPublicKey[offset++] = 0;
        _testPublicKey[offset++] = 0;
        _testPublicKey[offset++] = 0;
        _testPublicKey[offset++] = (byte)publicKeyBytes.Length;

        // Write key data
        Array.Copy(publicKeyBytes, 0, _testPublicKey, offset, publicKeyBytes.Length);

        // Setup SignDataAsync to generate valid signatures using the private key
        _mockAgentClient
            .Setup(c => c.SignDataAsync(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<byte[], byte[], CancellationToken>((pubKey, challenge, ct) => 
            {
                _lastChallenge = challenge;
            })
            .ReturnsAsync(() => 
            {
                // Generate a valid Ed25519 signature for the challenge
                if (_lastChallenge == null) return new byte[64];
                return SignatureAlgorithm.Ed25519.Sign(_testPrivateKey, _lastChallenge);
            });
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidAgentAndKey_CreatesSession()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.IsActive.Should().BeTrue();
        result.Value.SshKeyHash.Should().NotBeNullOrEmpty();
        result.Value.SshKeyHash.Should().HaveLength(64); // SHA256 hex string

        _mockAgentClient.Verify(
            c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockAgentClient.Verify(
            c => c.SignDataAsync(
                It.Is<byte[]>(pk => pk.SequenceEqual(_testPublicKey)),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAgentUnavailable_ReturnsFailure()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("SSH agent");
        result.Error.Should().Contain("not available");

        _mockAgentClient.Verify(
            c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockAgentClient.Verify(
            c => c.SignDataAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenAgentDeniesSignature_ReturnsFailure()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockAgentClient
            .Setup(c => c.SignDataAsync(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // Agent denied signature

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("denied");
        result.Error.Should().Contain("signature");

        _mockAgentClient.Verify(
            c => c.SignDataAsync(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidSignature_ReturnsFailure()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Return invalid signature (all zeros)
        var invalidSignature = new byte[64];
        _mockAgentClient
            .Setup(c => c.SignDataAsync(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidSignature);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("signature");
        result.Error.Should().Contain("verification");
    }

    [Fact]
    public async Task AuthenticateAsync_WithCancellation_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        Func<Task> act = async () => await service.AuthenticateAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithActiveSession_ReturnsTrue()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        await service.AuthenticateAsync();
        var isAuthenticated = await service.IsAuthenticatedAsync();

        // Assert
        isAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthenticatedAsync_WithoutSession_ReturnsFalse()
    {
        // Arrange
        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var isAuthenticated = await service.IsAuthenticatedAsync();

        // Assert
        isAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_WithActiveSession_InvalidatesSession()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        await service.AuthenticateAsync();

        // Act
        var result = await service.LogoutAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();

        var isAuthenticated = await service.IsAuthenticatedAsync();
        isAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_WithoutActiveSession_ReturnsError()
    {
        // Arrange
        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var result = await service.LogoutAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No active session");
    }

    [Fact]
    public async Task AuthenticateAsync_GeneratesUniqueChallenge_ForEachAttempt()
    {
        // Arrange
        var capturedChallenges = new List<byte[]>();

        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Constructor already sets up SignDataAsync with valid signature generation
        // Just need to capture challenges for this test
        var capturedChallengesLocal = capturedChallenges;
        _mockAgentClient
            .Setup(c => c.SignDataAsync(
                It.IsAny<byte[]>(),
                It.IsAny<byte[]>(),
                It.IsAny<CancellationToken>()))
            .Callback<byte[], byte[], CancellationToken>((pk, challenge, ct) =>
            {
                capturedChallengesLocal.Add((byte[])challenge.Clone());
                _lastChallenge = challenge;
            })
            .ReturnsAsync(() => 
            {
                if (_lastChallenge == null) return new byte[64];
                return SignatureAlgorithm.Ed25519.Sign(_testPrivateKey, _lastChallenge);
            });

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        await service.AuthenticateAsync();
        await service.LogoutAsync();
        await service.AuthenticateAsync();

        // Assert
        capturedChallenges.Should().HaveCount(2);
        capturedChallenges[0].Should().NotBeEquivalentTo(capturedChallenges[1],
            "each authentication attempt should use a unique challenge");
    }

    [Fact]
    public async Task AuthenticateAsync_LogsAuthenticationAttempt()
    {
        // Arrange
        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        await service.AuthenticateAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("authentication")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task AuthenticateAsync_WithAgentError_LogsErrorAndReturnsFailure()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Agent communication error");

        _mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var service = new SshAgentAuthenticationService(
            _mockAgentClient.Object,
            _testPublicKey,
            _mockLogger.Object);

        // Act
        var result = await service.AuthenticateAsync();

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("error");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.Is<Exception>(ex => ex == expectedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_WithNullPublicKey_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () =>
        {
            _ = new SshAgentAuthenticationService(
                _mockAgentClient.Object,
                null!,
                _mockLogger.Object);
        };

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("publicKey");
    }

    [Fact]
    public void Constructor_WithEmptyPublicKey_ThrowsArgumentException()
    {
        // Arrange & Act
        Action act = () =>
        {
            _ = new SshAgentAuthenticationService(
                _mockAgentClient.Object,
                Array.Empty<byte>(),
                _mockLogger.Object);
        };

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*public key*");
    }
}
