# Feature Specification: CLI REPL and Command Updates

**Feature Branch**: `001-cli-repl-updates`
**Created**: 2025-01-19
**Status**: Implemented
**Input**: User description: "CLI REPL and command updates"

## Clarifications

### Session 2025-01-19

- Q: How should Tab completion be implemented given Spectre.Console 0.51.1 limitations? → A: Custom Console.ReadKey implementation (repository evidence confirms JKToolKit.Spectre.AutoCompletion incompatible - designed for CommandApp, not TextPrompt)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Escape Mechanism for Commands (Priority: P1)

Users need a consistent way to cancel or escape from any interactive command prompt or submenu within the REPL, allowing them to return to the main prompt without completing the current operation. This is a standard pattern in modern CLI applications that improves user experience by providing a safe "back" mechanism.

**Why this priority**: This is a fundamental usability feature that prevents users from feeling trapped in a command flow. Without an escape mechanism, users may need to restart the entire REPL session to exit an unwanted command, which is frustrating and inefficient. This is critical for user confidence and adoption.

**Independent Test**: Can be fully tested by launching the REPL, starting any interactive command (e.g., `/config set` prompting for a value, `/record` waiting for audio input), pressing the escape key sequence, and verifying the user returns to the main prompt with no partial state changes applied.

**Acceptance Scenarios**:

1. **Given** a user is in the REPL at the main prompt, **When** they start an interactive command that prompts for input (e.g., `/config set` asking for a key), **Then** they can press the escape key sequence to cancel and return to the main prompt without any configuration changes
2. **Given** a user is in a multi-step command flow (e.g., `/auth login` prompting for SSH key selection), **When** they press the escape key sequence at any prompt, **Then** they return to the main prompt and the command is cancelled
3. **Given** a user presses the escape key sequence at the main prompt (not in a command), **When** the escape is triggered, **Then** nothing happens (no-op) and they remain at the prompt
4. **Given** a user is viewing paginated output from a command (e.g., `/search` results), **When** they press the escape key sequence, **Then** they return to the main prompt, abandoning the paginated view
5. **Given** a user is in a SelectionPrompt (e.g., `/audio config` step 2 selecting STT provider), **When** they press Escape, **Then** the selection is cancelled, they see a cancellation message, and return to the REPL prompt
6. **Given** a user is in a TextPrompt entering a value (e.g., `/config set TenSecondTom:Audio:MaxDuration`), **When** they press Escape, **Then** the text input is cancelled, no value is saved, and they return to the REPL prompt

---

### User Story 2 - Interactive Command Autocomplete (Priority: P2)

Users need real-time command autocomplete that activates as they type, allowing them to press Tab to cycle through matching command suggestions and complete commands faster with fewer keystrokes.

**Why this priority**: Autocomplete significantly improves command entry speed and reduces typing errors. While basic autocomplete exists (showing suggestions after typing), interactive Tab completion is the expected modern CLI pattern that users expect from tools like git, docker, and other professional CLI applications.

**Independent Test**: Can be fully tested by launching the REPL, typing a partial command like `/rec`, pressing Tab, and verifying the command completes to `/record` (or cycles through matches if multiple exist). This can be tested independently of history navigation.

**Acceptance Scenarios**:

1. **Given** a user is at the main prompt, **When** they type a partial command starting with `/` (e.g., `/rec`), **Then** pressing Tab completes the command to the best match (e.g., `/record`) or cycles through matches if multiple exist
2. **Given** a user has typed `/co` at the prompt, **When** they press Tab multiple times, **Then** the system cycles through all matching commands (e.g., `/config` and any other commands matching the prefix or substring) in a logical order
3. **Given** a user presses Tab with no input or non-command input, **When** Tab is pressed, **Then** nothing happens (no-op) - Tab completion only activates for input starting with `/`
4. **Given** a user types a complete, valid command (e.g., `/help`), **When** they press Tab, **Then** nothing happens (command is already complete)

