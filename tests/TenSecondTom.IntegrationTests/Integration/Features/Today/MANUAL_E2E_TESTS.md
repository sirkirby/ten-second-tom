# Manual End-to-End Tests: /today Command with Interactive Editor

**Feature**: Interactive Console Text Editing for `/today` Command  
**Date**: 2025-10-14  
**Purpose**: Validate the complete `/today` workflow with the new interactive text editor

## Prerequisites

- Built application: `dotnet build --configuration Release`
- Configured API keys: `tom setup` or environment variables set
- Test terminal: macOS Terminal.app or Windows Terminal
- Network access for LLM API calls

---

## Test Environment

**Platform**: [ ] macOS Terminal.app | [ ] macOS iTerm2 | [ ] Windows Terminal | [ ] Windows cmd.exe

**Application Version**: _________

**Date/Time**: _______________

**Tester**: _______________

---

## Test Scenarios

### TC-E2E-001: Basic /today Flow with Single-Line Answers

**Objective**: Verify basic workflow with simple single-line responses

**Steps**:
1. Run `tom today` (or `./bin/Release/net9.0/tom today`)
2. For "What happened today?": Type "Worked on feature X"
3. Press Ctrl+D
4. In preview dialog, press 'S' to save
5. For "Anything interesting planned for tomorrow?": Type "Meeting at 2pm"
6. Press Ctrl+D, press 'S'
7. For "Unfinished tasks?": Type "Code review pending"
8. Press Ctrl+D, press 'S'
9. Wait for LLM processing
10. Verify entry is saved

**Expected Results**:
- [ ] Editor launches 3 times (one per question)
- [ ] Hint line visible at bottom: "Ctrl+D: Done | Ctrl+C: Cancel | Arrows/Home/End: Navigate"
- [ ] Preview shows entered content
- [ ] S key saves and proceeds to next question
- [ ] After 3rd save, LLM processes the responses
- [ ] Success message displayed
- [ ] Entry file created in `.memory/today/` directory
- [ ] Entry contains all 3 responses

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-002: Multi-Line Answers with Editing

**Objective**: Verify multi-line text entry and editing capabilities

**Steps**:
1. Run `tom today`
2. For "What happened today?": Type:
   ```
   - Fixed bug in authentication
   - Reviewed pull request #123
   - Updated documentation
   ```
3. Press Up Arrow to go back to line 2
4. Use Left/Right arrows to position cursor
5. Edit text: change "123" to "456"
6. Press Ctrl+D
7. Verify preview shows edited content
8. Press 'S' to save
9. Continue with remaining questions (can be simple text)
10. Complete workflow

**Expected Results**:
- [ ] Multi-line entry works correctly
- [ ] Arrow keys navigate between lines
- [ ] Edits are preserved
- [ ] Preview shows final edited version
- [ ] Content saved includes all lines
- [ ] Entry file contains the edited text

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-003: Edit More Option (E Key)

**Objective**: Verify "Edit More" functionality to return to editing

**Steps**:
1. Run `tom today`
2. Type multi-line content for first question
3. Press Ctrl+D to preview
4. Press 'E' (Edit More) instead of 'S'
5. Verify editor reopens with previous content intact
6. Add more text: "And one more thing..."
7. Press Ctrl+D again
8. Press 'S' to save
9. Complete remaining questions
10. Verify entry is created

**Expected Results**:
- [ ] E key returns to editing
- [ ] Previous content preserved
- [ ] Can add more text
- [ ] Final saved content includes both parts
- [ ] Entry file contains complete text

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-004: Cancel During Editing (Ctrl+C)

**Objective**: Verify immediate cancellation with Ctrl+C

**Steps**:
1. Run `tom today`
2. Start typing answer to first question
3. Press Ctrl+C (NOT Ctrl+D followed by C)
4. Verify editor exits immediately
5. Verify no entry is created

**Expected Results**:
- [ ] Ctrl+C exits immediately
- [ ] No confirmation dialog shown
- [ ] Command exits gracefully
- [ ] No entry file created
- [ ] Console shows cancellation message

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-005: Cancel at Preview (C Key)

**Objective**: Verify cancellation from preview dialog

**Steps**:
1. Run `tom today`
2. Type answer to first question
3. Press Ctrl+D
4. In preview dialog, press 'C' (Cancel)
5. Verify command exits
6. Verify no entry created

**Expected Results**:
- [ ] Ctrl+D shows preview
- [ ] C key cancels entire operation
- [ ] Command exits gracefully
- [ ] No entry file created

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-006: Empty Answer Handling

**Objective**: Verify validation for empty answers

**Steps**:
1. Run `tom today`
2. For first question: Don't type anything
3. Press Ctrl+D immediately
4. Try to save with 'S'
5. Observe error message
6. Type valid content
7. Save and proceed

**Expected Results**:
- [ ] Empty content triggers validation
- [ ] Error message: "Answer cannot be empty"
- [ ] Editor allows retry
- [ ] Valid content proceeds normally

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-007: Emoji and Unicode Content

**Objective**: Verify emoji and non-ASCII characters are preserved

**Steps**:
1. Run `tom today`
2. For first question, type: "Great day! 🎉 Shipped v2.0 🚀"
3. Press Ctrl+D, save
4. For second question, type: "Café meeting, résumé review, 日本語 test"
5. Complete workflow
6. Open saved entry file
7. Verify all special characters preserved

