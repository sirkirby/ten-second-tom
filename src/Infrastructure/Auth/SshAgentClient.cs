using System.Buffers.Binary;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Implements SSH agent protocol communication for authentication.
/// Supports OpenSSH agent protocol via Unix domain sockets (macOS/Linux) and named pipes (Windows).
/// </summary>
public sealed class SshAgentClient : ISshAgentClient
{
    private const byte SSH_AGENTC_REQUEST_IDENTITIES = 11;
    private const byte SSH_AGENT_IDENTITIES_ANSWER = 12;
    private const byte SSH_AGENTC_SIGN_REQUEST = 13;
    private const byte SSH_AGENT_SIGN_RESPONSE = 14;
    private const byte SSH_AGENT_FAILURE = 5;
    private const int MaxResponseSize = 256 * 1024; // 256KB max response

    private readonly ILogger<SshAgentClient> _logger;
    private Socket? _socket;
    private bool _disposed;

    public SshAgentClient(ILogger<SshAgentClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets whether the client is currently connected to an SSH agent.
    /// </summary>
    public bool IsConnected => _socket?.Connected == true;

    /// <summary>
    /// Connects to the SSH agent using the specified provider.
    /// </summary>
    /// <param name="provider">The SSH agent provider to use. Defaults to Auto.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if connection successful, false otherwise.</returns>
    public async Task<bool> ConnectAsync(SshAgentProvider provider = SshAgentProvider.Auto, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get SSH agent socket path from provider resolver
            var socketPath = SshAgentProviderResolver.GetSocketPath(provider);
            
            if (string.IsNullOrEmpty(socketPath))
            {
                var providerName = SshAgentProviderResolver.GetProviderName(provider);
                _logger.LogWarning("{Provider} not available or not found", providerName);
                return false;
            }

            if (!File.Exists(socketPath))
            {
                _logger.LogWarning("SSH agent socket not found at {SocketPath}", socketPath);
                return false;
            }

            // Create Unix domain socket
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            await _socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            
            var detectedProvider = SshAgentProviderResolver.DetectProvider(socketPath);
            var detectedName = SshAgentProviderResolver.GetProviderName(detectedProvider);
            _logger.LogInformation("Connected to {Provider} at {SocketPath}", detectedName, socketPath);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSH agent connection cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SSH agent");
            return false;
        }
    }

