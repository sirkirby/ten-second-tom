# Implementation Plan: CLI REPL and Command Updates

**Branch**: `001-cli-repl-updates` | **Date**: 2025-01-19 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-cli-repl-updates/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Enhance the CLI REPL with three critical usability features: (1) escape mechanism for canceling interactive commands, (2) interactive Tab completion for commands using custom Console.ReadKey implementation (repository evidence confirms JKToolKit.Spectre.AutoCompletion is incompatible - designed for CommandApp, not TextPrompt), and (3) Arrow Up/Down history navigation. These features improve user experience by providing standard CLI interaction patterns that users expect from modern command-line tools.

## Technical Context

**Language/Version**: C# 14 with .NET 10  
**Primary Dependencies**: Spectre.Console 0.51.1, System.CommandLine 2.0-rc, MediatR 13.1.0 (no new NuGet package dependencies - uses built-in .NET Console.ReadKey API for custom input handling)  
**Storage**: JSON file persistence (`~/ten-second-tom/data/history.json`) - `IHistoryStore` with `SessionManager` integration  
**Testing**: xUnit 2.9+ with FluentAssertions 8.7+ (80% coverage minimum)  
**Target Platform**: macOS (primary), Windows (supported), Linux (future)  
**Project Type**: CLI application (single project)  
**Performance Goals**: 100ms escape response time, 200ms history navigation per step, 50% keystroke reduction with autocomplete  
**Constraints**: Must integrate with existing REPL infrastructure (`ReplLoop`, `ICommandRouter`, `ISessionManager`), no breaking changes to existing command execution flow  
**Scale/Scope**: Single-user CLI tool, in-memory history limited to 100 commands per session

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ Architecture Compliance
- **VSA Compliance**: Feature enhancements to existing Shell feature - no new feature slices needed
- **Co-location Pattern**: Changes will be within existing `src/Features/Shell/` structure
- **Cross-Feature Communication**: No cross-feature dependencies - all changes isolated to Shell feature
- **Shared Code**: Uses existing `CommandMetadata`, `CommandHistoryEntry` models - no new shared code needed

### ✅ Technology Stack Compliance
- **.NET 10**: ✅ Already using .NET 10
- **Spectre.Console**: ✅ Already in dependencies (0.51.1)
- **JKToolKit.Spectre.AutoCompletion**: ❌ **NOT USED** - Incompatible with `TextPrompt<T>` (designed for `CommandApp`, not interactive prompts). Custom `Console.ReadKey()` implementation used instead.
- **System.CommandLine**: ✅ Already using 2.0-rc
- **No Web/GUI Frameworks**: ✅ No violations

### ✅ Development Workflow Compliance
- **TDD Required**: ✅ Tests must be written first (Red-Green-Refactor)
- **80% Coverage**: ✅ All new code must meet coverage requirement
- **Modern C#**: ✅ Must use file-scoped namespaces, primary constructors, records

### ✅ Configuration Management Compliance
- **Options Pattern**: ✅ No configuration changes needed - feature uses existing infrastructure
- **No Direct IConfiguration**: ✅ No violations

### ⚠️ Potential Considerations
- **Tab Completion Implementation**: Repository evidence (003-cli-interface-upgrade/AUTOCOMPLETE-FIXES-SUMMARY.md) confirms `TextPrompt<T>` doesn't support Tab completion in 0.51.1. JKToolKit designed for `CommandApp`, not `TextPrompt`. Custom `Console.ReadKey` implementation required (see research.md)
- **Integration Complexity**: Must ensure escape, autocomplete, and history features don't interfere with existing Ctrl+C cancellation
- **Unicode Support**: Custom input reader must handle multi-byte characters (emoji, non-Latin scripts) correctly

**Gate Status**: ✅ **PASS** - All constitution requirements met. Proceed to Phase 0 research.

## Project Structure

### Documentation (this feature)

