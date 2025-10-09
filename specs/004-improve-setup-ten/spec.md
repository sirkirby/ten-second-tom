# Feature Specification: Guided Setup and Configuration Management

**Feature Branch**: `004-improve-setup-ten`  
**Created**: October 9, 2025  
**Status**: Draft  
**Input**: User description: "improve setup. ten second tom needs a better set up experience as there are several requirements to getting up and running. the user requires a valid ed2551 ssh key, either in the standard local directory or via ssh agen, which we auto detect by default, but if they do not, they simply cannot login. we have instructions for this, but we should have guided setup in app. We also need in app guidance for asking which AI provider they would like to use and then collecting their API key. We should also aske where we should store their memories. As well as other relevant options. I think this should be a new /setup command that we automatically initiate when the app first loads to collect all of this relevant information. this would ensure a smooth onboarding with much less confusion on what options and secrets are stored and where. as per our documentation, we recommend secrets be stored as ENV files, which would likely mean storing them in their shell config. .NET also has secrets support, so perhaps with this guided setup, that would be the preferred method as it's more secure and should not require further user intervention, we simply store then in .net serets, which this app is already configured to support, and if something changes they can run through setup again. Perhaps we also need a /config command that accepts arguments with help text that allows people to adjust each option individually. i think in combination with the guided setup, the app should be able to handle the bulk of configuration needs wiithough the user manually editing the appsettings file or shell config files."

## Execution Flow (main)
```
1. Parse user description from Input
   → SUCCESS: Feature description provided
2. Extract key concepts from description
   → Actors: new users, existing users
   → Actions: guided setup, configuration management, SSH key validation, API key collection
   → Data: SSH keys, API keys, memory storage location, LLM provider choice
   → Constraints: secure storage, .NET secrets preferred, no manual file editing
3. For each unclear aspect:
   → [RESOLVED] Setup command should auto-run on first launch
   → [RESOLVED] Use .NET User Secrets for secure storage
   → [RESOLVED] Provide /config command for individual setting changes
4. Fill User Scenarios & Testing section
   → SUCCESS: Clear user flows identified
5. Generate Functional Requirements
   → SUCCESS: All requirements are testable
6. Identify Key Entities
   → SUCCESS: Configuration entities identified
7. Run Review Checklist
   → SUCCESS: No ambiguities remain
8. Return: SUCCESS (spec ready for planning)
```

---

## ⚡ Quick Guidelines
- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

---

## Clarifications

### Session 2025-10-09

- Q: Configuration Conflict Resolution - When a user has configuration values in multiple locations (e.g., environment variables, existing appsettings.json, AND .NET User Secrets), how should the setup wizard handle this during first-time setup? → A: Merge all sources using priority hierarchy; only prompt if conflicts exist

- Q: API Key Validation Behavior - When the setup wizard validates an API key by testing connectivity to the provider (OpenAI or Anthropic), what should happen if the network request times out or fails? → A: Retry 3 times with exponential backoff; then offer skip option

- Q: User Secrets Write Failure - If the system cannot write to .NET User Secrets (due to permissions, disk full, or platform incompatibility), what should the setup wizard do? → A: Write to local appsettings.json with security warning (minimize user intervention in unlikely scenario)

- Q: Setup Operation Timeout - What is the maximum acceptable time for key operations during setup to prevent the user from waiting indefinitely? → A: SSH key detection: 5s, API validation: 10s per attempt, total setup: 2min (configurable in appsettings)

- Q: Setup Wizard Resumability - When a user cancels setup mid-way through the wizard, how should the system handle resuming when they run setup again? → A: Start fresh every time; show current values/selections if step was already completed (setup is complete process; config command updates individual values)

---

## User Scenarios & Testing

### Primary User Story

**New User Experience:**
A developer downloads Ten Second Tom for the first time. When they run their first command (e.g., `tom today`), the application detects that no configuration exists and automatically launches a guided setup wizard. The wizard walks them through:

1. SSH key detection and validation (or guidance on creating one)
2. Choosing an LLM provider (OpenAI or Anthropic)
3. Entering their API key securely
4. Choosing where to store their memories
5. Configuring optional settings (logging, data retention)

After setup completes, the user's original command executes immediately, providing a seamless first-run experience.

