# Feature Specification: Interactive Console Text Editing Experience (Reusable Editor Foundation)

**Feature Branch**: `006-improved-text-editing`  
**Created**: 2025-10-13  
**Status**: Draft  
**Input**: User description: "We need a new feature to significantly improve the text entry and editing experience for ten second tom. Currently, when using /today, which prompts the users with questions and they input their response, the input is very basic. The user cannot move their cursor if they make a typo, they can only delete, this is not a good experience. Now if we can't utilize Spectre.Console to improve then, then we need to consider an alternative way to offer a better text editing experience within the console. Research other console apps do this in .NET and ensure that the solution is cross platform compatible as we publish on Mac and Windows. In addition to the input experience on /today, we should also allow the user to view and edit a previous entry that was returned in /search, but instead of building that functionality in this spec, let's just lay the groundwork so that when we go to implement such a feature, that we are using the same underlying functionality we are using here for the input."

## Clarifications

### Session 2025-10-14

- Q: How should the editor handle potentially problematic input like control characters, ANSI escape sequences, or excessively nested formatting that users might paste from external sources? → A: Strip ANSI sequences only: Allow printable chars, newlines, tabs; strip ANSI escape sequences and other terminal control codes
- Q: What is the exact interaction pattern for the confirmation step before saving? → A: Preview + prompt: Show preview of content (first 10 lines if >10, otherwise full), then prompt "Save this entry? [S]ave, [E]dit more, [C]ancel" with single-key selection
- Q: What is the line wrapping strategy during editing and storage? → A: Hard wraps preserved: User's Enter keypresses create actual line breaks; visual display respects these breaks without adding additional wrapping
- Q: How should users trigger input completion, given that blank lines may be intentional paragraph separators? → A: Explicit finish gesture: Use Ctrl+D or Ctrl+Enter to trigger confirmation; blank lines are always preserved as content
- Q: Which terminal editing library should be used for implementation? → A: Preference is Spectre.Console if it supports required features; however, research during planning is expected to reveal Terminal.Gui subset is the most viable option. Final decision deferred to research phase to ensure proper evaluation of capabilities against all requirements.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Edit-as-you-type for /today (Priority: P1)

When answering prompts in `/today`, I can comfortably edit my response as I type: move the cursor, insert text, delete characters at the cursor, add new lines, and complete the response without losing work.

**Why this priority**: This is the primary daily workflow. Reducing friction here directly improves completion rate and user satisfaction.

**Independent Test**: Can be fully tested by running `/today`, answering one question, editing mid-line and across lines, and successfully submitting the response.

**Acceptance Scenarios**:

1. **Given** the `/today` prompt is waiting for input, **When** the user types "Thsi" then presses Left Arrow once, presses Delete once, and types "is", **Then** the input reads "This".
2. **Given** the `/today` prompt, **When** the user types multiple lines using Enter, moves Up/Down to navigate lines, and continues editing, **Then** the cursor reflects movement and edits apply at the cursor location.
3. **Given** the `/today` prompt with multi-line content including blank lines, **When** the blank lines remain in the content, **Then** they are preserved as paragraph separators and do not trigger submission.
4. **Given** the `/today` prompt, **When** the user presses Ctrl+D (or Ctrl+Enter), **Then** the system displays a preview (first 10 lines if >10, otherwise full) with prompt "Save this entry? [S]ave, [E]dit more, [C]ancel".
5. **Given** the confirmation prompt is shown, **When** the user presses "S", **Then** the entry is saved; **When** the user presses "E", **Then** editing mode resumes with content intact; **When** the user presses "C", **Then** the session cancels without saving.
6. **Given** the `/today` prompt, **When** the user presses Ctrl+C at any time, **Then** the session cancels with a clear message and no partial data is saved.

---

### User Story 2 - Multi-line comfort and paste support (Priority: P2)

As a user, I can write multi-line responses comfortably: each Enter keypress creates a new line, navigation across lines is intuitive, and I can paste content from my clipboard without breaking formatting.

**Why this priority**: Many users compose multi-paragraph reflections; smooth multi-line editing reduces frustration.

**Independent Test**: Paste a multi-paragraph response, verify line integrity, navigate with arrows/Home/End, submit successfully.

**Acceptance Scenarios**:

1. **Given** the input area is active, **When** the user pastes multi-line text, **Then** all line breaks are preserved and visible.
2. **Given** multi-line content, **When** the user presses Home/End, **Then** the cursor moves to the start/end of the current line; with repeated usage across lines it behaves consistently.
3. **Given** multi-line content, **When** the user navigates Up/Down at start/end of line, **Then** the cursor moves to the previous/next line maintaining column when possible.
4. **Given** multi-paragraph content with blank lines between paragraphs, **When** the user presses Ctrl+D to finish, **Then** all blank lines are preserved in the saved content.

---

### User Story 3 - Reusable editor for future entry edits (Priority: P3)

As a user, when I later choose to edit a previous entry (e.g., from `/search` results), I get the exact same editing experience with the entry pre-filled, and I can save or cancel changes.

**Why this priority**: Establishing a single, reusable editing experience minimizes cognitive load and future implementation cost.

**Independent Test**: Invoke the editor with pre-filled content (simulating an entry), confirm that editing, submission, and cancel behavior match `/today`.

