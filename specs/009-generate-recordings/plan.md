# Implementation Plan: Generate Command for Recording Processing

**Branch**: `009-generate-recordings` | **Date**: 2025-10-24 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/009-generate-recordings/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

The `generate` command enables users to process recorded transcripts with custom prompt templates via LLM providers. Users can interactively browse available recordings and templates, or use the `--template` argument for non-interactive execution. The feature includes a new bundled businessMeeting template for meeting summarization and supports re-processing recordings with different templates while preserving all outputs.

## Technical Context

**Language/Version**: C# 10+ with .NET 9
**Primary Dependencies**: System.CommandLine 2.0-rc, Spectre.Console 0.51.1, FluentValidation 12.0.0, Serilog 4.3.0
**Storage**: Local filesystem (recording directory for transcripts, template directory for prompt templates, memory directory for outputs)
**Testing**: xUnit, FluentAssertions, NSubstitute/Moq (80% minimum coverage per constitution)
**Target Platform**: macOS (primary), Windows (secondary), cross-platform CLI
**Project Type**: Single CLI application with Vertical Slice Architecture
**Performance Goals**: Interactive command response <500ms (excluding LLM processing), file operations <100ms, support 100+ recordings without UI degradation
**Constraints**:
- Must follow existing Today command patterns for template selection
- Token limit handling for LLM providers (configurable):
  - OpenAI models (GPT-4o, GPT-4o Mini): 128K context window, default safe limit 50K input tokens
  - Anthropic models (Claude 3-4 series): 200K context window, default safe limit 80K input tokens
- Must support interactive menu selection using Spectre.Console
- Must integrate with existing LLM provider abstraction (ILlmProvider from Infrastructure/Llm)
- Recording filename format: `M-D-Y_Increment.*` (e.g., `10-21-2025_1.txt`, `10-21-2025_1.wav`)
- Output filename format: `M-D-Y_TemplateName_Increment.md` (e.g., `10-21-2025_daily-summary_1.md`)
- Recording base name: M-D-Y_Increment (e.g., `10-21-2025_1`)
- Template name: filename without extension (e.g., "daily-summary" from "daily-summary.md")
**Scale/Scope**:
- Expected 10-100 recordings per user
- Template library: 2-20 templates
- Transcript size: typically 500-5000 words, max ~65K tokens (50K for OpenAI safe limit)
- Single-user local execution (no concurrency concerns)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Modern .NET & Idiomatic C#
✅ **PASS** - Using C# 10+ with .NET 9, following existing patterns from Today/Templates features
- Modern C# features: records, file-scoped namespaces, nullable reference types
- Async/await for LLM operations
- Serilog for structured logging

### II. CLI-First Interface
✅ **PASS** - Pure CLI command using System.CommandLine and Spectre.Console
- Command-line only, no web/GUI dependencies
- Interactive menu selection for recordings and templates
- Non-interactive `--template` argument for scripting
- Clear error messages and user feedback

### III. Test-First (NON-NEGOTIABLE)
✅ **PASS** - TDD approach required per constitution
- Unit tests for handlers and business logic (command/query handlers)
- Integration tests for CLI command execution
- 80% minimum coverage enforced
- Tests mirror source structure in TenSecondTom.Tests/Features/Generate/

### IV. DRY & Design Patterns
✅ **PASS** - Following established patterns
- Vertical Slice Architecture: Features/Generate/ with Commands, Queries, Handlers
- CQRS: Separate commands (GenerateOutputCommand) from queries (ListRecordingsQuery, ListTemplatesQuery)
- Reusing existing template selection patterns from Today feature
- Leveraging shared LLM provider abstraction
- DependencyInjection.cs for feature registration

### V. Semantic Versioning & Automated Releases
✅ **PASS** - No impact on release process
- MINOR version bump (new feature, backward compatible)
- Automated release on PR merge to main
- No breaking changes to existing commands

### VI. Cross-Platform Distribution
✅ **PASS** - No platform-specific dependencies
- File I/O via System.IO.Abstractions (testable)
- Cross-platform path handling
- Works on macOS and Windows

