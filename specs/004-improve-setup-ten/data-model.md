# Data Model: Guided Setup and Configuration Management

**Feature**: 004-improve-setup-ten  
**Date**: October 9, 2025  
**Status**: Complete

## Overview

This document defines the data model for the guided setup wizard and configuration management system. All entities support the Test-First development approach and are designed for immutability where appropriate using C# records.

## Core Entities

### 1. SetupProgress

Represents the user's current progress through the setup wizard.

**Purpose**: Track wizard state, enable back navigation, support incremental saving

```csharp
public sealed record SetupProgress
{
    public required int CurrentStep { get; init; }
    public required int TotalSteps { get; init; }
    public SshKeyInfo? SelectedSshKey { get; init; }
    public LlmProvider? SelectedProvider { get; init; }
    public string? ApiKey { get; init; }
    public string? MemoryDirectory { get; init; }
    public LogLevel? LogLevel { get; init; }
    public int? RetentionDays { get; init; }
    public Dictionary<int, bool> CompletedSteps { get; init; } = new();
    public DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
```

**Validation Rules** (FR-035):
- `CurrentStep` must be between 1 and `TotalSteps`
- `TotalSteps` must be > 0
- If `SelectedProvider` is set, it must be a valid enum value
- If `MemoryDirectory` is set, it must be a valid path
- `CompletedSteps` keys must be between 1 and `TotalSteps`

**State Transitions**:
- Initial: `CurrentStep = 1`, all optional fields null
- Forward: `CurrentStep++`, relevant field populated
- Backward: `CurrentStep--`, fields preserved
- Complete: `CompletedAt` set, all required fields populated

### 2. SshKeyInfo

Represents a detected or user-specified SSH key.

**Purpose**: Display available keys to user, validate selection, store configuration

```csharp
public sealed record SshKeyInfo
{
    public required string DisplayName { get; init; }
    public required SshKeySource Source { get; init; }
    public required string PublicKey { get; init; }
    public string? FilePath { get; init; }
    public string? AgentName { get; init; }
    public required bool IsEd25519 { get; init; }
    public DateTime DetectedAt { get; init; }
    public ValidationResult ValidationResult { get; init; } = ValidationResult.NotValidated;
}

public enum SshKeySource
{
    SystemAgent,
    OnePasswordAgent,
    SecretiveAgent,
    FileSystem,
    ManualPath
}

public enum ValidationResult
{
    NotValidated,
    Valid,
    InvalidFormat,
    InvalidKeyType,
    FileNotFound
}
```

**Validation Rules** (FR-009):
- `PublicKey` must not be empty
- If `Source` is `FileSystem` or `ManualPath`, `FilePath` must not be null
- If `Source` is agent-based, `AgentName` must not be null
- If `IsEd25519` is false, key is not selectable (project requirement)
- `DisplayName` format: "[AgentName] keyname" or "[File] ~/.ssh/keyname"

**Relationships**:
- Referenced by `SetupProgress.SelectedSshKey`
- Used in SSH key detection results list

### 3. LlmProviderInfo

Represents configuration for an LLM provider.

**Purpose**: Store provider-specific settings, support multiple providers

```csharp
public sealed record LlmProviderInfo
{
    public required LlmProvider Provider { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public string? ApiKey { get; init; }
    public required string ApiKeyPattern { get; init; }
    public string? DefaultModel { get; init; }
    public bool IsConfigured { get; init; }
    public DateTime? LastValidated { get; init; }
}

public enum LlmProvider
{
    OpenAI,
    Anthropic
}
```

**Validation Rules** (FR-014, FR-017):
- `ApiKey` must match `ApiKeyPattern` if provided
- `ApiKeyPattern` is regex for format validation:
  - OpenAI: `^sk-[a-zA-Z0-9]{48,}$` (allows newer formats)
  - Anthropic: `^sk-ant-[a-zA-Z0-9\-]{32,}$`
- `IsConfigured` is true only if `ApiKey` is set and format is valid
- `LastValidated` is set only after successful network validation

**Relationships**:
- Referenced by `SetupProgress.SelectedProvider`
- Referenced by `ConfigurationSettings.LlmProvider`

### 4. ConfigurationSettings

