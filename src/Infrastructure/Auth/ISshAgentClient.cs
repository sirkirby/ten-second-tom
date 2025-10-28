namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Provides an abstraction for communicating with SSH agents via the OpenSSH agent protocol.
/// </summary>
/// <remarks>
/// This interface abstracts SSH agent communication for testability and platform-specific implementations.
/// SSH agents allow applications to request cryptographic signatures without accessing private keys directly,
/// providing enhanced security and support for hardware keys (YubiKey, Touch ID, etc.).
/// </remarks>
public interface ISshAgentClient : IDisposable
{
    /// <summary>
    /// Connects to the SSH agent using the specified provider.
    /// </summary>
    /// <param name="provider">The SSH agent provider to use. Defaults to Auto.</param>
    /// <param name="cancellationToken">A token to cancel the connection operation.</param>
    /// <returns>
    /// <c>true</c> if the connection to the SSH agent was successful; otherwise, <c>false</c>.
    /// </returns>
    /// <remarks>
    /// The provider parameter allows selecting specific SSH agent implementations:
    /// - Auto: Automatically detects the best available agent (1Password, Secretive, or system default)
    /// - OnePassword: Uses 1Password's SSH agent
    /// - Secretive: Uses Secretive SSH agent (macOS only)
    /// - System: Uses the system's default SSH agent (SSH_AUTH_SOCK)
    /// </remarks>
    Task<bool> ConnectAsync(SshAgentProvider provider = SshAgentProvider.Auto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all identities (public keys) available in the SSH agent.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the list operation.</param>
    /// <returns>
    /// A list of public keys in SSH wire format, or an empty list if no keys are available.
    /// </returns>
    /// <remarks>
    /// This method sends an SSH_AGENTC_REQUEST_IDENTITIES (message type 11) to the agent and
    /// waits for an SSH_AGENT_IDENTITIES_ANSWER (message type 12).
    /// Each returned key includes the public key blob that can be used with SignDataAsync.
    /// </remarks>
    Task<IReadOnlyList<byte[]>> ListIdentitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests the SSH agent to sign the provided data using the specified public key.
    /// </summary>
    /// <param name="publicKey">The public key blob in SSH wire format.</param>
    /// <param name="data">The data to be signed (typically a challenge).</param>
    /// <param name="cancellationToken">A token to cancel the signature operation.</param>
    /// <returns>
    /// The signature blob if the agent successfully signed the data; otherwise, <c>null</c>
    /// if the agent denied the request or the key was not found in the agent.
    /// </returns>
    /// <remarks>
    /// This method sends an SSH_AGENTC_SIGN_REQUEST (message type 13) to the agent and
    /// waits for an SSH_AGENT_SIGN_RESPONSE (message type 14).
    /// The agent may prompt the user for approval (Touch ID, PIN, etc.) before signing.
    /// </remarks>
    Task<byte[]?> SignDataAsync(byte[] publicKey, byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the client is currently connected to the SSH agent.
    /// </summary>
    bool IsConnected { get; }
}