**Expected Results**:
- [ ] Emoji render correctly in editor
- [ ] Emoji visible in preview
- [ ] Accented characters display correctly
- [ ] Japanese characters display correctly
- [ ] Saved entry file contains exact Unicode content
- [ ] LLM response handles Unicode gracefully

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-008: Large Content (10+ Lines)

**Objective**: Verify handling of longer content and preview truncation

**Steps**:
1. Run `tom today`
2. For first question, type or paste 15 lines of text
3. Press Ctrl+D
4. Verify preview shows first 10 lines
5. Verify "... (5 more lines)" indicator
6. Press 'S' to save
7. Complete remaining questions
8. Verify full content saved (not truncated)

**Expected Results**:
- [ ] Editor handles 15 lines smoothly
- [ ] Preview shows 10 lines + indicator
- [ ] Full 15 lines saved to entry file
- [ ] LLM processes full content

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-009: Clipboard Paste

**Objective**: Verify clipboard paste functionality

**Steps**:
1. Copy multi-line text to clipboard (5-10 lines from external source)
2. Run `tom today`
3. Paste content with Ctrl+V (or terminal paste)
4. Verify all lines appear
5. Press Ctrl+D, verify preview shows pasted content
6. Save and complete workflow

**Expected Results**:
- [ ] Paste inserts all lines
- [ ] Blank lines preserved (if any)
- [ ] Content saved correctly
- [ ] No data loss during paste

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-010: Non-Interactive Mode (Piped Input)

**Objective**: Verify fallback to StreamBasedTextEditor for piped input

**Steps**:
1. Create test input file `input.txt`:
   ```
   Line 1: First response
   Line 2: More details
   
   Second response here
   
   Third response
   ```
2. Run: `echo -e "Line 1\nLine 2\n\n" | tom today --json`
3. Verify StreamBasedTextEditor is used
4. Verify entry created with piped content

**Expected Results**:
- [ ] StreamBasedTextEditor handles piped input
- [ ] Multi-line content preserved
- [ ] Ctrl+D (EOF) triggers completion
- [ ] Entry saved correctly
- [ ] JSON output provided

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-011: Error Recovery (LLM Failure)

**Objective**: Verify graceful handling when LLM API fails

**Steps**:
1. Temporarily set invalid API key: `export LLM__APIKEY="invalid"`
2. Run `tom today`
3. Complete all 3 questions with editor
4. Observe LLM failure
5. Verify partial entry saved

**Expected Results**:
- [ ] Editor workflow completes normally
- [ ] LLM call fails with clear error
- [ ] Partial entry saved with user input only
- [ ] Error message explains the issue
- [ ] User input not lost

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-012: Cross-Platform Compatibility

**Objective**: Verify editor works on Windows Terminal

**Platform**: Windows Terminal (required)

**Steps**:
1. Run `tom today` on Windows Terminal
2. Complete workflow with multi-line content
3. Test all keyboard shortcuts (Ctrl+D, Ctrl+C, arrows, Home, End)
4. Verify emoji and Unicode support
5. Save entry and verify file created

**Expected Results**:
- [ ] Editor launches correctly on Windows
- [ ] All keyboard shortcuts work
- [ ] Emoji/Unicode render correctly (or gracefully degrade)
- [ ] Entry saved successfully
- [ ] No platform-specific errors

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-013: Terminal Resize During Editing

**Objective**: Verify editor handles terminal resize gracefully

**Steps**:
1. Run `tom today`
2. Start typing multi-line content
3. Resize terminal window (drag corner or maximize/restore)
4. Verify display adapts
5. Continue editing
6. Complete workflow

**Expected Results**:
- [ ] Display adapts to new size
- [ ] No content lost
- [ ] Cursor position maintained (or reasonable fallback)
- [ ] Can continue editing normally
- [ ] Save completes successfully

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

### TC-E2E-014: Complete Workflow with Provider Override

**Objective**: Verify editor works with alternate LLM provider

**Steps**:
1. Run `tom today --provider Anthropic`
2. Complete all questions using editor
3. Verify entry created
4. Verify LLM response from Anthropic (check metadata)

**Expected Results**:
- [ ] Editor workflow identical regardless of provider
- [ ] Entry created successfully
- [ ] Entry metadata shows "Anthropic" as provider
- [ ] Response format correct

**Actual Results**: _________________________________________________

**Pass/Fail**: [ ] PASS | [ ] FAIL

---

## Summary

**Total Test Cases**: 14  
**Passed**: _____  
**Failed**: _____  
**Blocked**: _____  
**Not Tested**: _____

**Overall Status**: [ ] PASS | [ ] FAIL | [ ] PARTIAL

---

## Critical Issues Found

| TC# | Issue Description | Severity | Workaround | Notes |
|-----|-------------------|----------|------------|-------|
|     |                   |          |            |       |

---

## Performance Observations

**Editor Launch Time**: __________ ms (perceived)  
**Navigation Responsiveness**: [ ] Excellent | [ ] Good | [ ] Acceptable | [ ] Poor  
**Large Content (10+ lines)**: [ ] Smooth | [ ] Slight lag | [ ] Noticeable delay  
**Preview Display Time**: __________ ms

---

## Notes

_Additional observations, edge cases, or recommendations:_

---

**Sign-off**: _______________ (Tester Name)  
**Date**: _______________  
**Platform(s) Tested**: _______________