Represents the complete application configuration.

**Purpose**: Centralize all settings, provide single source of truth, support serialization

```csharp
public sealed record ConfigurationSettings
{
    // SSH Authentication
    public SshConfiguration Ssh { get; init; } = new();
    
    // LLM Provider
    public LlmConfiguration Llm { get; init; } = new();
    
    // Storage
    public StorageConfiguration Storage { get; init; } = new();
    
    // Optional Settings
    public OptionalConfiguration Optional { get; init; } = new();
    
    // Metadata
    public DateTime CreatedAt { get; init; }
    public DateTime? LastModifiedAt { get; init; }
    public string ConfigurationVersion { get; init; } = "1.0";
}

public sealed record SshConfiguration
{
    public string? KeyPath { get; init; }
    public SshKeySource? KeySource { get; init; }
    public string? AgentSocketPath { get; init; }
}

public sealed record LlmConfiguration
{
    public LlmProvider Provider { get; init; }
    public string? ApiKey { get; init; }
    public string? Model { get; init; }
}

public sealed record StorageConfiguration
{
    public required string MemoryDirectory { get; init; }
    public bool CreateIfMissing { get; init; } = true;
}

public sealed record OptionalConfiguration
{
    public LogLevel LogLevel { get; init; } = LogLevel.Information;
    public int RetentionDays { get; init; } = 30;
    public bool EnableTelemetry { get; init; } = false;
}
```

**Validation Rules** (FR-019, FR-035):
- `Ssh.KeyPath` must be valid file path if provided
- `Llm.Provider` must be valid enum value
- `Llm.ApiKey` must match provider's expected format
- `Storage.MemoryDirectory` must be valid, accessible path
- `Optional.RetentionDays` must be > 0
- `Optional.LogLevel` must be valid enum value

**Relationships**:
- Root configuration object
- Serialized to User Secrets or appsettings.json
- Loaded from IConfiguration at runtime

### 5. SetupCommand

Command to initiate or re-run the setup wizard.

**Purpose**: CQRS command pattern, encapsulate setup intent

```csharp
public sealed record SetupCommand : IRequest<Result<ConfigurationSettings>>
{
    public bool Force { get; init; } // Skip existing configuration check
    public bool NonInteractive { get; init; } // Use defaults, skip prompts (for testing)
    public ConfigurationSettings? ExistingConfiguration { get; init; }
}
```

**Validation Rules**:
- No required fields (all optional for flexibility)
- `NonInteractive` requires valid `ExistingConfiguration` if set

**Handler**: `SetupCommandHandler`

### 6. ConfigCommand

Command to modify individual configuration settings.