**Existing User Experience:**
A user who has already configured Ten Second Tom wants to switch from OpenAI to Anthropic. Instead of manually editing configuration files or environment variables, they run `tom config --llm-provider Anthropic` and are prompted to enter their Anthropic API key. The change is saved securely and takes effect immediately.

**Reconfiguration Experience:**
A user needs to reconfigure multiple settings (perhaps they got a new SSH key or want to change their memory directory). They run `tom setup` to launch the guided setup wizard again. The wizard walks through all configuration steps from the beginning, but displays their current configuration values as the default for each step, making it easy to keep existing values or update them as needed. This provides a complete review and update experience for comprehensive configuration changes. For quick single-setting updates, the user can use `tom config --setting value` instead.

### Acceptance Scenarios

1. **Given** the user runs Ten Second Tom for the first time, **When** they execute any command, **Then** the guided setup wizard launches automatically before the command runs.

2. **Given** the user is in the guided setup wizard, **When** they reach the SSH key step, **Then** the system detects available SSH keys (from ssh-agent, 1Password, Secretive, or local files) and presents them for selection, or guides the user to create a new ED25519 key.

3. **Given** the user has selected an SSH key, **When** the system validates it, **Then** the user receives clear feedback about whether the key is valid and properly formatted (ED25519 public key).

4. **Given** the user is choosing an LLM provider, **When** they select a provider (OpenAI or Anthropic), **Then** they are prompted to enter their API key, which is validated for format and securely stored.

5. **Given** the user is configuring memory storage, **When** they specify a directory path, **Then** the system validates that the path exists or can be created, and confirms write permissions.

6. **Given** the user completes the guided setup, **When** setup finishes, **Then** all configuration is saved securely using .NET User Secrets, and the user's original command executes.

7. **Given** the user runs `tom config --help`, **When** the help text displays, **Then** they see all available configuration options with descriptions and current values.

8. **Given** the user runs `tom config --llm-provider Anthropic`, **When** the provider change is processed, **Then** they are prompted for the Anthropic API key (if not already set), the change is saved, and confirmation is displayed.

9. **Given** the user runs `tom config --memory-directory /custom/path`, **When** the directory change is processed, **Then** the system validates the path, confirms write permissions, and saves the change.

10. **Given** the user runs `tom setup` when already configured, **When** the setup wizard launches, **Then** it displays current configuration values and allows selective updates without requiring re-entry of unchanged values.

11. **Given** the user's SSH agent is not running, **When** the setup wizard reaches the SSH key step, **Then** clear instructions are provided for starting their SSH agent or using file-based keys.

12. **Given** the user enters an invalid API key during setup, **When** validation occurs, **Then** they receive specific error feedback and can retry without restarting the entire setup process.

### Edge Cases

- **What happens when the user cancels setup mid-way?**
  - Partial configuration is saved if any steps completed successfully before cancellation
  - User can run `/setup` again to go through the complete wizard from the beginning
  - When re-running setup, current configuration values are shown as defaults for each step
  - Original command does not execute if setup was incomplete or cancelled
  - For quick single-setting updates, users should use `/config` command instead

- **How does the system handle missing SSH keys?**
  - Provide step-by-step instructions for generating an ED25519 key pair
  - Offer to guide the user through ssh-keygen command
  - Detect and suggest popular SSH agent setup (1Password, Secretive)

- **What if the user has multiple SSH keys?**
  - Display all available ED25519 keys with clear identification
  - Show which SSH agents are running and their keys
  - Allow user to select which key to use for authentication

- **How does configuration migration work for existing users?**
  - Detect existing environment variables or configuration files
  - Offer to migrate existing configuration to .NET User Secrets
  - Preserve user's current settings while offering improvements

- **What happens when .NET User Secrets are not available?**
  - System automatically falls back to writing configuration to `appsettings.json`
  - Prominent security warning displayed about storing secrets in plain text
  - User can proceed with setup without additional intervention
  - System logs the fallback event for troubleshooting

- **How does the system handle invalid directory paths for memory storage?**
  - Validate path syntax before attempting creation
  - Confirm write permissions before saving configuration
  - Offer to create non-existent directories with user confirmation

