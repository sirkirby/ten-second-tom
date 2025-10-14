# API Contract: IInteractiveTextEditor

**Service**: Interactive Text Editor
**Version**: 1.0
**Type**: Programmatic API (Service Interface)
**Date**: 2025-10-14

## Overview

`IInteractiveTextEditor` is the primary service interface for interactive multi-line text editing in the console. This contract defines the behavior, inputs, outputs, and guarantees for text editing sessions.

---

## Interface Definition

```csharp
namespace TenSecondTom.Shared.TextEditing.Services;

/// <summary>
/// Service for interactive multi-line text editing in the console.
/// </summary>
public interface IInteractiveTextEditor
{
    /// <summary>
    /// Start an interactive editing session with optional initial content.
    /// </summary>
    /// <param name="initialContent">Pre-filled content to edit (null for new entry)</param>
    /// <param name="configuration">Editor configuration options (null uses defaults)</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Result containing edited content and outcome</returns>
    /// <exception cref="EditorException">Thrown when terminal cannot support interactive editing</exception>
    Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
}
```

---

## Method: EditAsync

### Purpose
Launches an interactive multi-line text editor, allows the user to edit content, and returns the result when the user completes or cancels the session.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `initialContent` | `string?` | No | Pre-filled content to edit. `null` or empty string starts with blank editor. |
| `configuration` | `EditorConfiguration?` | No | Editor behavior settings. `null` uses `EditorConfiguration.Default`. |
| `cancellationToken` | `CancellationToken` | No | Token to cancel the operation. Default is `CancellationToken.None`. |

### Return Value

**Type**: `Task<EditorResult>`

**Structure**:
```csharp
public sealed record EditorResult
{
    public string Content { get; init; }           // Edited content (empty if cancelled)
    public EditorOutcome Outcome { get; init; }     // How session ended
    public bool IsSaved { get; }                    // True if user saved
    public bool IsCancelled { get; }                // True if user cancelled
    public bool IsError { get; }                    // True if error occurred
    public string? ErrorMessage { get; init; }      // Error details if IsError
    public EditorMetadata Metadata { get; init; }   // Session telemetry
}
```

### Behavior Guarantees

1. **Interactive Session Lifecycle**
   - Method blocks until user completes editing (Save/Cancel/Error)
   - User can edit content with full keyboard navigation
   - Session completes on explicit user action (not automatic)

2. **User Actions and Outcomes**
   - **Save**: User presses 'S' at confirmation → `EditorResult.IsSaved = true`, `Content` contains edited text
   - **Edit More**: User presses 'E' at confirmation → Returns to editing, process continues
   - **Cancel**: User presses 'C' at confirmation or Ctrl+C anytime → `EditorResult.IsCancelled = true`, `Content` is empty
   - **Ctrl+D or Ctrl+Enter**: Triggers confirmation preview (10-line preview if content > 10 lines)

3. **Content Handling**
   - All user-entered characters preserved (including emoji, non-Latin scripts)
   - Line breaks (Enter key) are hard breaks, preserved exactly
   - Blank lines (consecutive newlines) are preserved
   - ANSI escape sequences are stripped if `EditorConfiguration.SanitizeInput = true`
   - No automatic line wrapping added (only visual wrapping during editing)

4. **Error Handling**
   - If terminal is non-interactive (`Console.IsInputRedirected = true`), may throw `EditorException` or return fallback behavior (implementation-specific)
   - Terminal errors during editing → `EditorResult.IsError = true` with `ErrorMessage`
   - Cancellation via `CancellationToken` → `OperationCanceledException` thrown

5. **Performance**
   - Returns within 100ms of user completing action (Save/Cancel)
   - Supports content up to `EditorConfiguration.MaxContentLength` (default 50,000 chars)
   - Cursor operations remain responsive (< 100ms) for content up to 10,000 chars

### Exceptions

| Exception | Condition |
|-----------|-----------|
| `EditorException` | Terminal does not support interactive editing and no fallback available |
| `OperationCanceledException` | Operation cancelled via `cancellationToken` |
| `ArgumentException` | `initialContent` or `configuration` violates validation rules |

### Example Usage

```csharp
// Inject service via DI
public class CreateDailyEntryHandler(IInteractiveTextEditor editor)
{
    public async Task<Result<DailyEntry>> Handle(CreateDailyEntryCommand command)
    {
        // Launch editor with optional prompt text
        var result = await editor.EditAsync(
            initialContent: "What did you accomplish today?\n\n",
            configuration: EditorConfiguration.Default
        );

        if (result.IsCancelled)
        {
            return Result<DailyEntry>.Failure("Entry creation cancelled by user");
        }

        if (result.IsError)
        {
            return Result<DailyEntry>.Failure($"Editor error: {result.ErrorMessage}");
        }

        // Create entry with edited content
        var entry = new DailyEntry
        {
            Date = DateTime.UtcNow.Date,
            Content = result.Content
        };

        return Result<DailyEntry>.Success(entry);
    }
}
```

---

## Configuration Contract

### EditorConfiguration

```csharp
public sealed record EditorConfiguration
{
    public int MaxContentLength { get; init; } = 50_000;   // Max chars
    public int MaxLineCount { get; init; } = 1_000;         // Max lines
    public bool ShowHints { get; init; } = true;            // Show keyboard hints
    public int PreviewLineLimit { get; init; } = 10;        // Lines in preview (0 = all)
    public bool SanitizeInput { get; init; } = true;        // Strip ANSI sequences
}
```

