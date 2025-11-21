# Ten Second Tom Constitution

## Core Principles

### I. Modern .NET & Idiomatic C#
- **Target**: .NET 9 with C# 12+ modern features
- **Required Features**: File-scoped namespaces, primary constructors, records, required properties, collection expressions
- **Pattern**: Nullable reference types enabled project-wide
- **No Legacy Code**: Avoid outdated patterns (traditional constructors, verbose property initialization)

### II. CLI-First Interface
- **Single Purpose**: Command-line tool only - no web APIs, no GUI frameworks
- **Framework**: System.CommandLine 2.0-rc for command routing
- **Interactive Shell**: Spectre.Console for REPL and rich terminal UI
- **Exit Codes**: 0 for success, non-zero for errors
- **Platform Support**: macOS (primary), Windows (supported), Linux (future)

### III. Test-First Development (NON-NEGOTIABLE)
- **Methodology**: Test-Driven Development (TDD) - tests before implementation
- **Minimum Coverage**: 80% code coverage across all features
- **Test Framework**: xUnit with FluentAssertions for readable assertions
- **Test Types**: Unit tests (isolated logic), Integration tests (multi-component workflows)
- **Red-Green-Refactor**: Tests must fail first, then pass, then refactor

### IV. DRY & Proven Design Patterns
- **Architecture**: Vertical Slice Architecture (VSA) - features are self-contained
- **Communication**: CQRS via MediatR for cross-feature communication
- **Validation**: FluentValidation for input validation (auto-discovered)
- **Results**: Result<T> pattern for expected failures (no exceptions for flow control)
- **No God Objects**: Features own their configuration and behavior

### V. Vertical Slice Architecture (VSA)
- **Feature Organization**: All code for one use case in a single file (co-location pattern)
- **Independence**: Features must not directly reference other features
- **Cross-Feature Communication**: Use MediatR CQRS commands/queries only
- **Shared Code**: Only in `src/Shared/` - models, abstractions, utilities, constants
- **Infrastructure**: Cross-cutting concerns only (logging, configuration, DI, behaviors)

### VI. Configuration Management
- **Required Pattern**: .NET Options Pattern (`IOptions<T>`) - NO direct `IConfiguration` access
- **Type Safety**: Strongly-typed options classes with validators
- **Validation**: `IValidateOptions<T>` for startup validation
- **Storage**: `IConfigurationSectionStore` for reading/writing config sections
- **Feature Ownership**: Each feature owns its configuration and exposes via CQRS

### VII. Semantic Versioning & Automated Releases
- **Format**: MAJOR.MINOR.PATCH (e.g., 1.2.3)
- **Breaking Changes**: Increment MAJOR version
- **New Features**: Increment MINOR version
- **Bug Fixes**: Increment PATCH version
- **Automation**: GitHub Actions for CI/CD, automated releases on PR merge to main

### VIII. Secrets Management & Security
- **Never Commit Secrets**: No API keys, passwords, or tokens in source control
- **User Secrets**: Use .NET User Secrets for local development (excluded from git)
- **Environment Variables**: Support env var overrides for all configuration
- **SSH Authentication**: Use SSH keys for secure authentication (no plaintext passwords)

## Project Structure Standards (v1.7.0)

### Co-location Pattern (REQUIRED)
All code for a single use case must be in one file with nested classes:

```
src/Features/[FeatureName]/
├── [UseCase].cs              # Command/Query, Validator, Handler as nested types
├── Services/                 # Feature-specific services [if needed]
├── Models/                   # Feature-specific DTOs [if needed]
├── Migrations/               # Feature bootstrap migrations [if needed]
└── DependencyInjection.cs    # Feature service registration

src/Infrastructure/           # Cross-cutting concerns ONLY
├── Behaviors/                # MediatR pipeline behaviors
├── Configuration/            # Configuration infrastructure
├── Cli/                      # Command-line infrastructure
├── Logging/                  # Serilog setup
└── DependencyInjection/      # Infrastructure DI registration

src/Shared/                   # Shared across features
├── Models/                   # Common domain models, DTOs
├── Options/                  # Configuration options classes
├── Constants/                # Centralized constants (NO magic strings)
├── Abstractions/             # Interfaces, abstract classes
└── Extensions/               # Extension methods

tests/TenSecondTom.Tests/     # Unit tests (80% coverage minimum)
└── Features/[FeatureName]/[UseCase]Tests.cs

tests/TenSecondTom.IntegrationTests/  # Integration tests
└── Integration/Features/[FeatureName]/
```

