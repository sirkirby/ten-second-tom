# Quickstart: Model Selection and Configuration

**Feature**: 005-model-selection-and  
**Date**: 2025-10-11  
**Audience**: Developers implementing or extending this feature

## Overview

This quickstart guide helps you understand, implement, and test the model selection and configuration feature. It covers the key files, patterns, and workflows you'll work with.

## Prerequisites

- .NET 9 SDK installed
- Ten Second Tom repository cloned
- Familiarity with C# records, Spectre.Console, and xUnit
- Understanding of .NET configuration hierarchy (env vars > user secrets > appsettings)

## Key Concepts

### 1. Curated Model Registry

Models are **statically defined** in code, not loaded from external sources. This ensures:

- Zero runtime overhead
- Type safety and IntelliSense support
- Easy validation
- No network dependencies

**Pattern**: Static registry class with read-only collections.

### 2. Configuration Hierarchy

Ten Second Tom uses the standard .NET configuration hierarchy:

```text
Environment Variables (highest precedence)
    ↓
User Secrets (development)
    ↓
appsettings.json (fallback, lowest precedence)
```

**Key**: The `Llm.Model` property flows through this hierarchy automatically.

### 3. Vertical Slice Architecture

This feature extends the existing `Features/Setup` slice:

```text
Features/Setup/
  ├── Commands/         # Command definitions
  ├── Handlers/         # Command handlers and UI wizards
  ├── Models/           # Domain models (NEW: SupportedModel, ModelRegistry)
  └── Validation/       # Validation logic (NEW: ModelValidator)
```

## Quick Navigation

| What You Need | Where to Look |
|---------------|---------------|
| Model definitions | `src/Features/Setup/Models/ModelRegistry.cs` |
| Model validation | `src/Features/Setup/Validation/ModelValidator.cs` |
| Setup wizard UI | `src/Features/Setup/Handlers/SpectreConsoleSetupWizard.cs` |
| Config command | `src/Features/Setup/Handlers/ConfigCommandHandler.cs` |
| Configuration storage | `src/Infrastructure/Configuration/UserSecretsStorageService.cs` |
| Provider initialization | `src/Infrastructure/Llm/LlmProviderFactory.cs` |
| Tests | `tests/Unit/Features/Setup/Models/ModelRegistryTests.cs` |

## Development Workflow

### Phase 1: Create Model Registry (Test-First)

1. **Write tests first** (`ModelRegistryTests.cs`):

   ```csharp
   [Fact]
   public void OpenAIModels_ShouldContainAtLeastOneModel()
   {
       ModelRegistry.OpenAIModels.Should().NotBeEmpty();
   }

   [Fact]
   public void GetDefault_WithOpenAI_ShouldReturnDefaultModel()
   {
       var model = ModelRegistry.GetDefault(LlmProvider.OpenAI);
       model.Should().NotBeNull();
       model.IsDefault.Should().BeTrue();
   }

   [Fact]
   public void IsValid_WithValidModel_ShouldReturnTrue()
   {
       ModelRegistry.IsValid("gpt-4o-mini", LlmProvider.OpenAI)
           .Should().BeTrue();
   }
   ```

2. **Implement `SupportedModel` record**:

   ```csharp
   public sealed record SupportedModel
   {
       public required string Id { get; init; }
       public required string DisplayName { get; init; }
       public required LlmProvider Provider { get; init; }
       public required string CostTier { get; init; }
       public required string Description { get; init; }
       public bool IsDefault { get; init; }
   }
   ```

3. **Implement `ModelRegistry` static class**:

   ```csharp
   public static class ModelRegistry
   {
       public static IReadOnlyList<SupportedModel> OpenAIModels { get; } = 
       [
           new() {
               Id = "gpt-4o-mini",
               DisplayName = "GPT-4o Mini",
               Provider = LlmProvider.OpenAI,
               CostTier = "Budget",
               Description = "Fast and economical for most tasks",
               IsDefault = true
           },
           // ... more models
       ];

       public static SupportedModel GetDefault(LlmProvider provider) =>
           provider switch
           {
               LlmProvider.OpenAI => OpenAIModels.First(m => m.IsDefault),
               LlmProvider.Anthropic => AnthropicModels.First(m => m.IsDefault),
               _ => throw new ArgumentException($"Unsupported provider: {provider}")
           };
   }
   ```

4. **Run tests**: `dotnet test` (all tests should pass)

### Phase 2: Add Model Selection to Setup Wizard

1. **Write integration test first**:

   ```csharp
   [Fact]
   public async Task Setup_WithModelSelection_ShouldSaveModelToConfig()
   {
       // Arrange: Mock UI to return OpenAI + gpt-4o-mini
       // Act: Run setup command
       // Assert: Verify Llm.Model == "gpt-4o-mini" in saved config
   }
   ```