    /// <summary>
    /// Requests the list of identities from the SSH agent.
    /// This primes some agents (like 1Password) to be ready for signing requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>True if request succeeded (regardless of how many keys are returned), false otherwise.</returns>
    private async Task<bool> RequestIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_socket == null || !IsConnected)
        {
            _logger.LogError("Not connected to SSH agent");
            return false;
        }

        try
        {
            // Build SSH_AGENTC_REQUEST_IDENTITIES message
            var messageSize = 1; // Just the message type
            var buffer = new byte[4 + messageSize];
            
            var span = buffer.AsSpan();
            BinaryPrimitives.WriteUInt32BigEndian(span, (uint)messageSize);
            buffer[4] = SSH_AGENTC_REQUEST_IDENTITIES;

            // Send request
            await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            
            // Read response (we don't need to parse it, just verify we got a response)
            var response = await ReadAgentResponseAsync(cancellationToken).ConfigureAwait(false);
            
            if (response == null || response.Length == 0)
            {
                _logger.LogWarning("Empty response from SSH agent identity request");
                return false;
            }

            var messageType = response[0];
            if (messageType == SSH_AGENT_IDENTITIES_ANSWER)
            {
                _logger.LogDebug("SSH agent identities request successful");
                return true;
            }
            else if (messageType == SSH_AGENT_FAILURE)
            {
                _logger.LogDebug("SSH agent returned no identities (normal for 1Password)");
                return true; // This is actually OK - 1Password returns empty list
            }
            else
            {
                _logger.LogWarning("Unexpected response type from identity request: {Type}", messageType);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to request identities from SSH agent");
            return false;
        }
    }

    /// <summary>
    /// Lists all identities (public keys) available in the SSH agent.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A list of public keys in SSH wire format, or an empty list if no keys are available.</returns>
    public async Task<IReadOnlyList<byte[]>> ListIdentitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_socket == null || !IsConnected)
        {
            _logger.LogError("Not connected to SSH agent");
            return Array.Empty<byte[]>();
        }

        try
        {
            // Build SSH_AGENTC_REQUEST_IDENTITIES message
            var messageSize = 1; // Just the message type
            var buffer = new byte[4 + messageSize];

            var span = buffer.AsSpan();
            BinaryPrimitives.WriteUInt32BigEndian(span, (uint)messageSize);
            buffer[4] = SSH_AGENTC_REQUEST_IDENTITIES;

            // Send request
            await _socket.SendAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);

            // Read response
            var response = await ReadAgentResponseAsync(cancellationToken).ConfigureAwait(false);

            if (response == null || response.Length == 0)
            {
                _logger.LogWarning("Empty response from SSH agent identity request");
                return Array.Empty<byte[]>();
            }

            var messageType = response[0];
            if (messageType == SSH_AGENT_FAILURE)
            {
                _logger.LogDebug("SSH agent returned no identities");
                return Array.Empty<byte[]>();
            }

            if (messageType != SSH_AGENT_IDENTITIES_ANSWER)
            {
                _logger.LogWarning("Unexpected response type from identity request: {Type}", messageType);
                return Array.Empty<byte[]>();
            }

            // Parse the identities from the response
            // Format: 1 byte type + 4 bytes count + [4 bytes key length + key blob + 4 bytes comment length + comment]...
            if (response.Length < 5)
            {
                _logger.LogWarning("Response too short to contain identity count");
                return Array.Empty<byte[]>();
            }

            var offset = 1; // Skip message type
            var count = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
            offset += 4;

            var identities = new List<byte[]>((int)count);

            for (var i = 0; i < count; i++)
            {
                // Read key blob length
                if (offset + 4 > response.Length)
                {
                    _logger.LogWarning("Truncated response while reading key blob length for identity {Index}", i);
                    break;
                }

                var keyBlobLength = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
                offset += 4;

                // Read key blob
                if (offset + keyBlobLength > response.Length)
                {
                    _logger.LogWarning("Truncated response while reading key blob for identity {Index}", i);
                    break;
                }

                var keyBlob = response.AsSpan(offset, (int)keyBlobLength).ToArray();
                identities.Add(keyBlob);
                offset += (int)keyBlobLength;

                // Skip comment (4 bytes length + comment data)
                if (offset + 4 > response.Length)
                {
                    _logger.LogWarning("Truncated response while reading comment length for identity {Index}", i);
                    break;
                }

                var commentLength = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(offset));
                offset += 4;

                if (offset + commentLength > response.Length)
                {
                    _logger.LogWarning("Truncated response while skipping comment for identity {Index}", i);
                    break;
                }

                offset += (int)commentLength;
            }

            _logger.LogInformation("Retrieved {Count} identities from SSH agent", identities.Count);
            return identities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list identities from SSH agent");
            return Array.Empty<byte[]>();
        }
    }

    /// <summary>
    /// Requests the SSH agent to sign data with the specified public key.
    /// </summary>
    /// <param name="publicKey">The SSH public key blob to use for signing.</param>
    /// <param name="data">The data to sign (typically a challenge).</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The signature bytes, or null if signing failed.</returns>
    public async Task<byte[]?> SignDataAsync(byte[] publicKey, byte[] data, CancellationToken cancellationToken = default)
    {
        if (_socket == null || !IsConnected)
        {
            _logger.LogError("Not connected to SSH agent");
            return null;
        }

        try
        {
            // First, request identities from the agent
            // This primes some agents (like 1Password) to be ready for signing
            await RequestIdentitiesAsync(cancellationToken).ConfigureAwait(false);
            
            // Build SSH_AGENTC_SIGN_REQUEST message
            var requestBytes = BuildSignRequest(publicKey, data);
            
            // Send request to agent
            await _socket.SendAsync(requestBytes, SocketFlags.None, cancellationToken).ConfigureAwait(false);
            
            // Read response from agent
            var response = await ReadAgentResponseAsync(cancellationToken).ConfigureAwait(false);
            
            if (response == null || response.Length == 0)
            {
                _logger.LogError("Empty response from SSH agent");
                return null;
            }

            // Parse response
            return ParseSignResponse(response);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SSH agent sign request cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign data with SSH agent");
            return null;
        }
    }

    /// <summary>
    /// Builds an SSH_AGENTC_SIGN_REQUEST message according to OpenSSH agent protocol.
    /// </summary>
    private static byte[] BuildSignRequest(byte[] publicKey, byte[] data)
    {
        // Message format:
        // 4 bytes: message length (excluding this field)
        // 1 byte:  SSH_AGENTC_SIGN_REQUEST (13)
        // 4 bytes: public key blob length
        // n bytes: public key blob
        // 4 bytes: data length
        // n bytes: data
        // 4 bytes: flags (0 = default)

        var messageSize = 1 + 4 + publicKey.Length + 4 + data.Length + 4;
        var buffer = new byte[4 + messageSize]; // 4 bytes for length prefix
        
        var span = buffer.AsSpan();
        var offset = 0;

        // Write message length
        BinaryPrimitives.WriteUInt32BigEndian(span[offset..], (uint)messageSize);
        offset += 4;

        // Write message type
        buffer[offset++] = SSH_AGENTC_SIGN_REQUEST;

        // Write public key blob length and data
        BinaryPrimitives.WriteUInt32BigEndian(span[offset..], (uint)publicKey.Length);
        offset += 4;
        publicKey.CopyTo(span[offset..]);
        offset += publicKey.Length;

        // Write data length and data
        BinaryPrimitives.WriteUInt32BigEndian(span[offset..], (uint)data.Length);
        offset += 4;
        data.CopyTo(span[offset..]);
        offset += data.Length;

        // Write flags (0 = default)
        BinaryPrimitives.WriteUInt32BigEndian(span[offset..], 0);

        return buffer;
    }

    /// <summary>
    /// Reads a complete response from the SSH agent.
    /// </summary>
    private async Task<byte[]?> ReadAgentResponseAsync(CancellationToken cancellationToken)
    {
        if (_socket == null)
        {
            return null;
        }

        // Read 4-byte length prefix
        var lengthBuffer = new byte[4];
        var bytesRead = await _socket.ReceiveAsync(lengthBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
        
        if (bytesRead != 4)
        {
            _logger.LogError("Failed to read response length from SSH agent");
            return null;
        }

        var messageLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);
        
        if (messageLength == 0 || messageLength > MaxResponseSize)
        {
            _logger.LogError("Invalid message length from SSH agent: {Length}", messageLength);
            return null;
        }

        // Read message body
        var messageBuffer = new byte[messageLength];
        var totalRead = 0;
        
        while (totalRead < messageLength)
        {
            var read = await _socket.ReceiveAsync(
                messageBuffer.AsMemory(totalRead), 
                SocketFlags.None, 
                cancellationToken).ConfigureAwait(false);
            
            if (read == 0)
            {
                _logger.LogError("SSH agent closed connection before complete message received");
                return null;
            }
            
            totalRead += read;
        }

        return messageBuffer;
    }

    /// <summary>
    /// Parses an SSH_AGENT_SIGN_RESPONSE message to extract the signature.
    /// </summary>
    private byte[]? ParseSignResponse(byte[] response)
    {
        if (response.Length < 1)
        {
            return null;
        }

        var messageType = response[0];
        
        if (messageType == SSH_AGENT_FAILURE)
        {
            _logger.LogWarning("SSH agent returned failure response");
            return null;
        }

        if (messageType != SSH_AGENT_SIGN_RESPONSE)
        {
            _logger.LogError("Unexpected response type from SSH agent: {Type}", messageType);
            return null;
        }

        // Response format:
        // 1 byte:  SSH_AGENT_SIGN_RESPONSE (14)
        // 4 bytes: signature blob length
        // n bytes: signature blob

        if (response.Length < 5)
        {
            _logger.LogError("SSH agent response too short");
            return null;
        }

        var signatureBlobLength = BinaryPrimitives.ReadUInt32BigEndian(response.AsSpan(1));
        
        if (signatureBlobLength == 0 || signatureBlobLength > response.Length - 5)
        {
            _logger.LogError("Invalid signature blob length: {Length}", signatureBlobLength);
            return null;
        }

        // Extract signature blob
        var signatureBlob = new byte[signatureBlobLength];
        Array.Copy(response, 5, signatureBlob, 0, (int)signatureBlobLength);

        // Parse signature blob to extract actual signature
        // Blob format:
        // 4 bytes: algorithm length
        // n bytes: algorithm string (e.g., "ssh-ed25519")
        // 4 bytes: signature length
        // n bytes: signature

        if (signatureBlob.Length < 4)
        {
            return null;
        }

        var algorithmLength = BinaryPrimitives.ReadUInt32BigEndian(signatureBlob);
        var offset = 4 + (int)algorithmLength;

        if (offset + 4 > signatureBlob.Length)
        {
            _logger.LogError("Invalid signature blob format");
            return null;
        }

        var signatureLength = BinaryPrimitives.ReadUInt32BigEndian(signatureBlob.AsSpan(offset));
        offset += 4;

        if (offset + signatureLength > signatureBlob.Length)
        {
            _logger.LogError("Invalid signature length in blob");
            return null;
        }

        var signature = new byte[signatureLength];
        Array.Copy(signatureBlob, offset, signature, 0, (int)signatureLength);

        return signature;
    }

    /// <summary>
    /// Disposes resources used by the SSH agent client.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_socket != null)
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
                // Ignore shutdown errors
            }

            _socket.Dispose();
            _socket = null;
        }

        _disposed = true;
    }
}
