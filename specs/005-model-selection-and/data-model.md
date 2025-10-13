# Data Model: Model Selection and Configuration

**Feature**: 005-model-selection-and  
**Date**: 2025-10-11  
**Status**: Complete

## Overview

This document defines the entities, relationships, and validation rules for the model selection and configuration feature. The data model extends the existing `ConfigurationSettings` structure with model metadata and validation.

## Core Entities

### SupportedModel

**Purpose**: Represents a curated LLM model option with metadata for display and validation.

**Properties**:

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Id` | `string` | Yes | Unique model identifier used by the provider API (e.g., "gpt-4o-mini", "claude-3-5-haiku-20241022") |
| `DisplayName` | `string` | Yes | Human-readable name shown in UI (e.g., "GPT-4o Mini", "Claude 3.5 Haiku") |
| `Provider` | `LlmProvider` | Yes | The LLM provider this model belongs to (OpenAI or Anthropic) |
| `CostTier` | `string` | Yes | Cost category: "Budget", "Balanced", or "Premium" |
| `Description` | `string` | Yes | Brief description of model capabilities and use case (max 100 chars) |
| `IsDefault` | `bool` | No | Whether this is the default model for its provider (default: false) |

**Validation Rules**:

- `Id` must not be null or whitespace
- `Id` must be unique across all models
- `DisplayName` must not be null or whitespace
- `Provider` must be a valid enum value
- `CostTier` must be one of: "Budget", "Balanced", "Premium"
- `Description` must not be null or whitespace and max 100 characters
- Exactly one model per provider must have `IsDefault = true`

**Example**:

```csharp
new SupportedModel
{
    Id = "gpt-4o-mini",
    DisplayName = "GPT-4o Mini",
    Provider = LlmProvider.OpenAI,
    CostTier = "Budget",
    Description = "Fast and economical for most tasks",
    IsDefault = true
}
```

---

### ModelRegistry (Static)

**Purpose**: Centralized registry of all supported models, providing validation and lookup.

**Static Properties**:

| Property | Type | Description |
|----------|------|-------------|
| `OpenAIModels` | `IReadOnlyList<SupportedModel>` | All supported OpenAI models (3-4 models) |
| `AnthropicModels` | `IReadOnlyList<SupportedModel>` | All supported Anthropic models (3-4 models) |
| `AllModels` | `IReadOnlyList<SupportedModel>` | Combined list of all models (computed property) |

**Static Methods**:

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetDefault(LlmProvider provider)` | `SupportedModel` | Returns the default model for the given provider |
| `IsValid(string modelId, LlmProvider provider)` | `bool` | Validates if model ID is supported for provider |
| `GetById(string modelId)` | `SupportedModel?` | Retrieves model by ID, returns null if not found |
| `GetByProvider(LlmProvider provider)` | `IReadOnlyList<SupportedModel>` | Returns all models for given provider |

**Validation Rules**:

- At least 1 model per provider
- No duplicate model IDs across all providers
- Exactly one default model per provider

---

### LlmConfiguration (Extended)

**Purpose**: Existing configuration record, already contains Model property.

**Properties** (unchanged):

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `Provider` | `LlmProvider` | Yes | Selected LLM provider |
| `ApiKey` | `string?` | Yes | API key for authentication |
| `Model` | `string?` | No | Selected model identifier (NEW USAGE) |

**Validation Rules** (new):

- If `Model` is specified, it must be valid for the selected `Provider` (via `ModelRegistry`)
- If `Model` is null/empty, default for `Provider` is used at runtime
- `Model` and `Provider` must be compatible (validated by `ModelRegistry.IsValid`)

**State Transitions**:

1. **Unset** → User selects provider and model during setup → **Set and Valid**
2. **Set and Valid** → User changes model via `tom config llm` → **Set and Valid** (new model)
3. **Set but Invalid** → Validation error at startup → User must run `tom config llm` → **Set and Valid**

---

### ConfigAction (Existing Enum)

**Purpose**: Defines actions for ConfigCommand.

**Values**:

- `Show`: Display current configuration
- `Set`: Update a configuration value
- `Reset`: Reset configuration to defaults
- `Validate`: Validate current configuration