---

### User Story 3 - Command History Navigation (Priority: P2)

Users need to navigate through previously executed commands using Arrow Up/Down keys, allowing them to quickly re-execute or modify past commands without retyping them.

**Why this priority**: Command history navigation is a standard REPL feature that dramatically improves productivity for repetitive tasks. Users can quickly access their last commands, modify them slightly, and re-execute. This is essential for efficient CLI workflows.

**Independent Test**: Can be fully tested by launching the REPL, executing several commands (e.g., `/help`, `/config`, `/search test`), then pressing Arrow Up to navigate backward through history and Arrow Down to navigate forward, verifying the correct commands appear in the prompt.

**Acceptance Scenarios**:

1. **Given** a user has executed at least one command in the current REPL session, **When** they press Arrow Up at the main prompt, **Then** the last executed command appears in the prompt, ready to edit or execute
2. **Given** a user is navigating backward through history with Arrow Up, **When** they press Arrow Down, **Then** they move forward through history toward more recent commands
3. **Given** a user is at the oldest command in history, **When** they press Arrow Up again, **Then** they remain at the oldest command (no wrap-around)
4. **Given** a user is at the most recent command (or at the prompt with no history navigation), **When** they press Arrow Down, **Then** they return to an empty prompt (or remain at empty prompt)
5. **Given** a user navigates to a historical command with Arrow Up, **When** they edit the command and press Enter, **Then** the edited command executes and is added to history as a new entry
6. **Given** a user has executed more than 100 commands in a session, **When** they press Arrow Up repeatedly, **Then** they can navigate through the most recent 100 commands (older commands are not accessible due to circular buffer limit)

---

### Edge Cases

