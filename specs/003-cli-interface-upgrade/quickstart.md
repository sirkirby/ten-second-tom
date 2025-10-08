# Quickstart: Persistent CLI Session Experience

**Feature**: 003-cli-interface-upgrade  
**Date**: 2025-10-08  
**Purpose**: End-to-end validation scenarios for manual testing

## Prerequisites

- Ten Second Tom built and available in PATH or local bin/
- SSH authentication configured (for memory commands)
- LLM provider configured (Claude or OpenAI)
- Terminal with color support (for rich output)

## Scenario 1: Launch Shell and Execute Single Command

**Objective**: Verify basic shell launch and command execution

**Steps**:
1. Open terminal
2. Run: `tom` (no arguments)
3. Observe shell banner with logo
4. Type: `/today` (press Enter)
5. Wait for AI-generated reflection prompts and response
6. Observe prompt returns: `tom> `
7. Type: `/quit` (press Enter)

**Expected Results**:
- ✅ Shell launches with branded banner
- ✅ `/today` command executes successfully
- ✅ Output displays reflection prompts and summary
- ✅ Prompt returns immediately after output
- ✅ `/quit` exits cleanly with exit code 0

**Failure Indicators**:
- ❌ Shell exits after `/today` completes (single-exec behavior)
- ❌ Prompt does not return after command
- ❌ Error message displayed instead of output
- ❌ Process hangs or does not exit on `/quit`

---

## Scenario 2: Execute Multiple Commands in Sequence

**Objective**: Verify persistent session maintains state across commands

**Steps**:
1. Launch shell: `tom`
2. Type: `/today` (press Enter, wait for completion)
3. Type: `/thisweek` (press Enter, wait for completion)
4. Type: `/search "test"` (press Enter, wait for completion)
5. Type: `/quit` (press Enter)

**Expected Results**:
- ✅ All three commands execute successfully
- ✅ Prompt returns after each command
- ✅ No re-authentication required between commands
- ✅ Session maintains context (authentication, configuration)
- ✅ Clean exit after `/quit`

**Failure Indicators**:
- ❌ Second or third command fails with auth error
- ❌ Session exits after first command
- ❌ Prompt does not return between commands
- ❌ Commands execute in wrong order or overlap

---

## Scenario 3: Autocomplete Command with Tab

**Objective**: Verify autocomplete functionality

**Steps**:
1. Launch shell: `tom`
2. Type: `/thi` (do not press Enter)
3. Press Tab key
4. Observe autocomplete suggestion appears
5. Press Tab again to cycle or Enter to accept
6. Verify command completes to `/thisweek`
7. Press Enter to execute (or Ctrl+C to cancel)
8. Type: `/quit`

**Expected Results**:
- ✅ Typing `/thi` + Tab shows `/thisweek` suggestion
- ✅ Suggestion includes help text: "Generate a weekly review..."
- ✅ Accepting suggestion completes the command
- ✅ Command executes if Enter is pressed
- ✅ Multiple Tab presses cycle through matches (if multiple)

**Failure Indicators**:
- ❌ Tab key does not trigger autocomplete
- ❌ Suggestion does not appear or is incorrect
- ❌ Accepting suggestion does not complete command
- ❌ Autocomplete shows commands that don't match prefix

---

## Scenario 4: Command History Navigation with Arrow Keys

**Objective**: Verify command history recall

**Steps**:
1. Launch shell: `tom`
2. Type: `/today` (press Enter, wait)
3. Type: `/thisweek` (press Enter, wait)
4. Type: `/search "memory"` (press Enter, wait)
5. Press Arrow Up key once
6. Observe: `/search "memory"` appears in prompt
7. Press Arrow Up key again
8. Observe: `/thisweek` appears in prompt
9. Press Arrow Down key once
10. Observe: `/search "memory"` reappears
11. Type: `/quit`