**Extension**: No changes to enum itself, but `Set` action now supports `llm` as a setting name for interactive model selection.

---

### ConfigCommand (Extended)

**Purpose**: Existing command for configuration management, extended to support `tom config llm`.

**Properties** (unchanged):

| Property | Type | Description |
|----------|------|-------------|
| `Action` | `ConfigAction` | Action to perform (default: Show) |
| `SettingName` | `string?` | Setting to modify (e.g., "llm", "llm-provider", "api-key") |
| `SettingValue` | `string?` | New value for the setting |
| `ShowSecrets` | `bool` | Whether to display last 4 chars of secrets |

**New Usage Pattern**:

```bash
tom config llm                    # Interactive: select provider, then model
tom config show                   # Show current model in LLM section
tom config set llm-model gpt-4o   # Direct set (advanced, bypasses validation UI)
```

When `SettingName == "llm"`, the handler triggers interactive provider + model selection flow.

---

## Relationships

```text
┌─────────────────────────┐
│   ModelRegistry         │
│   (Static Singleton)    │
├─────────────────────────┤
│ + OpenAIModels          │
│ + AnthropicModels       │
│ + GetDefault()          │
│ + IsValid()             │
└───────────┬─────────────┘
            │
            │ contains
            │
            ▼
┌─────────────────────────┐
│   SupportedModel        │
│   (Immutable Record)    │
├─────────────────────────┤
│ + Id: string            │
│ + DisplayName: string   │
│ + Provider: LlmProvider │
│ + CostTier: string      │
│ + Description: string   │
│ + IsDefault: bool       │
└───────────┬─────────────┘
            │
            │ validates
            │
            ▼
┌─────────────────────────┐
│  LlmConfiguration       │
│  (Existing)             │
├─────────────────────────┤
│ + Provider: LlmProvider │
│ + ApiKey: string?       │
│ + Model: string?        │──────┐
└─────────────────────────┘      │
                                  │
                                  │ part of
                                  │
                                  ▼
┌──────────────────────────────────────┐
│  ConfigurationSettings               │
│  (Existing)                          │
├──────────────────────────────────────┤
│ + Ssh: SshConfiguration              │
│ + Llm: LlmConfiguration              │
│ + Storage: StorageConfiguration      │
│ + Optional: OptionalConfiguration    │
│ + CreatedAt: DateTime                │
│ + LastModifiedAt: DateTime?          │
│ + ConfigurationVersion: string       │
└──────────────────────────────────────┘
```

**Key Relationships**:

1. **ModelRegistry contains SupportedModels**: One-to-many static relationship
2. **SupportedModel validates LlmConfiguration.Model**: Validation relationship
3. **LlmConfiguration is part of ConfigurationSettings**: Composition
4. **ConfigCommand triggers model selection**: Behavioral relationship

---

## Data Flow

### 1. Guided Setup Flow

```text
User runs 'tom setup'
      │
      ▼
SpectreConsoleSetupWizard.PromptForLlmProviderAsync()
      │ returns: LlmProvider
      ▼
SpectreConsoleSetupWizard.PromptForModelAsync(provider)
      │ uses: ModelRegistry.GetByProvider(provider)
      │ displays: SupportedModel list with descriptions
      │ returns: SupportedModel
      ▼
SetupCommandHandler creates ConfigurationSettings
      │ sets: Llm.Provider, Llm.Model, Llm.ApiKey
      ▼
UserSecretsStorageService.SaveAsync(settings)
      │ persists: Llm:Model = selected.Id
      ▼
Configuration saved to user secrets
```

### 2. Config LLM Command Flow

```text
User runs 'tom config llm'
      │
      ▼
ConfigCommandHandler receives ConfigCommand
      │ detects: SettingName == "llm"
      ▼
ConfigCommandHandler.PromptForLlmProviderAsync()
      │ returns: LlmProvider
      ▼
ConfigCommandHandler.PromptForModelAsync(provider)
      │ uses: ModelRegistry.GetByProvider(provider)
      │ returns: SupportedModel
      ▼
Load existing ConfigurationSettings
      │
      ▼
Update settings with new Llm.Provider and Llm.Model
      │
      ▼
UserSecretsStorageService.SaveAsync(settings)
      │
      ▼
Display confirmation message
```