2. **Add `PromptForModelAsync` to `SpectreConsoleSetupWizard`**:

   ```csharp
   public Task<SupportedModel?> PromptForModelAsync(
       LlmProvider provider,
       string? currentModelId,
       CancellationToken cancellationToken)
   {
       var models = ModelRegistry.GetByProvider(provider);
       
       var prompt = new SelectionPrompt<SupportedModel>()
           .Title($"Select {provider} model:")
           .UseConverter(m => $"{m.DisplayName} [{m.CostTier}] - {m.Description}")
           .AddChoices(models);

       var selected = _console.Prompt(prompt);
       return Task.FromResult<SupportedModel?>(selected);
   }
   ```

3. **Update `SetupCommandHandler` to call model prompt after API key prompt**

4. **Run integration test**: Verify model is saved correctly

### Phase 3: Add Config LLM Command

1. **Write tests for `tom config llm`**:

   ```csharp
   [Fact]
   public async Task ConfigLlm_ShouldPromptForProviderThenModel()
   {
       // Arrange: Command with SettingName = "llm"
       // Act: Execute command
       // Assert: UI prompts called in correct order
   }
   ```

2. **Extend `ConfigCommandHandler` to detect `SettingName == "llm"`**:

   ```csharp
   if (command.SettingName?.ToLowerInvariant() == "llm")
   {
       var provider = await _wizard.PromptForLlmProviderAsync(current, ct);
       var model = await _wizard.PromptForModelAsync(provider, current, ct);
       // Update config with new provider and model
   }
   ```

3. **Run tests**: Verify command flow

### Phase 4: Add Validation

1. **Write validation tests**:

   ```csharp
   [Theory]
   [InlineData("gpt-4o-mini", LlmProvider.OpenAI, true)]
   [InlineData("gpt-4o-mini", LlmProvider.Anthropic, false)]
   [InlineData("invalid-model", LlmProvider.OpenAI, false)]
   public void ValidateModel_ShouldReturnExpectedResult(
       string modelId, LlmProvider provider, bool expectedValid)
   {
       var result = ModelValidator.Validate(modelId, provider);
       result.IsSuccess.Should().Be(expectedValid);
   }
   ```

2. **Implement `ModelValidator`**:

   ```csharp
   public static Result ValidateModel(string modelId, LlmProvider provider)
   {
       if (string.IsNullOrWhiteSpace(modelId))
           return Result.Failure("Model ID cannot be empty");

       if (!ModelRegistry.IsValid(modelId, provider))
       {
           var validModels = ModelRegistry.GetByProvider(provider);
           return Result.Failure(
               $"Model '{modelId}' is not valid for {provider}. " +
               $"Valid models: {string.Join(", ", validModels.Select(m => m.Id))}");
       }

       return Result.Success();
   }
   ```

3. **Add validation to startup** in `Program.cs` or `ConfigurationChecker`

4. **Run tests**: All validation scenarios pass

### Phase 5: Update LLM Providers

1. **Write tests for provider initialization with model**:

   ```csharp
   [Fact]
   public void CreateOpenAIProvider_WithConfiguredModel_ShouldUseModel()
   {
       var config = new LlmConfiguration 
       { 
           Provider = LlmProvider.OpenAI,
           Model = "gpt-4o",
           ApiKey = "sk-test"
       };
       
       var provider = factory.CreateProvider(config);
       // Assert provider uses gpt-4o
   }
   ```

2. **Update `OpenAILlmProvider` and `AnthropicLlmProvider` constructors** to accept model parameter

3. **Update `LlmProviderFactory`** to read model from config and pass to providers

4. **Run tests**: Providers use configured model

## Testing Strategy

### Unit Tests (Fast, Isolated)

```bash
# Run all unit tests
dotnet test tests/TenSecondTom.Tests

# Run specific test class
dotnet test --filter "FullyQualifiedName~ModelRegistryTests"
```

**Coverage Requirements**: 80% minimum

**Focus Areas**:

- ModelRegistry lookups and validation
- ModelValidator logic
- SupportedModel creation
- ConfigCommandHandler routing logic

### Integration Tests (End-to-End Flows)

```bash
# Run all integration tests
dotnet test tests/TenSecondTom.IntegrationTests

# Run model selection flow tests
dotnet test --filter "FullyQualifiedName~ModelSelectionFlowTests"
```

**Coverage Areas**:

- Complete setup flow with model selection
- `tom config llm` command end-to-end
- Configuration persistence (user secrets)
- Environment variable configuration
- Default model fallback