**Purpose**: CQRS command pattern, granular configuration updates

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
    Show,           // Display current configuration
    Set,            // Update a setting
    Reset,          // Reset to defaults
    Validate        // Validate current configuration
}
```

**Validation Rules** (FR-023, FR-025):
- If `Action` is `Set`, `SettingName` and `SettingValue` must not be null
- `SettingName` must be one of: "llm-provider", "api-key", "memory-directory", "ssh-key-path", "log-level", "retention-days"
- `SettingValue` must pass setting-specific validation

**Handler**: `ConfigCommandHandler`

### 7. GetCurrentConfigQuery

Query to retrieve current configuration.

**Purpose**: CQRS query pattern, read-only configuration access

```csharp
public sealed record GetCurrentConfigQuery : IRequest<Result<ConfigurationSettings>>
{
    public bool IncludeSecrets { get; init; }
    public bool ValidateCompleteness { get; init; } = true;
}
```

**Handler**: `GetCurrentConfigQueryHandler`

### 8. ValidateSshKeyQuery

Query to validate an SSH key.

**Purpose**: CQRS query pattern, reusable validation logic

```csharp
public sealed record ValidateSshKeyQuery : IRequest<Result<SshKeyInfo>>
{
    public required string PublicKeyOrPath { get; init; }
    public bool PerformDeepValidation { get; init; } = true;
}
```

**Validation Rules** (FR-009):
- `PublicKeyOrPath` must not be empty
- If path, file must exist
- Public key must be valid ED25519 format
- If `PerformDeepValidation`, check key can be parsed by NSec.Cryptography

**Handler**: `ValidateSshKeyQueryHandler`

### 9. DetectSshKeysQuery

Query to discover available SSH keys.

**Purpose**: CQRS query pattern, encapsulate detection logic

```csharp
public sealed record DetectSshKeysQuery : IRequest<Result<IReadOnlyList<SshKeyInfo>>>
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool IncludeFileSystemKeys { get; init; } = true;
    public bool IncludeAgentKeys { get; init; } = true;
}
```

**Handler**: `DetectSshKeysQueryHandler`

### 10. ValidateApiKeyQuery

Query to validate an LLM provider API key.

**Purpose**: CQRS query pattern, reusable API validation

```csharp
public sealed record ValidateApiKeyQuery : IRequest<Result<ApiKeyValidationResult>>
{
    public required LlmProvider Provider { get; init; }
    public required string ApiKey { get; init; }
    public bool PerformNetworkValidation { get; init; } = true;
    public int MaxRetries { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed record ApiKeyValidationResult
{
    public required bool IsValid { get; init; }
    public required bool FormatValid { get; init; }
    public bool? NetworkValid { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
}
```

**Validation Rules** (FR-014, FR-017):
- `ApiKey` must not be empty
- Format validation always performed
- Network validation optional, with retry logic
- Exponential backoff: 1s, 2s, 4s between attempts

**Handler**: `ValidateApiKeyQueryHandler`

## Value Objects

### TimeoutConfiguration

Configuration for setup operation timeouts.

```csharp
public sealed record TimeoutConfiguration
{
    public TimeSpan SshKeyDetection { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan ApiValidation { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan TotalSetup { get; init; } = TimeSpan.FromMinutes(2);
}
```

**Validation Rules** (FR-007):
- All timeouts must be positive
- Loaded from `Setup:Timeouts` section in appsettings.json

### RetryPolicy

Configuration for retry behavior.

```csharp
public sealed record RetryPolicy
{
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan InitialDelay { get; init; } = TimeSpan.FromSeconds(1);
    public double BackoffMultiplier { get; init; } = 2.0;
    public TimeSpan MaxDelay { get; init; } = TimeSpan.FromSeconds(10);
}
```

**Validation Rules**:
- `MaxAttempts` must be > 0
- `InitialDelay` must be positive
- `BackoffMultiplier` must be >= 1.0

## Domain Services

### ISshKeyDetector

Interface for SSH key detection across multiple sources.

```csharp
public interface ISshKeyDetector
{
    Task<Result<IReadOnlyList<SshKeyInfo>>> DetectKeysAsync(
        DetectSshKeysQuery query,
        CancellationToken cancellationToken = default);
}
```

**Implementations**:
- `SshAgentKeyDetector`: Detects keys from SSH agents
- `FileSystemKeyDetector`: Detects keys from ~/.ssh directory
- `CompositeSshKeyDetector`: Combines multiple detectors

### ISshKeyValidator

Interface for SSH key validation.

```csharp
public interface ISshKeyValidator
{
    Task<Result<SshKeyInfo>> ValidateAsync(
        ValidateSshKeyQuery query,
        CancellationToken cancellationToken = default);
}
```

**Implementation**: `Ed25519KeyValidator`

### IApiKeyValidator

Interface for LLM provider API key validation.

```csharp
public interface IApiKeyValidator
{
    Task<Result<ApiKeyValidationResult>> ValidateAsync(
        ValidateApiKeyQuery query,
        CancellationToken cancellationToken = default);
}
```

**Implementations**:
- `OpenAIApiKeyValidator`
- `AnthropicApiKeyValidator`
- `CompositeApiKeyValidator`: Routes to appropriate validator

### IConfigurationWriter

Interface for writing configuration to User Secrets or appsettings.json.

```csharp
public interface IConfigurationWriter
{
    Task<Result<Unit>> WriteAsync(
        ConfigurationSettings settings,
        CancellationToken cancellationToken = default);
    
    Task<Result<ConfigurationSettings>> ReadAsync(
        CancellationToken cancellationToken = default);
}
```

**Implementations**:
- `UserSecretsConfigurationWriter`: Primary implementation
- `AppSettingsConfigurationWriter`: Fallback implementation
- `CompositeConfigurationWriter`: Tries User Secrets, falls back to app settings

## Relationships Diagram

```
SetupCommand
    └─> SetupCommandHandler
        ├─> DetectSshKeysQuery
        │   └─> ISshKeyDetector
        │       └─> SshKeyInfo[]
        ├─> ValidateSshKeyQuery
        │   └─> ISshKeyValidator
        │       └─> SshKeyInfo
        ├─> ValidateApiKeyQuery
        │   └─> IApiKeyValidator
        │       └─> ApiKeyValidationResult
        └─> IConfigurationWriter
            └─> ConfigurationSettings

ConfigCommand
    └─> ConfigCommandHandler
        ├─> GetCurrentConfigQuery
        │   └─> ConfigurationSettings
        ├─> Validators (reused from Setup)
        └─> IConfigurationWriter
            └─> ConfigurationSettings

SetupProgress
    ├─> SshKeyInfo (SelectedSshKey)
    └─> LlmProvider (SelectedProvider)

ConfigurationSettings
    ├─> SshConfiguration
    ├─> LlmConfiguration
    ├─> StorageConfiguration
    └─> OptionalConfiguration
```

## Data Flow

### Setup Wizard Flow

1. **User triggers setup** → `SetupCommand` created
2. **Handler executes**:
   - Load existing configuration (if any) via `GetCurrentConfigQuery`
   - Create `SetupProgress` with current values as defaults
   - For each step:
     - Detect SSH keys via `DetectSshKeysQuery`
     - Display options to user (Spectre.Console)
     - Validate selection via `ValidateSshKeyQuery`
     - Update `SetupProgress`
     - Save incrementally via `IConfigurationWriter`
3. **Complete setup** → Final `ConfigurationSettings` saved

### Config Command Flow

1. **User runs config command** → `ConfigCommand` created
2. **Handler executes**:
   - Load current configuration via `GetCurrentConfigQuery`
   - If `Action = Show`: Display configuration (masked)
   - If `Action = Set`:
     - Validate new value
     - Update `ConfigurationSettings`
     - Save via `IConfigurationWriter`
     - Display confirmation
3. **Return result**

## Persistence

### User Secrets Location

**macOS/Linux**: `~/.microsoft/usersecrets/ten-second-tom-secrets/secrets.json`  
**Windows**: `%APPDATA%\Microsoft\UserSecrets\ten-second-tom-secrets\secrets.json`

### JSON Schema

```json
{
  "Ssh": {
    "KeyPath": "/Users/username/.ssh/id_ed25519",
    "KeySource": "FileSystem",
    "AgentSocketPath": null
  },
  "Llm": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "Model": "gpt-4"
  },
  "Storage": {
    "MemoryDirectory": "/Users/username/.memory/ten-second-tom",
    "CreateIfMissing": true
  },
  "Optional": {
    "LogLevel": "Information",
    "RetentionDays": 30,
    "EnableTelemetry": false
  },
  "CreatedAt": "2025-10-09T10:30:00Z",
  "LastModifiedAt": "2025-10-09T10:35:00Z",
  "ConfigurationVersion": "1.0"
}
```

## Validation Summary

All validation rules are implemented using FluentValidation and referenced in functional requirements:

- **FR-001**: First-run detection via configuration completeness check
- **FR-007**: Timeout enforcement via `TimeoutConfiguration`
- **FR-009**: SSH key ED25519 validation via `ISshKeyValidator`
- **FR-014**: API key format validation via regex patterns
- **FR-017**: API key network validation with retry via `IApiKeyValidator`
- **FR-019**: Directory path validation via file system checks
- **FR-025**: Config command value validation via reused validators
- **FR-028**: Fallback storage handling via `CompositeConfigurationWriter`
- **FR-035**: Immediate input validation at each wizard step
- **FR-040**: Configuration merge via standard .NET configuration priority

## Testing Considerations

- All entities are immutable records → easy to test, predictable behavior
- Commands/Queries follow CQRS → handlers are independently testable
- Interfaces for services → mockable for unit tests
- Validators are separate → testable in isolation
- Result types → clear success/failure testing

**Status**: ✅ Ready for contract generation (Phase 1 continuation)
