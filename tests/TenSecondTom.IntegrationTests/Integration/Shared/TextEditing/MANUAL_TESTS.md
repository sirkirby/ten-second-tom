# Manual Test Checklist: TerminalGuiTextEditor

**Feature**: Interactive Console Text Editing Experience  
**Component**: TerminalGuiTextEditor  
**Date**: 2025-10-14

## Purpose

Terminal.Gui requires an interactive terminal session and cannot be effectively unit tested. These manual tests validate the full editing experience across supported platforms.

## Prerequisites

- Built application: `dotnet build`
- Test command available: `dotnet run -- /today` (or similar test harness)
- Terminal with Unicode/emoji support

---

## Test Environment

**Platform**: [ ] macOS Terminal.app | [ ] macOS iTerm2 | [ ] Windows Terminal | [ ] Windows cmd.exe

**Terminal Size**: _____ cols x _____ rows

**Date/Time**: _______________

**Tester**: _______________

---

## Test Cases

### TC-001: Basic Text Entry and Navigation

**Steps**:
1. Run `dotnet run -- /today`
2. Type: "Line 1"
3. Press Enter
4. Type: "Line 2"
5. Press Up Arrow
6. Verify cursor moves to Line 1
7. Press Down Arrow
8. Verify cursor moves back to Line 2
9. Press Ctrl+D to trigger preview

**Expected Results**:
- [ ] Text appears correctly
- [ ] Enter creates new line
- [ ] Up arrow moves cursor to previous line
- [ ] Down arrow moves cursor to next line
- [ ] Ctrl+D shows preview dialog

**Actual Results**: _________________________________________________

---

### TC-002: Home/End Key Navigation

**Steps**:
1. Run editor
2. Type: "This is a long line of text"
3. Press Home
4. Verify cursor moves to start of line
5. Press End
6. Verify cursor moves to end of line
7. Press Left Arrow 5 times (cursor in middle)
8. Press Home
9. Verify cursor at start

**Expected Results**:
- [ ] Home moves cursor to line start
- [ ] End moves cursor to line end
- [ ] Works consistently from any position

**Actual Results**: _________________________________________________

---

### TC-003: Backspace and Delete

**Steps**:
1. Run editor
2. Type: "Hello World"
3. Position cursor between "Hello" and "World" (after space)
4. Press Backspace
5. Verify space is deleted
6. Press Delete
7. Verify 'W' is deleted

**Expected Results**:
- [ ] Backspace deletes character before cursor
- [ ] Delete removes character at cursor
- [ ] Text reflows correctly

**Actual Results**: _________________________________________________

---

### TC-004: Ctrl+D Preview and Save (S)

**Steps**:
1. Run editor
2. Type multi-line content (3 lines)
3. Press Ctrl+D
4. Verify preview dialog appears
5. Verify content is shown
6. Press 'S' (save)
7. Verify editor exits
8. Verify entry is saved

**Expected Results**:
- [ ] Ctrl+D triggers preview
- [ ] All 3 lines visible in preview
- [ ] S key saves and exits
- [ ] Content persisted correctly

**Actual Results**: _________________________________________________

---

### TC-005: Preview and Edit More (E)

**Steps**:
1. Run editor
2. Type: "First draft"
3. Press Ctrl+D
4. Press 'E' (edit more)
5. Verify editor reopens with content intact
6. Type: " with edits"
7. Press Ctrl+D
8. Press 'S'

**Expected Results**:
- [ ] E key returns to editing
- [ ] Previous content preserved
- [ ] Can continue editing
- [ ] Final content includes both parts

**Actual Results**: _________________________________________________

---

### TC-006: Preview and Cancel (C)

**Steps**:
1. Run editor
2. Type: "Some content"
3. Press Ctrl+D
4. Press 'C' (cancel)
5. Verify editor exits
6. Verify no entry created

**Expected Results**:
- [ ] C key cancels session
- [ ] Editor exits without saving
- [ ] No entry file created

**Actual Results**: _________________________________________________

---

### TC-007: Ctrl+C Immediate Cancel

**Steps**:
1. Run editor
2. Type: "Some content"
3. Press Ctrl+C (without preview)
4. Verify editor exits immediately

