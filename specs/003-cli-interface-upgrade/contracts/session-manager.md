# Contract: Session Manager

**Component**: `SessionManager`  
**Namespace**: `TenSecondTom.Features.Shell.Services`  
**Purpose**: Manages shell session state, command history, and lifecycle

## Interface Contract

```csharp
public interface ISessionManager
{
    /// <summary>
    /// Gets the current session.
    /// </summary>
    ShellSession CurrentSession { get; }
    
    /// <summary>
    /// Initializes a new shell session.
    /// </summary>
    void StartSession();
    
    /// <summary>
    /// Adds a command execution to the session history.
    /// </summary>
    /// <param name="command">The command that was executed.</param>
    /// <param name="result">The result of the command execution.</param>
    void AddToHistory(string command, CommandResult result);
    
    /// <summary>
    /// Gets the command history for the current session.
    /// </summary>
    /// <returns>Read-only list of history entries in chronological order.</returns>
    IReadOnlyList<CommandHistoryEntry> GetHistory();
    
    /// <summary>
    /// Cleans up session resources.
    /// </summary>
    void EndSession();
}
```

## Data Model

### ShellSession
```csharp
public sealed record ShellSession
{
    public Guid SessionId { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; set; }
    public int CommandCount { get; set; }
    public SessionStatus Status { get; set; }
}

public enum SessionStatus
{
    Created,
    Active,
    Terminated
}
```

### CommandHistoryEntry
```csharp
public sealed record CommandHistoryEntry
{
    public int SequenceNumber { get; init; }
    public string Command { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool WasSuccessful { get; init; }
    public bool WasInterrupted { get; init; }
    public string? ResultSummary { get; init; }
}
```

## Behavior Contract

### Session Initialization
- **Preconditions**: No active session exists
- **Actions**:
  1. Generate unique session ID (Guid.NewGuid())
  2. Set start timestamp (DateTimeOffset.UtcNow)
  3. Initialize empty command history
  4. Set status to Active
- **Postconditions**:
  - CurrentSession is not null
  - SessionId is unique
  - History is empty
  - Status is Active

### Add to History
- **Preconditions**: Session is active, command is not null
- **Actions**:
  1. Create CommandHistoryEntry with next sequence number
  2. Set timestamp to current time
  3. Extract result summary (first 100 chars of output or error message)
  4. Add entry to circular buffer (max 100 entries)
  5. Increment command count
- **Postconditions**:
  - History contains new entry
  - If history > 100 entries, oldest entry is removed (circular buffer)
  - Command count incremented

### Get History
- **Preconditions**: Session is active
- **Actions**:
  1. Return read-only copy of history list
  2. Entries are in chronological order (oldest first)
- **Output**: IReadOnlyList<CommandHistoryEntry>
- **Postconditions**: Returned list is immutable (caller cannot modify)

### End Session
- **Preconditions**: Session exists (may be active or terminated)
- **Actions**:
  1. Set end timestamp
  2. Set status to Terminated
  3. Log session summary (session ID, duration, command count)
  4. Clear history to free memory
- **Postconditions**:
  - EndTime is set
  - Status is Terminated
  - History is empty (memory released)

## History Management Contract

### Circular Buffer Behavior
- **Capacity**: 100 entries maximum
- **Overflow behavior**: When adding 101st entry, remove entry at index 0
- **Sequence numbers**: Continuously increment (do not reset on overflow)
- **Memory management**: Old entries are garbage collected after removal

### History Filtering (Future Enhancement)
- Not implemented in Phase 1
- Future: Filter by success/failure, date range, command type

## Error Handling Contract

### Invalid State
- **StartSession called twice**: Throw InvalidOperationException ("Session already active")
- **AddToHistory before StartSession**: Throw InvalidOperationException ("No active session")
- **EndSession before StartSession**: No-op (idempotent)

### Null Arguments
- **AddToHistory with null command**: Throw ArgumentNullException
- **AddToHistory with null result**: Treat as failure with no message

## Performance Contract

- **StartSession**: < 1ms (simple initialization)
- **AddToHistory**: < 1ms (list append with circular buffer check)
- **GetHistory**: < 1ms (return reference to read-only wrapper)
- **EndSession**: < 5ms (log write + memory cleanup)
- **Memory usage**: ~10KB for 100 history entries (estimated 100 bytes per entry)

## Persistence Contract

- **No persistence between launches** (constitutional requirement)
- Session data is in-memory only
- History is lost when process exits
- No file I/O, no database writes
- Constitutional justification: "Session state is in-memory only (no persistence between launches beyond same-day window)"

## Testing Contract

### Unit Tests (SessionManagerTests.cs)
1. `StartSession_InitializesSessionCorrectly`: Verify session ID, start time, status
2. `AddToHistory_AddsEntryToHistory`: Add command, verify in GetHistory()
3. `AddToHistory_WithCircularBufferOverflow_RemovesOldestEntry`: Add 101 entries, verify count = 100
4. `GetHistory_ReturnsReadOnlyList`: Verify returned list is IReadOnlyList
5. `EndSession_SetsEndTimeAndStatus`: End session, verify EndTime != null, Status == Terminated
6. `StartSession_WhenAlreadyActive_ThrowsException`: Start twice, expect exception
7. `AddToHistory_BeforeStartSession_ThrowsException`: Add before start, expect exception
8. `AddToHistory_WithNullCommand_ThrowsArgumentNullException`: Verify null check

### Integration Tests
- Session lifecycle tested as part of PersistentShellSessionTests

## Dependencies

- `ILogger<SessionManager>`: Logs session lifecycle events (start, end, command count)
- No external dependencies (pure in-memory state management)

## Example Usage

```csharp
var sessionManager = serviceProvider.GetRequiredService<ISessionManager>();

// Start session
sessionManager.StartSession();

// Execute commands and record history
await commandRouter.RouteAsync("/today", cancellationToken);
sessionManager.AddToHistory("/today", result);

// View history
var history = sessionManager.GetHistory();
Console.WriteLine($"Executed {history.Count} commands");

// End session
sessionManager.EndSession();
```

## Contract Validation

- [x] Interface defined with XML documentation
- [x] Data model specified (ShellSession, CommandHistoryEntry)
- [x] Behavior specified for all lifecycle methods
- [x] Circular buffer semantics documented
- [x] Error cases enumerated
- [x] Performance requirements stated
- [x] Persistence contract clarified (no persistence)
- [x] Test scenarios identified
- [x] Dependencies documented
