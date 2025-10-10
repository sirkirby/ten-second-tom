# SetupCommand Contract

**Feature**: Guided Setup Wizard  
**Type**: Command (Mutation)  
**Handler**: SetupCommandHandler

## Purpose

Initiates or re-runs the guided setup wizard to collect and configure all application settings.

## Request

```csharp
public sealed record SetupCommand : IRequest<Result<ConfigurationSettings>>
{
    public bool Force { get; init; }
    public bool NonInteractive { get; init; }
    public ConfigurationSettings? ExistingConfiguration { get; init; }
}
```

### Parameters

| Parameter | Type | Required | Description | Default |
|-----------|------|----------|-------------|---------|
| Force | bool | No | Skip existing configuration check and always run full wizard | false |
| NonInteractive | bool | No | Use defaults, skip prompts (for automated testing) | false |
| ExistingConfiguration | ConfigurationSettings | No | Pre-existing configuration to use as defaults | null |

### Validation Rules

- If `NonInteractive` is true and `ExistingConfiguration` is null, use system defaults
- `Force` and `NonInteractive` can both be true (forced non-interactive setup with defaults)

## Response

### Success Response

```csharp
Result<ConfigurationSettings>.Success(configurationSettings)
```

**ConfigurationSettings** contains:
- Complete SSH configuration (key path, source, agent)
- Complete LLM configuration (provider, API key, model)
- Complete storage configuration (memory directory)
- Optional settings (log level, retention days)
- Metadata (created/modified timestamps, version)

### Failure Responses

| Error Code | Message | Scenario |
|------------|---------|----------|
| `Setup.Cancelled` | "Setup was cancelled by user" | User pressed Ctrl+C or chose to cancel |
| `Setup.Timeout` | "Setup exceeded maximum duration of {timeout}" | Total setup time exceeded configured limit |
| `Setup.ValidationFailed` | "Configuration validation failed: {details}" | Final configuration is invalid or incomplete |
| `Setup.SaveFailed` | "Failed to save configuration: {reason}" | User Secrets and appsettings.json both failed |
| `Setup.SshKeyDetectionFailed` | "Failed to detect SSH keys: {reason}" | SSH key detection timed out or failed |
| `Setup.ApiKeyValidationFailed` | "API key validation failed: {reason}" | API key format or network validation failed after all retries |

## Behavior Specifications

### Scenario: First-Time Setup (FR-001)

**Given**: No existing configuration  
**When**: SetupCommand is executed  
**Then**:
- Wizard displays welcome message
- All 8 setup steps are presented in order
- Each step validates input before proceeding
- Configuration is saved to User Secrets
- Success response contains complete configuration

### Scenario: Re-Running Setup with Existing Configuration (FR-002, FR-006a)

**Given**: Valid existing configuration  
**When**: SetupCommand is executed  
**Then**:
- Wizard displays "Reconfiguration" message
- All 8 setup steps are presented from the beginning
- Existing values are shown as defaults for each step
- User can keep existing values or change them
- Configuration is updated and saved
- Success response contains updated configuration

### Scenario: Forced Setup (FR-002)

**Given**: Any configuration state  
**When**: SetupCommand with `Force = true` is executed  
**Then**:
- Existing configuration check is skipped
- Wizard runs full setup process
- Previous configuration is overwritten
- Success response contains new configuration

### Scenario: Non-Interactive Setup for Testing

**Given**: Test environment  
**When**: SetupCommand with `NonInteractive = true` is executed  
**Then**:
- All prompts are skipped
- Default values are used for all settings
- Validation still occurs
- Configuration is saved
- Success response contains default configuration

### Scenario: Setup Cancellation (FR-006)

**Given**: Setup wizard is running at step 3  
**When**: User presses Ctrl+C  
**Then**:
- Wizard displays cancellation confirmation
- Partial progress from steps 1-2 is saved
- Error response with `Setup.Cancelled` is returned
- User can re-run setup to continue