**Expected Results**:
- ✅ Arrow Up navigates backward through history
- ✅ Arrow Down navigates forward through history
- ✅ Commands appear in reverse chronological order
- ✅ History includes exactly the commands executed
- ✅ At history start, Arrow Up does nothing
- ✅ At history end, Arrow Down clears prompt

**Failure Indicators**:
- ❌ Arrow keys do not navigate history
- ❌ Wrong commands appear in history
- ❌ History order is incorrect
- ❌ History includes commands from previous sessions

---

## Scenario 5: Interrupt Long-Running Command with Ctrl+C

**Objective**: Verify graceful command cancellation

**Steps**:
1. Launch shell: `tom`
2. Type: `/thisweek` (press Enter)
3. While command is executing (LLM generating summary), press Ctrl+C
4. Observe output: "(interrupted)" or similar message
5. Observe prompt returns immediately
6. Type: `/today` to verify session is still functional
7. Wait for `/today` to complete
8. Type: `/quit`

**Expected Results**:
- ✅ Ctrl+C cancels the running command
- ✅ Interruption message displayed
- ✅ Prompt returns immediately (no hang)
- ✅ Partial results displayed if available (optional)
- ✅ Subsequent commands execute normally
- ✅ Session remains stable after interruption

**Failure Indicators**:
- ❌ Ctrl+C exits entire shell (should cancel command only)
- ❌ Command continues executing after Ctrl+C
- ❌ Prompt does not return or hangs
- ❌ Session becomes unstable (next command fails)
- ❌ No interruption feedback displayed

---

## Scenario 6: Display Help Command

**Objective**: Verify `/help` displays available commands

**Steps**:
1. Launch shell: `tom`
2. Type: `/help` (press Enter)
3. Observe output: list of available commands with descriptions
4. Verify all commands are listed:
   - `/today`
   - `/thisweek`
   - `/search`
   - `/login`
   - `/logout`
   - `/quit` (and `/exit` alias)
   - `/help`
5. Type: `/quit`

**Expected Results**:
- ✅ `/help` displays formatted command list
- ✅ Each command shows name and description
- ✅ Aliases noted (e.g., `/exit` is alias for `/quit`)
- ✅ Commands grouped or sorted logically
- ✅ Output fits within terminal viewport (or paginated)

**Failure Indicators**:
- ❌ `/help` returns error or "unknown command"
- ❌ Command list is incomplete or incorrect
- ❌ Descriptions are missing or truncated
- ❌ Formatting is broken (unreadable)

---

## Scenario 7: Error Handling - Unknown Command

**Objective**: Verify inline error display for invalid commands

**Steps**:
1. Launch shell: `tom`
2. Type: `/unknown` (press Enter)
3. Observe error message: "Unknown command: /unknown. Type /help for available commands."
4. Observe prompt returns immediately
5. Type: `/today` to verify session recovery
6. Wait for `/today` to complete successfully
7. Type: `/quit`

**Expected Results**:
- ✅ Unknown command displays clear error message
- ✅ Error includes helpful hint (e.g., "Type /help")
- ✅ Prompt returns immediately (no exit)
- ✅ Subsequent commands work normally
- ✅ Session remains stable after error

**Failure Indicators**:
- ❌ Shell exits on unknown command
- ❌ Error message is cryptic or unhelpful
- ❌ Prompt does not return
- ❌ Next command fails due to state corruption

---

## Scenario 8: Backward Compatibility - Single-Execution Mode

**Objective**: Verify existing script behavior unchanged

**Steps**:
1. Run: `tom today` (command as argument, not shell mode)
2. Observe `/today` executes
3. Observe process exits after completion (does not enter shell)
4. Run: `tom search "test"` (another single command)
5. Observe `/search` executes and exits
6. Run: `echo $?` to check exit code

