# Quickstart: Guided Setup and Configuration Management

**Feature**: 004-improve-setup-ten  
**Purpose**: Validate implementation through end-to-end user scenarios  
**Audience**: Developers testing the feature

## Prerequisites

- Ten Second Tom compiled and available in PATH as `tom`
- Valid ED25519 SSH key available (either in ~/.ssh or via SSH agent)
- Valid API key for OpenAI or Anthropic
- Clean test environment (no existing configuration)

## Scenario 1: First-Time Setup (Happy Path)

**Goal**: Complete guided setup wizard as a new user

### Steps

1. **Trigger first-time setup**
   ```bash
   tom today
   ```
   
   **Expected**: Setup wizard launches automatically with welcome message

2. **SSH Key Detection Step**
   ```
   Welcome to Ten Second Tom! Let's get you set up.
   
   Step 1 of 8: SSH Key Configuration
   
   Detecting SSH keys...
   ✓ Found 3 SSH keys:
   
   1. [System Agent] id_ed25519
   2. [1Password] work_key
   3. [File] ~/.ssh/personal_ed25519
   
   Select SSH key to use: _
   ```
   
   **Action**: Select key 1 (using arrow keys and Enter)
   
   **Expected**: Key is validated and marked as selected

3. **LLM Provider Selection**
   ```
   Step 2 of 8: LLM Provider Selection
   
   Choose your AI provider:
   
   1. OpenAI (GPT-4, GPT-3.5)
   2. Anthropic (Claude 3.5)
   
   Select provider: _
   ```
   
   **Action**: Select OpenAI (option 1)
   
   **Expected**: Provider selected

4. **API Key Entry**
   ```
   Step 3 of 8: API Key Configuration
   
   Enter your OpenAI API key: ****************************************
   
   Validating API key...
   ✓ Format valid
   ✓ Network validation successful
   ```
   
   **Action**: Enter valid OpenAI API key
   
   **Expected**: Key is validated (format + network)

5. **Memory Directory Configuration**
   ```
   Step 4 of 8: Memory Storage Location
   
   Where should I store your memories?
   Default: ~/.memory/ten-second-tom
   
   Directory path [default]: _
   ```
   
   **Action**: Press Enter to accept default
   
   **Expected**: Default directory is validated/created

6. **Optional Settings**
   ```
   Step 5 of 8: Logging Level
   
   Select logging level:
   1. Debug (verbose)
   2. Information (recommended)
   3. Warning (quiet)
   4. Error (silent)
   
   Select level: _
   ```
   
   **Action**: Select option 2 (Information)
   
   **Expected**: Logging level set

7. **Data Retention**
   ```
   Step 6 of 8: Data Retention
   
   How long should memories be kept?
   
   Retention days [30]: _
   ```
   
   **Action**: Enter 60
   
   **Expected**: Retention days set to 60

8. **Setup Summary**
   ```
   Step 7 of 8: Configuration Summary
   
   SSH Key: [System Agent] id_ed25519
   LLM Provider: OpenAI
   API Key: sk-...1234
   Memory Directory: ~/.memory/ten-second-tom
   Log Level: Information
   Retention Days: 60
   
   Save this configuration? (Y/n): _
   ```
   
   **Action**: Type Y and press Enter
   
   **Expected**: Configuration is saved

9. **Completion**
   ```
   ✓ Setup complete!
   
   Configuration saved to User Secrets
   Location: ~/.microsoft/usersecrets/ten-second-tom-secrets/
   
   Running your original command: tom today
   
   [Command output follows]
   ```
   
   **Expected**:
   - Success message displayed
   - Original command `tom today` executes
   - Configuration persisted to User Secrets

### Validation

- [ ] Setup wizard launches automatically on first run
- [ ] All 8 steps complete successfully
- [ ] SSH key detection finds available keys
- [ ] API key validation works (format + network)
- [ ] Configuration is saved to User Secrets
- [ ] Original command executes after setup
- [ ] No secrets are displayed in plain text
- [ ] Exit code is 0

## Scenario 2: Reconfigure Existing Setup

**Goal**: Update configuration using setup wizard

### Steps

1. **Run setup command**
   ```bash
   tom setup
   ```
   
   **Expected**: Setup wizard launches with "Reconfiguration" message

2. **SSH Key Step Shows Current Value**
   ```
   Reconfiguring Ten Second Tom
   
   Step 1 of 8: SSH Key Configuration
   
   Current: [System Agent] id_ed25519
   
   Detecting SSH keys...
   ✓ Found 3 SSH keys:
   
   1. [System Agent] id_ed25519 (current)
   2. [1Password] work_key
   3. [File] ~/.ssh/personal_ed25519
   
   Select SSH key to use: _
   ```
   
   **Action**: Select key 1 to keep current
   
   **Expected**: Current value is preserved

3. **Switch LLM Provider**
   ```
   Step 2 of 8: LLM Provider Selection
   
   Current: OpenAI
   
   Choose your AI provider:
   
   1. OpenAI (current)
   2. Anthropic (Claude 3.5)
   
   Select provider: _
   ```
   
   **Action**: Select option 2 (Anthropic)
   
   **Expected**: Provider switch initiated