### VII. Local Development Excellence
✅ **PASS** - Follows existing development patterns
- Cloneable and runnable with existing setup
- Fast build and test cycles
- Integrates with existing project structure
- Clear feature organization

### VIII. Secrets Management
✅ **PASS** - No new secrets required
- Uses existing LLM API key configuration
- No hardcoded credentials

### Magic Strings & Constants (Constitution v1.3.0)
⚠️ **NEEDS ATTENTION** - Must add new constants to Shared/Constants/
- CommandNames.Generate (new command name)
- DirectoryNames.Recording (if not already defined)
- TemplateConstants.BusinessMeetingTemplateId (new template type)
- Potentially: LlmConstants for token limits

**GATE STATUS**: ✅ PASS - All principles satisfied, minor constants work needed in implementation

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Features/Generate/                    # New vertical slice for generate command
├── Commands/
│   └── GenerateOutputCommand.cs         # Main command for processing recordings
├── Queries/
│   ├── ListRecordingsQuery.cs           # Query available recordings from storage
│   └── GetRecordingTranscriptQuery.cs   # Load specific transcript content
├── Handlers/
│   ├── GenerateOutputCommandHandler.cs  # Core business logic for generation
│   ├── ListRecordingsQueryHandler.cs    # Recording discovery and sorting
│   └── GetRecordingTranscriptQueryHandler.cs  # Transcript loading
├── Models/
│   ├── RecordingListItem.cs             # Display model for recording selection
│   └── GeneratedOutput.cs               # Result model with output content and metadata
├── Services/
│   ├── IRecordingService.cs             # Abstraction for recording operations
│   ├── RecordingService.cs              # Recording file discovery and parsing
│   ├── IOutputStorageService.cs         # Abstraction for output file operations
│   └── OutputStorageService.cs          # Saves generated outputs to filesystem
└── DependencyInjection.cs               # Feature service registration

src/Shared/Constants/
├── CommandNames.cs                       # Add: Generate
└── TemplateConstants.cs                  # Add: BusinessMeetingTemplateId

src/Infrastructure/Prompts/Templates/
└── business-meeting.md                   # New bundled template

tests/TenSecondTom.Tests/Features/Generate/
├── Commands/
│   └── GenerateOutputCommandTests.cs
├── Queries/
│   ├── ListRecordingsQueryTests.cs
│   └── GetRecordingTranscriptQueryTests.cs
├── Handlers/
│   ├── GenerateOutputCommandHandlerTests.cs
│   ├── ListRecordingsQueryHandlerTests.cs
│   └── GetRecordingTranscriptQueryHandlerTests.cs
└── Services/
    ├── RecordingServiceTests.cs
    └── OutputStorageServiceTests.cs

