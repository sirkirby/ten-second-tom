using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Shell.Models;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Shell.Services;

/// <summary>
/// Manages shell session lifecycle and command history.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Starts a new shell session. Loads persisted history from previous sessions.
    /// </summary>
    /// <exception cref="InvalidOperationException">If a session is already active.</exception>
    void StartSession();

    /// <summary>
    /// Adds a command execution to the session history.
    /// History is automatically persisted after each command.
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
    /// Ends the current session. History is persisted before session ends.
    /// </summary>
    /// <exception cref="InvalidOperationException">If no session is active.</exception>
    void EndSession();

    /// <summary>
    /// Gets the current session (null if no active session).
    /// </summary>
    ShellSession? GetCurrentSession();
}

/// <summary>
/// Implements session management with persistent history storage.
/// History is stored in a JSON file and persisted across sessions.
/// </summary>
public sealed class SessionManager : ISessionManager
{
    private const int MaxHistoryCapacity = 100;

    private readonly IHistoryStore _historyStore;
    private readonly ILogger<SessionManager> _logger;
    private ShellSession? _currentSession;
    private readonly List<CommandHistoryEntry> _history = new(MaxHistoryCapacity);
    private int _nextSequenceNumber = 1;

    public SessionManager(IHistoryStore historyStore, ILogger<SessionManager> logger)
    {
        _historyStore = historyStore;
        _logger = logger;
    }

    /// <summary>
    /// Parameterless constructor for backward compatibility with tests.
    /// Uses no persistence (in-memory only).
    /// </summary>
    public SessionManager() : this(null!, null!)
    {
    }

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

        // Load persisted history from previous sessions
        _history.Clear();
        if (_historyStore != null)
        {
            try
            {
                var result = _historyStore.LoadAsync().GetAwaiter().GetResult();
                if (result.IsSuccess && result.Value.Count > 0)
                {
                    // Take only the most recent entries up to capacity
                    var entries = result.Value.TakeLast(MaxHistoryCapacity).ToList();
                    _history.AddRange(entries);
                    _nextSequenceNumber = entries.Count > 0 ? entries.Max(e => e.SequenceNumber) + 1 : 1;
                    _logger?.LogInformation("Loaded {Count} history entries from previous session", entries.Count);
                }
                else
                {
                    _nextSequenceNumber = 1;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load history, starting with empty history");
                _nextSequenceNumber = 1;
            }
        }
        else
        {
            _nextSequenceNumber = 1;
        }
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

        // Persist history after each command for durability
        PersistHistory();
    }

    /// <summary>
    /// Persists history to storage. Failures are logged but don't throw.
    /// </summary>
    private void PersistHistory()
    {
        if (_historyStore == null) return;

        try
        {
            var result = _historyStore.SaveAsync(_history).GetAwaiter().GetResult();
            if (!result.IsSuccess)
            {
                _logger?.LogWarning("Failed to persist history: {Error}", result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist history");
        }
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

        // Persist history one final time before ending
        PersistHistory();

        _currentSession.EndTime = DateTimeOffset.UtcNow;
        _currentSession.Status = SessionStatus.Terminated;
    }

    /// <inheritdoc/>
    public ShellSession? GetCurrentSession() => _currentSession;
}
