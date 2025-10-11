# Research: Model Selection and Configuration

**Feature**: 005-model-selection-and  
**Date**: 2025-10-11  
**Status**: Complete

## Executive Summary

Research confirms that maintaining a curated, static model registry with validation against it is the best approach for Ten Second Tom. The .NET configuration system already supports the required precedence (env vars > user secrets > appsettings), and Spectre.Console provides excellent interactive selection primitives. Key findings support a simple implementation with strong validation and clear error messages.

## Research Questions & Findings

### Q1: How should we maintain and validate the curated model list?

**Decision**: Static registry class with read-only collections

**Rationale**:

- **Simplicity**: Static class with constant definitions is easiest to maintain and has zero runtime overhead
- **Type Safety**: Strongly-typed model objects prevent configuration errors
- **Discoverability**: IntelliSense shows all available models to developers
- **Testability**: Easy to mock and test validation logic
- **Performance**: No file I/O or parsing at runtime

**Implementation Approach**:

```csharp
public sealed record SupportedModel
{
    public required string Id { get; init; }              // e.g., "gpt-4o-mini"
    public required string DisplayName { get; init; }     // e.g., "GPT-4o Mini"
    public required LlmProvider Provider { get; init; }
    public required string CostTier { get; init; }        // "Budget", "Balanced", "Premium"
    public required string Description { get; init; }
    public bool IsDefault { get; init; }
}

public static class ModelRegistry
{
    public static IReadOnlyList<SupportedModel> OpenAIModels { get; }
    public static IReadOnlyList<SupportedModel> AnthropicModels { get; }
    public static SupportedModel GetDefault(LlmProvider provider);
    public static bool IsValid(string modelId, LlmProvider provider);
}
```

**Alternatives Considered**:

- JSON configuration file: Added complexity for parsing, error handling, and distribution
- Database: Massive overkill for 6-8 models
- External API: Introduces network dependency, latency, and failure modes

**References**:

- .NET Constants and static readonly best practices
- Spectre.Console SelectionPrompt requires in-memory collections

---

### Q2: How does .NET configuration precedence work, and will it support our requirements?

**Decision**: Use Microsoft.Extensions.Configuration's built-in hierarchy

**Rationale**:

- **Already Implemented**: Ten Second Tom already uses this correctly
- **Well-Documented**: Standard .NET pattern, widely understood
- **Precedence Order**: Environment variables > User secrets > appsettings.json (exactly what we need)
- **Key Format**: `TenSecondTom__Llm__Model` (double underscore for nested sections)

**Implementation Verification**:

Current `ConfigurationSettings.cs` already has:

```csharp
public sealed record LlmConfiguration
{
    public LlmProvider Provider { get; init; }
    public string? ApiKey { get; init; }
    public string? Model { get; init; }  // ← Already exists!
}
```

Current `UserSecretsStorageService.cs` already persists:

```csharp
configData["Llm:Model"] = settings.Llm.Model;  // ← Already saved!
```

**What We Need to Add**:

1. Populate the Model field during setup wizard
2. Validate the Model field against ModelRegistry
3. Pass Model to LLM provider constructors
4. Add default fallback if Model is null/empty

**Environment Variable Format**:

```bash
export TenSecondTom__Llm__Model=gpt-4o-mini
export TenSecondTom__Llm__Provider=OpenAI
export TenSecondTom__Llm__ApiKey=sk-...
```

**Alternatives Considered**: None - this is the standard .NET approach and already in use.

**References**:

- [Configuration in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)

---

### Q3: What are the best practices for interactive model selection UI in Spectre.Console?

**Decision**: Use `SelectionPrompt<T>` with rich descriptions and provider filtering

**Rationale**:

- **User Experience**: SelectionPrompt provides keyboard navigation, search, and visual feedback
- **Type Safety**: Can use `SelectionPrompt<SupportedModel>` with custom ToString() or converter
- **Rich Display**: Supports colors, markup, and multi-line descriptions
- **Consistent**: Already used for SSH key selection, LLM provider selection