### Manual Testing

#### Test 1: Guided Setup

```bash
# Clean slate
rm -rf ~/.microsoft/usersecrets/ten-second-tom-secrets

# Run setup
dotnet run --project src -- setup

# Follow prompts, select provider and model
# Verify model is saved: tom config show
```

#### Test 2: Config LLM Command

```bash
# Change model
dotnet run --project src -- config llm

# Select different provider/model
# Verify: tom config show
```

#### Test 3: Environment Variable

```bash
# Set env var
export TenSecondTom__Llm__Model=gpt-4o
export TenSecondTom__Llm__Provider=OpenAI
export TenSecondTom__Llm__ApiKey=sk-test

# Run app - should use gpt-4o
dotnet run --project src -- today
```

#### Test 4: Invalid Model Handling

```bash
# Manually edit user secrets to invalid model
# Run app - should show clear error with fix instructions
```

## Common Patterns

### Pattern 1: Static Registry with Validation

```csharp
// Registry provides data
var models = ModelRegistry.GetByProvider(provider);

// Validator checks against registry
var isValid = ModelValidator.Validate(modelId, provider);
```

### Pattern 2: Interactive Prompt with Rich Display

```csharp
var prompt = new SelectionPrompt<SupportedModel>()
    .Title("Select model:")
    .UseConverter(m => $"{m.DisplayName} [{m.CostTier}] - {m.Description}")
    .AddChoices(models);

var selected = _console.Prompt(prompt);
```

### Pattern 3: Configuration Hierarchy Access

```csharp
// .NET automatically resolves from env vars > user secrets > appsettings
var model = configuration.GetValue<string>("Llm:Model");

// Or via strongly-typed binding
var llmConfig = configuration.GetSection("Llm").Get<LlmConfiguration>();
```

### Pattern 4: Fail Fast with Actionable Errors

```csharp
if (!ModelRegistry.IsValid(model, provider))
{
    var validModels = ModelRegistry.GetByProvider(provider);
    throw new InvalidOperationException(
        $"Invalid model '{model}' for {provider}.\n\n" +
        $"Valid models:\n" +
        string.Join("\n", validModels.Select(m => $"  • {m.Id} ({m.CostTier})")) +
        $"\n\nTo fix: tom config llm"
    );
}
```

## Debugging Tips

### Issue: Model not being saved

**Check**:

1. Is `Llm.Model` set in `ConfigurationSettings` before calling `SaveAsync`?
2. Is `UserSecretsStorageService` saving the `Llm:Model` key?
3. Are there conflicting environment variables?

**Debug**:

```csharp
_logger.LogDebug("Saving model: {Model} for provider: {Provider}", 
    settings.Llm.Model, settings.Llm.Provider);
```

### Issue: Wrong model being used

**Check**:

1. Environment variable precedence (env var overrides user secrets)
2. Provider factory reading model correctly?
3. Default fallback logic triggering?

**Debug**:

```bash
# Check what's actually configured
dotnet run -- config show

# Check environment variables
env | grep TenSecondTom
```

### Issue: Validation not working

**Check**:

1. Is validation being called at startup?
2. Are model IDs case-sensitive in comparison?
3. Is the provider enum value correct?

**Debug**:

```csharp
var allModels = ModelRegistry.AllModels;
_logger.LogDebug("All models: {@Models}", allModels);
_logger.LogDebug("Checking: {Model} against {Provider}", modelId, provider);
```

## Code Review Checklist

- [ ] Tests written before implementation (TDD)
- [ ] All new code has 80%+ test coverage
- [ ] XML documentation on public APIs
- [ ] Validation returns `Result<T>` with clear error messages
- [ ] Logging uses Serilog with structured context
- [ ] No compiler warnings
- [ ] Configuration precedence tested (env vars, user secrets, appsettings)
- [ ] UI uses Spectre.Console patterns consistently
- [ ] Models are immutable (C# records with `init`)
- [ ] No hardcoded secrets or API keys

## Next Steps

After completing this feature:

1. Run full test suite: `dotnet test`
2. Verify code coverage: Check coverage report for 80%+ on new code
3. Manual testing: Test all flows (setup, config llm, env vars)
4. Update documentation: README, CONFIGURATION.md
5. Create PR with conventional commit message

## Resources

- [Feature Spec](./spec.md)
- [Implementation Plan](./plan.md)
- [Data Model](./data-model.md)
- [Research Notes](./research.md)
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [.NET Configuration Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)

## Questions?

Consult the [constitution](../../.specify/memory/constitution.md) for architectural principles and the [AGENTS.md](../../AGENTS.md) for AI agent development guidelines.
