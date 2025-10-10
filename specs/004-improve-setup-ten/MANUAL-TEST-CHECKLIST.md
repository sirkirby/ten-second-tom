# Manual Test Checklist - Setup Feature

## Purpose

This checklist provides systematic manual verification of the setup feature after implementation. Complex UI flows and user interactions are better verified manually than through brittle automated tests with heavy mocking.

## Test Environment Setup

- [ ] Clean test environment (no existing configuration)
- [ ] SSH keys available in standard locations
- [ ] SSH agent running (if testing agent detection)
- [ ] Valid API keys for OpenAI and Anthropic
- [ ] Network connectivity for API validation

## Scenario 1: First-Time Setup - Happy Path

**Purpose**: Verify the complete first-time setup experience works correctly.

### Steps

1. [ ] Run `tom setup` with no existing configuration
2. [ ] Verify welcome message is displayed
3. [ ] Verify SSH key detection runs and completes within 5 seconds
4. [ ] Verify ED25519 keys are prioritized if available
5. [ ] Select an SSH key from the list
6. [ ] Verify LLM provider selection prompt appears
7. [ ] Select OpenAI as provider
8. [ ] Enter valid OpenAI API key (starts with `sk-`)
9. [ ] Verify API key format validation passes
10. [ ] Verify network validation occurs (with retry if needed)
11. [ ] Verify memory directory prompt appears with default value
12. [ ] Accept default memory directory
13. [ ] Verify retention days prompt appears with default (30)
14. [ ] Accept default retention days
15. [ ] Verify configuration summary is displayed
16. [ ] Confirm configuration
17. [ ] Verify success message is displayed
18. [ ] Verify configuration is saved to User Secrets

### Expected Results

- [ ] Setup completes without errors
- [ ] All prompts appear in correct order
- [ ] Configuration is persisted correctly
- [ ] `tom config show` displays saved configuration

### Verification Commands

```bash
tom config show
tom config show --json
```

## Scenario 2: Re-running Setup with Existing Configuration

**Purpose**: Verify that re-running setup with existing config requires --force flag.

### Steps

1. [ ] Ensure configuration exists from Scenario 1
2. [ ] Run `tom setup` without `--force` flag
3. [ ] Verify error message indicating config already exists
4. [ ] Verify error message suggests using `--force` flag
5. [ ] Run `tom setup --force`
6. [ ] Verify existing values are shown as defaults
7. [ ] Change LLM provider from OpenAI to Anthropic
8. [ ] Enter valid Anthropic API key
9. [ ] Keep other values the same
10. [ ] Confirm new configuration
11. [ ] Verify configuration is updated
12. [ ] Verify CreatedAt timestamp is preserved
13. [ ] Verify LastModifiedAt timestamp is updated

### Expected Results

- [ ] Setup without --force fails gracefully
- [ ] Setup with --force pre-populates existing values
- [ ] Configuration changes are saved
- [ ] Timestamps are managed correctly

### Verification Commands

```bash
tom setup
tom setup --force
tom config show
```

## Scenario 3: SSH Key Detection - Multiple Sources

**Purpose**: Verify SSH key detection from various sources works correctly.

### Test Cases

#### 3a: FileSystem Detection

1. [ ] Ensure SSH keys exist in `~/.ssh/`
2. [ ] Run `tom setup --force`
3. [ ] Verify keys from filesystem are listed
4. [ ] Verify each key shows file path
5. [ ] Verify key fingerprints are displayed

#### 3b: SSH Agent Detection

1. [ ] Ensure SSH agent is running (`ssh-add -l`)
2. [ ] Add keys to agent (`ssh-add ~/.ssh/id_ed25519`)
3. [ ] Run `tom setup --force`
4. [ ] Verify agent keys are listed with "[System Agent]" prefix
5. [ ] Verify agent keys are prioritized

#### 3c: ED25519 Prioritization

1. [ ] Ensure both RSA and ED25519 keys exist
2. [ ] Run `tom setup --force`
3. [ ] Verify ED25519 key is pre-selected as default
4. [ ] Verify user can manually select RSA key if desired

#### 3d: No Keys Found

1. [ ] Temporarily move all SSH keys out of `~/.ssh/`
2. [ ] Stop SSH agent
3. [ ] Run `tom setup --force`
4. [ ] Verify helpful error message appears
5. [ ] Verify error suggests creating SSH key

### Expected Results

- [ ] All key sources are detected correctly
- [ ] Key types are identified accurately
- [ ] ED25519 keys are prioritized
- [ ] Graceful handling when no keys found

## Scenario 4: API Key Validation with Retry

**Purpose**: Verify API key validation and retry logic works correctly.

### Test Cases

#### 4a: Invalid Format

