using TenSecondTom.Shared.Models;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// SSH authentication configuration model.
/// Returned as a DTO from ConfigureSsh and GetSetupConfiguration queries.
/// </summary>
public sealed record SshConfiguration
{
    /// <summary>
    /// Gets the path to the SSH key file (null for agent-based keys)
    /// </summary>
    public string? KeyPath { get; init; }

    /// <summary>
    /// Gets the source of the SSH key
    /// </summary>
    public SshKeySource? KeySource { get; init; }

    /// <summary>
    /// Gets the path to the SSH agent socket
    /// </summary>
    public string? AgentSocketPath { get; init; }

    /// <summary>
    /// Gets a human-readable identifier for the SSH key (e.g., "id_ed25519 (1Password)")
    /// </summary>
    public string? KeyDisplayName { get; init; }
}