### 3. Application Startup Validation Flow

```text
Application starts
      │
      ▼
Load ConfigurationSettings from config hierarchy
      │ (env vars > user secrets > appsettings.json)
      ▼
ConfigurationSettings.IsValid() validation
      │
      ├─── Llm.Model is null/empty?
      │    │ YES: Use ModelRegistry.GetDefault(Llm.Provider)
      │    │      Log warning about using default
      │    │ NO:  Continue
      │
      ▼
ModelValidator.ValidateModel(Llm.Model, Llm.Provider)
      │
      ├─── Valid? NO: Throw with clear error message + valid options
      │
      ▼
LlmProviderFactory.CreateProvider(Provider, Model)
      │
      ▼
Application ready
```

---

## Validation Rules Summary

### SupportedModel Validation

- ✅ Required: Id, DisplayName, Provider, CostTier, Description
- ✅ Id must be unique globally
- ✅ CostTier must be "Budget", "Balanced", or "Premium"
- ✅ Description max length: 100 characters
- ✅ One default per provider

### ModelRegistry Validation

- ✅ At least 1 model per provider
- ✅ No duplicate IDs across all models
- ✅ Exactly 1 default model per provider
- ✅ All models have valid Provider enum values

### LlmConfiguration Validation

- ✅ If Model is set, must exist in ModelRegistry for Provider
- ✅ Model and Provider must be compatible
- ✅ ApiKey required for any LLM operations
- ✅ Provider must be valid enum value

### Runtime Validation