- What happens when a user presses escape during a long-running command (e.g., `/record` capturing audio)? The command should be interruptible via Ctrl+C (existing behavior), and escape should work for prompt-level interactions
- How does the system handle escape when a command is waiting for external input (e.g., file selection dialog)? Escape should cancel the prompt and return to main prompt
- What happens when autocomplete has no matches for the typed prefix? The system should indicate no matches (visual feedback) and not complete anything
- How does history navigation work when the user has typed partial input at the prompt? Arrow Up **replaces** the current input with the historical command. The partial input is **not preserved** when navigating away - this is the simpler approach and acceptable for MVP.
- What happens when history is empty (no commands executed yet)? Arrow Up should do nothing (no-op)
- How does autocomplete handle commands with subcommands (e.g., `/config set`)? **Out of scope for MVP**: Autocomplete only works for command names (e.g., `/config`), not subcommands. Future enhancement: After completing `/config`, Tab could suggest subcommands like `set`, `get`, etc.
- What happens when a user presses Tab multiple times rapidly? The system should cycle through suggestions without errors or UI glitches
- How does escape work in nested command contexts (e.g., within a multi-step configuration flow)? Escape should exit the entire nested flow, not just the current step
- What happens when a user types a character while cycling through autocomplete suggestions? The current suggestion is accepted into the buffer, the new character is appended, and autocomplete state resets (triggers new lookup)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide an escape key sequence (e.g., Escape key or Ctrl+[) that cancels any interactive command prompt and returns the user to the main REPL prompt
- **FR-002**: System MUST ensure the escape mechanism works consistently across all interactive commands that prompt for user input
- **FR-003**: System MUST ensure that when escape is pressed during a command, no partial state changes are applied (e.g., no configuration values are saved if escape is pressed during `/config set`)
- **FR-004**: System MUST provide interactive Tab completion for commands, allowing users to press Tab to cycle through matching command suggestions as they type
- **FR-005**: System MUST display autocomplete suggestions in real-time as users type partial commands (starting with `/`)
- **FR-006**: System MUST allow users to navigate command history using Arrow Up to go backward and Arrow Down to go forward through previously executed commands
- **FR-007**: System MUST populate the prompt with historical commands when navigating with Arrow keys, allowing users to edit and re-execute them
- **FR-008**: System MUST ensure history navigation works with the existing in-memory history storage (up to 100 commands per session)
- **FR-009**: System MUST ensure that when a user edits a historical command and executes it, the edited version is added as a new history entry (not replacing the original)
- **FR-010**: System MUST ensure escape, autocomplete, and history navigation do not interfere with each other or with existing Ctrl+C cancellation behavior
- **FR-011**: System MUST provide escape key cancellation for Spectre.Console interactive prompts (`SelectionPrompt<T>`, `TextPrompt<T>`, `ConfirmationPrompt`, `MultiSelectionPrompt<T>`) used in commands and wizards
- **FR-012**: System MUST persist command history to disk across REPL sessions (stored at `~/ten-second-tom/data/history.json`)

### Key Entities *(include if feature involves data)*

- **Command History Entry**: Represents a previously executed command with its execution context (command text, success status, timestamp). Already exists in the system via `CommandHistoryEntry` model. No changes needed to the data model.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can escape from any interactive command prompt and return to the main prompt within 100 milliseconds of pressing the escape key sequence
- **SC-002**: Users can complete a command using Tab autocomplete with 50% fewer keystrokes compared to typing the full command name
- **SC-003**: Users can navigate to any command in their session history (up to 100 commands) using Arrow Up/Down keys within 200 milliseconds per navigation step
- **SC-004**: 95% of users successfully use the escape mechanism to cancel unwanted commands without needing to restart the REPL session
- **SC-005**: Autocomplete correctly suggests matching commands for 100% of valid command prefixes typed by users
- **SC-006**: History navigation preserves command text accurately, allowing users to edit and re-execute historical commands without data loss or corruption

## Assumptions

- The escape key sequence will be the standard Escape key (ASCII 27) or Ctrl+[ (which sends the same code), following common CLI application patterns
- Autocomplete will use the existing `IAutocompleteEngine` service and `CommandMetadata` catalog, with custom `Console.ReadKey()` implementation for interactive Tab completion (Spectre.Console 0.51.1's `TextPrompt<T>` does not support Tab completion, and JKToolKit.Spectre.AutoCompletion is designed for `CommandApp`, not `TextPrompt<T>`)
- History navigation will integrate with the existing `ISessionManager.GetHistory()` method, which already maintains an in-memory circular buffer of up to 100 commands
- The implementation will use custom `Console.ReadKey()` for input handling while maintaining Spectre.Console styling for output display
- Escape, autocomplete, and history features will work seamlessly with existing command execution flow and cancellation (Ctrl+C) behavior
- Command history is persisted to `~/ten-second-tom/data/history.json` across REPL sessions (implemented via `IHistoryStore`)

## Dependencies

- Existing REPL infrastructure (`ReplLoop`, `ICommandRouter`, `ISessionManager`)
- Existing autocomplete engine (`IAutocompleteEngine`, `CommandAutoCompleteSource`)
- Existing command metadata catalog (`CommandMetadata.CommandCatalog`)
- Spectre.Console library (already in dependencies) for enhanced terminal output formatting
- .NET `Console.ReadKey()` API for custom input handling (no new NuGet package dependencies - uses built-in .NET APIs. Repository evidence confirms JKToolKit.Spectre.AutoCompletion incompatible with `TextPrompt<T>`)

## Out of Scope

- Autocomplete for command arguments or options (only command names are autocompleted)
- History search/filtering capabilities (only sequential navigation via Arrow keys)
- Customizable key bindings (escape, Tab, Arrow keys are fixed)
- History editing capabilities beyond basic text editing in the prompt (no advanced editing features)
- Advanced Unicode support (grapheme cluster handling) - MVP uses simple codepoint-based cursor movement; multi-codepoint characters like emoji (👨‍👩‍👧) may display incorrectly
- "Back" navigation in multi-step wizards (Escape exits wizard entirely, does not go back one step)