**Implementation Pattern**:

```csharp
public async Task<SupportedModel?> PromptForModelAsync(
    LlmProvider provider,
    string? currentModelId,
    CancellationToken cancellationToken)
{
    var models = provider == LlmProvider.OpenAI 
        ? ModelRegistry.OpenAIModels 
        : ModelRegistry.AnthropicModels;

    var prompt = new SelectionPrompt<SupportedModel>()
        .Title($"Select {provider} model:")
        .PageSize(10)
        .UseConverter(m => $"{m.DisplayName} ([{m.CostTier}]) - {m.Description}")
        .AddChoices(models);

    if (currentModelId != null)
    {
        var current = models.FirstOrDefault(m => m.Id == currentModelId);
        if (current != null)
        {
            prompt.HighlightStyle(new Style(Color.Green));
        }
    }

    var selected = _console.Prompt(prompt);
    return selected;
}
```

**Display Format Examples**:

```text
Select OpenAI model:
> GPT-4o Mini [Budget] - Fast and economical for most tasks
  GPT-4o [Balanced] - Best balance of cost and capability
  GPT-3.5 Turbo [Budget] - Lowest cost option
```

**Alternatives Considered**:

- Text input with validation: Poor UX, requires users to know model IDs
- Multi-step menu: More complex, slower interaction

**References**:

- [Spectre.Console Prompts](https://spectreconsole.net/prompts/selection)
- Existing `SpectreConsoleSetupWizard.PromptForLlmProviderAsync` implementation

---

### Q4: What should the default model fallback strategy be?

**Decision**: Explicit defaults in ModelRegistry, fail loudly if misconfigured

**Rationale**:

- **Predictable Costs**: Users should explicitly choose models to avoid surprise API bills
- **Fail Fast**: Better to error during setup than silently use wrong model
- **Clear Guidance**: Error messages direct users to `tom config llm` or `tom setup`
- **Sensible Defaults**: When defaults are used, pick most cost-effective options

**Default Model Strategy**:

1. If Model is configured (env var, user secrets, or appsettings): **Use it and validate**
2. If Model is missing but Provider is configured: **Use provider default and log warning**
3. If both are missing: **Fail with actionable error message**

**Default Models**:

- OpenAI: `gpt-4o-mini` (best cost/performance ratio as of 2025)
- Anthropic: `claude-3-5-haiku-20241022` (fastest and most economical)

**Implementation**:

```csharp
public static class ModelRegistry
{
    public static SupportedModel GetDefault(LlmProvider provider)
    {
        return provider switch
        {
            LlmProvider.OpenAI => OpenAIModels.First(m => m.IsDefault),
            LlmProvider.Anthropic => AnthropicModels.First(m => m.IsDefault),
            _ => throw new ArgumentException($"Unsupported provider: {provider}")
        };
    }
}
```

**Error Message Example**:

```text
❌ Configuration Error: No LLM model configured

Your configuration is missing the 'Llm.Model' setting.

To fix this, run one of:
  • tom setup           (guided setup wizard)
  • tom config llm      (configure LLM settings)
  • Set environment variable: TenSecondTom__Llm__Model=gpt-4o-mini

Supported models for OpenAI:
  • gpt-4o-mini (Budget, recommended)
  • gpt-4o (Balanced)
  • gpt-3.5-turbo (Budget)
```

**Alternatives Considered**:

- Always use defaults silently: Risk of unexpected API costs
- Prompt at runtime: Breaks non-interactive usage, complicates testing

**References**:

- Principle of Least Surprise
- Fail-fast pattern for configuration errors

---

### Q5: How should we handle model deprecation and migration?

**Decision**: Validation with clear migration path, no automatic updates

**Rationale**:

- **User Control**: Users should decide when to migrate, not forced automatically
- **Backward Compatibility**: Old configurations should work until model is actually removed by provider
- **Clear Communication**: Error messages guide users to updated models
- **Graceful Degradation**: Warn on deprecated but still-working models

**Implementation Strategy**:

1. Add `IsDeprecated` and `ReplacementId` properties to `SupportedModel`
2. Validation warns (not errors) on deprecated models
3. Startup validation detects invalid models and suggests replacements
4. Documentation update process when providers deprecate models

**Future Enhancement** (out of scope for this feature):

```csharp
public sealed record SupportedModel
{
    // ... existing properties ...
    public bool IsDeprecated { get; init; }
    public string? ReplacementId { get; init; }
    public string? DeprecationMessage { get; init; }
}
```

**Alternatives Considered**:

- Auto-migration: Too aggressive, could surprise users
- Hard errors on deprecated models: Breaks existing users unnecessarily
- No deprecation support: Poor user experience when providers deprecate

**References**:

- Semantic versioning principles
- Azure SDK deprecation patterns

---

## Technology Stack Confirmation

| Technology | Version | Usage | Status |
|------------|---------|-------|--------|
| .NET | 9.0 | Runtime and SDK | ✅ In use |
| C# | 12 | Language features | ✅ In use |
| System.CommandLine | Latest | CLI framework | ✅ In use |
| Spectre.Console | Latest | Rich terminal UI | ✅ In use |
| xUnit | Latest | Testing framework | ✅ In use |
| FluentAssertions | Latest | Test assertions | ✅ In use |
| Moq | Latest | Mocking framework | ✅ In use |
| Microsoft.Extensions.Configuration | 9.0 | Configuration hierarchy | ✅ In use |
| Microsoft.Extensions.Configuration.UserSecrets | 9.0 | Dev secrets | ✅ In use |

## Best Practices Applied

### Configuration Management

1. **Hierarchical Configuration**: Leverage .NET's built-in precedence
2. **Fail Fast**: Validate configuration at startup, not at first use
3. **Clear Errors**: Actionable error messages with specific fix instructions
4. **Secure by Default**: No secrets in model configuration (only identifiers)

### User Experience

1. **Progressive Disclosure**: Show only relevant models for selected provider
2. **Smart Defaults**: Recommend most cost-effective options
3. **Visual Feedback**: Highlight current selection, use color for emphasis
4. **Consistent Patterns**: Match existing setup wizard UX

### Testing

1. **Test Both Paths**: User secrets (dev) and environment variables (production)
2. **Validate Validation**: Test all error cases with invalid models
3. **Integration Tests**: End-to-end flows for setup and config commands
4. **Fast Feedback**: No network calls in tests, all data in-memory

### Code Quality

1. **Immutable Models**: Use C# records for configuration types
2. **Static Validation**: Use ModelRegistry for compile-time safety where possible
3. **Null Safety**: Leverage nullable reference types, explicit defaults
4. **Single Responsibility**: Separate registry, validation, and UI concerns

## Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Provider changes model IDs | Low | High | Version model list, add deprecation support in future |
| User has old model in config | Medium | Low | Validation with clear error + migration guide |
| Environment variable typo | Medium | Medium | Validation at startup, show all valid options in error |
| Model list gets stale | Medium | Low | Document update process, consider future API integration |
| Distribution breaks config | Low | High | Integration tests for both user secrets and env vars |

## Open Questions

None remaining. All research questions resolved with clear implementation paths.

## Next Steps

Proceed to Phase 1: Data Model and Contracts

1. Create `data-model.md` with entity definitions
2. Generate JSON schemas for `contracts/`
3. Create `quickstart.md` for developer onboarding
4. Update agent context with model selection patterns

## References

- [.NET Configuration Providers](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [Spectre.Console Documentation](https://spectreconsole.net/)
- [OpenAI Models](https://platform.openai.com/docs/models)
- [Anthropic Models](https://docs.anthropic.com/claude/docs/models-overview)
- Existing codebase: `SpectreConsoleSetupWizard.cs`, `ConfigurationSettings.cs`, `UserSecretsStorageService.cs`
