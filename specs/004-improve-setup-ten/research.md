# Research: Guided Setup and Configuration Management

**Feature**: 004-improve-setup-ten  
**Date**: October 9, 2025  
**Status**: Complete

## Overview

This document captures research findings for implementing a comprehensive guided setup wizard and configuration management system for Ten Second Tom. The research focuses on SSH key detection across multiple providers, interactive CLI wizard patterns, secure secret storage, and configuration validation strategies.

## Research Areas

### 1. SSH Key Detection Across Multiple Providers

**Decision**: Implement a provider-based detection strategy with priority ordering

**Rationale**:
- SSH keys can come from multiple sources on macOS and Windows
- Users increasingly use SSH agents from password managers (1Password, Secretive) rather than file-based keys
- Detection must be fast (<5s) and reliable across platforms
- Must handle ED25519 keys specifically (project requirement)

**Implementation Approach**:
- **SSH Agent Detection**: Use SSH.NET library to connect to SSH agents via socket (Unix) or named pipe (Windows)
  - System ssh-agent: `SSH_AUTH_SOCK` environment variable on Unix, `\\.\pipe\openssh-ssh-agent` on Windows
  - 1Password: `~/Library/Group Containers/2BUA8C4S2C.com.1password/t/agent.sock` on macOS
  - Secretive: `~/Library/Containers/com.maxgoedjen.Secretive.SecretAgent/Data/socket.ssh` on macOS
- **File-based Detection**: Scan `~/.ssh/` directory for ED25519 keys (id_ed25519.pub, *.pub files)
- **Validation**: Parse public key files using NSec.Cryptography to verify ED25519 format
- **Priority Order**: 
  1. Running SSH agents (most secure, frequently updated)
  2. File-based keys (fallback, still common)
  3. Manual path entry (escape hatch for non-standard locations)

**Alternatives Considered**:
- **File-only detection**: Rejected - doesn't support modern SSH agent workflows
- **OpenSSH ssh-add parsing**: Rejected - requires spawning process, parsing output, platform-specific
- **Third-party SSH key management library**: Rejected - adds unnecessary dependency

**References**:
- SSH.NET documentation: https://github.com/sshnet/SSH.NET
- NSec.Cryptography for Ed25519: https://nsec.rocks/
- 1Password SSH Agent: https://developer.1password.com/docs/ssh/
- Secretive SSH Agent: https://github.com/maxgoedjen/secretive

### 2. Interactive CLI Wizard with Spectre.Console

**Decision**: Use Spectre.Console for rich, interactive terminal UI

**Rationale**:
- Already a project dependency (version 0.51.1)
- Provides excellent interactive prompts, progress indicators, and tables
- Cross-platform terminal support (Windows Terminal, macOS Terminal, iTerm2)
- Type-safe prompt API with validation
- Supports multi-select, text input with masking, confirmation prompts

**Implementation Approach**:
- **Step-by-step wizard**: Use `AnsiConsole.Status()` to show current step
- **SSH key selection**: Use `SelectionPrompt<T>` for multiple key choices
- **API key input**: Use `TextPrompt<string>().Secret()` to mask input
- **Provider selection**: Use `SelectionPrompt<LlmProvider>` with descriptions
- **Directory path**: Use `TextPrompt<string>` with path validation
- **Progress indication**: Show "Step X of Y" header on each page
- **Cancellation**: Detect Ctrl+C and handle gracefully with partial save option

**Alternatives Considered**:
- **System.CommandLine prompts**: Rejected - less rich, requires more custom code
- **Custom terminal manipulation**: Rejected - reinventing the wheel, cross-platform issues
- **Text-only non-interactive**: Rejected - poor user experience for complex setup

**References**:
- Spectre.Console documentation: https://spectreconsole.net/
- Prompts guide: https://spectreconsole.net/prompts/

### 3. .NET User Secrets with Graceful Fallback

**Decision**: Use .NET User Secrets as primary storage with automatic fallback to appsettings.json

**Rationale**:
- User Secrets are secure, platform-appropriate (keychain/credential manager integration)
- Already configured in project (`UserSecretsId` in .csproj)
- Prevents accidental secret commits to source control
- Standard .NET pattern for development secrets
- Fallback ensures setup never fails due to storage issues