**Expected Results**:
- [ ] Ctrl+C cancels without preview
- [ ] No confirmation dialog
- [ ] Editor exits immediately
- [ ] No entry saved

**Actual Results**: _________________________________________________

---

### TC-008: Emoji Input and Display

**Steps**:
1. Run editor
2. Type: "Hello 👋 World 🌍"
3. Press Ctrl+D
4. Verify emoji visible in preview
5. Press S to save
6. Verify saved entry contains emoji

**Expected Results**:
- [ ] Emoji render correctly during editing
- [ ] Emoji visible in preview
- [ ] Emoji preserved in saved content

**Actual Results**: _________________________________________________

---

### TC-009: Non-Latin Characters (Accents, Unicode)

**Steps**:
1. Run editor
2. Type: "Café, naïve, résumé, 日本語, العربية"
3. Press Ctrl+D
4. Verify all characters visible
5. Press S to save

**Expected Results**:
- [ ] Accented characters (é, ï) display correctly
- [ ] Unicode characters (Japanese, Arabic) display correctly
- [ ] All preserved in saved content

**Actual Results**: _________________________________________________

---

### TC-010: Multi-line Clipboard Paste

**Steps**:
1. Copy multi-line text to clipboard (e.g., 5 lines from external source)
2. Run editor
3. Paste content (Ctrl+V or terminal paste)
4. Verify all lines appear
5. Verify blank lines preserved (if any in source)
6. Press Ctrl+D and save

**Expected Results**:
- [ ] Paste inserts all lines
- [ ] Blank lines preserved
- [ ] Formatting intact
- [ ] Content saved correctly

**Actual Results**: _________________________________________________

---

### TC-011: Preview with >10 Lines

**Steps**:
1. Run editor
2. Type or paste 15 lines of content
3. Press Ctrl+D
4. Verify preview shows first 10 lines
5. Verify "... (5 more lines)" indicator appears
6. Press S to save

**Expected Results**:
- [ ] Preview shows 10 lines
- [ ] Indicator shows remaining line count
- [ ] Full content saved (not truncated)

**Actual Results**: _________________________________________________

---

### TC-012: Hint Line Display

**Steps**:
1. Run editor
2. Verify hint line visible at bottom
3. Verify text: "Ctrl+D: Done | Ctrl+C: Cancel | Arrows/Home/End: Navigate"

**Expected Results**:
- [ ] Hint line always visible
- [ ] Text readable
- [ ] Doesn't overlap content

**Actual Results**: _________________________________________________

---

### TC-013: Empty Content Save

**Steps**:
1. Run editor
2. Do NOT type anything
3. Press Ctrl+D
4. Verify preview shows empty
5. Press S to save

**Expected Results**:
- [ ] Preview shows empty content
- [ ] Can save empty entry
- [ ] No error occurs

**Actual Results**: _________________________________________________

---

### TC-014: Large Content (10,000 characters)

**Steps**:
1. Prepare 10,000 character text file
2. Run editor
3. Paste content
4. Navigate with arrows (test responsiveness)
5. Press Ctrl+D
6. Verify preview loads quickly (<200ms perceived)
7. Press S to save

**Expected Results**:
- [ ] Paste completes quickly (<200ms)
- [ ] Arrow navigation responsive (<100ms)
- [ ] Preview loads quickly
- [ ] All content saved

**Actual Results**: _________________________________________________

---

### TC-015: Terminal Resize During Editing

**Steps**:
1. Run editor
2. Type several lines
3. Resize terminal window (drag corner)
4. Verify display adapts
5. Verify content not lost
6. Continue editing
7. Press Ctrl+D and save

**Expected Results**:
- [ ] Display adapts to new size
- [ ] No content lost
- [ ] Editing continues normally
- [ ] Saved content intact

**Actual Results**: _________________________________________________

---

### TC-016: Ctrl+Enter Alternative to Ctrl+D

**Steps**:
1. Run editor
2. Type: "Test content"
3. Press Ctrl+Enter
4. Verify preview dialog appears
5. Press S to save

**Expected Results**:
- [ ] Ctrl+Enter triggers preview (same as Ctrl+D)
- [ ] Dialog and save work identically