- **What if API key validation fails due to network issues?**
  - System retries up to 3 times with exponential backoff (1s, 2s, 4s)
  - After failed retries, user can skip validation and proceed
  - Format validation always occurs regardless of network connectivity
  - User receives guidance on testing the connection manually later

- **How does reconfiguration handle credential updates?**
  - Allow updating individual credentials without affecting others
  - Validate new credentials before removing old ones
  - Provide rollback guidance if new configuration doesn't work

---

## Requirements

### Functional Requirements

**Setup Wizard:**

- **FR-001**: System MUST automatically detect when the application is being run for the first time (no existing configuration) and launch the guided setup wizard before executing any user command.

- **FR-002**: System MUST provide a `/setup` command that users can run manually to reconfigure the application at any time.

- **FR-003**: Guided setup wizard MUST collect all required configuration in the following order: SSH key selection/validation, LLM provider choice, API key entry, memory storage location, and optional settings.

- **FR-004**: Setup wizard MUST display progress indicators showing which step the user is on and how many steps remain.

- **FR-005**: Setup wizard MUST allow users to navigate back to previous steps to correct entries without restarting the entire process.

- **FR-006**: Setup wizard MUST allow users to cancel at any point, with clear indication of what configuration (if any) has been saved.

- **FR-006a**: When running the setup wizard (via `/setup` or auto-launch), system MUST always walk through all configuration steps from the beginning. For steps where configuration already exists, the wizard MUST display the current value as the default option but still require user confirmation or selection. Setup is a complete end-to-end process; individual setting updates should use the `/config` command instead.

- **FR-007**: Setup wizard MUST enforce configurable timeout limits for key operations: SSH key detection (default: 5 seconds), API key validation per attempt (default: 10 seconds), and total setup duration (default: 2 minutes). Timeout values MUST be configurable via appsettings.json. If any operation exceeds its timeout, the wizard MUST display an error message and allow the user to retry or skip that step.

**SSH Key Management:**

- **FR-008**: System MUST automatically detect and list available SSH keys from all supported sources: SSH agents (system, 1Password, Secretive), and local file system (~/.ssh/).

- **FR-009**: System MUST validate that selected SSH keys are ED25519 format and properly structured.

- **FR-010**: System MUST provide clear, actionable guidance when no valid SSH keys are found, including step-by-step instructions for generating an ED25519 key pair.

- **FR-011**: System MUST detect which SSH agents are currently running (1Password, Secretive, system ssh-agent) and prioritize them over file-based keys.

- **FR-012**: System MUST allow users to manually specify an SSH key path if automatic detection fails or they have keys in non-standard locations.

**LLM Provider Configuration:**

- **FR-013**: System MUST present a clear choice between supported LLM providers (OpenAI and Anthropic) with brief descriptions of each.

- **FR-014**: System MUST validate API key format for the selected provider before saving configuration.

- **FR-015**: System MUST securely store API keys using .NET User Secrets by default.

- **FR-016**: System MUST mask API key input during entry (showing only asterisks or dots).

- **FR-017**: System MUST allow users to test their API key connection before completing setup. If the connection test fails or times out, system MUST retry up to 3 times using exponential backoff (1s, 2s, 4s). After all retries fail, system MUST offer the user the option to skip validation and proceed with setup.

**Memory Storage Configuration:**

- **FR-018**: System MUST allow users to specify a custom directory path for storing memory entries.

- **FR-019**: System MUST validate that the specified directory path is accessible and writable before saving configuration.

- **FR-020**: System MUST offer to create the memory directory if it doesn't exist, with user confirmation.

- **FR-021**: System MUST provide a sensible default memory directory path (e.g., `~/.memory/ten-second-tom`) if the user prefers not to customize.

**Configuration Management Command:**

- **FR-022**: System MUST provide a `/config` command that accepts arguments to modify individual configuration settings.

- **FR-023**: `/config` command MUST support updating: LLM provider, API keys, memory directory, SSH key path, logging level, and data retention policy.

- **FR-024**: `/config` command MUST display comprehensive help text when run with `--help`, showing all available options, their current values, and descriptions.

- **FR-025**: `/config` command MUST validate new values before applying changes and provide clear error messages for invalid inputs.

- **FR-026**: `/config` command MUST confirm successful configuration changes with a summary of what was updated.

- **FR-027**: `/config` command MUST allow viewing current configuration without making changes (e.g., `tom config --show`).