**Implementation Approach**:
- **Primary path**: Write to User Secrets using `IConfiguration` and manual JSON file update
  - Location: `~/.microsoft/usersecrets/ten-second-tom-secrets/secrets.json` (macOS/Linux)
  - Location: `%APPDATA%\Microsoft\UserSecrets\ten-second-tom-secrets\secrets.json` (Windows)
- **Fallback path**: Write to `appsettings.json` in app directory with security warning
- **Detection**: Try User Secrets path first; if write fails (permissions, disk space, platform issues), fall back automatically
- **User notification**: Display prominent warning when fallback is used
- **Configuration hierarchy**: Command-line args > Environment variables > User Secrets > appsettings.json (preserved)

**Alternatives Considered**:
- **Environment variables only**: Rejected - requires manual shell config, not beginner-friendly
- **Custom encryption**: Rejected - complex, error-prone, reinventing OS features
- **Cloud secret storage**: Rejected - requires network, adds complexity, out of scope

**References**:
- .NET User Secrets: https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets
- Configuration in .NET: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration

### 4. API Key Validation with Retry Logic

**Decision**: Implement format validation first, then optional network validation with exponential backoff

**Rationale**:
- Format validation is instant and catches most user errors (typos, wrong key)
- Network validation provides confidence but can fail for transient reasons
- Exponential backoff prevents hammering APIs during transient failures
- Skip option allows users to proceed if network is unavailable
- Timeout limits prevent indefinite waits

**Implementation Approach**:
- **Format validation**: Regex patterns for each provider
  - OpenAI: `^sk-[a-zA-Z0-9]{48}$` or newer formats with prefixes
  - Anthropic: `^sk-ant-[a-zA-Z0-9\-]+$`
- **Network validation**: Make minimal API call to verify key works
  - OpenAI: `GET /v1/models` (lightweight, no usage cost)
  - Anthropic: Similar lightweight endpoint
  - Use existing SDK client setup code
- **Retry strategy**: 
  - Attempt 1: Immediate
  - Attempt 2: Wait 1s
  - Attempt 3: Wait 2s
  - Attempt 4: Wait 4s
  - After failures: Offer skip option
- **Timeout**: 10s per attempt (configurable via appsettings.json `Setup:ApiValidationTimeoutSeconds`)
- **Error handling**: Distinguish network errors from authentication errors

**Alternatives Considered**:
- **Network validation only**: Rejected - fails when offline or network issues occur
- **Format validation only**: Rejected - allows invalid keys that will fail later
- **No retry**: Rejected - transient failures would require manual re-run
- **Fixed retry interval**: Rejected - exponential backoff is better for transient network issues

**References**:
- OpenAI API documentation: https://platform.openai.com/docs/api-reference
- Anthropic API documentation: https://docs.anthropic.com/claude/reference/
- Polly retry patterns: (Not adding new dependency, implementing inline)

### 5. Configuration Command Design

**Decision**: Implement granular `/config` command with subcommands and help system

**Rationale**:
- Users need quick way to update single settings without full wizard
- Subcommand pattern is familiar from Git, Docker, Kubernetes CLIs
- Help text generation built into System.CommandLine
- Validation logic can be shared with setup wizard
- Changes take effect immediately without app restart (where possible)

**Implementation Approach**:
- **Command structure**: `tom config <setting> <value> [options]`
  - `tom config llm-provider openai` - Switch provider
  - `tom config api-key` - Prompt for new API key (masked input)
  - `tom config memory-directory /custom/path` - Change memory location
  - `tom config ssh-key-path ~/.ssh/custom_key` - Override SSH key
  - `tom config show` - Display current configuration (masked secrets)
  - `tom config show --show-secrets` - Display with last 4 characters of secrets
- **Validation**: Reuse validators from setup wizard (DRY principle)
- **Confirmation**: Show old -> new value before saving
- **Rollback guidance**: If new configuration doesn't work, show how to revert