**Actual Results**: _________________________________________________

---

### TC-017: Five-Paragraph Paste with Blank Lines (T036 - User Story 2)

**Purpose**: Verify that pasting multi-paragraph content with blank line separators preserves all formatting.

**Steps**:
1. Copy the following 5-paragraph text to clipboard:

```
This is the first paragraph. It contains multiple sentences to make it substantial. Lorem ipsum dolor sit amet, consectetur adipiscing elit.

This is the second paragraph, separated by a blank line above. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.

The third paragraph comes after another blank line. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.

Paragraph four is here. This one also has interesting content that spans multiple lines and includes various characters: @#$%^&*().

Finally, the fifth paragraph concludes our test content. Notice how each paragraph is clearly separated by blank lines to maintain readability.
```

2. Run editor
3. Paste the content (Ctrl+V or terminal paste)
4. Navigate with arrows to verify all paragraphs visible
5. Press Home key - verify cursor at start of current line
6. Press End key - verify cursor at end of current line
7. Navigate to blank lines between paragraphs
8. Verify blank lines are preserved (no collapsing)
9. Press Ctrl+D to save
10. Open saved entry file and verify all 5 paragraphs and 4 blank lines preserved

**Expected Results**:
- [ ] All 5 paragraphs paste successfully
- [ ] All 4 blank lines between paragraphs preserved
- [ ] Home/End keys work on multi-line content
- [ ] No formatting corruption or line collapsing
- [ ] Saved content matches pasted content exactly

**Actual Results**: _________________________________________________

---

### TC-018: Edit Pre-filled Content (T041 - User Story 3)

**Purpose**: Verify that the editor can be invoked with pre-filled content and allows editing, demonstrating reusability for future `/search` edit feature.

**Steps**:
1. Create a test file with existing content: `echo -e "Line 1: Original content\nLine 2: More original text\nLine 3: Final line" > test-entry.txt`
2. Run editor with pre-filled content simulation:
   - Manually modify `TodayCommandHandler` temporarily to pre-fill content, OR
   - Create a quick test harness that calls `IInteractiveTextEditor.EditAsync("Existing content here")`
3. Verify editor opens with content already populated
4. Navigate to Line 2 using arrows
5. Modify Line 2 text (e.g., change "More" to "Updated")
6. Navigate to end and add a new Line 4
7. Press Ctrl+D to save
8. Verify saved content includes:
   - Original Line 1 (unchanged)
   - Modified Line 2 (changed)
   - Original Line 3 (unchanged)
   - New Line 4 (added)

**Expected Results**:
- [ ] Editor opens with pre-filled content visible
- [ ] Can navigate through existing content with arrows
- [ ] Can modify existing lines
- [ ] Can add new lines
- [ ] Ctrl+D saves all changes (original + modifications)
- [ ] No loss of existing content
- [ ] `EditorResult.Metadata.WasModified` is true after changes

**Alternative Test (Simpler)**:
1. Run `dotnet run -- /today`
2. Type some content and save
3. Manually open the saved file in `data/entries/`
4. Copy the file path
5. Modify the `TodayCommandHandler` temporarily to call:
   ```csharp
   var existingContent = await File.ReadAllTextAsync(copiedFilePath);
   var result = await _editor.EditAsync(initialContent: existingContent, ...);
   ```
6. Run `/today` again
7. Verify content is pre-filled
8. Make edits and save
9. Verify modifications were saved

**Actual Results**: _________________________________________________

**Notes**: This test demonstrates the editor's reusability for future features like editing entries from `/search` results. The `initialContent` parameter works correctly in both `TerminalGuiTextEditor` and `StreamBasedTextEditor`.

---

## Summary

**Total Test Cases**: 18  
**Passed**: _____  
**Failed**: _____  
**Blocked**: _____  

**Overall Status**: [ ] PASS | [ ] FAIL

---

## Issues Found

| TC# | Issue Description | Severity | Notes |
|-----|-------------------|----------|-------|
|     |                   |          |       |

---

## Notes

_Additional observations, edge cases, or platform-specific behaviors:_

---

**Sign-off**: _______________ (Tester Name)  
**Date**: _______________