**Security & Storage:**

- **FR-028**: System MUST store all sensitive configuration (API keys, SSH key paths) using .NET User Secrets when available. If User Secrets write fails (permissions, disk space, platform incompatibility), system MUST automatically fall back to writing configuration to `appsettings.json` and display a prominent security warning to the user about storing secrets in plain text files.

- **FR-029**: System MUST never display full API keys in plain text; only show masked versions (e.g., `sk-...xyz`) or last 4 characters.

- **FR-030**: System MUST never log sensitive configuration values (API keys, private key material) even in debug/verbose mode.

- **FR-031**: System MUST provide clear guidance on configuration storage location and how to back up or migrate settings when using .NET User Secrets.

**User Experience:**

- **FR-032**: All setup and configuration commands MUST provide clear, friendly, non-technical language suitable for developers of all experience levels.

- **FR-033**: System MUST provide helpful links to documentation when users need more detailed information about SSH keys, API key generation, or provider selection.

- **FR-034**: Setup wizard MUST display the user's original command and confirm it will run after setup completes.

- **FR-035**: System MUST validate all user inputs immediately upon entry with specific error messages, not after the entire setup process completes.

- **FR-036**: System MUST provide a setup summary at the end showing all configured values (with sensitive data masked) and confirming the configuration is ready to use.

**Backward Compatibility:**

- **FR-037**: System MUST continue to support existing configuration methods (environment variables, appsettings.json) for users who prefer not to use guided setup.

- **FR-038**: System MUST detect existing configuration from environment variables or configuration files and offer to migrate it to .NET User Secrets.

- **FR-039**: Configuration priority MUST remain unchanged: command-line arguments > environment variables > user secrets > configuration files.

- **FR-040**: During first-time setup, system MUST merge configuration values from all sources (environment variables, appsettings.json, User Secrets) using the standard priority hierarchy. If conflicting values exist for the same setting across sources, system MUST prompt the user to choose which value to keep.

### Key Entities

- **SetupWizard**: Represents the guided setup process, tracking current step, collected configuration values, and completion status. Contains validation state for each configuration element.

- **ConfigurationSettings**: Represents the complete application configuration, including: SSH authentication settings (key path, agent provider), LLM settings (provider, API key, model), memory storage settings (directory path), optional settings (logging, retention policy).

- **SshKeyInfo**: Represents a detected SSH key, including: source (agent name or file path), key type (ED25519), public key value, validation status, and whether it's currently in use.

- **LlmProviderInfo**: Represents an available LLM provider, including: provider name (OpenAI, Anthropic), required API key format, available models, and configuration status (configured, not configured, needs update).

- **ConfigurationCommand**: Represents a configuration change request, including: setting name, new value, validation rules, and whether it requires additional input (like prompting for API key).

- **SetupProgress**: Represents the user's progress through the setup wizard, including: completed steps, current step, validation results, and whether setup can proceed to the next step.

---

## Review & Acceptance Checklist

### Content Quality
- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness
- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous  
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

---

## Execution Status

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked (none remain)
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [x] Review checklist passed

---

## Notes

### Assumptions
- Users are familiar with basic command-line concepts but may not be experts in SSH or API configuration
- .NET User Secrets infrastructure is available and working on all target platforms
- SSH agents follow OpenSSH agent protocol standards
- API keys can be validated for format but may require network calls for full validation

### Out of Scope
- Automatic SSH key generation (system will guide but not perform the generation)
- SSH agent installation or configuration (system detects and uses, but doesn't install)
- API key generation (users must obtain from provider websites)
- Cloud-based configuration synchronization across machines
- GUI-based setup wizard (CLI interactive mode only)
- Configuration encryption beyond what .NET User Secrets provides
- Multi-user configuration management on shared systems

### Dependencies
- .NET User Secrets must be available on target platform
- SSH agent detection requires platform-specific socket/pipe locations
- API key validation may require network connectivity
- File system access for memory directory creation and validation

### Success Metrics
- Percentage of first-time users who complete setup without viewing documentation
- Average time to complete guided setup from first launch
- Reduction in configuration-related support requests or issues
- Percentage of users using .NET User Secrets vs. environment variables
- User satisfaction with setup and configuration experience

---