4. **Enter New API Key**
   ```
   Step 3 of 8: API Key Configuration
   
   Provider changed to Anthropic. New API key required.
   
   Enter your Anthropic API key: ****************************************
   ```
   
   **Action**: Enter valid Anthropic API key
   
   **Expected**: New API key validated

5. **Complete Setup**
   
   **Action**: Complete remaining steps, accepting defaults
   
   **Expected**: Configuration updated with new provider

### Validation

- [ ] Setup wizard shows current values
- [ ] Provider switch requires new API key
- [ ] Configuration is updated successfully
- [ ] Old configuration is overwritten
- [ ] Exit code is 0

## Scenario 3: Individual Setting Update via Config Command

**Goal**: Update single setting without running full wizard

### Steps

1. **Show current configuration**
   ```bash
   tom config show
   ```
   
   **Expected Output**:
   ```
   Current Configuration:
   
   SSH Authentication:
     Key Path: ~/.ssh/id_ed25519
     Key Source: FileSystem
   
   LLM Provider:
     Provider: Anthropic
     API Key: sk-...1234
     Model: claude-3-5-sonnet-20241022
   
   Storage:
     Memory Directory: ~/.memory/ten-second-tom
   
   Optional Settings:
     Log Level: Information
     Retention Days: 60
   
   Last Modified: 2025-10-09 10:35:00
   ```

2. **Update memory directory**
   ```bash
   tom config set memory-directory /custom/memory/path
   ```
   
   **Expected Output**:
   ```
   Validating directory path...
   Directory does not exist. Create it? (Y/n): _
   ```
   
   **Action**: Type Y and press Enter
   
   **Expected**:
   ```
   ✓ Directory created: /custom/memory/path
   ✓ Write permissions verified
   
   ✓ Configuration updated
   
   memory-directory: ~/.memory/ten-second-tom → /custom/memory/path
   
   Changes saved successfully.
   ```

3. **Update log level**
   ```bash
   tom config set log-level Debug
   ```
   
   **Expected Output**:
   ```
   ✓ Configuration updated
   
   log-level: Information → Debug
   
   Changes saved successfully.
   ```

4. **Verify changes**
   ```bash
   tom config show
   ```
   
   **Expected**: Updated values are displayed

### Validation

- [ ] Config show displays current configuration with masked secrets
- [ ] Config set validates new values
- [ ] Directory creation prompts user for confirmation
- [ ] Changes are saved to User Secrets
- [ ] Confirmation message shows old → new values
- [ ] Exit code is 0 for all successful operations

## Scenario 4: Error Handling - Invalid Inputs

**Goal**: Verify validation and error messages

### Steps

1. **Invalid setting name**
   ```bash
   tom config set invalid-setting value
   ```
   
   **Expected Output**:
   ```
   ✗ Error: Unknown setting 'invalid-setting'
   
   Valid settings:
     - llm-provider
     - api-key
     - memory-directory
     - ssh-key-path
     - log-level
     - retention-days
   
   Run 'tom config --help' for more information.
   ```
   
   **Expected Exit Code**: 1

2. **Invalid provider name**
   ```bash
   tom config set llm-provider InvalidProvider
   ```
   
   **Expected Output**:
   ```
   ✗ Error: Invalid value for llm-provider: 'InvalidProvider'
   
   Valid providers: OpenAI, Anthropic
   ```
   
   **Expected Exit Code**: 1

3. **Invalid log level**
   ```bash
   tom config set log-level InvalidLevel
   ```
   
   **Expected Output**:
   ```
   ✗ Error: Invalid value for log-level: 'InvalidLevel'
   
   Valid levels: Debug, Information, Warning, Error
   ```
   
   **Expected Exit Code**: 1

4. **Invalid retention days**
   ```bash
   tom config set retention-days -5
   ```
   
   **Expected Output**:
   ```
   ✗ Error: Invalid value for retention-days: '-5'
   
   Retention days must be a positive integer.
   ```
   
   **Expected Exit Code**: 1

### Validation

- [ ] Invalid setting names return clear error message
- [ ] Invalid values return validation errors
- [ ] Help text shows valid options
- [ ] Configuration is not modified on error
- [ ] Exit codes are non-zero for errors

## Scenario 5: Setup Cancellation

**Goal**: Verify partial progress is saved on cancellation

### Steps

1. **Start setup wizard**
   ```bash
   tom setup --force
   ```
   
   **Expected**: Setup wizard launches

2. **Complete SSH key step**
   
   **Action**: Select an SSH key

3. **Complete provider step**
   
   **Action**: Select a provider

4. **Cancel at API key step**
   
   **Action**: Press Ctrl+C
   
   **Expected Output**:
   ```
   Setup cancelled by user.
   
   Partial progress saved:
   - SSH Key configured
   - LLM Provider selected
   
   Run 'tom setup' to continue configuration.
   ```