```text
specs/001-cli-repl-updates/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Features/
│   └── Shell/
│       ├── Services/
│       │   ├── ReplLoop.cs              # Enhanced with escape, autocomplete, history
│       │   ├── ConsoleKeyReader.cs      # New: IConsoleKeyReader + SystemConsoleKeyReader (testability)
│       │   ├── IEnhancedInputReader.cs  # New: Interface for enhanced input
│       │   ├── EnhancedInputReader.cs   # New: Custom input reader for Tab/Arrow/Escape
│       │   ├── IHistoryStore.cs         # New: Interface for history persistence
│       │   ├── HistoryStore.cs          # New: JSON file-based history persistence
│       │   ├── CommandRouter.cs          # No changes
│       │   ├── SessionManager.cs        # Modified: Integrated with IHistoryStore
│       │   ├── AutocompleteEngine.cs    # No changes (uses existing)
│       │   └── CommandAutoCompleteSource.cs  # No changes (uses existing)
│       └── Models/
│           └── CommandHistoryEntry.cs    # No changes
├── Features/
│   └── Shell/
│       └── DependencyInjection.cs  # Register IConsoleKeyReader, IEnhancedInputReader, IHistoryStore
├── Infrastructure/
│   └── Cli/
│       ├── CancellablePrompt.cs         # New: Static helper for escape-cancellable prompts
│       └── EscapeCancellableInput.cs    # New: IAnsiConsoleInput implementation
└── Shared/
    └── Models/
        └── CommandMetadata.cs            # No changes

tests/
├── TenSecondTom.Tests/
│   └── Features/
│       └── Shell/
│           ├── EnhancedInputReaderTests.cs  # Lean unit tests with mocked IConsoleKeyReader
│           └── HistoryStoreTests.cs         # Tests for history persistence
└── (No integration tests - lean test strategy focuses on critical unit tests only)
```

**Structure Decision**: Single project structure maintained. All changes are enhancements to existing Shell feature. No new feature slices or infrastructure projects needed. Tests follow existing patterns in `TenSecondTom.Tests` and `TenSecondTom.IntegrationTests`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations detected. All changes comply with VSA and constitution requirements.

## Phase Completion Summary

### Phase 0: Research ✅ Complete

**Output**: `research.md`

