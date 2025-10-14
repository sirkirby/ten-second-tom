# Data Model: Interactive Console Text Editing

**Feature**: Interactive Console Text Editing Experience
**Date**: 2025-10-14
**Status**: Design

## Overview

This data model defines the entities and value objects required for the interactive multi-line text editor feature. The design follows CQRS principles, uses modern C# records for immutability, and integrates with the existing TenSecondTom architecture.

---

## Core Entities

### 1. TextEditingSession

**Purpose**: Represents a user interaction session for collecting or editing text content.

**Type**: Entity (mutable state during editing)

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Represents an interactive text editing session with lifecycle management.
/// </summary>
public sealed class TextEditingSession
{
    /// <summary>
    /// Unique identifier for this editing session (for logging/tracing)
    /// </summary>
    public Guid SessionId { get; }

    /// <summary>
    /// Initial content provided when session started (may be empty for new entry)
    /// </summary>
    public string InitialContent { get; }

    /// <summary>
    /// Current content being edited (updated during session)
    /// </summary>
    public string CurrentContent { get; private set; }

    /// <summary>
    /// When the editing session started (UTC)
    /// </summary>
    public DateTime StartedAt { get; }

    /// <summary>
    /// When the editing session ended (UTC), null if still active
    /// </summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>
    /// Final outcome of the editing session
    /// </summary>
    public EditorOutcome? Outcome { get; private set; }

    /// <summary>
    /// Whether the content was modified during the session
    /// </summary>
    public bool HasChanges => CurrentContent != InitialContent;

    /// <summary>
    /// Whether the session is still active
    /// </summary>
    public bool IsActive => EndedAt == null;

    /// <summary>
    /// Length of current content in characters
    /// </summary>
    public int ContentLength => CurrentContent.Length;

    /// <summary>
    /// Number of lines in current content
    /// </summary>
    public int LineCount => CurrentContent.Split('\n').Length;

    public TextEditingSession(string? initialContent = null)
    {
        SessionId = Guid.NewGuid();
        InitialContent = initialContent ?? string.Empty;
        CurrentContent = InitialContent;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Update the current content during editing
    /// </summary>
    public void UpdateContent(string content)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update content of completed session");

        CurrentContent = content ?? string.Empty;
    }

    /// <summary>
    /// Complete the session with the given outcome
    /// </summary>
    public void Complete(EditorOutcome outcome)
    {
        if (!IsActive)
            throw new InvalidOperationException("Session already completed");

        Outcome = outcome;
        EndedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Duration of the editing session
    /// </summary>
    public TimeSpan Duration => (EndedAt ?? DateTime.UtcNow) - StartedAt;
}
```

**Relationships**:
- Created by `InteractiveTextEditor` service
- Returns `EditorResult` value object on completion
- Logged via Serilog with `SessionId` correlation

**Validation Rules**:
- `CurrentContent` cannot be null (empty string allowed)
- Cannot update content after session completed
- Cannot complete session twice

**State Transitions**:
```
Created (IsActive=true)
  → UpdateContent (0-n times)
  → Complete (IsActive=false)
```

---

### 2. EditorOutcome (Enum)

**Purpose**: Defines the possible outcomes of an editing session.

**Type**: Enumeration

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Defines the possible outcomes when an editing session completes.
/// </summary>
public enum EditorOutcome
{
    /// <summary>
    /// User explicitly saved the content (pressed Save in confirmation)
    /// </summary>
    Saved,

    /// <summary>
    /// User cancelled the session without saving (pressed Cancel or Ctrl+C)
    /// </summary>
    Cancelled,

    /// <summary>
    /// Session timed out (if timeout implemented in future)
    /// </summary>
    TimedOut,

    /// <summary>
    /// An error occurred during editing (terminal issues, etc.)
    /// </summary>
    Error
}
```

---

## Value Objects

### 3. EditorResult

**Purpose**: Immutable result returned from a completed editing session.

**Type**: Record (immutable value object)

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Immutable result returned when an editing session completes.
/// Follows the Result pattern for explicit success/failure handling.
/// </summary>
public sealed record EditorResult
{
    /// <summary>
    /// The edited content if session was saved, empty if cancelled/error
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// How the editing session ended
    /// </summary>
    public EditorOutcome Outcome { get; init; }

