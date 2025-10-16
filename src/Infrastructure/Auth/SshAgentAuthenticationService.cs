using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Provides authentication using SSH agent for signing challenges.
/// This is more secure than file-based SSH keys as the private key never leaves the agent.
/// </summary>
public sealed class SshAgentAuthenticationService : IAuthenticationService
{
    private readonly ISshAgentClient _agentClient;
    private readonly byte[] _publicKey;
    private readonly ILogger<SshAgentAuthenticationService> _logger;
    private UserSession? _currentSession;

    /// <summary>
    /// Initializes a new instance of the SSH agent authentication service.
    /// </summary>
    /// <param name="agentClient">The SSH agent client for communication.</param>
    /// <param name="publicKey">The SSH public key to authenticate with (SSH wire format).</param>
    /// <param name="logger">Logger instance.</param>
    public SshAgentAuthenticationService(
        ISshAgentClient agentClient,
        byte[] publicKey,
        ILogger<SshAgentAuthenticationService> logger)
    {
        ArgumentNullException.ThrowIfNull(agentClient);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(publicKey);
        
        if (publicKey.Length == 0)
        {
            throw new ArgumentException("Public key cannot be empty", nameof(publicKey));
        }

        _agentClient = agentClient;
        _logger = logger;
        _publicKey = publicKey;
    }

    /// <summary>
    /// Authenticates using SSH agent challenge-response.
    /// </summary>
    public async Task<Result<UserSession>> AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Attempting SSH agent authentication");

            // Connect to SSH agent (auto-detect provider)
            var connected = await _agentClient.ConnectAsync(SshAgentProvider.Auto, cancellationToken).ConfigureAwait(false);
            if (!connected)
            {
                _logger.LogWarning("SSH agent not available");
                return Result<UserSession>.Failure("SSH agent not available. Ensure SSH agent is running (1Password, Secretive, or system SSH agent).");
            }

            // Generate random challenge
            var challenge = GenerateChallenge();

            // Request signature from agent
            var signature = await _agentClient.SignDataAsync(_publicKey, challenge, cancellationToken).ConfigureAwait(false);
            if (signature == null)
            {
                _logger.LogWarning("SSH agent denied signature request");
                return Result<UserSession>.Failure("SSH agent denied signature request. Key may not be loaded.");
            }

            // Verify signature
            var verificationResult = VerifySignature(_publicKey, challenge, signature);
            if (!verificationResult.IsSuccess || verificationResult.Value == false)
            {
                var error = verificationResult.Error ?? "Signature verification failed";
                _logger.LogError("Signature verification failed: {Error}", error);
                return Result<UserSession>.Failure(error);
            }

            // Create session
            var now = DateTimeOffset.UtcNow;
            _currentSession = new UserSession
            {
                SessionId = Guid.NewGuid(),
                SshKeyHash = ComputeKeyHash(_publicKey),
                CreatedAt = now,
                LastAccessedAt = now,
                IsActive = true
            };

            _logger.LogInformation("SSH agent authentication successful");
            return Result<UserSession>.Success(_currentSession);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSH agent authentication cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSH agent authentication failed with exception");
            return Result<UserSession>.Failure($"Authentication failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentSession != null);
    }

