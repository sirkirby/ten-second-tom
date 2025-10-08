# Data Model: Persistent CLI Session Experience

**Feature**: 003-cli-interface-upgrade  
**Date**: 2025-10-08

## Overview

This document defines the data entities and their relationships for the persistent CLI shell feature. All entities are in-memory only (no database persistence) as per constitutional requirements.

## Core Entities

### 1. ShellSession

**Purpose**: Represents an active shell session from launch to termination.

**Properties**:
- `SessionId` (Guid, required): Unique identifier for this session instance
- `StartTime` (DateTimeOffset, required): UTC timestamp when session was created
- `EndTime` (DateTimeOffset?, optional): UTC timestamp when session terminated (null if active)
- `CommandCount` (int, required): Total number of commands executed in this session
- `Status` (SessionStatus enum, required): Current session state

**Validation Rules**:
- SessionId must be unique (enforced by Guid.NewGuid())
- StartTime must be <= current time
- EndTime must be >= StartTime (if set)
- CommandCount must be >= 0
- Status transitions: Created → Active → Terminated (no backward transitions)

**Lifecycle**:
1. Created: Session object initialized, not yet active
2. Active: Session is running, accepting commands
3. Terminated: Session ended, resources released

**Example**:
```csharp
public sealed record ShellSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public DateTimeOffset StartTime { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndTime { get; set; }
    public int CommandCount { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Created;
}
```

### 2. CommandHistoryEntry

**Purpose**: Records a single command execution with its outcome.

**Properties**:
- `SequenceNumber` (int, required): Incrementing sequence across session (not index)
- `Command` (string, required): The command text entered by user (e.g., "/today")
- `Timestamp` (DateTimeOffset, required): UTC timestamp when command was executed
- `WasSuccessful` (bool, required): True if command completed successfully
- `WasInterrupted` (bool, required): True if command was cancelled via Ctrl+C
- `ResultSummary` (string?, optional): First 100 chars of output or error message

**Validation Rules**:
- SequenceNumber must be > 0
- Command must not be null or whitespace
- Timestamp must be >= session StartTime
- ResultSummary length <= 100 characters (truncated if longer)
- WasSuccessful and WasInterrupted cannot both be true

**Storage**:
- Stored in SessionManager's circular buffer (max 100 entries)
- When capacity exceeded, oldest entry is removed (FIFO)
- No persistence between launches (in-memory only)

**Example**:
```csharp
public sealed record CommandHistoryEntry
{
    public required int SequenceNumber { get; init; }
    public required string Command { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required bool WasSuccessful { get; init; }
    public bool WasInterrupted { get; init; }
    public string? ResultSummary { get; init; }
}
```

### 3. CommandMetadata

**Purpose**: Describes available slash commands for autocomplete and help display.

**Properties**:
- `Name` (string, required): Command name including slash (e.g., "/today")
- `HelpText` (string, required): Brief description for user guidance
- `Aliases` (string[]?, optional): Alternative names (e.g., ["/exit"] for "/quit")
- `RequiresAuthentication` (bool, required): True if command needs active auth session

**Validation Rules**:
- Name must start with '/' and contain no whitespace
- Name length must be 2-20 characters
- HelpText length must be 10-200 characters
- Aliases (if provided) must follow same format as Name

**Static Catalog**:
```csharp
private static readonly CommandMetadata[] CommandCatalog = 
{
    new("/today", "Capture today's reflection with 3-5 prompts", RequiresAuthentication: true),
    new("/thisweek", "Generate a weekly review from recent daily entries", RequiresAuthentication: true),
    new("/search", "Search memory entries by text query", RequiresAuthentication: true),
    new("/login", "Authenticate with SSH key and create a session", RequiresAuthentication: false),
    new("/logout", "Log out and invalidate the current session", RequiresAuthentication: true),
    new("/quit", "Exit the shell", Aliases: ["/exit"], RequiresAuthentication: false),
    new("/help", "Display available commands with descriptions", RequiresAuthentication: false),
};
```

### 4. AutocompleteSuggestion

**Purpose**: Represents a single autocomplete suggestion for display.

**Properties**:
- `CommandName` (string, required): The command being suggested (e.g., "/today")
- `HelpText` (string, required): Brief description to show alongside
- `MatchScore` (int, required): Relevance score for ranking (0-100)

**Validation Rules**:
- CommandName must match a valid command from CommandMetadata
- HelpText must not be null
- MatchScore must be 0-100 (inclusive)

