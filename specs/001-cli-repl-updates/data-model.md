# Data Model: CLI REPL Enhancements

**Feature**: 001-cli-repl-updates  
**Date**: 2025-01-19

## Overview

No new data entities are required for this feature. All enhancements use existing data models:

- **CommandHistoryEntry**: Already exists in `src/Features/Shell/Models/CommandHistoryEntry.cs` - no changes needed
- **CommandMetadata**: Already exists in `src/Shared/Models/CommandMetadata.cs` - no changes needed
- **AutocompleteSuggestion**: Already exists in `src/Features/Shell/Models/AutocompleteSuggestion.cs` - no changes needed

## Existing Entities (No Changes)

### CommandHistoryEntry

**Location**: `src/Features/Shell/Models/CommandHistoryEntry.cs`

**Purpose**: Represents a previously executed command with execution context.

**Fields**:
- `SequenceNumber` (int): Order of execution in session
- `Command` (string): The command text that was executed
- `WasSuccessful` (bool): Whether command completed successfully
- `WasInterrupted` (bool): Whether command was cancelled via Ctrl+C
- `ResultSummary` (string?): Optional summary of command result (truncated to 100 chars)
- `Timestamp` (DateTimeOffset): When command was executed

**Usage in This Feature**:
- History navigation (Arrow Up/Down) reads from `ISessionManager.GetHistory()` which returns `IReadOnlyList<CommandHistoryEntry>`
- No changes to storage or retrieval logic
- Circular buffer limit (100 commands) already enforced by `SessionManager`

### CommandMetadata

**Location**: `src/Shared/Models/CommandMetadata.cs`

**Purpose**: Describes available slash commands for autocomplete and help display.

**Fields**:
- `Name` (string): Command name including slash prefix (e.g., "/today")
- `HelpText` (string): Brief description for user guidance
- `Aliases` (string[]?): Alternative names for the command
- `RequiresAuthentication` (bool): Whether command requires active session

**Usage in This Feature**:
- Tab completion uses `CommandMetadata.CommandCatalog` via `IAutocompleteEngine.GetSuggestions()`
- No changes to catalog or matching logic
- Existing scoring and ranking algorithms remain unchanged

### AutocompleteSuggestion

**Location**: `src/Features/Shell/Models/AutocompleteSuggestion.cs`

**Purpose**: Represents a command suggestion with match score.

**Fields**:
- `CommandName` (string): The command name (or alias) that matches
- `HelpText` (string): Help text from CommandMetadata
- `MatchScore` (int): Relevance score (higher = better match)

**Usage in This Feature**:
- Tab completion cycles through `AutocompleteSuggestion` results from `IAutocompleteEngine`
- No changes to suggestion model or scoring

## State Management

### Input Buffer State (In-Memory Only)

**Purpose**: Tracks current input state during REPL prompt interaction.

**Fields** (not persisted, managed by `IEnhancedInputReader`):
- `CurrentBuffer` (string): Current text in input buffer
- `CursorPosition` (int): Current cursor position within buffer
- `HistoryIndex` (int): Current position in history navigation (-1 = not navigating)
- `AutocompleteIndex` (int): Current position in autocomplete suggestions (-1 = not cycling)

**Lifecycle**:
- Created when user starts typing at REPL prompt
- Updated on each keystroke (character input, Tab, Arrow keys)
- Discarded when command is submitted or cancelled (Escape)

### Session State (Existing)

**Purpose**: Tracks REPL session lifecycle and command history.

**Managed By**: `ISessionManager` (no changes)

**State Transitions**:
- `Inactive` → `Active`: When REPL starts (`StartSession()`)
- `Active` → `Terminated`: When REPL exits (`EndSession()`)

**History Storage**:
- In-memory `List<CommandHistoryEntry>` (max 100 entries)
- Circular buffer: oldest entries removed when capacity reached
- Cleared on session start
- Not persisted between sessions

## Data Flow

### Tab Completion Flow

```
User types "/rec" + Tab
  ↓
IEnhancedInputReader detects Tab key
  ↓
Calls IAutocompleteEngine.GetSuggestions("/rec")
  ↓
Returns List<AutocompleteSuggestion> (e.g., ["/record", "/recording"])
  ↓
Cycles through suggestions, displays in prompt
  ↓
User presses Tab again → next suggestion
  ↓
User presses Enter → command executed
```

### History Navigation Flow

```
User presses Arrow Up
  ↓
IEnhancedInputReader detects UpArrow key
  ↓
Calls ISessionManager.GetHistory()
  ↓
Returns IReadOnlyList<CommandHistoryEntry> (up to 100 entries)
  ↓
Navigates backward through history (newest → oldest)
  ↓
Displays historical command in prompt buffer
  ↓
User can edit command, then press Enter
  ↓
Edited command executed and added as new history entry
```

### Escape Flow

```
User presses Escape key
  ↓
IEnhancedInputReader detects Escape (ASCII 27)
  ↓
Cancels current input, clears buffer
  ↓
Returns null to ReplLoop
  ↓
ReplLoop continues loop, displays fresh prompt
```

## Validation Rules

### Input Buffer Validation

- **Max Length**: No explicit limit (commands typically <200 chars)
- **Empty Input**: Allowed (user can press Enter with empty prompt)
- **Whitespace**: Trimmed before command execution (existing behavior)

### History Validation

- **Max Entries**: 100 commands per session (enforced by `SessionManager`)
- **Empty History**: Arrow Up does nothing (no-op)
- **History Index**: Must be -1 (not navigating) or 0-99 (valid history index)

### Autocomplete Validation

- **Prefix Required**: Suggestions only shown for input starting with '/'
- **Min Length**: At least 1 character after '/' to show suggestions
- **Max Suggestions**: Top 10 matches (enforced by `IAutocompleteEngine`)

## Relationships

```
ISessionManager
  └── Manages List<CommandHistoryEntry> (1-to-many)
  
IAutocompleteEngine
  └── Uses CommandMetadata.CommandCatalog (many-to-many via matching)
  └── Returns List<AutocompleteSuggestion> (1-to-many)

IEnhancedInputReader
  └── Uses IAutocompleteEngine (1-to-1)
  └── Uses ISessionManager (1-to-1)
  └── Manages Input Buffer State (1-to-1, ephemeral)
```

## Migration Notes

**No Migration Required**: This feature adds new functionality without changing existing data structures. All enhancements are additive and backward-compatible.

