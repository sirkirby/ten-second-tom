# ConfigCommand Contract

**Feature**: Configuration Management  
**Type**: Command (Mutation/Query hybrid)  
**Handler**: ConfigCommandHandler

## Purpose

Allows users to view or modify individual configuration settings without running the full setup wizard.

## Request

```csharp
public sealed record ConfigCommand : IRequest<Result<ConfigurationSettings>>
{
    public ConfigAction Action { get; init; } = ConfigAction.Show;
    public string? SettingName { get; init; }
    public string? SettingValue { get; init; }
    public bool ShowSecrets { get; init; }
}

public enum ConfigAction
{
    Show,
    Set,
    Reset,
    Validate
}
```

### Parameters

| Parameter | Type | Required | Description | Default |
|-----------|------|----------|-------------|---------|
| Action | ConfigAction | No | Action to perform | Show |
| SettingName | string | Conditional | Setting name to modify (required for Set action) | null |
| SettingValue | string | Conditional | New value for setting (required for Set action) | null |
| ShowSecrets | bool | No | Display last 4 chars of secrets (for Show action) | false |

### Valid Setting Names (FR-023)

- `llm-provider`: LLM provider (OpenAI, Anthropic)
- `api-key`: API key for current provider
- `memory-directory`: Directory for storing memories
- `ssh-key-path`: Path to SSH key file
- `log-level`: Logging level (Debug, Information, Warning, Error)
- `retention-days`: Days to retain memory entries

### Validation Rules (FR-025)

- If `Action` is `Set`, `SettingName` must not be null or empty
- If `Action` is `Set`, `SettingValue` must not be null or empty
- `SettingName` must be one of the valid setting names (case-insensitive)
- `SettingValue` must pass setting-specific validation:
  - `llm-provider`: Must be "OpenAI" or "Anthropic"
  - `api-key`: Must match provider's API key format
  - `memory-directory`: Must be valid, accessible directory path
  - `ssh-key-path`: Must be valid file path to ED25519 key
  - `log-level`: Must be valid LogLevel enum value
  - `retention-days`: Must be integer > 0

## Response

### Success Response

```csharp
Result<ConfigurationSettings>.Success(configurationSettings)
```

**ConfigurationSettings** contains current configuration (after modification for Set/Reset actions).

### Failure Responses

| Error Code | Message | Scenario |
|------------|---------|----------|
| `Config.InvalidSettingName` | "Unknown setting: {name}" | SettingName is not recognized |
| `Config.MissingSettingName` | "Setting name is required for Set action" | SettingName is null for Set |
| `Config.MissingSettingValue` | "Setting value is required for Set action" | SettingValue is null for Set |
| `Config.ValidationFailed` | "Invalid value for {setting}: {reason}" | Value fails setting-specific validation |
| `Config.SaveFailed` | "Failed to save configuration: {reason}" | Configuration write failed |
| `Config.NotConfigured` | "No configuration found. Run /setup first" | No configuration exists for Show/Set |
| `Config.ApiKeyRequired` | "API key required for provider {provider}" | Switching provider without API key |

## Behavior Specifications

### Scenario: Show Current Configuration (FR-024, FR-027)

**Given**: Valid configuration exists  
**When**: ConfigCommand with `Action = Show` is executed  
**Then**:
- Current configuration is loaded
- All settings are displayed in readable format
- Secrets are masked (e.g., `sk-...xyz1234`)
- Success response contains configuration
- Exit code 0

### Scenario: Show Configuration with Secrets (FR-029)

**Given**: Valid configuration exists  
**When**: ConfigCommand with `Action = Show, ShowSecrets = true` is executed  
**Then**:
- Current configuration is loaded
- All settings are displayed
- Secrets show last 4 characters (e.g., `sk-...1234`)
- Full secrets are never displayed
- Success response contains configuration
- Exit code 0

### Scenario: Change LLM Provider (FR-008, FR-023)

**Given**: Configuration with OpenAI provider  
**When**: ConfigCommand with `Action = Set, SettingName = "llm-provider", SettingValue = "Anthropic"`  
**Then**:
- Current configuration is loaded
- Provider is validated (Anthropic is valid)
- User is prompted for Anthropic API key (if not already set)
- API key is validated (format + optional network)
- Configuration is updated
- Configuration is saved
- Confirmation message displayed showing old → new
- Success response contains updated configuration
- Exit code 0

### Scenario: Update Memory Directory (FR-009, FR-019, FR-020)

**Given**: Configuration with default memory directory  
**When**: ConfigCommand with `Action = Set, SettingName = "memory-directory", SettingValue = "/custom/path"`  
**Then**:
- Path syntax is validated
- Path existence is checked
- If path doesn't exist, user is asked to create it
- Write permissions are verified
- Configuration is updated and saved
- Confirmation message displayed
- Success response contains updated configuration
- Exit code 0

### Scenario: Update SSH Key Path (FR-012)

**Given**: Configuration with SSH agent key  
**When**: ConfigCommand with `Action = Set, SettingName = "ssh-key-path", SettingValue = "~/.ssh/custom_key"`  
**Then**:
- Path is expanded (~/ resolved)
- File existence is verified
- Key file is read and validated as ED25519
- Configuration is updated and saved
- Confirmation message displayed
- Success response contains updated configuration
- Exit code 0

### Scenario: Invalid Setting Name

