# Implementation Plan: Improved Prompt Template Support

**Branch**: `007-improved-prompt-template` | **Date**: 2025-10-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-improved-prompt-template/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Add support for filesystem-based prompt templates that users can edit and customize. During guided setup, default daily and weekly templates are automatically installed to the configured memory directory's templates/ subdirectory. Users can select which template to use when generating summaries, with templates filtered by type (daily/weekly). Templates use YAML front matter metadata to indicate their type and properties. Existing users are automatically migrated via configuration validation, with the system self-healing by recreating missing templates or falling back to embedded defaults.

## Technical Context

**Language/Version**: C# 12+ / .NET 9
**Primary Dependencies**: System.CommandLine, YamlDotNet, Serilog, Spectre.Console
**Storage**: File system (markdown files with YAML front matter in `{MemoryDirectory}/templates/`)
**Testing**: xUnit, FluentAssertions, Moq/NSubstitute (80% minimum coverage required)
**Target Platform**: macOS, Windows (cross-platform CLI)
**Project Type**: Single CLI application (Vertical Slice Architecture)
**Performance Goals**: Template loading <100ms, template selection UI <10s, setup/migration <2s
**Constraints**: 1MB max template file size, must work with existing embedded templates, must support concurrent template reads, graceful degradation on errors
**Scale/Scope**: ~5-20 templates per user, template directory auto-managed, supports custom user templates

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Principle I: Modern .NET & Idiomatic C#
✅ **PASS** - Will use C# 12+ features (records for models, file-scoped namespaces, nullable reference types, primary constructors where appropriate)
✅ **PASS** - Serilog already in use for logging template operations
✅ **PASS** - Modern async/await patterns for file I/O operations

### Principle II: CLI-First Interface
✅ **PASS** - Feature enhances existing CLI commands (`today`, `thisweek`) with template selection
✅ **PASS** - Uses Spectre.Console for interactive template selection UI
✅ **PASS** - All interaction through terminal, no web/GUI dependencies

### Principle III: Test-First (NON-NEGOTIABLE)
✅ **PASS** - TDD approach required: write tests before implementation
✅ **PASS** - Target 80%+ coverage using xUnit
✅ **PASS** - Tests organized by feature vertical slice
- Unit tests for template loader, parser, validator
- Integration tests for template selection workflow
- CLI command tests for end-to-end scenarios

### Principle IV: DRY & Design Patterns
✅ **PASS** - Follows Vertical Slice Architecture (templates feature is self-contained)
✅ **PASS** - CQRS pattern: queries for loading templates, commands for creating/migrating
✅ **PASS** - Factory pattern for template loader instantiation
✅ **PASS** - Reuses existing patterns: Result<T> for error handling, IPromptTemplateLoader interface already exists

### Principle V: Semantic Versioning & Automated Releases
✅ **PASS** - No breaking changes to existing API
✅ **PASS** - New feature = MINOR version bump
✅ **PASS** - Automated GitHub release workflow already in place

### Principle VI: Cross-Platform Distribution
✅ **PASS** - File system operations use Path.Combine for cross-platform compatibility
✅ **PASS** - Templates directory respects platform-specific user directories
✅ **PASS** - No platform-specific code required

### Principle VII: Local Development Excellence
✅ **PASS** - Clear feature documentation in implementation plan
✅ **PASS** - Fast test execution (file I/O mocked in unit tests)
✅ **PASS** - Easy to test locally with sample templates

### Principle VIII: Secrets Management
✅ **PASS** - No secrets stored in templates
✅ **PASS** - Templates stored in user's home directory (not source control)
✅ **PASS** - No new secrets required for this feature

### Overall Gate Status: ✅ PASS
All constitutional principles satisfied. No violations requiring justification. Ready to proceed to Phase 0.

---

## Post-Design Constitution Re-Check

*Evaluated after Phase 1 design completion*

### Principle I: Modern .NET & Idiomatic C# - ✅ CONFIRMED
- Design uses `sealed record` for all models (TemplateMetadata, TemplateListItem)
- Leverages primary constructors where appropriate
- File-scoped namespaces throughout
- Async/await patterns for all I/O operations
- Nullable reference types properly annotated
- **No issues identified**

### Principle II: CLI-First Interface - ✅ CONFIRMED
- Spectre.Console SelectionPrompt used for template selection
- No web or GUI dependencies introduced
- Text-based output for all operations
- Standard CLI patterns maintained
- **No issues identified**

### Principle III: Test-First (NON-NEGOTIABLE) - ✅ CONFIRMED
- Quick start guide enforces TDD approach (Day 1-9 plan)
- Test structure documented with AAA pattern examples
- 80% coverage target maintained
- Unit tests, integration tests, and E2E tests planned
- **No issues identified**

### Principle IV: DRY & Design Patterns - ✅ CONFIRMED
- Vertical Slice Architecture: Templates feature fully encapsulated
- CQRS: Clear separation (InstallDefaultTemplatesCommand, ListTemplatesQuery)
- Factory Pattern: Template loader instantiation
- Result<T> pattern consistently used
- No duplication introduced
- **No issues identified**