### Use Case File Structure
```csharp
namespace TenSecondTom.Features.[FeatureName];

/// <summary>Brief description of use case</summary>
public static class [UseCase]
{
    /// <summary>Command/Query DTO</summary>
    public sealed record Command(...) : IRequest<Result<T>>;

    /// <summary>Input validation (auto-discovered)</summary>
    public sealed class Validator : AbstractValidator<Command> { }

    /// <summary>Business logic (auto-discovered)</summary>
    public sealed class Handler(...) : IRequestHandler<Command, Result<T>> { }
}
```

## Naming Conventions

- **Use Cases**: `[Verb][Noun].cs` (e.g., `CreateDailyEntry.cs`, `ListTemplates.cs`)
- **Nested Types**: `Command`, `Query`, `Validator`, `Handler` (no prefixes)
- **Options Classes**: `[Feature]Options` (e.g., `AudioOptions`, `LlmOptions`)
- **Validators**: `[Options]Validator` (e.g., `AudioOptionsValidator`)
- **DI Methods**: `Add[Feature]Feature` (e.g., `AddAuthFeature`, `AddTemplatesFeature`)
- **Test Files**: `[UseCase]Tests.cs` (e.g., `CreateDailyEntryTests.cs`)

## Prohibited Patterns

### ❌ God Objects
- NO monolithic configuration classes with all app settings
- NO services that know about all features
- Use VSA - each feature owns its data and behavior

### ❌ Magic Strings
- NO hardcoded strings for config keys, commands, file paths
- Use constants from `Shared/Constants/` (e.g., `ConfigurationKeys`, `CommandNames`)
- Exception: Logging messages can use literal strings

### ❌ Direct IConfiguration Access
- NO `_configuration["TenSecondTom:SomeKey"]` in services
- Use Options Pattern (`IOptions<T>`) exclusively
- Create strongly-typed options classes with validators

### ❌ Cross-Feature Dependencies
- Features must NOT directly reference other features
- Use MediatR for communication: `await _mediator.Send(new OtherFeature.Query())`
- Infrastructure can coordinate features but owns no business logic

### ❌ Legacy Patterns
- NO traditional constructors when primary constructors work
- NO `[Obsolete]` code - delete it or refactor it
- NO backward compatibility layers - clean breaks with version increments

## Technology Stack

### Required Dependencies
- **.NET 9** - Latest LTS runtime
- **System.CommandLine 2.0-rc** - CLI framework
- **MediatR 13.1+** - CQRS implementation
- **FluentValidation 12.0+** - Input validation
- **Serilog 4.3+** - Structured logging
- **Spectre.Console 0.51+** - Rich console UI
- **xUnit 2.9+** - Test framework
- **FluentAssertions 8.7+** - Test assertions

### Allowed Package Types
- Microsoft.Extensions.* - Framework utilities
- Logging, validation, testing libraries
- CLI/terminal libraries
- **NOT ALLOWED**: Web frameworks (ASP.NET), GUI frameworks (WPF, WinForms)

## Development Workflow

### Before Any Change
1. Read this constitution - these are non-negotiable principles
2. Check existing tests and understand current behavior
3. Locate the feature slice - all related code should be nearby
4. Look for existing patterns - refactor rather than duplicate

### TDD Cycle (Red-Green-Refactor)
1. **Write Test**: Create test showing expected behavior
2. **Verify Red**: Confirm test fails with clear error message
3. **Minimal Implementation**: Write just enough code to pass
4. **Verify Green**: Confirm test passes
5. **Refactor**: Clean up code while keeping tests green
6. **Document**: Add XML comments to public APIs

### Pull Request Requirements
- All tests pass (including integration tests) NO EXCEPTIONS, NO EXCUSES
- 80% minimum code coverage maintained
- No `[Obsolete]` warnings (clean up or remove deprecated code)
- VSA compliance verified (architecture tests pass)
- XML documentation on all public APIs

## Architecture Enforcement

### Automated Compliance Tests
Location: `tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs`

Enforces:
- Features must not reference other features directly
- Infrastructure must not reference features
- Shared code must not reference features or infrastructure
- All cross-feature communication via MediatR