**Alternatives Considered**:
- **Environment variable only**: Rejected - not persistent, requires shell restart
- **Direct file editing**: Rejected - defeats purpose of guided configuration
- **Separate config file format**: Rejected - .NET configuration system is sufficient

**References**:
- System.CommandLine: https://learn.microsoft.com/en-us/dotnet/standard/commandline/
- Command design patterns: Git, Docker, kubectl command structures

### 6. First-Run Detection

**Decision**: Check for presence of required configuration keys; if missing, launch setup automatically

**Rationale**:
- Simple, reliable detection mechanism
- No need for separate "first run" flag file
- Works correctly if user manually deletes configuration
- Handles partial configuration (incomplete previous setup)

**Implementation Approach**:
- **Required configuration keys**: 
  - `Auth:SshKeyPath` or SSH agent configuration
  - `Llm:Provider` (OpenAI or Anthropic)
  - `Llm:ApiKey` or provider-specific key
  - `Storage:MemoryDirectory`
- **Detection logic**: On app startup, before any command execution
  - Check if all required keys are present in merged configuration
  - If any key is missing: Launch setup wizard automatically
  - If all keys present: Execute user's command normally
- **User notification**: "First-time setup detected. Let's get you configured..."
- **Bypass**: Allow environment variable `TEN_SKIP_SETUP=1` to skip auto-setup for CI/testing

**Alternatives Considered**:
- **First-run flag file**: Rejected - extra file to manage, can get out of sync
- **Version-based detection**: Rejected - doesn't handle manual config deletion
- **Always prompt**: Rejected - annoying for configured users

### 7. Setup Wizard State Management

**Decision**: Use in-memory state object with incremental save at each step