    /// <summary>
    /// Whether the user saved the content
    /// </summary>
    public bool IsSaved => Outcome == EditorOutcome.Saved;

    /// <summary>
    /// Whether the user cancelled the session
    /// </summary>
    public bool IsCancelled => Outcome == EditorOutcome.Cancelled;

    /// <summary>
    /// Whether an error occurred
    /// </summary>
    public bool IsError => Outcome == EditorOutcome.Error;

    /// <summary>
    /// Error message if Outcome is Error, null otherwise
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Session metadata (duration, line count, etc.)
    /// </summary>
    public EditorMetadata Metadata { get; init; } = EditorMetadata.Empty;

    // Factory methods

    /// <summary>
    /// Create a successful result with saved content
    /// </summary>
    public static EditorResult Saved(string content, EditorMetadata metadata) => new()
    {
        Content = content,
        Outcome = EditorOutcome.Saved,
        Metadata = metadata
    };

    /// <summary>
    /// Create a cancelled result
    /// </summary>
    public static EditorResult Cancelled(EditorMetadata metadata) => new()
    {
        Content = string.Empty,
        Outcome = EditorOutcome.Cancelled,
        Metadata = metadata
    };

    /// <summary>
    /// Create an error result with message
    /// </summary>
    public static EditorResult Error(string errorMessage, EditorMetadata metadata) => new()
    {
        Content = string.Empty,
        Outcome = EditorOutcome.Error,
        ErrorMessage = errorMessage,
        Metadata = metadata
    };
}
```

**Invariants**:
- `Content` is never null (empty string if cancelled/error)
- `ErrorMessage` is non-null only when `Outcome == EditorOutcome.Error`
- `Metadata` is never null (use `EditorMetadata.Empty` for minimal data)

---

### 4. EditorMetadata

**Purpose**: Metadata about the editing session (for telemetry and diagnostics).

**Type**: Record (immutable value object)

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Metadata about an editing session, useful for telemetry and diagnostics.
/// </summary>
public sealed record EditorMetadata
{
    /// <summary>
    /// Session identifier for correlation
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Duration of the editing session
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of lines in final content
    /// </summary>
    public int LineCount { get; init; }

    /// <summary>
    /// Number of characters in final content
    /// </summary>
    public int CharacterCount { get; init; }

    /// <summary>
    /// Whether content was modified from initial state
    /// </summary>
    public bool WasModified { get; init; }

    /// <summary>
    /// Empty metadata for cancelled/error scenarios
    /// </summary>
    public static readonly EditorMetadata Empty = new()
    {
        SessionId = Guid.Empty,
        Duration = TimeSpan.Zero,
        LineCount = 0,
        CharacterCount = 0,
        WasModified = false
    };

    /// <summary>
    /// Create metadata from a completed session
    /// </summary>
    public static EditorMetadata FromSession(TextEditingSession session) => new()
    {
        SessionId = session.SessionId,
        Duration = session.Duration,
        LineCount = session.LineCount,
        CharacterCount = session.ContentLength,
        WasModified = session.HasChanges
    };
}
```

---

### 5. EditorConfiguration

**Purpose**: Configuration options for the text editor behavior.

**Type**: Record (immutable value object)

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Configuration options for the interactive text editor.
/// </summary>
public sealed record EditorConfiguration
{
    /// <summary>
    /// Maximum content length in characters (for performance)
    /// </summary>
    public int MaxContentLength { get; init; } = 50_000;

    /// <summary>
    /// Maximum number of lines in content
    /// </summary>
    public int MaxLineCount { get; init; } = 1_000;

    /// <summary>
    /// Whether to show hint text with keyboard shortcuts
    /// </summary>
    public bool ShowHints { get; init; } = true;

    /// <summary>
    /// Number of lines to show in preview (0 = all)
    /// </summary>
    public int PreviewLineLimit { get; init; } = 10;

    /// <summary>
    /// Whether to sanitize ANSI escape sequences from input
    /// </summary>
    public bool SanitizeInput { get; init; } = true;