### Manual Review Checklist
- [ ] Feature is self-contained (co-location pattern)
- [ ] Configuration uses Options Pattern with validators
- [ ] No magic strings (uses constants)
- [ ] Tests written first (TDD)
- [ ] 80% code coverage maintained
- [ ] No God Objects or tight coupling
- [ ] XML documentation on public APIs

## Governance

### Constitution Authority
- This constitution supersedes all other documentation
- CLAUDE.md provides implementation guidance referencing this constitution
- On conflicts: Constitution wins

### Amendment Process
1. Document proposed change with justification
2. Update affected code to comply
3. Update constitution version
4. Update CLAUDE.md references if needed

### Violation Handling
- **Critical Violations** (God Objects, cross-feature coupling): Block PR, require refactor
- **Warning Violations** (missing tests, low coverage): Require fix before merge
- **Style Violations** (naming, comments): Fix in PR or create follow-up issue

## Changelog

### Version 1.8.0 (2025-01-19)

**Type**: MINOR - Configuration Management Refactor

**Changes**:
- **ConfigurationSettings God Object Removal**: Completely removed the monolithic `ConfigurationSettings` class that violated VSA principles by centralizing all application configuration
- **IConfigurationSectionStore Pattern**: Established `IConfigurationSectionStore` as the standard replacement for obsolete `IConfigurationStorageService` and `IAppSettingsStorageService`
- **Feature-Owned Configuration**: Enforced that each feature owns its configuration and exposes it via CQRS queries (e.g., `GetAudioConfiguration.Query`, `GetSetupConfiguration.Query`)
- **SetupResult & SetupSummary DTOs**: Created lightweight DTOs to replace `ConfigurationSettings` in setup wizard flow, maintaining VSA compliance
- **Force Parameter Pattern**: Documented the pattern where configuration commands accept a `Force` boolean to enable idempotent behavior in setup wizard (`Force=false`) vs. forced reconfiguration when called directly (`Force=true`)
- **Obsolete Code Elimination**: Removed all `[Obsolete]` attributes and CS0618 suppressions - deprecated code is now completely deleted, not just marked
- **Legacy Service Removal**: Deleted `IConfigurationStorageService`, `ConfigurationStorageService`, `IAppSettingsStorageService`, and `ConfigurationSettingsValidator`
- **Test Cleanup**: Removed integration tests tied to obsolete configuration patterns (`UserSecretsPersistenceTests`, `FirstTimeSetupTests`, `EnvironmentVariableConfigTests`)

**Files Deleted** (8):
- `src/Shared/Models/ConfigurationSettings.cs` - God Object
- `src/Features/Setup/Services/ConfigurationSettingsValidator.cs`
- `src/Features/Setup/Services/SetupCommandFactory.cs`
- `src/Infrastructure/Configuration/IConfigurationStorageService.cs`
- `src/Infrastructure/Configuration/ConfigurationStorageService.cs`
- `src/Infrastructure/Configuration/IAppSettingsStorageService.cs`
- `tests/TenSecondTom.Tests/Unit/Infrastructure/Configuration/ConfigurationStorageServiceTests.cs`
- `tests/TenSecondTom.Tests/Unit/Infrastructure/Configuration/ConfigurationSettingsTests.cs`

**Files Created** (2):
- `src/Features/Setup/Models/SetupResult.cs` - VSA-compliant DTO for setup command response
- `src/Features/Setup/Models/SetupSummary.cs` - VSA-compliant DTO for setup confirmation display

**Rationale**: The ConfigurationSettings God Object violated Principle V (VSA) by creating tight coupling between features. Each feature now owns its configuration (Options Pattern) and exposes it via CQRS, maintaining feature independence. The Force parameter pattern enables configuration commands to be called both from the setup wizard (idempotent) and directly by users (forced), solving the "already configured" UX problem.

**Test Results**: 1,253 passing tests (down from 1,329 due to obsolete test removal)

---

### Version 1.7.0 (2025-10-28)

**Type**: MINOR - Co-location Pattern

**Changes**:
- **Co-location Pattern Established**: Mandated single-file organization per use case with Command/Query, Validator, and Handler as nested classes within a static container class
- **Naming Conventions**: Established `[Verb][Noun].cs` file naming with nested types named simply `Command`, `Query`, `Validator`, `Handler`
- **Assembly Scanning**: Documented that MediatR and FluentValidation automatically discover nested handlers and validators
- **Project Structure Update**: Updated canonical structure to reflect co-location pattern in Core Principles and Project Structure Standards