**Rationale**:
- Allows back navigation without losing data
- Partial progress is saved (user doesn't lose all work on cancellation)
- Simple to test (no complex state machine)
- Clear separation between UI flow and persistence

**Implementation Approach**:
- **State object**: `SetupProgress` record with current step index, collected values, validation results
- **Step progression**:
  1. Welcome + explanation
  2. SSH key detection + selection
  3. SSH key validation
  4. LLM provider selection
  5. API key entry + validation
  6. Memory directory configuration
  7. Optional settings (logging, retention)
  8. Summary + confirmation
- **Save strategy**: After each step completes successfully, save to User Secrets
- **Navigation**: Allow "Back" option to return to previous step, current step value preserved
- **Cancellation**: Save partial progress, offer to resume later or start fresh

**Alternatives Considered**:
- **All-or-nothing save**: Rejected - user loses all progress on cancellation
- **External state file**: Rejected - unnecessary complexity
- **Step-by-step wizard without state**: Rejected - can't go back, poor UX

### 8. Timeout Configuration

**Decision**: Make all timeout values configurable via appsettings.json with sensible defaults

**Rationale**:
- Different environments have different performance characteristics
- Users on slow networks or systems may need longer timeouts
- Testability - can use shorter timeouts in tests
- Specified in feature requirements (FR-007)

**Implementation Approach**:
- **Configuration section**: `Setup:Timeouts` in appsettings.json
  ```json
  {
    "Setup": {
      "Timeouts": {
        "SshKeyDetectionSeconds": 5,
        "ApiValidationSeconds": 10,
        "TotalSetupMinutes": 2
      }
    }
  }
  ```
- **Usage**: Inject `IConfiguration` into handlers, read timeout values
- **Enforcement**: Use `CancellationTokenSource.CancelAfter()` for operation timeouts
- **User feedback**: Show timeout error with suggestion to adjust if needed

**Alternatives Considered**:
- **Hardcoded timeouts**: Rejected - not flexible for different environments
- **Command-line timeout flags**: Rejected - too granular, confusing for users
- **No timeouts**: Rejected - operations could hang indefinitely

## Key Technologies

### Required (Already in Project)
- **System.CommandLine 2.0**: CLI framework for commands and arguments
- **Spectre.Console 0.51**: Rich terminal UI and interactive prompts
- **FluentValidation 12.0**: Validation rules for configuration inputs
- **Serilog 4.3**: Structured logging throughout setup process
- **Microsoft.Extensions.Configuration.UserSecrets 9.0**: Secure secret storage
- **SSH.NET 2025.0**: SSH agent communication
- **NSec.Cryptography 25.4**: Ed25519 key validation
- **OpenAI SDK**: API key validation for OpenAI
- **Anthropic.SDK**: API key validation for Anthropic

### No New Dependencies Required
All necessary functionality can be implemented with existing project dependencies.

## Performance Considerations

### Expected Performance
- **SSH key detection**: <5s (across all providers)
- **API key format validation**: <10ms
- **API key network validation**: <10s per attempt (3 attempts max with backoff = ~21s worst case)
- **User Secrets write**: <100ms
- **Total setup time**: 2-5 minutes (user-paced, depends on typing speed and decision making)

### Optimization Strategies
- **Parallel SSH detection**: Query all SSH agents simultaneously (Task.WhenAll)
- **Lazy validation**: Only validate SSH key when selected, not during initial detection
- **Cached detection results**: Store detected keys in memory, don't re-detect on back navigation
- **Async I/O**: All file operations use async APIs to prevent blocking

## Security Considerations

### Secret Handling
- **Never log secrets**: Ensure Serilog configuration excludes API keys, SSH key material
- **Masked display**: Only show last 4 characters of API keys in UI
- **Memory safety**: Clear sensitive strings from memory after use (SecureString not used due to .NET Core limitations, but minimal exposure window)
- **File permissions**: User Secrets directory has restrictive permissions by default (user-only access)

### Validation
- **Input sanitization**: Validate all user inputs before use (paths, keys, etc.)
- **Path traversal prevention**: Reject paths containing `..` or absolute paths outside user directory for memory storage
- **Command injection prevention**: Never spawn processes with user input directly

## Testing Strategy

### Unit Tests
- **SSH key detection**: Mock file system and SSH agent responses
- **Validation rules**: Test all FluentValidation validators
- **Configuration management**: Test User Secrets write/read with fallback
- **Retry logic**: Test exponential backoff timing and skip behavior
- **State management**: Test SetupProgress navigation and persistence

### Integration Tests
- **Setup wizard flow**: Test full wizard with in-memory configuration
- **SSH agent integration**: Test against real SSH agents (1Password, Secretive, system) in dev environment
- **API validation**: Test with real API keys (in CI environment with secrets)
- **Config command**: Test all config subcommands with various inputs

### Manual Testing
- **First-run experience**: Test on clean machine with no configuration
- **Reconfiguration**: Test `/setup` command with existing configuration
- **Error scenarios**: Test invalid keys, network failures, permission issues
- **Cross-platform**: Test on macOS and Windows with different terminal emulators

## Open Questions (All Resolved)

All questions from the feature specification have been resolved through clarifications:

1. ✅ **Configuration conflict resolution**: Merge all sources using priority hierarchy; prompt only if conflicts exist
2. ✅ **API validation timeout behavior**: Retry 3 times with exponential backoff; offer skip option after failures
3. ✅ **User Secrets write failure**: Automatic fallback to appsettings.json with security warning
4. ✅ **Setup operation timeouts**: Configurable via appsettings.json (SSH: 5s, API: 10s, Total: 2min)
5. ✅ **Setup resumability**: Always start fresh; show current values as defaults for completed steps

## References

- Feature Specification: [spec.md](./spec.md)
- Ten Second Tom Constitution: [.specify/memory/constitution.md](../../.specify/memory/constitution.md)
- System.CommandLine Docs: https://learn.microsoft.com/en-us/dotnet/standard/commandline/
- Spectre.Console Docs: https://spectreconsole.net/
- .NET Configuration: https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration
- .NET User Secrets: https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets

## Summary

All research areas have been investigated with clear decisions made for each. The implementation approach is well-defined, leveraging existing project dependencies without requiring new packages. The design prioritizes user experience (interactive wizard, clear guidance), security (User Secrets, masked input), and reliability (retry logic, graceful fallback, timeout limits). All open questions from the feature specification have been resolved through the clarifications session.

**Status**: ✅ Ready to proceed to Phase 1 (Design & Contracts)
