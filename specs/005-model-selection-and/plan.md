# Implementation Plan: Model Selection and Configuration

**Branch**: `005-model-selection-and` | **Date**: 2025-10-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/005-model-selection-and/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Enable users to select from curated, cost-effective models during guided setup and via dedicated config command. Implement model validation, proper configuration hierarchy (env vars > user secrets > appsettings), and defaults. Address current bug where models aren't being configured properly during setup. Ensure consistent experience across local development (user secrets) and distributed binary (env vars).

## Technical Context

**Language/Version**: C# 12 with .NET 9

**Primary Dependencies**:

- System.CommandLine (CLI framework)
- Spectre.Console (rich terminal UI)
- Serilog (structured logging - organizational standard)
- Microsoft.Extensions.Configuration (configuration hierarchy)
- Microsoft.Extensions.Configuration.UserSecrets (development secrets)

**Storage**:

- User Secrets (~/.microsoft/usersecrets/ten-second-tom-secrets/secrets.json) for local development
- Environment variables (TenSecondTom__Llm__Model) for distributed binary
- appsettings.json fallback with security warning
- Configuration precedence: Environment > User Secrets > appsettings.json

**Testing**:

- xUnit (mandatory testing framework)
- FluentAssertions (readable assertions)
- Moq (mocking framework)
- Minimum 80% coverage (constitutional requirement)

**Target Platform**:

- Cross-platform CLI (macOS, Windows via Homebrew/Chocolatey)
- Self-contained binaries with all dependencies
- .NET 9 runtime included

**Project Type**: Single CLI project with Vertical Slice Architecture

**Performance Goals**:

- Model selection UI response < 200ms
- Configuration read/write < 50ms
- No network calls during configuration (offline-capable)

**Constraints**:

- Must work identically in local dev (user secrets) and distributed binary (env vars)
- Cannot break existing user configurations
- Model list must be maintainable without code changes (future: could be JSON resource)
- Must support model deprecation gracefully

**Scale/Scope**:

- 6-8 curated models total (3-4 per provider)
- 2 providers (OpenAI, Anthropic)
- Single user, local storage
- No backend/API dependencies

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### ✅ I. Modern .NET & Idiomatic C#

- All code written in C# 12 with .NET 9
- Using modern patterns: records for immutable models, pattern matching for validation
- Async/await for I/O operations (file-based configuration)
- Serilog for structured logging throughout

### ✅ II. CLI-First Interface

- Extends existing CLI with interactive model selection
- Uses Spectre.Console for rich terminal experience
- Maintains scriptable usage via environment variables
- Clear error messages with actionable guidance

### ✅ III. Test-First (NON-NEGOTIABLE)

- TDD approach: tests written before implementation
- xUnit framework exclusively
- Target 80%+ coverage for all new code
- Unit tests for validation, integration tests for configuration flow
- Tests must work identically for local (user secrets) and distributed (env vars) scenarios

### ✅ IV. DRY & Design Patterns

- Extends existing VSA structure in Features/Setup
- Uses existing CQRS pattern (ConfigCommand/ConfigQuery)
- Reuses existing configuration infrastructure (ConfigurationSettings, UserSecretsStorageService)
- Factory pattern already in place (LlmProviderFactory) - extends for model validation
- No duplication: curated model list as single source of truth

### ✅ V. Semantic Versioning & Automated Releases

- Feature addition = MINOR version bump
- Backward compatible with existing configurations
- Automated release via GitHub Actions on merge to main

### ✅ VI. Cross-Platform Distribution

- Works with existing self-contained distribution
- Homebrew/Chocolatey packages unchanged
- Configuration via user secrets (dev) and env vars (production) already supported

### ✅ VII. Local Development Excellence

- Leverages existing project setup
- Fast feedback: model selection is UI-only, no API calls
- Clear README updates for model configuration
- Works in both VSCode and Rider

### ✅ VIII. Secrets Management

- No secrets in source control
- Uses existing user secrets infrastructure
- Environment variable support for production
- Model identifiers are NOT secrets (no API keys stored with models)

### 🟢 VERDICT: No constitutional violations. Feature aligns with all core principles.

This is an enhancement to existing Setup feature using established patterns. Low complexity, high testability, leverages existing infrastructure.

## Project Structure

### Documentation (this feature)

```text
specs/005-model-selection-and/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── ModelSelection.schema.json
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Features/
│   └── Setup/
│       ├── Commands/
│       │   ├── ConfigCommand.cs (EXTEND - add llm subcommand support)
│       │   └── SetupCommand.cs (no changes)
│       ├── Handlers/
│       │   ├── ConfigCommandHandler.cs (EXTEND - handle llm action)
│       │   ├── SpectreConsoleSetupWizard.cs (EXTEND - add model selection prompt)
│       │   └── SetupCommandHandler.cs (no changes)
│       ├── Models/
│       │   ├── SupportedModel.cs (NEW - curated model metadata)
│       │   └── ModelRegistry.cs (NEW - static registry of supported models)
│       └── Validation/
│           ├── ConfigCommandValidator.cs (EXTEND - validate model selection)
│           └── ModelValidator.cs (NEW - validate model against registry)
├── Infrastructure/
│   ├── Configuration/
│   │   ├── ConfigurationSettings.cs (existing - LlmConfiguration.Model property already exists)
│   │   └── UserSecretsStorageService.cs (verify model read/write)
│   └── Llm/
│       ├── LlmProviderFactory.cs (EXTEND - validate model at provider creation)
│       ├── OpenAILlmProvider.cs (EXTEND - use configured model)
│       └── AnthropicLlmProvider.cs (EXTEND - use configured model)
└── Shared/
    └── Constants/
        └── LlmConstants.cs (NEW - model identifiers, display names, tiers)

tests/
├── Unit/
│   └── Features/
│       └── Setup/
│           ├── Models/
│           │   ├── SupportedModelTests.cs (NEW)
│           │   └── ModelRegistryTests.cs (NEW)
│           ├── Validation/
│           │   └── ModelValidatorTests.cs (NEW)
│           └── Handlers/
│               ├── ConfigCommandHandlerTests.cs (EXTEND)
│               └── SpectreConsoleSetupWizardTests.cs (EXTEND)
└── Integration/
    └── Features/
        └── Setup/
            ├── ModelSelectionFlowTests.cs (NEW - end-to-end setup with model)
            └── ConfigLlmCommandTests.cs (NEW - end-to-end config llm command)
```

**Structure Decision**: This feature extends the existing `Features/Setup` vertical slice. New model-related types go in `Setup/Models`, validation in `Setup/Validation`. Constants extracted to `Shared/Constants` for reuse across setup and provider initialization. No new feature slices needed - this is a focused enhancement to configuration.

## Complexity Tracking

**No violations** - Feature aligns with all constitutional principles. No complexity justification required.