1. [ ] Run `tom setup --force`
2. [ ] Enter API key with wrong format (e.g., "invalid-key")
3. [ ] Verify format validation error message
4. [ ] Verify error describes correct format (sk- prefix)
5. [ ] Enter correctly formatted key

#### 4b: Network Validation

1. [ ] Run `tom setup --force`
2. [ ] Enter valid format but fake API key
3. [ ] Verify network validation occurs
4. [ ] Verify retry logic with exponential backoff (1s, 2s, 4s)
5. [ ] After failures, verify option to skip validation
6. [ ] Choose to skip or enter valid key

#### 4c: Provider-Specific Validation

1. [ ] Test OpenAI key must start with `sk-`
2. [ ] Test Anthropic key must start with `sk-ant-`
3. [ ] Verify provider-specific error messages

### Expected Results

- [ ] Format validation catches invalid keys immediately
- [ ] Network validation retries with backoff
- [ ] Clear error messages for each failure type
- [ ] Option to skip validation after retries

## Scenario 5: Configuration Persistence

**Purpose**: Verify configuration save/load works correctly.

### Steps

1. [ ] Complete setup with known configuration
2. [ ] Run `tom config show`
3. [ ] Verify all settings are displayed correctly
4. [ ] Verify API key is masked (shows `sk-***...***xyz`)
5. [ ] Run `tom config show --json`
6. [ ] Verify JSON output is valid and complete
7. [ ] Manually inspect User Secrets file
8. [ ] Verify configuration structure is correct
9. [ ] Verify timestamps are present
10. [ ] Restart application
11. [ ] Verify configuration persists across restarts

### Expected Results

- [ ] Configuration saves to User Secrets successfully
- [ ] Configuration loads correctly on next run
- [ ] API keys are stored securely
- [ ] Timestamps are accurate
- [ ] Configuration survives application restarts

### Verification Commands

```bash
tom config show
tom config show --json
cat ~/.microsoft/usersecrets/TenSecondTom/secrets.json
```

## Scenario 6: Setup Cancellation

**Purpose**: Verify setup can be cancelled at any step without corrupting state.

### Test Cases

#### 6a: Cancel at SSH Key Selection

1. [ ] Run `tom setup --force`
2. [ ] Press Ctrl+C during SSH key selection
3. [ ] Verify cancellation message appears
4. [ ] Verify existing config is unchanged
5. [ ] Run `tom config show` to verify

#### 6b: Cancel at API Key Entry

1. [ ] Run `tom setup --force`
2. [ ] Select SSH key
3. [ ] Press Ctrl+C during API key entry
4. [ ] Verify cancellation message appears
5. [ ] Verify existing config is unchanged

#### 6c: Cancel at Configuration Confirmation

1. [ ] Run `tom setup --force`
2. [ ] Complete all prompts
3. [ ] Press Ctrl+C or select "No" at confirmation
4. [ ] Verify setup is cancelled
5. [ ] Verify existing config is unchanged

### Expected Results

- [ ] Ctrl+C is handled gracefully at all steps
- [ ] Cancellation does not corrupt existing config
- [ ] Clear cancellation message is displayed
- [ ] Application exits cleanly

## Scenario 7: Config Show Command

**Purpose**: Verify config show displays configuration correctly.

### Test Cases

#### 7a: Show Existing Configuration

1. [ ] Ensure configuration exists
2. [ ] Run `tom config show`
3. [ ] Verify SSH key path is displayed
4. [ ] Verify LLM provider is displayed
5. [ ] Verify API key is masked
6. [ ] Verify memory directory is displayed
7. [ ] Verify retention days is displayed
8. [ ] Verify timestamps are displayed

#### 7b: Show with JSON Format

1. [ ] Run `tom config show --json`
2. [ ] Verify output is valid JSON
3. [ ] Verify API key is still masked in JSON
4. [ ] Verify all fields are present

#### 7c: Show with No Configuration

1. [ ] Run `tom config reset` to remove config
2. [ ] Run `tom config show`
3. [ ] Verify helpful error message
4. [ ] Verify error suggests running `tom setup`

### Expected Results

- [ ] Configuration displays correctly
- [ ] Secrets are masked appropriately
- [ ] JSON output is valid and complete
- [ ] Helpful error when no config exists

## Scenario 8: Config Set Command

**Purpose**: Verify individual settings can be updated.

### Test Cases

#### 8a: Update LLM Provider

1. [ ] Run `tom config set llm.provider anthropic`
2. [ ] Verify success message
3. [ ] Verify new API key is prompted
4. [ ] Enter valid Anthropic key
5. [ ] Run `tom config show` to verify change

#### 8b: Update Memory Directory