**Key Decisions**:
1. **Tab Completion**: Custom `Console.ReadKey()` implementation (JKToolKit incompatible - designed for `CommandApp`, not `TextPrompt<T>`)
2. **Escape Key**: Escape (ASCII 27) or Ctrl+[ for cancellation
3. **History Navigation**: Arrow Up/Down with existing `ISessionManager.GetHistory()`
4. **Integration**: Enhance `ReplLoop.ReadInput()` method, maintain backward compatibility

**Research Findings**:
- **Repository Evidence**: `specs/003-cli-interface-upgrade/AUTOCOMPLETE-FIXES-SUMMARY.md` definitively proves `TextPrompt<T>` doesn't support Tab completion in Spectre.Console 0.51.1
- **JKToolKit Incompatibility**: JKToolKit.Spectre.AutoCompletion designed for `CommandApp` (command framework), not `TextPrompt<T>` (interactive prompt) - these are separate components
- **Current State**: Codebase shows `CommandAutoCompleteSource` exists but only provides post-input suggestions, not real-time Tab
- Custom implementation provides full control over key handling while maintaining Spectre.Console styling
- Existing infrastructure (`IAutocompleteEngine`, `ISessionManager`) sufficient for all features

### Phase 1: Design & Contracts ✅ Complete

**Outputs**:
- `data-model.md`: No new entities required, uses existing models
- `contracts/IEnhancedInputReader.md`: New interface contracts for enhanced input handling
- `quickstart.md`: User and developer guides

**Design Artifacts**:
- **New Abstraction**: `IConsoleKeyReader` + `SystemConsoleKeyReader` for testability (enables mocking `Console.ReadKey()`)
- **New Interface**: `IEnhancedInputReader` with constructor-injected dependencies (follows project patterns)
- **Integration Point**: `ReplLoop.ReadInput()` enhanced to use `IEnhancedInputReader` with TextPrompt fallback
- **Service Registration**: `IConsoleKeyReader` and `IEnhancedInputReader` registered as singletons in Shell feature DI
- **Test Strategy**: Lean unit tests with mocked `IConsoleKeyReader` - critical paths only

**Agent Context**: ✅ Updated (Cursor IDE context file)

### Phase 2: Task Breakdown ✅ Complete

**Output**: `tasks.md` with 26 lean, focused tasks

**Task Summary**:
1. Create `IConsoleKeyReader` abstraction for testability
2. Create `IEnhancedInputReader` interface with constructor injection
3. Implement `EnhancedInputReader` with custom key handling
4. Enhance `ReplLoop.ReadInput()` with fallback to TextPrompt
5. Register services in DI
6. Write lean unit tests with mocked `IConsoleKeyReader` (5 critical tests)
7. Manual validation on macOS

### Phase 2.5: History Persistence ✅ Complete

**Output**: Persistent history storage implemented

**Implementation**:
1. Created `IHistoryStore` interface for history persistence abstraction
2. Implemented `HistoryStore` with JSON file storage at `~/ten-second-tom/data/history.json`
3. Integrated `SessionManager` with `IHistoryStore` - loads on session start, saves after each command
4. Registered `IHistoryStore` as singleton in DI

### Phase 3: Escape Support for Spectre.Console Prompts 🔄 In Progress

**Problem**: Escape key only works at REPL prompt, not in `SelectionPrompt`, `TextPrompt`, etc. used throughout commands/wizards.

**Solution**: `CancellablePrompt` static helper with `EscapeCancellableInput` IAnsiConsoleInput wrapper

**Research Findings** (see `research.md` Section 7):
- Spectre.Console 0.51.1 has NO native Escape support in prompts
- Must intercept Escape at `IAnsiConsoleInput.ReadKey()` layer
- Throw `PromptCancelledException` on Escape, catch in wrapper methods

**Files to Create**:
| File | Description |
|------|-------------|
| `src/Infrastructure/Cli/EscapeCancellableInput.cs` | `IAnsiConsoleInput` implementation that throws on Escape |
| `src/Infrastructure/Cli/CancellablePrompt.cs` | Static helper methods for cancellable prompts |

**Integration Pattern**:
```csharp
// Before:
var provider = AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select:").AddChoices(["A","B"]));

// After:
var provider = CancellablePrompt.Selection<string>(p => p.Title("Select:").AddChoices(["A","B"]));
if (provider is null) { /* User pressed Escape */ return; }
```

**Commands to Update**: All commands using `AnsiConsole.Prompt()` with interactive prompts

## Next Steps

1. ~~**Run `/speckit.tasks`**: Generate task breakdown from this plan~~ ✅ Done
2. ~~**Phase 2 Implementation**: Enhanced input reader~~ ✅ Done
3. ~~**Phase 2.5 Implementation**: History persistence~~ ✅ Done
4. **Phase 3 Implementation**: Create `CancellablePrompt` helper with `EscapeCancellableInput`
5. **Phase 3 Integration**: Update commands/wizards to use `CancellablePrompt.*` methods
6. **Testing**: Achieve 80% code coverage minimum
7. **Manual Testing**: Validate escape works in all prompt types on macOS

## Notes

- **JKToolKit.Spectre.AutoCompletion**: Repository evidence confirms incompatibility - designed for `CommandApp` (command framework), not `TextPrompt<T>` (interactive prompt). Previous feature (003-cli-interface-upgrade) proved `TextPrompt` doesn't support Tab completion in 0.51.1. Custom implementation required.
- **Backward Compatibility**: All changes are additive - existing REPL behavior preserved
- **Performance**: Target <100ms escape, <200ms history navigation per success criteria
- **Cross-Platform**: Uses .NET `Console.ReadKey()` for platform-agnostic key handling
