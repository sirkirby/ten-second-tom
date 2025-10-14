# Manual Testing Checklist - Interactive Text Editor Feature

**Date**: 2025-10-14  
**Feature Branch**: `006-improved-text-editing`  
**Binary**: `bin/release-test/TenSecondTom` (22MB, macOS ARM64)  
**Test Environment**: macOS Terminal.app

---

## Prerequisites

✅ **Before testing, ensure**:
1. Release binary built: `bin/release-test/TenSecondTom`
2. App is configured (run `./bin/release-test/TenSecondTom setup` if needed)
3. Terminal.app is your active terminal (not Cursor or VS Code terminal)
4. Terminal size is reasonable (80+ cols, 24+ rows)

---

## Critical Test Scenarios

### ✅ Test 1: Basic Interactive Editor (TC-001)

**What**: Verify Terminal.Gui editor launches and basic navigation works

**Steps**:
```bash
cd /Users/chris/Repos/ten-second-tom
./bin/release-test/TenSecondTom today
```

**In the editor**:
1. Type: "Line 1"
2. Press Enter
3. Type: "Line 2"
4. Press Up Arrow → verify cursor moves to Line 1
5. Press Down Arrow → verify cursor back to Line 2
6. Press Ctrl+D → editor should save
7. Verify entry created

**Expected**:
- [ ] Terminal.Gui editor launches (full-screen mode)
- [ ] Hint line visible at bottom: "Ctrl+D: Save & Continue | Ctrl+C: Cancel | Arrows/Home/End: Navigate"
- [ ] Arrow navigation works
- [ ] Ctrl+D saves successfully
- [ ] Entry file created in `data/entries/`

---

### ✅ Test 2: Multi-line Paste with Blank Lines (TC-017)

**What**: Verify clipboard paste preserves formatting including blank lines

**Steps**:
1. Copy this text to clipboard:
```
This is paragraph one. It has some content here.

This is paragraph two after a blank line.

Paragraph three here.

Fourth paragraph.

Final fifth paragraph.
```

2. Run: `./bin/release-test/TenSecondTom today`
3. Paste content (Cmd+V)
4. Press Ctrl+D to save
5. Open the saved entry file and verify all 5 paragraphs with 4 blank lines preserved

**Expected**:
- [ ] All 5 paragraphs paste correctly
- [ ] All 4 blank lines between paragraphs preserved
- [ ] No formatting corruption
- [ ] Saved file matches pasted content exactly

---

### ✅ Test 3: Home/End Navigation (TC-002)

**What**: Verify Home/End keys work correctly

