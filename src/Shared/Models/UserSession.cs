namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents an authenticated user session tracked by SSH key.
/// </summary>
public record UserSession
{
    /// <summary>
    /// Gets the unique identifier for the session.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Gets the hash of the SSH public key used for authentication.
    /// Format: "algorithm:hash" (e.g., "sha256:abc123def456...")
    /// </summary>
    public required string SshKeyHash { get; init; }

    /// <summary>
    /// Gets a value indicating whether the session is currently active.
    /// </summary>
    public required bool IsActive { get; init; }

    /// <summary>
    /// Gets the timestamp when the session was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets the timestamp of the most recent session access.
    /// </summary>
    public required DateTimeOffset LastAccessedAt { get; init; }

    /// <summary>
    /// Gets the optional expiration timestamp for the session.
    /// If null, the session does not expire automatically.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
