# Tasks: CLI REPL and Command Updates

**Input**: Design documents from `/specs/001-cli-repl-updates/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are included to meet the 80% coverage requirement. Write tests FIRST (Red-Green-Refactor).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., [US1], [US2], [US3])
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 [P] Review existing Shell feature structure in `src/Features/Shell/`
- [X] T002 [P] Review existing REPL infrastructure (`ReplLoop`, `ISessionManager`, `IAutocompleteEngine`) in `src/Features/Shell/Services/`
- [X] T003 [P] Review existing test structure in `tests/TenSecondTom.Tests/Features/Shell/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create `IConsoleKeyReader` interface and `SystemConsoleKeyReader` implementation in `src/Features/Shell/Services/ConsoleKeyReader.cs` per `contracts/IEnhancedInputReader.md`
- [X] T005 Create `IEnhancedInputReader` interface in `src/Features/Shell/Services/IEnhancedInputReader.cs` with constructor injection pattern per `contracts/IEnhancedInputReader.md`
- [X] T006 Create `EnhancedInputReader` implementation class in `src/Features/Shell/Services/EnhancedInputReader.cs` with `IsAvailable()` method and primary constructor
- [X] T007 Register `IConsoleKeyReader` and `IEnhancedInputReader` services in `src/Features/Shell/DependencyInjection.cs` as singletons
- [X] T008 [P] Add basic cursor position management (Left/Right Arrow, Home/End keys) in `src/Features/Shell/Services/EnhancedInputReader.cs` for input editing
- [X] T009 [P] Add Backspace/Delete key handling in `src/Features/Shell/Services/EnhancedInputReader.cs` for input editing

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Escape Mechanism for Commands (Priority: P1) 🎯 MVP

**Goal**: Users can press Escape to cancel any interactive command prompt and return to the main REPL prompt without partial state changes.

**Independent Test**: Launch REPL, start any interactive command (e.g., `/config set` prompting for a value), press Escape key, verify user returns to main prompt with no configuration changes applied.

### Tests for User Story 1 ⚠️

> **NOTE: Write tests FIRST (TDD). Focus on critical paths - lean tests that verify core behavior.**

- [X] T010 [P] [US1] Create `EnhancedInputReaderTests.cs` in `tests/TenSecondTom.Tests/Features/Shell/` with test `ReadInputAsync_EscapeKey_ReturnsNull()` - covers escape at any point

### Implementation for User Story 1