**Steps**:
1. Run: `./bin/release-test/TenSecondTom today`
2. Type: "This is a long line of text with many words"
3. Press Home → cursor should jump to start of line
4. Press End → cursor should jump to end of line
5. Press Left Arrow 5 times (cursor in middle)
6. Press Home → cursor at start again
7. Press Ctrl+C to cancel (don't save)

**Expected**:
- [ ] Home moves cursor to line start
- [ ] End moves cursor to line end
- [ ] Works from any cursor position

---

### ✅ Test 4: Ctrl+C Cancel (TC-007)

**What**: Verify Ctrl+C cancels immediately without saving

**Steps**:
1. Run: `./bin/release-test/TenSecondTom today`
2. Type: "Some test content"
3. Press Ctrl+C (NOT Ctrl+D)
4. Verify editor exits immediately
5. Verify NO new entry file was created

**Expected**:
- [ ] Ctrl+C cancels without preview
- [ ] Editor exits immediately
- [ ] No entry saved

---

### ✅ Test 5: Emoji and Unicode (TC-008 & TC-009)

**What**: Verify emoji and Unicode characters work correctly

**Steps**:
1. Run: `./bin/release-test/TenSecondTom today`
2. Type: "Hello 👋 World 🌍 with emoji 😊"
3. Press Enter
4. Type: "Café, naïve, résumé, 日本語"
5. Press Ctrl+D to save
6. Open saved file and verify all characters preserved

**Expected**:
- [ ] Emoji render correctly in editor
- [ ] Emoji preserved in saved file
- [ ] Accented characters (é, ï) work
- [ ] Unicode (Japanese) works

---

### ✅ Test 6: Large Paste (TC-014)

**What**: Verify large content (5,000+ chars) works without issues

**Steps**:
1. Create a large text file:
```bash
cd /Users/chris/Repos/ten-second-tom
python3 -c "print('Lorem ipsum dolor sit amet. ' * 500)" > test-large.txt
wc -c test-large.txt  # Should be ~10,000 characters
```

2. Copy content of `test-large.txt`
3. Run: `./bin/release-test/TenSecondTom today`
4. Paste content (Cmd+V)
5. Navigate with arrows (verify responsive)
6. Press Ctrl+D to save
7. Verify all content saved

**Expected**:
- [ ] Paste completes quickly (<200ms perceived)
- [ ] Arrow navigation still responsive
- [ ] All content saved without truncation
- [ ] No performance issues

**Cleanup**:
```bash
rm test-large.txt
```

---

### ✅ Test 7: Piped Input Fallback (T047)

**What**: Verify non-interactive terminal uses StreamBasedTextEditor fallback

**Steps**:
```bash
cd /Users/chris/Repos/ten-second-tom
echo -e "Piped input test.\nLine 2.\nLine 3." | ./bin/release-test/TenSecondTom today
```

**Expected**:
- [ ] Command completes without launching Terminal.Gui
- [ ] StreamBasedTextEditor used (check logs for "Using StreamBasedTextEditor")
- [ ] Entry created with piped content
- [ ] All 3 lines preserved

**Verify entry**:
```bash
# Find the most recent entry
ls -lt data/entries/ | head -5
# Read it to verify content
cat data/entries/YYYY-MM-DD.md  # Use actual date
```

---

### ✅ Test 8: ANSI Code Sanitization (TC/Security)

**What**: Verify ANSI escape sequences are stripped (security test)

**Steps**:
1. Run: `./bin/release-test/TenSecondTom today`
2. Paste this content with ANSI codes:
```
Normal text [31mRed text[0m more text [1mBold[0m
```
3. Press Ctrl+D to save
4. Open saved entry file
5. Verify ANSI codes (`[31m`, `[0m`, etc.) are NOT in the file

**Expected**:
- [ ] ANSI escape sequences stripped from input
- [ ] Only clean text saved
- [ ] No terminal corruption during editing

---

## Edge Cases

### ✅ Test 9: Empty Content

**Steps**:
1. Run: `./bin/release-test/TenSecondTom today`
2. Don't type anything
3. Press Ctrl+D
4. Verify entry created (even if empty)

**Expected**:
- [ ] Empty entry can be saved
- [ ] No error occurs

---

### ✅ Test 10: Terminal Resize

**Steps**:
1. Run: `./bin/release-test/TenSecondTom today`
2. Type several lines
3. Resize terminal window (drag corner)
4. Verify display adapts
5. Continue editing
6. Press Ctrl+D to save

**Expected**:
- [ ] Display adapts to new size
- [ ] No content lost
- [ ] Editing continues normally

---

## Test Results Summary

**Date Tested**: _______________  
**Tester**: _______________  
**Terminal**: macOS Terminal.app version: _______________

| Test # | Scenario | Status | Notes |
|--------|----------|--------|-------|
| 1 | Basic Interactive Editor | ☐ Pass ☐ Fail | |
| 2 | Multi-line Paste | ☐ Pass ☐ Fail | |
| 3 | Home/End Navigation | ☐ Pass ☐ Fail | |
| 4 | Ctrl+C Cancel | ☐ Pass ☐ Fail | |
| 5 | Emoji/Unicode | ☐ Pass ☐ Fail | |
| 6 | Large Paste | ☐ Pass ☐ Fail | |
| 7 | Piped Input | ☐ Pass ☐ Fail | |
| 8 | ANSI Sanitization | ☐ Pass ☐ Fail | |
| 9 | Empty Content | ☐ Pass ☐ Fail | |
| 10 | Terminal Resize | ☐ Pass ☐ Fail | |

**Overall Result**: ☐ ALL PASS ☐ SOME FAILURES

---

## Issues Found

| Test # | Issue Description | Severity | Reproducible? |
|--------|-------------------|----------|---------------|
| | | | |

---

## Additional Notes

_Any other observations, platform-specific behaviors, or comments:_

---

## Sign-off

**Tested By**: _______________  
**Date**: _______________  
**Binary Version**: 1.0.0+6c8deec214e830b7b02a095dcddaecd1442d19b3  
**Approved for Release**: ☐ Yes ☐ No (see issues)

---

## Quick Test Commands

```bash
# Setup (if needed)
./bin/release-test/TenSecondTom setup

# Test interactive editor
./bin/release-test/TenSecondTom today

# Test piped input
echo -e "Test line 1\nTest line 2" | ./bin/release-test/TenSecondTom today

# View recent entries
ls -lt data/entries/ | head -5

# Read an entry
cat data/entries/$(date +%Y-%m-%d).md

# Check binary size
ls -lh bin/release-test/TenSecondTom

# Verify version
./bin/release-test/TenSecondTom --version
```

---

**Note**: For detailed test case descriptions, see `tests/TenSecondTom.IntegrationTests/Integration/Shared/TextEditing/MANUAL_TESTS.md` (18 comprehensive test cases).