**Expected Results**:
- ✅ `tom today` executes command and exits
- ✅ No shell prompt appears
- ✅ Exit code is 0 (success) or non-zero (error)
- ✅ Behavior identical to pre-feature Ten Second Tom
- ✅ Scripts using `tom <command>` continue to work

**Failure Indicators**:
- ❌ `tom today` enters shell mode instead of executing
- ❌ Command fails where it succeeded before
- ❌ Exit code is incorrect
- ❌ Scripts break due to behavior change

---

## Scenario 9: Concurrent Sessions

**Objective**: Verify multiple shell instances can run simultaneously

**Steps**:
1. Open two terminal windows side-by-side
2. In Terminal 1: Run `tom` (shell mode)
3. In Terminal 2: Run `tom` (shell mode)
4. In Terminal 1: Type `/today` (press Enter)
5. In Terminal 2: Type `/thisweek` (press Enter)
6. Observe both commands execute independently
7. In Terminal 1: Type `/quit`
8. Observe Terminal 2 remains active
9. In Terminal 2: Type `/quit`

**Expected Results**:
- ✅ Both sessions launch successfully
- ✅ Commands execute independently (no interference)
- ✅ Session 1 exit does not affect Session 2
- ✅ Each session has isolated command history
- ✅ No file locking errors or contention warnings

**Failure Indicators**:
- ❌ Second session fails to launch (lock error)
- ❌ Commands in one session affect the other
- ❌ Quitting one session terminates the other
- ❌ Shared command history between sessions

---

## Scenario 10: JSON Output Mode in Shell

**Objective**: Verify `--output-json` flag works in shell mode

**Steps**:
1. Run: `tom --shell --output-json` (explicit shell + JSON mode)
2. Observe shell launches (banner may be suppressed in JSON mode)
3. Type: `/today` (press Enter)
4. Observe output is valid JSON (no human-readable text)
5. Type: `/quit`
6. Verify clean exit

**Expected Results**:
- ✅ Shell mode activates with `--shell` flag
- ✅ JSON output format applied to all commands
- ✅ Output is parseable JSON (no mixed formats)
- ✅ Banner/prompts adapted for JSON mode (or suppressed)
- ✅ Exit is clean (no formatting errors)

**Failure Indicators**:
- ❌ Shell does not launch (args conflict)
- ❌ Output mixes JSON and text formats
- ❌ JSON is malformed or unparseable
- ❌ Banner/prompts break JSON structure

---

## Performance Validation

### Shell Startup Time
**Target**: < 500ms from launch to first prompt

**Measurement**:
```bash
time (tom <<< '/quit')
```

**Expected**: Real time < 0.5 seconds

---

### Command Execution Responsiveness
**Target**: < 3 seconds for command result display (NFR-001)

**Measurement**:
```bash
time (tom <<< '/today
/quit')
```

**Expected**: Total time includes LLM call (varies), but prompt returns < 3s after output

---

### Autocomplete Latency
**Target**: < 100ms from Tab to suggestion display

**Measurement**: Manual observation (subjective)

**Expected**: Suggestion appears instantly on Tab press

---

## Quickstart Validation Checklist

- [ ] Scenario 1: Basic shell launch and single command ✅
- [ ] Scenario 2: Multiple commands in sequence ✅
- [ ] Scenario 3: Autocomplete with Tab ✅
- [ ] Scenario 4: Command history with arrow keys ✅
- [ ] Scenario 5: Interrupt with Ctrl+C ✅
- [ ] Scenario 6: Display help command ✅
- [ ] Scenario 7: Error handling for unknown command ✅
- [ ] Scenario 8: Backward compatibility (single-exec mode) ✅
- [ ] Scenario 9: Concurrent sessions ✅
- [ ] Scenario 10: JSON output mode in shell ✅
- [ ] Performance: Startup time < 500ms ✅
- [ ] Performance: Command responsiveness < 3s ✅
- [ ] Performance: Autocomplete latency < 100ms ✅

---

**Validation Status**: Ready for execution after implementation complete