**Acceptance Scenarios**:

1. **Given** an existing entry body, **When** the editing experience opens pre-filled, **Then** the user can edit with the same keys and behaviors as in `/today`.
2. **Given** an existing entry is being edited, **When** the user chooses to save, **Then** the edited content is returned as the new body; **When** the user cancels, **Then** no changes are returned.

---

### Edge Cases

- Terminal is non-interactive (e.g., when input is piped): system must fall back to basic single-line input and clearly inform the user that advanced editing is unavailable.
- Very long responses (e.g., >10,000 characters): editor remains responsive and supports scrolling/navigation without data loss.
- Multi-byte text (characters like emoji and accented letters): cursor movement and deletion operate on complete characters.
- Window resize during editing: display adapts to new terminal width without losing content; line breaks remain unchanged since only hard wraps (user Enter presses) are used.
- Paste of large content: paste does not hang or truncate; content remains intact.
- Pasted content containing ANSI escape sequences or terminal control codes: sequences are stripped automatically to prevent terminal injection attacks while preserving legitimate text content.
- Unexpected termination (e.g., Ctrl+C or closing the terminal): session exits gracefully with a clear message and no partial save.
- Restricted environments where certain key codes are not delivered: provide on-screen hints for alternative navigation and a working finish/cancel path.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Provide interactive text input for `/today` that supports cursor movement (Left/Right, Up/Down), Home/End, insertion at cursor, Backspace (left delete), and Delete (at cursor).
- **FR-002**: Support multi-line input where each Enter keypress creates an explicit line break (hard wrap); navigation across lines is intuitive and all line breaks are preserved exactly as entered on submit.
- **FR-003**: Allow users to finish input by pressing Ctrl+D or Ctrl+Enter (explicit finish gesture), then display a preview (first 10 lines if content exceeds 10 lines, otherwise full content) with the prompt "Save this entry? [S]ave, [E]dit more, [C]ancel" accepting single-key responses (S/E/C). Blank lines in content are always preserved as paragraph separators and never trigger completion.
- **FR-004**: Allow users to cancel input at any time (e.g., Ctrl+C) with a clear message and without saving partial responses.
- **FR-005**: Support pasting content from the system clipboard, preserving newlines and characters.
- **FR-006**: Display a concise, always-visible hint line summarizing key actions (e.g., "Arrows: navigate | Enter: new line | Ctrl+D/Ctrl+Enter: finish | Ctrl+C: cancel").
- **FR-007**: Expose the same editing experience as a reusable component that can be invoked with pre-filled text and returns edited text or a cancel outcome.
- **FR-008**: Provide a non-interactive fallback: when advanced editing is unavailable (e.g., piped input detected via `Console.IsInputRedirected`), use line-by-line input via `Console.ReadLine()` loop with a clear message about the limited capabilities (no cursor navigation, Ctrl+D sends EOF to finish instead of preview), and allow submission with Enter for each line.
- **FR-009**: Ensure the editing experience functions consistently on macOS and Windows default terminals.
- **FR-010**: Preserve all user-entered characters, including emoji and non-Latin scripts, without corruption.
- **FR-011**: *(Removed - duplicates FR-003 preview/confirmation specification)*
- **FR-012**: Respect reasonable performance constraints: inputs up to 10,000 characters must remain responsive with cursor operations completing in <100ms (perceived as instant).
- **FR-013**: Sanitize user input by stripping ANSI escape sequences and terminal control codes while preserving printable characters, newlines, and tabs to prevent terminal injection attacks and display corruption.
- **FR-014**: Preserve all blank lines (consecutive newlines without content between them) as intentional paragraph separators; only the explicit Ctrl+D or Ctrl+Enter gesture triggers completion.

### Key Entities *(include if feature involves data)*

- **EntryContent**: The textual body a user provides for a given day or existing entry; attributes include body text and metadata like length.
- **TextEditingSession**: A user interaction session that collects or edits `EntryContent`, with outcomes: Submitted (edited text) or Cancelled.

### Assumptions & Dependencies

- Users operate in interactive terminals that generally support arrow keys and common navigation keys; when unavailable, a basic fallback remains usable.
- The same editing experience will be used for future edit flows (e.g., editing a `/search` result) by invoking the reusable component with pre-filled content.
- Pasted multi-line content should retain original line breaks and characters without transformation.
- Cross-platform behavior targets mainstream default terminals on macOS and Windows.
- **Technology research required**: Planning phase must evaluate Spectre.Console (preferred) and Terminal.Gui (expected best fit) against all functional requirements, with particular attention to multi-line editing, Ctrl+D/Ctrl+Enter support, cross-platform consistency, and reusability. Research must document capabilities, limitations, and trade-offs before final library selection.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At least 90% of users can correct a mid-word typo and submit a `/today` response in under 30 seconds.
- **SC-002**: 95% of paste operations up to 5,000 characters complete instantly (perceived under 0.2 seconds) with formatting preserved.
- **SC-003**: Task completion rate for `/today` increases by 25% relative to baseline within one release cycle.
- **SC-004**: Reported support issues related to input/editing drop by 50% over two release cycles.
- **SC-005**: The same editing experience is used for both new and existing entries (verified by manual acceptance tests covering both flows on macOS and Windows).