### Principle V: Semantic Versioning & Automated Releases - ✅ CONFIRMED
- No breaking changes to existing APIs
- New feature = MINOR version bump
- Backward compatible design
- **No issues identified**

### Principle VI: Cross-Platform Distribution - ✅ CONFIRMED
- Path.Combine used for all file paths
- No platform-specific APIs used
- File operations compatible with macOS and Windows
- **No issues identified**

### Principle VII: Local Development Excellence - ✅ CONFIRMED
- Quick start guide provides clear implementation steps
- Test patterns documented
- Troubleshooting section included
- Fast test execution (mocked file I/O)
- **No issues identified**

### Principle VIII: Secrets Management - ✅ CONFIRMED
- No secrets stored in templates
- Templates stored in user directory (not source control)
- No new secrets required
- **No issues identified**

### Design Quality Assessment

**Strengths**:
1. Clean separation of concerns (VSA)
2. Comprehensive error handling with Result<T>
3. Self-healing design (auto-recreate templates)
4. Graceful degradation (fallback to embedded)
5. Clear validation rules at multiple levels
6. Well-documented contracts and data models
7. Idempotent operations (safe to retry)
8. Backward compatible with existing code

**Potential Concerns**: None identified

**Complexity Assessment**:
- Total new files: ~15
- Modified files: ~7
- Total test files: ~10
- Estimated LOC: ~2000 (implementation + tests)
- **Verdict**: Appropriate complexity for feature scope

### Final Gate Status: ✅ PASS

All constitutional principles remain satisfied after detailed design. No concerns or violations identified. Design is clean, testable, and maintainable. Ready to proceed to Phase 2 (task generation via `/speckit.tasks`).

## Project Structure

### Documentation (this feature)

```
specs/007-improved-prompt-template/
├── plan.md              # This file (/speckit.plan command output)
├── spec.md              # Feature specification (already exists)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```
src/
├── Features/
│   ├── Templates/                              # NEW: Template management feature
│   │   ├── Commands/
│   │   │   └── InstallDefaultTemplatesCommand.cs      # For setup/migration
│   │   ├── Queries/
│   │   │   └── ListTemplatesQuery.cs                  # List available templates
│   │   ├── Handlers/
│   │   │   ├── InstallDefaultTemplatesHandler.cs
│   │   │   └── ListTemplatesQueryHandler.cs
│   │   ├── Validation/
│   │   │   └── TemplateValidator.cs                   # Validate template metadata
│   │   └── Models/
│   │       ├── TemplateMetadata.cs                    # YAML front matter model
│   │       └── TemplateListItem.cs                    # For selection UI
│   ├── Today/                                  # EXISTING: Enhanced for template selection
│   │   └── Commands/CreateDailyEntryCommand.cs        # Updated
│   ├── ThisWeek/                               # EXISTING: Enhanced for template selection
│   │   └── Commands/CreateWeeklyReviewCommand.cs      # Updated
│   └── Setup/                                  # EXISTING: Enhanced for template installation
│       └── Handlers/SetupCommandHandler.cs            # Updated
├── Infrastructure/
│   ├── Prompts/
│   │   ├── IPromptTemplateLoader.cs            # EXISTING: Enhanced interface
│   │   ├── EmbeddedPromptTemplateLoader.cs     # EXISTING: Updated implementation
│   │   ├── FileSystemTemplateLoader.cs         # NEW: Loads from filesystem with YAML parsing
│   │   └── Templates/                          # EXISTING: Embedded default templates
│   │       ├── daily-summary.md                # Updated with YAML front matter
│   │       └── weekly-review.md                # Updated with YAML front matter
│   ├── Configuration/
│   │   └── ConfigurationChecker.cs             # EXISTING: Enhanced for migration
│   └── Cli/
│       └── TemplateSelectionUI.cs              # NEW: Spectre.Console template picker
└── Shared/
    └── Models/
        └── PromptTemplate.cs                   # EXISTING: May need metadata field

tests/
├── Unit/
│   └── Features/
│       └── Templates/
│           ├── InstallDefaultTemplatesHandlerTests.cs
│           ├── ListTemplatesQueryHandlerTests.cs
│           ├── FileSystemTemplateLoaderTests.cs
│           └── TemplateValidatorTests.cs
└── Integration/
    ├── Cli/
    │   ├── TemplateSelectionTests.cs
    │   └── SetupWithTemplatesTests.cs
    └── Features/
        └── Templates/
            └── TemplateWorkflowTests.cs
```

**Structure Decision**: Single CLI project using Vertical Slice Architecture. The Templates feature is organized as a self-contained vertical slice under `src/Features/Templates/` with all its layers (Commands, Queries, Handlers, Validation, Models). Existing features (Today, ThisWeek, Setup) are enhanced to integrate with the new template system. Infrastructure components provide cross-cutting concerns like file system template loading and YAML parsing.

## Complexity Tracking

*No violations detected - this section intentionally left empty.*