- ✅ Validate at application startup (fail fast)
- ✅ Provide actionable error messages with fix instructions
- ✅ Warn (don't error) when using default model
- ✅ Clear error if model is invalid for provider

---

## Configuration Persistence

### User Secrets Format (Development)

```json
{
  "Ssh:KeyPath": "/Users/user/.ssh/id_ed25519",
  "Ssh:KeySource": "FileSystem",
  "Llm:Provider": "OpenAI",
  "Llm:ApiKey": "sk-...",
  "Llm:Model": "gpt-4o-mini",
  "Storage:MemoryDirectory": "/Users/user/.memory/ten-second-tom",
  "Optional:LogLevel": "Information",
  "Optional:RetentionDays": "-1"
}
```

### Environment Variables Format (Production)

```bash
export TenSecondTom__Ssh__KeyPath="/Users/user/.ssh/id_ed25519"
export TenSecondTom__Llm__Provider="OpenAI"
export TenSecondTom__Llm__ApiKey="sk-..."
export TenSecondTom__Llm__Model="gpt-4o-mini"
export TenSecondTom__Storage__MemoryDirectory="/Users/user/.memory/ten-second-tom"
```

### appsettings.json Format (Fallback)

```json
{
  "Llm": {
    "Provider": "OpenAI",
    "Model": "gpt-4o-mini"
  }
}
```

Note: ApiKey should NOT be in appsettings.json (security warning issued if detected).

---

## Initial Model Catalog

### OpenAI Models

```csharp
new SupportedModel
{
    Id = "gpt-4o-mini",
    DisplayName = "GPT-4o Mini",
    Provider = LlmProvider.OpenAI,
    CostTier = "Budget",
    Description = "Fast and economical for most tasks",
    IsDefault = true
},
new SupportedModel
{
    Id = "gpt-4o",
    DisplayName = "GPT-4o",
    Provider = LlmProvider.OpenAI,
    CostTier = "Balanced",
    Description = "Best balance of cost and capability",
    IsDefault = false
},
new SupportedModel
{
    Id = "gpt-3.5-turbo",
    DisplayName = "GPT-3.5 Turbo",
    Provider = LlmProvider.OpenAI,
    CostTier = "Budget",
    Description = "Lowest cost option for simple tasks",
    IsDefault = false
}
```

### Anthropic Models

```csharp
new SupportedModel
{
    Id = "claude-3-5-haiku-20241022",
    DisplayName = "Claude 3.5 Haiku",
    Provider = LlmProvider.Anthropic,
    CostTier = "Budget",
    Description = "Fast and economical for straightforward tasks",
    IsDefault = true
},
new SupportedModel
{
    Id = "claude-3-5-sonnet-20241022",
    DisplayName = "Claude 3.5 Sonnet",
    Provider = LlmProvider.Anthropic,
    CostTier = "Balanced",
    Description = "Latest Sonnet with excellent performance",
    IsDefault = false
},
new SupportedModel
{
    Id = "claude-3-opus-20240229",
    DisplayName = "Claude 3 Opus",
    Provider = LlmProvider.Anthropic,
    CostTier = "Premium",
    Description = "Most capable model with highest quality",
    IsDefault = false
}
```

---

## Error Cases and Messages

### Invalid Model for Provider

**Scenario**: User has `Llm.Model = "gpt-4o"` but `Llm.Provider = "Anthropic"`

**Error Message**:

```text
❌ Configuration Error: Invalid model for provider

Model 'gpt-4o' is not compatible with provider 'Anthropic'.

Supported models for Anthropic:
  • claude-3-5-haiku-20241022 (Budget, default)
  • claude-3-5-sonnet-20241022 (Balanced)
  • claude-3-opus-20240229 (Premium)

To fix this, run: tom config llm
```

### Model Not Found

**Scenario**: User has `Llm.Model = "gpt-5-ultra"` (doesn't exist)

**Error Message**:

```text
❌ Configuration Error: Model not found

Model 'gpt-5-ultra' is not a supported model.

Supported models for OpenAI:
  • gpt-4o-mini (Budget, default)
  • gpt-4o (Balanced)
  • gpt-3.5-turbo (Budget)

To fix this, run: tom config llm
```

### Missing Model (Using Default)

**Scenario**: User has no Model configured, using default

**Warning Message**:

```text
⚠️  No model configured, using default: gpt-4o-mini

To select a different model, run: tom config llm
```

---

## Testing Considerations

### Unit Test Coverage

- ✅ SupportedModel creation and validation
- ✅ ModelRegistry lookup methods (GetDefault, IsValid, GetById, GetByProvider)
- ✅ ModelValidator validation logic for all error cases
- ✅ ConfigCommand handling for "llm" setting
- ✅ LlmConfiguration validation with valid and invalid models

### Integration Test Coverage

- ✅ End-to-end setup flow with model selection
- ✅ End-to-end `tom config llm` command
- ✅ Configuration load from user secrets with model
- ✅ Configuration load from environment variables with model
- ✅ Default model fallback when Model is missing
- ✅ Error handling for invalid model configurations

### Test Data

Use test models for deterministic testing:

```csharp
public static class TestModelRegistry
{
    public static readonly SupportedModel TestOpenAIModel = new()
    {
        Id = "test-openai-model",
        DisplayName = "Test OpenAI Model",
        Provider = LlmProvider.OpenAI,
        CostTier = "Budget",
        Description = "For testing only",
        IsDefault = true
    };
    
    // Similar for Anthropic test model
}
```

---

## Migration Path

### Existing Users Without Model Configured

**Current State**: `Llm.Model` is null/empty in their configuration

**Migration**:

1. Application detects missing model at startup
2. Logs warning and uses default model
3. Displays one-time notice on next interactive command
4. User can explicitly set model via `tom config llm` at their convenience

**No breaking changes**: Existing configurations continue to work.

---

## Future Enhancements (Out of Scope)

- Model deprecation support (`IsDeprecated`, `ReplacementId` properties)
- Per-command model override (different models for different features)
- Dynamic model list loaded from external JSON/API
- Real-time pricing information
- Model performance benchmarking
- Automatic model recommendation based on usage patterns

---

## Summary

This data model extends the existing configuration infrastructure with:

- **SupportedModel**: Rich metadata for curated models
- **ModelRegistry**: Static validation and lookup
- **Enhanced LlmConfiguration**: Model property now validated
- **Updated ConfigCommand**: Support for interactive `tom config llm`

All entities follow existing patterns: immutable records, clear validation, strong typing. No new storage mechanisms required - leverages existing UserSecretsStorageService and .NET configuration hierarchy.