    /// <inheritdoc/>
    public Task<Result<bool>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_currentSession == null)
        {
            return Task.FromResult(Result<bool>.Failure("No active session to logout"));
        }

        _currentSession = null;
        _logger.LogInformation("SSH agent session logged out");
        return Task.FromResult(Result<bool>.Success(true));
    }

    /// <summary>
    /// Generates a random 32-byte challenge for authentication.
    /// </summary>
    private static byte[] GenerateChallenge()
    {
        var challenge = new byte[32];
        RandomNumberGenerator.Fill(challenge);
        return challenge;
    }

    /// <summary>
    /// Verifies the SSH agent's signature against the challenge.
    /// Supports Ed25519 and RSA key types.
    /// </summary>
    private Result<bool> VerifySignature(byte[] publicKey, byte[] challenge, byte[] signature)
    {
        try
        {
            // Parse public key to determine algorithm
            var keyType = ParseKeyType(publicKey);
            
            return keyType switch
            {
                "ssh-ed25519" => VerifyEd25519Signature(publicKey, challenge, signature),
                "ssh-rsa" => VerifyRsaSignature(publicKey, challenge, signature),
                _ => Result<bool>.Failure($"Unsupported key type: {keyType}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Signature verification failed");
            return Result<bool>.Failure($"Signature verification error: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses the SSH key type from the public key blob.
    /// </summary>
    private static string ParseKeyType(byte[] publicKey)
    {
        if (publicKey.Length < 4)
        {
            throw new ArgumentException("Public key too short", nameof(publicKey));
        }

        var typeLength = BinaryPrimitives.ReadUInt32BigEndian(publicKey);
        if (typeLength > publicKey.Length - 4)
        {
            throw new ArgumentException("Invalid public key format", nameof(publicKey));
        }

        return System.Text.Encoding.ASCII.GetString(publicKey, 4, (int)typeLength);
    }

    /// <summary>
    /// Verifies Ed25519 signature using NSec.Cryptography.
    /// </summary>
    private Result<bool> VerifyEd25519Signature(byte[] publicKey, byte[] challenge, byte[] signature)
    {
        try
        {
            // Extract Ed25519 public key (32 bytes) from SSH public key blob
            // Format: 4 bytes type length + type string + 4 bytes key length + 32 bytes key
            var offset = 4 + 11; // Skip "ssh-ed25519" (11 bytes)
            
            if (offset + 4 > publicKey.Length)
            {
                return Result<bool>.Failure("Invalid Ed25519 public key format");
            }

            var keyLength = BinaryPrimitives.ReadUInt32BigEndian(publicKey.AsSpan(offset));
            offset += 4;

            if (keyLength != 32 || offset + 32 > publicKey.Length)
            {
                return Result<bool>.Failure("Invalid Ed25519 public key length");
            }

            // Ed25519 signatures are always 64 bytes
            if (signature.Length != 64)
            {
                return Result<bool>.Failure($"Invalid Ed25519 signature length: expected 64, got {signature.Length}");
            }

            // Extract the 32-byte Ed25519 public key
            var ed25519PublicKey = publicKey.AsSpan(offset, 32).ToArray();

            // Verify signature using NSec.Cryptography (RFC 8032 compliant)
            var algorithm = NSec.Cryptography.SignatureAlgorithm.Ed25519;
            var key = NSec.Cryptography.PublicKey.Import(
                algorithm,
                ed25519PublicKey,
                NSec.Cryptography.KeyBlobFormat.RawPublicKey);

            bool isValid = algorithm.Verify(key, challenge, signature);

            if (isValid)
            {
                _logger.LogDebug("Ed25519 signature verification successful");
                return Result<bool>.Success(true);
            }
            else
            {
                _logger.LogWarning("Ed25519 signature verification failed: invalid signature");
                return Result<bool>.Failure("SSH agent signature verification failed");
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Ed25519 signature verification error: invalid key or signature format");
            return Result<bool>.Failure($"Signature verification error: {ex.Message}");
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            _logger.LogError(ex, "Ed25519 cryptographic verification error");
            return Result<bool>.Failure($"Cryptographic error during signature verification: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during Ed25519 signature verification");
            return Result<bool>.Failure($"Unexpected verification error: {ex.Message}");
        }
    }

    /// <summary>
    /// Verifies RSA signature using .NET's built-in RSA support.
    /// </summary>
    private Result<bool> VerifyRsaSignature(byte[] publicKey, byte[] challenge, byte[] signature)
    {
        try
        {
            // Parse RSA public key from SSH format
            // Format: 4 bytes type length + "ssh-rsa" + 4 bytes e length + e + 4 bytes n length + n
            var offset = 4 + 7; // Skip "ssh-rsa" (7 bytes)
            
            if (offset + 4 > publicKey.Length)
            {
                return Result<bool>.Failure("Invalid RSA public key format");
            }

            // Read exponent (e)
            var eLength = BinaryPrimitives.ReadUInt32BigEndian(publicKey.AsSpan(offset));
            offset += 4;
            
            if (offset + eLength > publicKey.Length)
            {
                return Result<bool>.Failure("Invalid RSA exponent length");
            }

            var exponent = publicKey.AsSpan(offset, (int)eLength).ToArray();
            offset += (int)eLength;

            // Read modulus (n)
            if (offset + 4 > publicKey.Length)
            {
                return Result<bool>.Failure("Invalid RSA public key format");
            }

            var nLength = BinaryPrimitives.ReadUInt32BigEndian(publicKey.AsSpan(offset));
            offset += 4;
            
            if (offset + nLength > publicKey.Length)
            {
                return Result<bool>.Failure("Invalid RSA modulus length");
            }

            var modulus = publicKey.AsSpan(offset, (int)nLength).ToArray();

            // Create RSA parameters
            var rsaParams = new RSAParameters
            {
                Exponent = exponent,
                Modulus = modulus
            };

            // Verify signature
            using var rsa = RSA.Create(rsaParams);
            var isValid = rsa.VerifyData(
                challenge, 
                signature, 
                HashAlgorithmName.SHA256, 
                RSASignaturePadding.Pkcs1);
            
            return isValid 
                ? Result<bool>.Success(true)
                : Result<bool>.Failure("RSA signature verification failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RSA signature verification error");
            return Result<bool>.Failure($"RSA verification error: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes SHA-256 hash of the public key for session identification.
    /// </summary>
    private static string ComputeKeyHash(byte[] publicKey)
    {
        var hash = SHA256.HashData(publicKey);
        return Convert.ToHexString(hash).ToUpperInvariant();
    }
}