- [X] T011 [US1] Implement `ReadInputAsync()` method in `src/Features/Shell/Services/EnhancedInputReader.cs` with Escape key detection (ASCII 27 or Ctrl+[) returning null
- [X] T012 [US1] Enhance `ReplLoop.ReadInput()` method in `src/Features/Shell/Services/ReplLoop.cs` to use `IEnhancedInputReader` with TextPrompt fallback, handle null return as no-op

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. Users can press Escape to cancel commands and return to prompt.

---

## Phase 4: User Story 2 - Interactive Command Autocomplete (Priority: P2)

**Goal**: Users can press Tab to cycle through matching command suggestions as they type, completing commands faster with fewer keystrokes.

**Independent Test**: Launch REPL, type partial command like `/rec`, press Tab, verify command completes to `/record` (or cycles through matches if multiple exist).

### Tests for User Story 2 ⚠️

> **NOTE: Write tests FIRST (TDD). Focus on critical paths - lean tests that verify core behavior.**

- [X] T013 [P] [US2] Add unit test `ReadInputAsync_TabKey_CompletesAndCycles()` - covers Tab completion with single match and cycling through multiple matches
- [X] T014 [P] [US2] Add unit test `ReadInputAsync_TabKeyNoMatches_NoChange()` - verifies no-op when no suggestions exist

### Implementation for User Story 2

- [X] T015 [US2] Implement Tab key detection and `IAutocompleteEngine.GetSuggestions()` integration in `src/Features/Shell/Services/EnhancedInputReader.cs`
- [X] T016 [US2] Implement suggestion cycling logic (Tab = forward, Shift+Tab = backward) with buffer update in `src/Features/Shell/Services/EnhancedInputReader.cs`
- [X] T017 [US2] Handle input during cycling (accept suggestion + append character + reset state) in `src/Features/Shell/Services/EnhancedInputReader.cs`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. Users can press Tab to complete commands and Escape to cancel.

---

## Phase 5: User Story 3 - Command History Navigation (Priority: P2)

**Goal**: Users can navigate through previously executed commands using Arrow Up/Down keys, allowing quick re-execution or modification of past commands.

**Independent Test**: Launch REPL, execute several commands (e.g., `/help`, `/config`, `/search test`), press Arrow Up to navigate backward through history, Arrow Down to navigate forward, verify correct commands appear in prompt.

### Tests for User Story 3 ⚠️

> **NOTE: Write tests FIRST (TDD). Focus on critical paths - lean tests that verify core behavior.**

- [X] T018 [P] [US3] Add unit test `ReadInputAsync_ArrowUpDown_NavigatesHistory()` - covers backward/forward navigation through history with correct boundary handling
- [X] T019 [P] [US3] Add unit test `ReadInputAsync_ArrowUpEmptyHistory_NoOp()` - verifies no-op when history is empty

### Implementation for User Story 3

- [X] T020 [US3] Implement Arrow Up/Down key detection with `ISessionManager.GetHistory()` integration in `src/Features/Shell/Services/EnhancedInputReader.cs`
- [X] T021 [US3] Implement history index tracking with boundary handling (oldest stays at oldest, newest returns to empty) in `src/Features/Shell/Services/EnhancedInputReader.cs`
- [X] T022 [US3] Reset autocomplete state when navigating history in `src/Features/Shell/Services/EnhancedInputReader.cs`

**Checkpoint**: All user stories should now be independently functional. Users can press Escape to cancel, Tab to complete, and Arrow Up/Down to navigate history.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup and validation

- [X] T023 [P] Add XML documentation comments to `IConsoleKeyReader`, `IEnhancedInputReader`, and `EnhancedInputReader` in `src/Features/Shell/Services/`
- [X] T024 [P] Verify 80% code coverage requirement met for all new code
- [X] T025 [P] Manual testing on macOS - verify all keyboard shortcuts work (Tab, Arrow Up/Down, Escape)
- [X] T026 [P] Verify escape, autocomplete, and history features don't interfere with existing Ctrl+C cancellation behavior

---

## Phase 7: Escape Support for Spectre.Console Prompts (Added)

**Purpose**: Extend Escape key support to all Spectre.Console prompts used in wizards and commands

**Problem**: The Phase 3 escape support only worked at the REPL prompt level. Multi-step wizards (e.g., `/audio config`, `/config all`) use Spectre.Console `SelectionPrompt<T>`, `TextPrompt<T>`, etc., which don't natively support Escape cancellation.

**Solution**: Custom `IAnsiConsole` wrapper that intercepts Escape key and throws `PromptCancelledException`

- [X] T027 [P] [US1] Create `EscapeCancellableInput.cs` in `src/Infrastructure/Cli/` implementing `IAnsiConsoleInput` with Escape key interception
- [X] T028 [P] [US1] Create `EscapeCancellableConsole.cs` in `src/Infrastructure/Cli/` implementing `IAnsiConsole` wrapper with custom input
- [X] T029 [US1] Update `CancellablePrompt.cs` in `src/Infrastructure/Cli/` to use `EscapeCancellableConsole`
- [X] T030 [US1] Update `SpectreConsoleSetupWizard.cs` in `src/Features/Setup/Services/` - replace all `_console.Prompt()` calls with escape-aware helpers
- [X] T031 [P] Manual testing - verify Escape cancels multi-step wizards (e.g., `/audio config`) and returns to REPL
- [X] T032 [P] Update README.md Shell Features section to document Escape key support

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P2)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - No dependencies on US1
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - No dependencies on US1/US2

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel
- Once Foundational phase completes, all user stories can start in parallel
- Polish phase tasks marked [P] can run in parallel

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009)
3. Complete Phase 3: User Story 1 - Escape (T010-T012)
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. User Story 1 (Escape) → Test → MVP!
3. User Story 2 (Tab Completion) → Test → Deploy
4. User Story 3 (History Navigation) → Test → Deploy
5. Polish → Final release

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story
- Tests are lean and critical-impact focused (TDD)
- `IConsoleKeyReader` abstraction enables unit testing without real console
- Custom implementation required (JKToolKit incompatible)
- Maintain backward compatibility with existing REPL behavior

---

## Task Summary

**Total Tasks**: 32 (26 original + 6 added in Phase 7)

**Completed Tasks**: 32/32 ✅

**Task Count by Phase**:
- Phase 1 (Setup): 3 tasks (T001-T003) ✅
- Phase 2 (Foundational): 6 tasks (T004-T009) ✅
- Phase 3 (User Story 1 - Escape): 3 tasks (1 test + 2 implementation) ✅
- Phase 4 (User Story 2 - Tab Completion): 5 tasks (2 tests + 3 implementation) ✅
- Phase 5 (User Story 3 - History Navigation): 5 tasks (2 tests + 3 implementation) ✅
- Phase 6 (Polish): 4 tasks (T023-T026) ✅
- Phase 7 (Spectre.Console Escape Support): 6 tasks (T027-T032) ✅

**Task Count by User Story**:
- User Story 1 (P1): 9 tasks (3 original + 6 Phase 7) ✅
- User Story 2 (P2): 5 tasks ✅
- User Story 3 (P2): 5 tasks ✅

**Test Strategy**: Lean unit tests with mocked `IConsoleKeyReader` - critical paths only

**Implementation Summary**:
- **User Story 1**: Press Escape → returns null → no state changes (REPL + Spectre.Console prompts)
- **User Story 2**: Press Tab → cycles suggestions → buffer updated
- **User Story 3**: Press Arrow Up/Down → navigates history → buffer updated

**Key Files Created/Modified**:
- `src/Features/Shell/Services/EnhancedInputReader.cs` - Tab completion, history navigation, Escape at REPL
- `src/Infrastructure/Cli/EscapeCancellableInput.cs` - Escape key interception for Spectre.Console
- `src/Infrastructure/Cli/EscapeCancellableConsole.cs` - IAnsiConsole wrapper with custom input
- `src/Infrastructure/Cli/CancellablePrompt.cs` - Static helpers for escape-aware prompts
- `src/Features/Setup/Services/SpectreConsoleSetupWizard.cs` - All prompts now support Escape

**Format Validation**: ✅ All tasks follow checklist format