1. [ ] Run `tom config set storage.directory /new/path`
2. [ ] Verify confirmation prompt for directory creation
3. [ ] Confirm directory creation
4. [ ] Verify directory is created
5. [ ] Run `tom config show` to verify change

#### 8c: Update Retention Days

1. [ ] Run `tom config set optional.retention-days 60`
2. [ ] Verify success message
3. [ ] Run `tom config show` to verify change

#### 8d: Update SSH Key Path

1. [ ] Run `tom config set ssh.key-path /path/to/key`
2. [ ] Verify key validation occurs
3. [ ] Verify success message
4. [ ] Run `tom config show` to verify change

#### 8e: Invalid Setting Name

1. [ ] Run `tom config set invalid.setting value`
2. [ ] Verify error message listing valid settings
3. [ ] Verify configuration is unchanged

#### 8f: No Configuration Exists

1. [ ] Run `tom config reset` to remove config
2. [ ] Run `tom config set llm.provider openai`
3. [ ] Verify error message
4. [ ] Verify error suggests running `tom setup`

### Expected Results

- [ ] Individual settings update correctly
- [ ] LastModifiedAt timestamp updates
- [ ] Validation occurs for setting values
- [ ] Clear errors for invalid settings
- [ ] Helpful error when no config exists

### Verification Commands

```bash
tom config set llm.provider openai
tom config set storage.directory /new/path
tom config set optional.retention-days 90
tom config show
```

## Scenario 9: Config Reset Command

**Purpose**: Verify configuration can be completely reset.

### Steps

1. [ ] Ensure configuration exists
2. [ ] Run `tom config reset`
3. [ ] Verify confirmation prompt appears
4. [ ] Confirm reset
5. [ ] Verify success message
6. [ ] Run `tom config show`
7. [ ] Verify error indicating no configuration
8. [ ] Run `tom setup` to verify fresh setup works
9. [ ] Run `tom config reset` again (idempotent test)
10. [ ] Verify no error when resetting already-reset config

### Expected Results

- [ ] Reset requires confirmation
- [ ] All configuration is removed
- [ ] Reset is idempotent (can run multiple times)
- [ ] Fresh setup works after reset
- [ ] User Secrets file is removed or cleared

### Verification Commands

```bash
tom config reset
tom config show
tom setup
```

## Scenario 10: Non-Interactive Mode

**Purpose**: Verify non-interactive mode uses defaults and doesn't prompt.

### Steps

1. [ ] Ensure SSH key exists at standard location
2. [ ] Set environment variable `OPENAI_API_KEY`
3. [ ] Run `tom setup --non-interactive`
4. [ ] Verify no prompts appear
5. [ ] Verify setup completes automatically
6. [ ] Verify defaults are used:
   - First detected ED25519 SSH key
   - OpenAI provider (from env var)
   - Default memory directory
   - Default retention days (30)
7. [ ] Run `tom config show` to verify configuration

### Expected Results

- [ ] Setup completes without user interaction
- [ ] Sensible defaults are applied
- [ ] Configuration is valid and usable
- [ ] Useful for CI/CD and scripting scenarios

### Verification Commands

```bash
export OPENAI_API_KEY="sk-test-key"
tom setup --non-interactive
tom config show
```

## Error Scenarios

### Missing SSH Key

1. [ ] Remove all SSH keys
2. [ ] Run `tom setup`
3. [ ] Verify clear error message
4. [ ] Verify instructions for creating SSH key

### Invalid API Key Format

1. [ ] Enter key without proper prefix
2. [ ] Verify format validation error
3. [ ] Verify error describes correct format

### Network Timeout

1. [ ] Simulate network issues
2. [ ] Run `tom setup`
3. [ ] Verify timeout is handled gracefully
4. [ ] Verify retry logic with exponential backoff

### Corrupted Configuration

1. [ ] Manually corrupt User Secrets JSON
2. [ ] Run `tom config show`
3. [ ] Verify error message
4. [ ] Verify suggestion to reset or re-run setup

## Performance Verification

- [ ] SSH key detection completes within 5 seconds
- [ ] API key validation completes within 10 seconds (including retries)
- [ ] Configuration save/load is nearly instantaneous (< 100ms)
- [ ] Setup wizard feels responsive, not sluggish

## Cross-Platform Verification (if applicable)

- [ ] Test on macOS
- [ ] Test on Linux
- [ ] Test on Windows (if supported)
- [ ] Verify SSH key paths work on all platforms
- [ ] Verify User Secrets location works on all platforms

## Checklist Completion

**Date Tested**: ________________
**Tested By**: ________________
**Platform**: ________________
**Result**: ☐ Pass ☐ Fail

**Issues Found**:

1. _______________________________________
2. _______________________________________
3. _______________________________________

**Notes**:

_____________________________________________
_____________________________________________
_____________________________________________