### Scenario: Setup Timeout (FR-007)

**Given**: Setup wizard is running  
**When**: Total setup time exceeds configured timeout (default 2 minutes)  
**Then**:
- Wizard displays timeout error
- Partial progress is saved
- Error response with `Setup.Timeout` is returned
- Error message includes timeout value and suggests adjustment

### Scenario: SSH Key Detection Timeout (FR-007, FR-008)

**Given**: Setup wizard reaches SSH key step  
**When**: SSH key detection exceeds 5 seconds  
**Then**:
- Detection is cancelled
- User is shown timeout error
- User can retry or manually enter key path
- Wizard does not fail completely

### Scenario: API Key Validation with Retry (FR-017)

**Given**: Setup wizard reaches API key validation step  
**When**: Network validation fails transiently  
**Then**:
- System retries up to 3 times with exponential backoff (1s, 2s, 4s)
- User sees retry progress indication
- If all retries fail, user is offered skip option
- If skipped, format validation result is used
- Wizard continues to next step

### Scenario: User Secrets Write Failure Fallback (FR-028)

**Given**: Setup wizard completes all steps  
**When**: Writing to User Secrets fails (permissions, disk space)  
**Then**:
- System automatically attempts to write to appsettings.json
- Prominent security warning is displayed
- Success response is returned with configuration
- Log entry captures fallback event

## Side Effects

### Data Changes
- User Secrets JSON file is created or updated
- Fallback: appsettings.json is updated if User Secrets fails
- Memory directory is created if it doesn't exist (with user confirmation)

### External Interactions
- SSH agents are queried for available keys (read-only)
- File system is scanned for SSH keys in ~/.ssh (read-only)
- LLM provider APIs are called for key validation (lightweight, no usage cost)
- File system permissions are checked for memory directory

### Observability
- Setup start event logged (Information level)
- Each step completion logged (Debug level)
- Validation failures logged (Warning level)
- Configuration save location logged (Information level)
- Fallback to appsettings.json logged (Warning level)
- Setup completion logged (Information level)
- **No secrets are ever logged** (constitutional requirement)

## Performance Requirements

- SSH key detection: <5 seconds (configurable)
- API key format validation: <10ms
- API key network validation: <10s per attempt (configurable)
- Total setup time: <2 minutes (configurable, user-paced)
- Configuration save: <100ms

## Security Requirements (FR-028, FR-029, FR-030)

- API keys are masked during input (only asterisks shown)
- SSH private key content is never accessed or displayed
- Configuration display shows only last 4 characters of secrets
- Secrets are never logged, even in debug mode
- User Secrets files have user-only permissions
- Fallback appsettings.json displays security warning

## Test Requirements

### Unit Tests
- [ ] Constructor initializes with default values
- [ ] Force flag behaves correctly
- [ ] NonInteractive flag behaves correctly
- [ ] ExistingConfiguration is used as defaults when provided

### Integration Tests
- [ ] Full setup flow with valid inputs completes successfully
- [ ] Setup with existing configuration shows defaults correctly
- [ ] Forced setup overwrites existing configuration
- [ ] Non-interactive setup uses defaults
- [ ] Cancellation at each step saves partial progress
- [ ] Timeout enforcement works correctly
- [ ] SSH key detection timeout triggers retry/manual entry
- [ ] API key validation retry logic works with exponential backoff
- [ ] User Secrets fallback to appsettings.json works
- [ ] Invalid inputs are rejected with clear error messages
- [ ] Memory directory creation works with user confirmation
- [ ] Secrets masking works in all UI displays

## Dependencies

- `Spectre.Console`: Interactive prompts and UI
- `FluentValidation`: Input validation
- `IConfigurationWriter`: Configuration persistence
- `ISshKeyDetector`: SSH key discovery
- `ISshKeyValidator`: SSH key validation
- `IApiKeyValidator`: API key validation
- `Serilog`: Logging
