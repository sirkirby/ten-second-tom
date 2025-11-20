using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NSec.Cryptography;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Auth;

/// <summary>
/// Tests for Ed25519 signature verification implementation.
/// Uses RFC 8032 test vectors and security test cases.
/// </summary>
public sealed class Ed25519SignatureVerificationTests
{
    // RFC 8032 Test Vector 1
    // https://datatracker.ietf.org/doc/html/rfc8032#section-7.1
    private static readonly byte[] TestVector1_SecretKey = Convert.FromHexString(
        "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
    
    private static readonly byte[] TestVector1_PublicKey = Convert.FromHexString(
        "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");
    
    private static readonly byte[] TestVector1_Message = Array.Empty<byte>();
    
    private static readonly byte[] TestVector1_Signature = Convert.FromHexString(
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
        "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");

    // RFC 8032 Test Vector 2
    private static readonly byte[] TestVector2_PublicKey = Convert.FromHexString(
        "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c");
    
    private static readonly byte[] TestVector2_Message = Convert.FromHexString("72");
    
    private static readonly byte[] TestVector2_Signature = Convert.FromHexString(
        "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da" +
        "085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00");

    // RFC 8032 Test Vector 3
    private static readonly byte[] TestVector3_PublicKey = Convert.FromHexString(
        "fc51cd8e6218a1a38da47ed00230f0580816ed13ba3303ac5deb911548908025");
    
    private static readonly byte[] TestVector3_Message = Convert.FromHexString("af82");
    
    private static readonly byte[] TestVector3_Signature = Convert.FromHexString(
        "6291d657deec24024827e69c3abe01a30ce548a284743a445e3680d7db5ac3ac" +
        "18ff9b538d16f290ae67f760984dc6594a7c15e9716ed28dc027beceea1ec40a");

    /// <summary>
    /// Helper to create SSH-formatted Ed25519 public key
    /// Format: 4 bytes type length + "ssh-ed25519" + 4 bytes key length + 32 bytes key
    /// </summary>
    private static byte[] CreateSshEd25519PublicKey(byte[] rawPublicKey)
    {
        if (rawPublicKey.Length != 32)
            throw new ArgumentException("Ed25519 public key must be 32 bytes", nameof(rawPublicKey));

        var keyType = "ssh-ed25519"u8.ToArray();
        var sshKey = new byte[4 + keyType.Length + 4 + rawPublicKey.Length];
        var offset = 0;

        // Write type length (big-endian)
        sshKey[offset++] = 0;
        sshKey[offset++] = 0;
        sshKey[offset++] = 0;
        sshKey[offset++] = (byte)keyType.Length;

        // Write type string
        Array.Copy(keyType, 0, sshKey, offset, keyType.Length);
        offset += keyType.Length;

        // Write key length (big-endian)
        sshKey[offset++] = 0;
        sshKey[offset++] = 0;
        sshKey[offset++] = 0;
        sshKey[offset++] = (byte)rawPublicKey.Length;

        // Write key data
        Array.Copy(rawPublicKey, 0, sshKey, offset, rawPublicKey.Length);

        return sshKey;
    }

    /// <summary>
    /// Helper to authenticate with specific signature and challenge.
    /// Returns true if signature verification succeeded.
    /// </summary>
    private static async Task<bool> AuthenticateWithSignature(
        byte[] publicKey,
        byte[] challenge,
        byte[] signature)
    {
        var mockLogger = new Mock<ILogger<SshAgentAuthenticationService>>();
        var mockAgentClient = new Mock<ISshAgentClient>();

        // Mock successful connection
        mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<SshAgentProvider>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Mock SignDataAsync to return the provided signature with the expected challenge
        mockAgentClient
            .Setup(c => c.SignDataAsync(
                It.IsAny<byte[]>(),
                It.Is<byte[]>(ch => ch.SequenceEqual(challenge)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(signature);

        var service = new SshAgentAuthenticationService(
            mockAgentClient.Object,
            publicKey,
            mockLogger.Object);

        var result = await service.AuthenticateAsync();
        return result.IsSuccess;
    }

    #region RFC 8032 Test Vector Validation

    [Fact]
    public void VerifySignature_WithRfc8032TestVector1_ReturnsTrue()
    {
        // Arrange - Test that NSec correctly implements RFC 8032
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(algorithm, TestVector1_PublicKey, KeyBlobFormat.RawPublicKey);
        
        // Act
        bool isValid = algorithm.Verify(publicKey, TestVector1_Message, TestVector1_Signature);

        // Assert
        isValid.Should().BeTrue("RFC 8032 Test Vector 1 (empty message) should verify correctly");
    }

    [Fact]
    public void VerifySignature_WithRfc8032TestVector2_ReturnsTrue()
    {
        // Arrange - Test that NSec correctly implements RFC 8032
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(algorithm, TestVector2_PublicKey, KeyBlobFormat.RawPublicKey);
        
        // Act
        bool isValid = algorithm.Verify(publicKey, TestVector2_Message, TestVector2_Signature);

        // Assert
        isValid.Should().BeTrue("RFC 8032 Test Vector 2 (single byte 0x72) should verify correctly");
    }

    [Fact]
    public void VerifySignature_WithRfc8032TestVector3_ReturnsTrue()
    {
        // Arrange - Test that NSec correctly implements RFC 8032
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(algorithm, TestVector3_PublicKey, KeyBlobFormat.RawPublicKey);
        
        // Act
        bool isValid = algorithm.Verify(publicKey, TestVector3_Message, TestVector3_Signature);

        // Assert
        isValid.Should().BeTrue("RFC 8032 Test Vector 3 (two bytes 0xaf82) should verify correctly");
    }

    [Fact]
    public void VerifySignature_WithModifiedSignatureFromTestVector1_ReturnsFalse()
    {
        // Arrange - Modify one byte of valid signature
        var modifiedSignature = (byte[])TestVector1_Signature.Clone();
        modifiedSignature[0] ^= 0x01; // Flip one bit
        
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(algorithm, TestVector1_PublicKey, KeyBlobFormat.RawPublicKey);

        // Act
        bool isValid = algorithm.Verify(publicKey, TestVector1_Message, modifiedSignature);

        // Assert
        isValid.Should().BeFalse("modified signature should be rejected (tamper detection)");
    }

    [Fact]
    public void VerifySignature_WithModifiedMessageFromTestVector1_ReturnsFalse()
    {
        // Arrange - Use different message than what was signed
        var differentMessage = new byte[] { 0xFF };
        var algorithm = SignatureAlgorithm.Ed25519;
        var publicKey = PublicKey.Import(algorithm, TestVector1_PublicKey, KeyBlobFormat.RawPublicKey);

        // Act
        bool isValid = algorithm.Verify(publicKey, differentMessage, TestVector1_Signature);

        // Assert
        isValid.Should().BeFalse("signature with wrong message should be rejected");
    }

    [Fact]
    public void VerifySignature_WithWrongPublicKeyFromTestVector1_ReturnsFalse()
    {
        // Arrange - Use TestVector2 public key with TestVector1 signature
        var algorithm = SignatureAlgorithm.Ed25519;
        var wrongPublicKey = PublicKey.Import(algorithm, TestVector2_PublicKey, KeyBlobFormat.RawPublicKey);

        // Act
        bool isValid = algorithm.Verify(wrongPublicKey, TestVector1_Message, TestVector1_Signature);

        // Assert
        isValid.Should().BeFalse("signature with wrong public key should be rejected");
    }

    #endregion

}