**Rationale**: Single source of truth per use case, reduced navigation between folders, improved maintainability. Industry pattern used by ConciergeWorkflowServices, FastEndpoints, and Jimmy Bogard's Vertical Slice Architecture.

---

### Version 1.6.0 (2025-10-28)

**Type**: MINOR - MediatR Pipeline Behaviors

**Changes**:
- **Pipeline Behaviors Mandated**: Established `Infrastructure/Behaviors/` for `ValidationPipelineBehavior` and `RequestLoggingPipelineBehavior`
- **Cross-Cutting Concerns**: Centralized validation and logging as pipeline behaviors instead of handler boilerplate

**Rationale**: Reduces duplication in handlers, ensures consistent validation and logging across all CQRS operations, aligns with MediatR best practices.

---

### Version 1.5.0 (2025-10-28)

**Type**: MINOR - Assembly Scanning

**Changes**:
- **Assembly Scanning Pattern**: Established automatic discovery of MediatR handlers and FluentValidation validators via assembly scanning
- **Registration Simplification**: Eliminated requirement for manual handler registration in DI

**Rationale**: Reduce boilerplate, improve developer experience, prevent registration errors.

---

### Version 1.4.0 (2025-10-28)

**Type**: MINOR - Options Pattern Mandate

**Changes**:
- **Options Pattern Required**: Made .NET Options Pattern (`IOptions<T>`) mandatory for all configuration management
- **Direct IConfiguration Prohibited**: Explicitly prohibited direct `IConfiguration` access with string keys
- **Options Organization**: Mandated `*Options` classes in `Shared/Options/` with `IValidateOptions<T>` validators in `Shared/Options/Validation/`
- **Interface Documentation**: Documented `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>` usage patterns

**Rationale**: Type-safe configuration with startup validation, IntelliSense support, testability, eliminates magic strings and runtime configuration errors.

---

### Version 1.3.0 (2025-10-21)

**Type**: MINOR - Magic Strings & Constants

**Changes**:
- **Magic Strings Prohibited**: Explicitly prohibited hardcoded strings for configuration keys, shared definitions, and identifiers
- **Constants Organization**: Mandated centralized constants in `Shared/Constants/` directory (VSA-compliant)
- **Allowed Exceptions**: Defined that logging messages, diagnostic output, and user-facing text may use literals
- **Naming Conventions**: Established naming rules for constant classes (ending in "Constants", "Keys", "Names", "Providers")
- **Documentation Requirements**: Required XML documentation for all constants

**Rationale**: Eliminate maintenance burden and runtime errors from magic strings, provide type safety and IDE autocomplete, establish single source of truth for shared identifiers.

---

### Version 1.2.0 (2025-10-16)

**Type**: MINOR - Project Structure Standards

**Changes**:
- **Canonical Structure**: Documented official directory layout for Vertical Slice Architecture
- **Structural Rules**: Added explicit rules for feature organization, naming conventions, and file placement
- **VSA Guidance**: Enhanced Principle IV with reference to Project Structure Standards section

**Rationale**: Prevent VSA implementation drift, provide clear structural guidance for new features, ensure consistency across codebase.

---

### Version 1.1.0 (2025-10-02)

**Type**: MINOR - Serilog Logging Mandate

**Changes**:
- **Serilog Required**: Made Serilog the mandatory logging framework (organizational standard)
- **Logging Standards**: Added structured logging requirements, log levels, and security guidelines
- **Sensitive Data**: Added prohibition on logging secrets or sensitive user data

**Rationale**: Organizational standardization, structured logging best practices, security compliance.

---

### Version 1.0.0 (2025-10-01)

**Type**: MAJOR - Initial Constitution

**Changes**:
- Established 8 core principles (Modern .NET, CLI-First, Test-First, DRY, VSA, Configuration, Versioning, Secrets)
- Defined architecture and design standards
- Set quality and testing requirements (80% coverage minimum)
- Established development and operations standards
- Created governance structure and amendment process

**Rationale**: Initial ratification of project constitutional principles.

---

**Version**: 1.8.0 | **Ratified**: 2025-01-15 | **Last Amended**: 2025-01-19