**Given**: Any configuration state  
**When**: ConfigCommand with `Action = Set, SettingName = "invalid-setting", SettingValue = "value"`  
**Then**:
- Validation fails
- Error response with `Config.InvalidSettingName`
- Help text is displayed showing valid setting names
- Configuration is not modified
- Exit code 1

### Scenario: Invalid Setting Value

**Given**: Valid configuration  
**When**: ConfigCommand with `Action = Set, SettingName = "llm-provider", SettingValue = "InvalidProvider"`  
**Then**:
- Value validation fails
- Error response with `Config.ValidationFailed`
- Error message explains valid values
- Configuration is not modified
- Exit code 1

### Scenario: Reset to Defaults

**Given**: Modified configuration  
**When**: ConfigCommand with `Action = Reset` is executed  
**Then**:
- User is prompted for confirmation
- Configuration is reset to defaults
- User Secrets file is cleared
- Success response with default configuration
- Confirmation message displayed
- Exit code 0

### Scenario: Validate Current Configuration

**Given**: Any configuration state  
**When**: ConfigCommand with `Action = Validate` is executed  
**Then**:
- All settings are validated
- SSH key accessibility is checked
- API key format is verified
- Directory paths are checked
- Validation report is displayed
- If valid: Success response, exit code 0
- If invalid: Failure response with details, exit code 1

### Scenario: No Configuration Exists

**Given**: No configuration exists (first run)  
**When**: ConfigCommand with `Action = Show` is executed  
**Then**:
- Error response with `Config.NotConfigured`
- Guidance message: "Run `tom setup` to configure"
- Exit code 1

## Side Effects

### Data Changes
- User Secrets JSON file is updated (for Set, Reset actions)
- Fallback: appsettings.json is updated if User Secrets fails
- Memory directory may be created (for memory-directory setting)

### External Interactions
- SSH key file is read and validated (for ssh-key-path setting)
- LLM provider API may be called (for api-key setting validation)
- File system is checked for directory paths

### Observability
- Config command invocation logged (Debug level)
- Setting changes logged: `"{setting}" changed from {old} to {new}"` (Information level)
- Validation failures logged (Warning level)
- Configuration save logged (Debug level)
- **No secrets are logged** (constitutional requirement)

## Performance Requirements (FR-035)

- Show action: <50ms
- Set action with validation: <500ms (excluding network validation)
- Set action with API validation: <11s (10s validation + 1s overhead)
- Reset action: <100ms
- Validate action: <500ms (excluding network checks)

## Security Requirements (FR-029, FR-030)

- Secrets are masked in Show output (`sk-...1234` format)
- Full secrets never displayed, even with ShowSecrets flag
- Secrets are never logged
- API key input is masked (when prompted)
- Configuration file permissions are preserved

## CLI Output Format

### Show Output (Human-Readable)
```
Current Configuration:

SSH Authentication:
  Key Path: ~/.ssh/id_ed25519
  Key Source: FileSystem

LLM Provider:
  Provider: OpenAI
  API Key: sk-...1234
  Model: gpt-4

Storage:
  Memory Directory: ~/.memory/ten-second-tom

Optional Settings:
  Log Level: Information
  Retention Days: 30

Last Modified: 2025-10-09 10:35:00
```

### Show Output (JSON)
```json
{
  "ssh": {
    "keyPath": "~/.ssh/id_ed25519",
    "keySource": "FileSystem"
  },
  "llm": {
    "provider": "OpenAI",
    "apiKey": "sk-...1234",
    "model": "gpt-4"
  },
  "storage": {
    "memoryDirectory": "~/.memory/ten-second-tom"
  },
  "optional": {
    "logLevel": "Information",
    "retentionDays": 30
  },
  "lastModified": "2025-10-09T10:35:00Z"
}
```

### Set Confirmation Output
```
✓ Configuration updated

llm-provider: OpenAI → Anthropic

API key for Anthropic:  [masked input]
✓ API key validated

Changes saved successfully.
```

## Test Requirements

### Unit Tests
- [ ] Show action loads and displays configuration correctly
- [ ] ShowSecrets flag displays last 4 characters of secrets
- [ ] Set action validates setting name
- [ ] Set action validates setting value
- [ ] Invalid setting name returns appropriate error
- [ ] Invalid setting value returns appropriate error
- [ ] Reset action clears configuration
- [ ] Validate action checks all settings

### Integration Tests
- [ ] Show command displays complete configuration with masked secrets
- [ ] Set llm-provider prompts for API key and validates
- [ ] Set memory-directory creates directory if missing
- [ ] Set ssh-key-path validates ED25519 key
- [ ] Set log-level updates logging configuration
- [ ] Set retention-days updates storage configuration
- [ ] Invalid setting name shows help with valid names
- [ ] Invalid setting value shows validation error
- [ ] Reset command clears User Secrets file
- [ ] Validate command reports all configuration issues
- [ ] Configuration saves to User Secrets successfully
- [ ] Fallback to appsettings.json works if User Secrets fails
- [ ] No configuration error shows setup guidance

## Dependencies

- `Spectre.Console`: Interactive prompts and formatted output
- `FluentValidation`: Setting value validation
- `IConfigurationWriter`: Configuration persistence
- `IConfigurationReader`: Configuration loading
- `ISshKeyValidator`: SSH key validation (for ssh-key-path)
- `IApiKeyValidator`: API key validation (for api-key)
- `Serilog`: Logging
