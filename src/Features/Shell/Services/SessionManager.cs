using TenSecondTom.Features.Shell.Models;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Manages shell session lifecycle and command history.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Starts a new shell session.
    /// </summary>
    /// <exception cref="InvalidOperationException">If a session is already active.</exception>
    void StartSession();

    /// <summary>
    /// Adds a command execution to the session history.
    /// </summary>
    /// <param name="command">The command that was executed.</param>
    /// <param name="wasSuccessful">True if the command completed successfully.</param>
    /// <param name="wasInterrupted">True if the command was cancelled via Ctrl+C.</param>
    /// <param name="resultSummary">Optional summary of the command result (truncated to 100 chars).</param>
    /// <exception cref="InvalidOperationException">If no session is active.</exception>
    void AddToHistory(string command, bool wasSuccessful, bool wasInterrupted = false, string? resultSummary = null);

    /// <summary>
    /// Gets the command history in chronological order.
    /// </summary>
    /// <returns>Read-only list of history entries (max 100).</returns>
    /// <exception cref="InvalidOperationException">If no session is active.</exception>
    IReadOnlyList<CommandHistoryEntry> GetHistory();

    /// <summary>
    /// Ends the current session.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no session is active.</exception>
    void EndSession();

    /// <summary>
    /// Gets the current session (null if no active session).
    /// </summary>
    ShellSession? GetCurrentSession();
}

/// <summary>
/// Implements session management with circular buffer history storage.
/// History is in-memory only, no persistence between launches.
/// </summary>
public sealed class SessionManager : ISessionManager
{
    private const int MaxHistoryCapacity = 100;

    private ShellSession? _currentSession;
    private readonly List<CommandHistoryEntry> _history = new(MaxHistoryCapacity);
    private int _nextSequenceNumber = 1;

    /// <inheritdoc/>
    public void StartSession()
    {
        if (_currentSession?.Status == SessionStatus.Active)
        {
            throw new InvalidOperationException("A session is already active. Call EndSession() first.");
        }

        _currentSession = new ShellSession
        {
            Status = SessionStatus.Active
        };

        _history.Clear();
        _nextSequenceNumber = 1;
    }

    /// <inheritdoc/>
    public void AddToHistory(string command, bool wasSuccessful, bool wasInterrupted = false, string? resultSummary = null)
    {
        if (_currentSession?.Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        // Truncate result summary if needed
        string? truncated = CommandHistoryEntry.TruncateResultSummary(resultSummary);

        var entry = new CommandHistoryEntry
        {
            SequenceNumber = _nextSequenceNumber++,
            Command = command,
            WasSuccessful = wasSuccessful,
            WasInterrupted = wasInterrupted,
            ResultSummary = truncated
        };

        // Circular buffer: Remove oldest if at capacity
        if (_history.Count >= MaxHistoryCapacity)
        {
            _history.RemoveAt(0);
        }

        _history.Add(entry);
        _currentSession.CommandCount++;
    }

    /// <inheritdoc/>
    public IReadOnlyList<CommandHistoryEntry> GetHistory()
    {
        if (_currentSession?.Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("No active session. Call StartSession() first.");
        }

        return _history.AsReadOnly();
    }

    /// <inheritdoc/>
    public void EndSession()
    {
        if (_currentSession?.Status != SessionStatus.Active)
        {
            throw new InvalidOperationException("No active session to end.");
        }

        _currentSession.EndTime = DateTimeOffset.UtcNow;
        _currentSession.Status = SessionStatus.Terminated;
    }

    /// <inheritdoc/>
    public ShellSession? GetCurrentSession() => _currentSession;
}