tests/TenSecondTom.IntegrationTests/Features/Generate/
└── GenerateCommandIntegrationTests.cs    # End-to-end CLI command tests
```

**Structure Decision**: Single project following Vertical Slice Architecture. The Generate feature is fully self-contained in `src/Features/Generate/` with all layers (Commands, Queries, Handlers, Models, Services). This mirrors the existing Today and Templates feature organization. Integration points are through shared abstractions (ILlmService from infrastructure, PromptTemplate from Shared.Models, StoredRecording from Audio.Models).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

N/A - All constitutional principles satisfied with no violations.

---

## Post-Design Constitution Re-evaluation

**Date**: 2025-10-24 (after Phase 1 completion)

### Design Artifacts Reviewed
- ✅ `research.md` - All NEEDS CLARIFICATION items resolved
- ✅ `data-model.md` - Entities, services, and relationships defined
- ✅ `contracts/` - Commands, queries, and services documented
- ✅ `quickstart.md` - Developer onboarding guide created

### Re-evaluation Results

#### I. Modern .NET & Idiomatic C# - ✅ PASS
**Confirmed**: Design uses modern C# patterns throughout
- Records for immutable value objects (RecordingListItem, GeneratedOutput)
- Primary constructors for service classes
- File-scoped namespaces
- Result<T> pattern for error handling
- Async/await throughout

#### II. CLI-First Interface - ✅ PASS
**Confirmed**: Pure CLI with System.CommandLine and Spectre.Console
- No web/GUI dependencies introduced
- Interactive menu selection for recordings and templates
- Non-interactive `--template` argument for scripting
- Terminal-based UI using Spectre.Console patterns from existing features

#### III. Test-First (NON-NEGOTIABLE) - ✅ PASS
**Confirmed**: TDD approach documented and enforced
- Test structure mirrors source structure
- Unit tests for all services, handlers, and business logic
- Integration tests for end-to-end workflows
- Testing strategy defined in contracts and quickstart
- 80% coverage target maintained

#### IV. DRY & Design Patterns - ✅ PASS
**Confirmed**: No duplication, proper patterns applied
- Vertical Slice Architecture: Features/Generate/ with all layers
- CQRS: Commands (GenerateOutputCommand) and Queries (ListRecordingsQuery, GetRecordingTranscriptQuery) separated
- Service abstractions: IRecordingService, ITranscriptProcessor, IOutputStorageService
- Reusing existing patterns: Templates feature for template selection, ILlmProvider for LLM integration
- Factory pattern for RecordingListItem creation
- DependencyInjection.cs for feature registration

#### V. Semantic Versioning & Automated Releases - ✅ PASS
**Confirmed**: No impact on existing release process
- MINOR version bump appropriate (new feature, backward compatible)
- No breaking changes to existing commands
- Automated release on PR merge to main (existing workflow)

#### VI. Cross-Platform Distribution - ✅ PASS
**Confirmed**: Platform-agnostic design
- System.IO.Abstractions for filesystem operations (Windows/macOS compatible)
- No platform-specific APIs used
- Path handling uses Path.Combine for cross-platform compatibility

#### VII. Local Development Excellence - ✅ PASS
**Confirmed**: Developer experience prioritized
- Comprehensive quickstart.md guide created
- Clear architecture diagrams and patterns documented
- Testing strategies explained
- Common tasks and debugging tips provided
- Follows existing project structure conventions

#### VIII. Secrets Management - ✅ PASS
**Confirmed**: No new secrets introduced
- Uses existing LLM provider configuration (ApiKey from config/secrets)
- No hardcoded credentials in any design artifacts
- Configuration through existing ConfigurationKeys constants

#### Magic Strings & Constants (v1.3.0) - ✅ PASS
**Confirmed**: Constants properly defined
- CommandNames.Generate (new constant identified)
- TemplateConstants.BusinessMeetingTemplateId (new constant identified)
- LlmConstants.cs (new file for token limits and estimation constants)
- DirectoryNames.Recording (verified existing or to be added)
- All constants documented in data-model.md

### Final Gate Status: ✅ PASS

**All constitutional principles satisfied after Phase 1 design.**

No architectural violations. Design is ready for Phase 2 (tasks.md generation via `/speckit.tasks` command).

### Design Quality Metrics

| Metric | Target | Actual | Status |
|--------|--------|--------|--------|
| Test Coverage | ≥ 80% | Planned | ✅ |
| Constants Usage | 100% | 100% | ✅ |
| Service Abstractions | All I/O | All abstracted | ✅ |
| CQRS Separation | Clear | Commands/Queries separated | ✅ |
| VSA Compliance | Full | Features/Generate/ structure | ✅ |
| Documentation | Comprehensive | 4 design docs + quickstart | ✅ |

### Integration Points Verified

| Component | Location | Integration Method |
|-----------|----------|-------------------|
| LLM Provider | Infrastructure/Llm/ILlmProvider | DI injection |
| Template Loading | Features/Templates/ | ListTemplatesQuery |
| Recording Model | Features/Audio/Models/StoredRecording | Reference model for patterns |
| File System | System.IO.Abstractions | DI injection |
| Configuration | Shared/Constants/ConfigurationKeys | Static constants |
| Logging | Serilog via ILogger<T> | DI injection |

**Recommendation**: Proceed to Phase 2 (`/speckit.tasks`) to generate actionable task list for implementation.