**Validation Rules**:
- `MaxContentLength` must be > 0 and ≤ 1,000,000
- `MaxLineCount` must be > 0 and ≤ 100,000
- `PreviewLineLimit` must be ≥ 0 (0 means no limit)

---

## Keyboard Shortcuts (User-Facing Contract)

The editor MUST support these keyboard shortcuts:

### Navigation
- **Arrow Keys** (↑↓←→): Move cursor
- **Home**: Move to start of current line
- **End**: Move to end of current line
- **Ctrl+Home**: Move to first line (optional)
- **Ctrl+End**: Move to last line (optional)

### Editing
- **Character keys**: Insert character at cursor
- **Enter**: Insert new line at cursor
- **Backspace**: Delete character before cursor
- **Delete**: Delete character at cursor
- **Tab**: Insert tab character (if supported by implementation)

### Completion
- **Ctrl+D** or **Ctrl+Enter**: Trigger confirmation preview
- At confirmation:
  - **S**: Save content and exit
  - **E**: Return to editing
  - **C**: Cancel without saving

### Cancellation
- **Ctrl+C**: Cancel editing session immediately (no confirmation)

---

## Fallback Behavior (Non-Interactive Terminals)

When `Console.IsInputRedirected = true` or terminal does not support interactive editing:

**Option 1: Stream-Based Fallback**
- Use `Console.ReadLine()` in a loop
- Each line read separately
- Ctrl+D (EOF) to finish
- No cursor navigation available
- Still returns `EditorResult`

**Option 2: Throw Exception**
- Throw `EditorException` with clear message
- Caller can handle by using alternative input method

**Option 3: External Editor**
- Launch `$EDITOR` (nano, vim, notepad) via `Process.Start()`
- Read edited content from temp file
- Returns `EditorResult`

Implementation determines which fallback to use.

---

## Thread Safety

- `EditAsync` is **NOT thread-safe** - only one editing session per instance at a time
- Calling `EditAsync` while another session is active should throw `InvalidOperationException`
- Recommend scoping `IInteractiveTextEditor` as **Transient** in DI container

---

## Logging Contract

Implementations MUST log the following via Serilog:

**Session Start** (Information level):
```csharp
_logger.LogInformation(
    "Starting text editing session {SessionId} with {InitialLength} characters",
    sessionId,
    initialContent?.Length ?? 0
);
```

**Session Complete** (Information level):
```csharp
_logger.LogInformation(
    "Completed text editing session {SessionId}: Outcome={Outcome}, Duration={Duration}ms, FinalLength={FinalLength}",
    sessionId,
    outcome,
    duration.TotalMilliseconds,
    content.Length
);
```

**Session Error** (Error level):
```csharp
_logger.LogError(
    exception,
    "Error in text editing session {SessionId}: {ErrorMessage}",
    sessionId,
    errorMessage
);
```

**Telemetry Fields**:
- `SessionId` (Guid)
- `Outcome` (EditorOutcome enum)
- `Duration` (TimeSpan)
- `InitialLength` (int)
- `FinalLength` (int)
- `LineCount` (int)
- `WasModified` (bool)

---

## Implementation Variants

### 1. TerminalGuiTextEditor (Primary)
- Uses Terminal.Gui `TextView` widget
- Full multi-line editing with all keyboard shortcuts
- Proper Unicode/emoji support
- Requires TUI mode (screen takeover)

### 2. StreamBasedTextEditor (Fallback)
- Uses `Console.ReadLine()` loop
- Line-by-line input, no cursor navigation
- Unicode/emoji support via stream-based input
- Works in all terminal environments

### 3. ExternalEditorLauncher (Alternative)
- Launches `$EDITOR` environment variable
- Falls back to `nano` (macOS/Linux) or `notepad` (Windows)
- Full editing capabilities depend on external editor
- Works across all platforms

---

## Versioning

**Current Version**: 1.0

**Breaking Changes** (would require MAJOR version bump):
- Changing method signature of `EditAsync`
- Removing or renaming `EditorResult` properties
- Changing `EditorOutcome` enum values

**Non-Breaking Changes** (MINOR version bump):
- Adding optional parameters to `EditAsync`
- Adding new `EditorConfiguration` properties
- Adding new `EditorOutcome` enum values
- Adding new `EditorMetadata` fields

---

## Testing Contract

Implementations MUST pass the following test scenarios:

### Unit Tests
1. **Empty initial content** → User saves → Returns saved content
2. **Pre-filled content** → User does not modify → `WasModified = false`
3. **User cancels** → `IsCancelled = true`, `Content` is empty
4. **Ctrl+C pressed** → `IsCancelled = true` immediately
5. **Content exceeds MaxContentLength** → Validation error or truncation (implementation-defined)
6. **ANSI sequences in input** → Stripped if `SanitizeInput = true`
7. **Emoji input** → Preserved correctly in output
8. **Multi-line with blank lines** → All blank lines preserved

### Integration Tests
1. **Full /today workflow** → Edit → Save → Entry created with content
2. **Full /today workflow** → Edit → Cancel → No entry created
3. **Non-interactive terminal** → Fallback behavior works or exception thrown
4. **Large content (10,000 chars)** → Performance < 100ms for operations
5. **Terminal resize during editing** → No data loss, display adapts

---

**Contract Version**: 1.0
**Last Updated**: 2025-10-14
**Status**: Ready for Implementation
