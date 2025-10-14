# API Contracts: Interactive Text Editing

**Feature**: 006-improved-text-editing
**Date**: 2025-10-14
**Status**: Design Phase

## Overview

This directory contains the API contracts (programmatic interfaces) for the interactive text editing feature. Since TenSecondTom is a CLI application (not a REST API), these contracts define service interfaces, behavior specifications, and integration points.

## Contract Documents

### [IInteractiveTextEditor.md](./IInteractiveTextEditor.md)
Primary service interface for interactive multi-line text editing.

**Key Details**:
- **Method**: `EditAsync(initialContent?, configuration?, cancellationToken?)`
- **Returns**: `Task<EditorResult>`
- **Outcomes**: Saved, Cancelled, Error
- **Keyboard Shortcuts**: Arrow keys, Home/End, Ctrl+D/Ctrl+Enter, Ctrl+C
- **Guarantees**: Unicode preservation, blank line preservation, ANSI sanitization

## Service Architecture

```
┌─────────────────────────────────────────────────────────┐
│                  IInteractiveTextEditor                  │
│                  (Service Interface)                     │
└─────────────────────────────────────────────────────────┘
                          ▲
                          │ implements
        ┌─────────────────┼─────────────────┐
        │                 │                 │
┌───────┴────────┐ ┌──────┴───────┐ ┌──────┴────────────┐
│TerminalGuiText │ │StreamBased   │ │ExternalEditor     │
│Editor          │ │TextEditor    │ │Launcher           │
│(Primary)       │ │(Fallback)    │ │(Alternative)      │
└────────────────┘ └──────────────┘ └───────────────────┘
```

## Integration Points

### 1. Features/Today/CreateDailyEntryHandler
**Current Behavior**: Prompts user with questions, accepts single-line input
**New Behavior**: Launches `IInteractiveTextEditor.EditAsync()` for multi-line editing

**Before**:
```csharp
var response = AnsiConsole.Ask<string>("What did you accomplish today?");
```

**After**:
```csharp
var result = await _editor.EditAsync("What did you accomplish today?\n\n");
if (result.IsCancelled) return Result.Failure("Cancelled");
var response = result.Content;
```

### 2. Future: Features/Search (Edit Previous Entry)
**Planned**: Allow editing previous entries from search results
**Integration**: Same `IInteractiveTextEditor` instance with pre-filled content

```csharp
// Pre-fill with existing entry content
var result = await _editor.EditAsync(
    initialContent: existingEntry.Content,
    configuration: EditorConfiguration.Default
);
```

## Data Flow

```
User Action (CLI)
    ↓
CreateDailyEntryHandler (CQRS Handler)
    ↓
IInteractiveTextEditor.EditAsync() (Service)
    ↓
TextEditingSession (Domain Model)
    ↓
Terminal.Gui TextView OR Console.ReadLine (Implementation)
    ↓
EditorResult (Value Object)
    ↓
Handler creates DailyEntry
    ↓
FileSystemStorageProvider (Persistence)
```

## Dependency Injection Registration

```csharp
// Program.cs or ServiceCollectionExtensions.cs
services.AddTransient<IInteractiveTextEditor, TerminalGuiTextEditor>();

// Alternative registration with fallback strategy
services.AddTransient<IInteractiveTextEditor>(sp =>
{
    if (Console.IsInputRedirected)
    {
        return new StreamBasedTextEditor(
            sp.GetRequiredService<ILogger<StreamBasedTextEditor>>()
        );
    }

    return new TerminalGuiTextEditor(
        sp.GetRequiredService<ILogger<TerminalGuiTextEditor>>()
    );
});
```

## Testing Strategy

### Unit Tests (`tests/TenSecondTom.Tests/Unit/Shared/TextEditing/`)
- `EditorResultTests.cs` - Factory methods and properties
- `EditorMetadataTests.cs` - Metadata calculation
- `TextEditingSessionTests.cs` - Session lifecycle
- `EditorConfigurationTests.cs` - Validation rules

### Integration Tests (`tests/TenSecondTom.IntegrationTests/Integration/`)
- `TodayCommandWithEditorTests.cs` - Full `/today` flow with editor
- `EditorWorkflowTests.cs` - Save, Cancel, Error scenarios
- `NonInteractiveEditorTests.cs` - Fallback behavior

### Manual Testing
- Test on macOS Terminal.app
- Test on Windows Terminal
- Test with emoji and Unicode characters
- Test with 10,000+ character content
- Test terminal resize during editing

## Configuration

### EditorConfiguration Defaults
```json
{
  "MaxContentLength": 50000,
  "MaxLineCount": 1000,
  "ShowHints": true,
  "PreviewLineLimit": 10,
  "SanitizeInput": true
}
```

### Customization
Users can customize via `appsettings.json`:
```json
{
  "TextEditing": {
    "MaxContentLength": 100000,
    "ShowHints": false
  }
}
```

## Error Handling

### EditorException
Custom exception type for editor-specific errors:
```csharp
public class EditorException : Exception
{
    public EditorException(string message) : base(message) { }
    public EditorException(string message, Exception inner) : base(message, inner) { }
}
```

**Usage**:
- Thrown when terminal cannot support interactive editing
- Thrown when editor initialization fails
- Caller should handle gracefully or show user-friendly error

## Performance Benchmarks

### Success Criteria (from spec SC-002, SC-003)
- Paste operations (5,000 chars): < 200ms
- Cursor movements: < 100ms perceived delay
- Task completion rate increase: 25%

### Monitoring
Log all session metadata via Serilog:
- Session duration
- Content length
- Line count
- Outcome (Saved/Cancelled/Error)

Analyze metrics to verify performance targets.

## Future Enhancements

### Planned Additions (Post-MVP)
1. **Syntax Highlighting** - Markdown syntax highlighting during editing
2. **Auto-Save** - Periodic draft saving during long sessions
3. **Templates** - Pre-fill with daily reflection prompts
4. **Spell Check** - Inline spell checking (underline errors)
5. **Word Count** - Display word/character count in status bar

### Extensibility Points
- `IInteractiveTextEditor` can add overloads (keep signature stable)
- `EditorConfiguration` can add new properties (backward compatible)
- `EditorMetadata` can add new telemetry fields
- New `EditorOutcome` enum values can be added

---

**Contracts Version**: 1.0
**Status**: Ready for Implementation
**Next Phase**: Generate tasks.md with `/speckit.tasks`