**Ranking Algorithm**:
- Exact prefix match: Score = 100 - (command_length - input_length)
- Case-insensitive prefix: Score = 90 - (command_length - input_length)
- Substring match: Score = 50 - position_index
- No match: Not included in suggestions

**Example**:
```csharp
public sealed record AutocompleteSuggestion(
    string CommandName,
    string HelpText,
    int MatchScore)
{
    public override string ToString() => $"{CommandName} - {HelpText}";
}
```

### 5. CommandResult

**Purpose**: Encapsulates the outcome of a command execution.

**Properties**:
- `IsSuccess` (bool, required): True if command completed without errors
- `Message` (string?, optional): Success message or error description
- `Error` (Exception?, optional): Exception object if execution failed

**Validation Rules**:
- If IsSuccess = false, Message should describe the error
- If Error is not null, IsSuccess should be false
- Message should not contain stack traces (user-friendly only)

**Example**:
```csharp
public sealed record CommandResult(
    bool IsSuccess,
    string? Message = null,
    Exception? Error = null)
{
    public static CommandResult Success(string? message = null) 
        => new(true, message);
    
    public static CommandResult Failure(string message, Exception? error = null) 
        => new(false, message, error);
    
    public static CommandResult Interrupted() 
        => new(true, "(interrupted)");
}
```

## Enumerations

### SessionStatus

**Purpose**: Tracks the current state of a shell session.

```csharp
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
```

**Transitions**:
- Created → Active: StartSession() called
- Active → Terminated: EndSession() called or process exit
- No backward transitions allowed (enforced by SessionManager)

## Entity Relationships

```
ShellSession (1) ----< (0..100) CommandHistoryEntry
    │
    └─ Managed by SessionManager (singleton per process)

CommandMetadata (static catalog) 
    │
    ├─ Referenced by AutocompleteEngine → (0..10) AutocompleteSuggestion
    └─ Referenced by CommandRouter for validation

CommandResult
    │
    └─ Produced by CommandRouter after handler execution
```

## Persistence Strategy

**Constitutional Requirement**: "Session state is in-memory only (no persistence between launches beyond same-day window)"

**Implementation**:
- All entities stored in process memory (heap allocation)
- No file I/O for session data
- No database writes
- When process exits, all session data is lost
- This design supports multiple concurrent sessions (each in separate process with isolated memory)

**Memory Management**:
- Circular buffer for CommandHistoryEntry prevents unbounded growth
- ShellSession and CommandMetadata have fixed size
- AutocompleteSuggestion objects are ephemeral (created on-demand, GC'd immediately)
- Expected total memory usage: < 100KB per session

## Validation Summary

### Required Validations
- [x] SessionId uniqueness (via Guid.NewGuid())
- [x] Timestamp ordering (StartTime < EndTime)
- [x] Status transition integrity (no backward transitions)
- [x] Command text non-empty for CommandHistoryEntry
- [x] Circular buffer capacity enforcement (100 entries max)
- [x] MatchScore range validation (0-100)
- [x] CommandMetadata name format (starts with '/', no whitespace)

### No Persistence Validations Required
- No foreign key constraints (no database)
- No unique constraints across sessions (process isolation)
- No transaction integrity (pure in-memory, atomic updates)

## Testing Considerations

### Unit Test Data Fixtures
```csharp
public static class TestDataFixtures
{
    public static ShellSession CreateTestSession() => new()
    {
        SessionId = Guid.NewGuid(),
        StartTime = DateTimeOffset.UtcNow,
        Status = SessionStatus.Active
    };
    
    public static CommandHistoryEntry CreateTestHistoryEntry(int seq, string cmd) => new()
    {
        SequenceNumber = seq,
        Command = cmd,
        Timestamp = DateTimeOffset.UtcNow,
        WasSuccessful = true,
        WasInterrupted = false
    };
    
    public static CommandResult CreateSuccessResult(string? message = null)
        => CommandResult.Success(message);
        
    public static CommandResult CreateFailureResult(string message)
        => CommandResult.Failure(message);
}
```

## Data Model Validation Checklist

- [x] All entities defined with properties and types
- [x] Validation rules specified for each entity
- [x] Relationships documented
- [x] Enumerations defined with valid transitions
- [x] Persistence strategy clarified (in-memory only)
- [x] Memory management approach documented
- [x] Testing fixtures provided
- [x] Constitutional compliance verified (no persistence)
