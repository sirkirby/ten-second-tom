namespace TenSecondTom.Features.Shell.Models;

/// <summary>
/// Represents an active shell session from launch to termination.
/// Session state is maintained in-memory only (no persistence between launches).
/// </summary>
public sealed record ShellSession
{
    /// <summary>
    /// Unique identifier for this session instance.
    /// </summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when the session was created.
    /// </summary>
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// UTC timestamp when the session terminated (null if still active).
    /// </summary>
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Total number of commands executed in this session.
    /// </summary>
    public int CommandCount { get; set; }

    /// <summary>
    /// Current state of the session.
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Created;

    /// <summary>
    /// Validates that the session's EndTime is after StartTime if set.
    /// </summary>
    public bool IsValid() =>
        CommandCount >= 0 &&
        (EndTime == null || EndTime >= StartTime);
}

/// <summary>
/// Defines the lifecycle states of a shell session.
/// </summary>
public enum SessionStatus
{
    /// <summary>
    /// Session object created but not yet active.
    /// </summary>
    Created = 0,

    /// <summary>
    /// Session is running and accepting commands.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Session has ended and resources are being released.
    /// </summary>
    Terminated = 2
}