5. **Verify partial config saved**
   ```bash
   tom config show
   ```
   
   **Expected**: SSH key and provider are configured, API key is missing

### Validation

- [ ] Ctrl+C cancels setup gracefully
- [ ] Partial progress is saved
- [ ] Cancellation message is clear
- [ ] User can resume later
- [ ] Exit code is non-zero (cancelled)

## Scenario 6: API Key Validation with Retry

**Goal**: Verify retry logic for transient network failures

### Setup

Mock network failures for first 2 attempts (implementation detail: use test mode or intercept)

### Steps

1. **Start setup and reach API key validation**
   
   **Action**: Enter valid API key
   
   **Expected**:
   ```
   Validating API key...
   ✓ Format valid
   ✗ Network validation failed (timeout)
   
   Retrying in 1 second... (Attempt 2 of 4)
   ✗ Network validation failed (timeout)
   
   Retrying in 2 seconds... (Attempt 3 of 4)
   ✓ Network validation successful
   ```

2. **Complete setup**
   
   **Expected**: Setup continues after successful retry

### Validation

- [ ] Network validation retries up to 3 times
- [ ] Exponential backoff is used (1s, 2s, 4s)
- [ ] Retry progress is shown to user
- [ ] Setup continues after successful retry
- [ ] Format validation always succeeds regardless of network

## Scenario 7: User Secrets Fallback

**Goal**: Verify fallback to appsettings.json when User Secrets fails

### Setup

Make User Secrets directory read-only (to simulate write failure)

```bash
mkdir -p ~/.microsoft/usersecrets/ten-second-tom-secrets
chmod 444 ~/.microsoft/usersecrets/ten-second-tom-secrets
```

### Steps

1. **Complete setup wizard**
   
   **Expected During Save**:
   ```
   ✗ Failed to save to User Secrets: Permission denied
   
   ⚠ WARNING: Falling back to appsettings.json
   
   Your configuration will be saved to:
     /path/to/TenSecondTom/appsettings.json
   
   Security Notice:
     This file contains secrets in plain text.
     Ensure it is not committed to source control.
     Consider fixing User Secrets permissions and re-running setup.
   
   ✓ Configuration saved to appsettings.json
   ```

2. **Verify configuration saved**
   ```bash
   cat /path/to/TenSecondTom/appsettings.json
   ```
   
   **Expected**: Configuration exists with API key visible (plain text)

### Cleanup

```bash
chmod 755 ~/.microsoft/usersecrets/ten-second-tom-secrets
```

### Validation

- [ ] User Secrets write failure is detected
- [ ] Automatic fallback to appsettings.json occurs
- [ ] Prominent security warning is displayed
- [ ] Setup completes successfully
- [ ] Configuration is functional

## Scenario 8: Help and Documentation

**Goal**: Verify help text and documentation

### Steps

1. **Setup help**
   ```bash
   tom setup --help
   ```
   
   **Expected**: Help text with options (--force, --non-interactive)

2. **Config help**
   ```bash
   tom config --help
   ```
   
   **Expected**: Help text with all available settings and actions

3. **Config command examples**
   
   **Expected in Help**:
   ```
   Examples:
     tom config show
     tom config show --show-secrets
     tom config set llm-provider Anthropic
     tom config set api-key
     tom config set memory-directory /custom/path
     tom config validate
     tom config reset
   ```

### Validation

- [ ] Help text is comprehensive and clear
- [ ] All options are documented
- [ ] Examples are provided
- [ ] Help text follows standard CLI conventions

## Success Criteria

All scenarios must pass with:
- ✅ Correct behavior at each step
- ✅ Clear, helpful error messages
- ✅ Secrets properly masked in all outputs
- ✅ Configuration saved to correct location
- ✅ Appropriate exit codes (0 for success, non-zero for errors)
- ✅ No secrets logged or displayed in plain text
- ✅ Fast, responsive UI (<500ms per step excluding network)

## Performance Benchmarks

Run each scenario and verify:
- SSH key detection: <5 seconds
- API key format validation: <10ms
- API key network validation: <10 seconds per attempt
- Configuration save: <100ms
- Config show: <50ms
- Total setup time: <2 minutes (user-paced)

## Manual Testing Checklist

- [ ] Scenario 1: First-time setup completes successfully
- [ ] Scenario 2: Reconfiguration with current values shown
- [ ] Scenario 3: Individual setting updates work
- [ ] Scenario 4: Invalid inputs rejected with clear errors
- [ ] Scenario 5: Cancellation saves partial progress
- [ ] Scenario 6: Retry logic works for network failures
- [ ] Scenario 7: Fallback to appsettings.json works
- [ ] Scenario 8: Help documentation is clear and complete
- [ ] Performance benchmarks are met
- [ ] No secrets visible in any output or logs
- [ ] Cross-platform: Works on macOS and Windows

---

**Note**: This quickstart should be executable immediately after implementation to validate all functionality. Each scenario should be run in sequence to build up configuration state, or reset configuration between scenarios for independent testing.