    /// <summary>
    /// Default configuration with sensible defaults
    /// </summary>
    public static readonly EditorConfiguration Default = new();
}
```

---

## Supporting Types

### 6. SanitizedText

**Purpose**: Represents text that has been sanitized (ANSI sequences stripped).

**Type**: Record (immutable value object)

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

/// <summary>
/// Represents text that has been sanitized to remove ANSI escape sequences
/// and terminal control codes.
/// </summary>
public sealed record SanitizedText
{
    /// <summary>
    /// The sanitized text content (safe for storage and display)
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Whether any content was removed during sanitization
    /// </summary>
    public bool WasSanitized { get; init; }

    /// <summary>
    /// Original length before sanitization
    /// </summary>
    public int OriginalLength { get; init; }

    /// <summary>
    /// Number of characters removed
    /// </summary>
    public int RemovedCount => OriginalLength - Content.Length;
}
```

---

## Integration with Existing Models

### EntryContent (Existing in Features/Today)

The text editor integrates with the existing `DailyEntry` model:

```csharp
// Existing model (no changes required)
namespace TenSecondTom.Features.Today.Models;

public sealed class DailyEntry
{
    public DateTime Date { get; init; }
    public string Content { get; init; } = string.Empty;  // ← Editor provides this
    public DailySummary? Summary { get; init; }
    // ... other properties
}
```

**Integration**:
- `CreateDailyEntryHandler` will invoke `InteractiveTextEditor.EditAsync()`
- `EditorResult.Content` maps to `DailyEntry.Content`
- If `EditorResult.IsCancelled`, handler aborts without creating entry

---

## Service Layer Integration

### IInteractiveTextEditor (Service Interface)

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
    /// <param name="configuration">Editor configuration options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing edited content and outcome</returns>
    Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
}
```

**Implementations**:
1. `TerminalGuiTextEditor` - Uses Terminal.Gui TextView (primary)
2. `StreamBasedTextEditor` - Fallback for non-interactive terminals
3. `ExternalEditorLauncher` - Launches $EDITOR (alternative approach)

---

## Validation Rules

### Content Validation

Implemented via FluentValidation:

```csharp
namespace TenSecondTom.Shared.TextEditing.Validation;

public sealed class EditorContentValidator : AbstractValidator<string>
{
    public EditorContentValidator(EditorConfiguration config)
    {
        RuleFor(content => content)
            .NotNull()
            .WithMessage("Content cannot be null");

        RuleFor(content => content.Length)
            .LessThanOrEqualTo(config.MaxContentLength)
            .WithMessage($"Content exceeds maximum length of {config.MaxContentLength} characters");

        RuleFor(content => content.Split('\n').Length)
            .LessThanOrEqualTo(config.MaxLineCount)
            .WithMessage($"Content exceeds maximum of {config.MaxLineCount} lines");
    }
}
```

---

## Persistence Considerations

**No persistence at data model layer** - these are transient objects:
- `TextEditingSession` lives only during editing interaction
- `EditorResult` is passed to calling handler (e.g., `CreateDailyEntryHandler`)
- Calling handler is responsible for persisting content to storage

**Logging**:
- All sessions logged via Serilog with structured logging
- `SessionId` used for correlation
- Metadata (duration, line count, outcome) logged for telemetry

---

## Performance Characteristics

### Memory Usage
- `TextEditingSession`: ~200 bytes + content size
- `EditorResult`: ~150 bytes + content size
- `EditorMetadata`: ~100 bytes

### Expected Scale
- Typical content: 500-5,000 characters (few KB)
- Maximum supported: 50,000 characters (~50 KB)
- Sessions are short-lived (< 5 minutes typical)

### Disposal
No explicit disposal required - all types are managed objects. Terminal.Gui TextView (if used) requires `Application.Shutdown()` for cleanup, handled by `TerminalGuiTextEditor` service.

---

## Future Extensibility

### Planned Extensions (Not in current spec)
1. **Auto-save**: Persist drafts periodically during editing
2. **Spell-check**: Highlight spelling errors during editing
3. **Templates**: Pre-fill with templates or prompts
4. **Version history**: Track multiple versions of same entry
5. **Collaborative editing**: Multi-user editing (remote)

### Extension Points
- `EditorConfiguration` can add new properties (backward compatible)
- `EditorOutcome` can add new enum values
- `EditorMetadata` can add new telemetry fields
- `IInteractiveTextEditor` can add overloads (keep original signature)

---

**Document Version**: 1.0
**Last Updated**: 2025-10-14
**Status**: Ready for implementation
