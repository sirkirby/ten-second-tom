
# Implementation Plan: Ten Second Tom - Personal Memory Management CLI

**Branch**: `001-ten-second-tom` | **Date**: October 1, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-ten-second-tom/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from file system structure or context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, `GEMINI.md` for Gemini CLI, `QWEN.md` for Qwen Code or `AGENTS.md` for opencode).
7. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Ten Second Tom is a CLI application for personal memory management that guides users through daily reflection prompts, leverages LLM APIs (OpenAI/Anthropic) to generate structured summaries, and stores memory entries in a markdown-based file system. The application supports daily (`/today`) and weekly (`/thisweek`) commands with customizable LLM prompt templates, SSH key-based authentication, and user-configurable data retention policies. All memory entries and LLM-generated summaries are stored as markdown files in an organized directory structure under `./.memory/`, with responses rendered to the terminal in formatted markdown.

## Technical Context
**Language/Version**: C# with .NET 9  
**Primary Dependencies**: 
- System.CommandLine (CLI framework)
- Official OpenAI .NET SDK
- Official Anthropic .NET SDK (or popular OSS alternative if official unavailable)
- Markdig (markdown parsing and rendering)
- SSH.NET or similar for SSH key authentication
- Microsoft.Extensions.Configuration (configuration management)
- Microsoft.Extensions.DependencyInjection (DI container)
- Serilog (logging framework - organizational standard)
- Serilog.Sinks.Console (console output)
- Serilog.Sinks.File (file-based logs)
- Serilog.Extensions.Logging (Microsoft.Extensions.Logging integration)
- Serilog.Enrichers.Environment (environment enrichers)
- Serilog.Settings.Configuration (appsettings.json configuration)

**Storage**: File system (markdown files in `./.memory/` directory structure), abstracted with provider pattern for future database/blob storage support  
**Testing**: xUnit with FluentAssertions and Moq/NSubstitute  
**Target Platform**: Cross-platform CLI (macOS, Windows, Linux)  
**Project Type**: Single CLI project with vertical slice architecture  
**Performance Goals**: 
- CLI command response time < 500ms (excluding LLM API calls)
- LLM API calls expected 2-10 seconds depending on provider
- Markdown file I/O < 100ms per operation

**Constraints**: 
- No secrets in source control (use .NET User Secrets for dev, environment variables for production)
- SSH keys from standard locations (~/.ssh/id_ed25519 or ~/.ssh/id_rsa)
- Self-contained deployment for macOS (Homebrew) and Windows (Chocolatey/winget)
- Persistent authentication sessions until explicit logout
- Multiple daily entries allowed per day with timestamp-based naming

**Scale/Scope**: 
- Single-user local application
- Expected 1-10 daily entries per user
- Weekly reviews aggregate 7 days of data
- Support for years of accumulated memory entries
- Prompt templates stored as markdown resources in project

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Modern .NET & Idiomatic C#
- [x] Using .NET 9 with modern C# idioms
- [x] Following Microsoft C# coding conventions
- [x] Leveraging nullable reference types, pattern matching, records
- [x] Using async/await for I/O-bound operations (file system, LLM API calls)
- [x] Using Serilog as logging framework (organizational standard per constitution v1.1.0)

### II. CLI-First Interface
- [x] Command-line interface with System.CommandLine
- [x] No web or GUI dependencies
- [x] Supporting standard CLI patterns (/today, /thisweek, /logout)
- [x] Clear error messages and help commands
- [x] Supporting both interactive and scripted usage

### III. Test-First (NON-NEGOTIABLE)
- [x] 80% minimum test coverage requirement documented
- [x] xUnit as testing framework
- [x] TDD approach: tests written before implementation
- [x] Tests must be fast, isolated, deterministic
- [x] Coverage includes unit, integration, and CLI command tests

### IV. DRY & Design Patterns
- [x] Vertical Slice Architecture for feature organization
- [x] CQRS for commands (CreateDailyEntry, CreateWeeklyReview) and queries (SearchMemories, GetEntries)
- [x] Factory pattern for LLM provider instantiation (OpenAI vs Anthropic)
- [x] Provider pattern for storage abstraction (IMemoryStorageProvider)
- [x] No code duplication - reusable components for prompt loading, markdown rendering, file I/O

### V. Semantic Versioning & Automated Releases
- [x] GitHub Actions for automated releases on merge to main
- [x] Semantic versioning (MAJOR.MINOR.PATCH)
- [x] Automated release notes generation
- [x] Version tracking in project files

### VI. Cross-Platform Distribution
- [x] Self-contained executables for macOS and Windows
- [x] Homebrew support (macOS)
- [x] Chocolatey/winget support (Windows)
- [x] Automated publishing via GitHub Actions
- [x] No external dependencies required for end users

### VII. Local Development Excellence
- [x] Clear README with setup instructions
- [x] Fast build and test cycles
- [x] Comprehensive IDE debugging support
- [x] Local secrets via .NET User Secrets
- [x] Example configuration provided

### VIII. Secrets Management
- [x] No secrets in source control
- [x] .NET User Secrets for development (OpenAI/Anthropic API keys)
- [x] Environment variables for production
- [x] SSH key location from standard paths (~/.ssh/)
- [x] Example config files provided (without real secrets)

**Constitution Compliance**: ✅ PASS - All constitutional requirements met

## Project Structure

### Documentation (this feature)
```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)

```
src/
├── Features/                    # Vertical slices
│   ├── Today/                  # /today command feature
│   │   ├── Commands/
│   │   │   └── CreateDailyEntryCommand.cs
│   │   ├── Handlers/
│   │   │   └── CreateDailyEntryHandler.cs
│   │   └── Validation/
│   │       └── CreateDailyEntryValidator.cs
│   ├── ThisWeek/               # /thisweek command feature
│   │   ├── Commands/
│   │   │   └── CreateWeeklyReviewCommand.cs
│   │   ├── Handlers/
│   │   │   └── CreateWeeklyReviewHandler.cs
│   │   └── Validation/
│   │       └── CreateWeeklyReviewValidator.cs
│   ├── Search/                 # Memory search feature
│   │   ├── Queries/
│   │   │   └── SearchMemoriesQuery.cs
│   │   ├── Handlers/
│   │   │   └── SearchMemoriesHandler.cs
│   │   └── Validation/
│   └── Auth/                   # Authentication feature
│       ├── Commands/
│       │   └── LogoutCommand.cs
│       ├── Services/
│       │   └── SshAuthenticationService.cs
│       └── Models/
├── Infrastructure/              # Cross-cutting concerns
│   ├── LLM/                    # LLM provider abstractions
│   │   ├── ILlmProvider.cs
│   │   ├── OpenAiProvider.cs
│   │   ├── AnthropicProvider.cs
│   │   └── LlmProviderFactory.cs
│   ├── Storage/                # Storage abstractions
│   │   ├── IMemoryStorageProvider.cs
│   │   ├── FileSystemStorageProvider.cs
│   │   └── Models/
│   │       └── MemoryEntry.cs
│   ├── Prompts/                # Prompt template management
│   │   ├── IPromptTemplateLoader.cs
│   │   ├── PromptTemplateLoader.cs
│   │   └── Templates/          # Embedded markdown templates
│   │       ├── daily-summary.md
│   │       └── weekly-review.md
│   ├── Configuration/
│   │   └── TenSecondTomOptions.cs
│   └── Markdown/
│       ├── IMarkdownRenderer.cs
│       └── MarkdownRenderer.cs
├── Commands/                    # CLI command definitions
│   ├── TodayCommand.cs
│   ├── ThisWeekCommand.cs
│   ├── SearchCommand.cs
│   └── LogoutCommand.cs
└── Program.cs                   # Entry point with DI setup

tests/
├── Unit/                        # Fast, isolated unit tests
│   ├── Features/
│   │   ├── Today/
│   │   │   └── CreateDailyEntryHandlerTests.cs
│   │   ├── ThisWeek/
│   │   │   └── CreateWeeklyReviewHandlerTests.cs
│   │   └── Search/
│   └── Infrastructure/
│       ├── LLM/
│       │   ├── OpenAiProviderTests.cs
│       │   ├── AnthropicProviderTests.cs
│       │   └── LlmProviderFactoryTests.cs
│       ├── Storage/
│       │   └── FileSystemStorageProviderTests.cs
│       └── Prompts/
│           └── PromptTemplateLoaderTests.cs
├── Integration/                 # Component integration tests
│   ├── Features/
│   │   ├── Today/
│   │   │   └── DailyEntryWorkflowTests.cs
│   │   └── ThisWeek/
│   │       └── WeeklyReviewWorkflowTests.cs
│   ├── Cli/                    # CLI command tests
│   │   ├── TodayCommandTests.cs
│   │   ├── ThisWeekCommandTests.cs
│   │   └── SearchCommandTests.cs
│   └── Storage/
│       └── FileSystemIntegrationTests.cs
└── TestHelpers/                 # Shared test utilities
    ├── Fixtures/
    │   ├── TestMemoryEntries.cs
    │   └── TestPromptTemplates.cs
    └── Mocks/
        └── MockLlmProvider.cs

.memory/                         # User memory storage (gitignored)
├── today/
│   ├── 10-01-2025_1.md
│   └── 10-01-2025_2.md
└── thisweek/
    └── 2025-40_1.md
```

**Structure Decision**: Single CLI project using Vertical Slice Architecture. Each feature (Today, ThisWeek, Search, Auth) is self-contained with its own commands/queries, handlers, and validation. Infrastructure layer provides cross-cutting concerns (LLM providers, storage, prompts, markdown rendering). Test structure mirrors source organization with unit, integration, and CLI-specific tests.

## Phase 0: Outline & Research
1. **Extract unknowns from Technical Context** above:
   - For each NEEDS CLARIFICATION → research task
   - For each dependency → best practices task
   - For each integration → patterns task

2. **Generate and dispatch research agents**:
   ```
   For each unknown in Technical Context:
     Task: "Research {unknown} for {feature context}"
   For each technology choice:
     Task: "Find best practices for {tech} in {domain}"
   ```

3. **Consolidate findings** in `research.md` using format:
   - Decision: [what was chosen]
   - Rationale: [why chosen]
   - Alternatives considered: [what else evaluated]

**Output**: research.md with all NEEDS CLARIFICATION resolved

## Phase 1: Design & Contracts
*Prerequisites: research.md complete*

1. **Extract entities from feature spec** → `data-model.md`:
   - Entity name, fields, relationships
   - Validation rules from requirements
   - State transitions if applicable

2. **Generate API contracts** from functional requirements:
   - For each user action → endpoint
   - Use standard REST/GraphQL patterns
   - Output OpenAPI/GraphQL schema to `/contracts/`

3. **Generate contract tests** from contracts:
   - One test file per endpoint
   - Assert request/response schemas
   - Tests must fail (no implementation yet)

4. **Extract test scenarios** from user stories:
   - Each story → integration test scenario
   - Quickstart test = story validation steps

5. **Update agent file incrementally** (O(1) operation):
   - Run `.specify/scripts/bash/update-agent-context.sh copilot`
     **IMPORTANT**: Execute it exactly as specified above. Do not add or remove any arguments.
   - If exists: Add only NEW tech from current plan
   - Preserve manual additions between markers
   - Update recent changes (keep last 3)
   - Keep under 150 lines for token efficiency
   - Output to repository root

**Output**: data-model.md, /contracts/*, failing tests, quickstart.md, agent-specific file

## Phase 2: Task Planning Approach
*This section describes what the /tasks command will do - DO NOT execute during /plan*

### Task Generation Strategy

**When /tasks command is invoked, it will:**

1. **Extract From Contracts**: Each contract file (`contracts/*.md`) generates multiple tasks:
   - Test task: Implement unit tests from "Test Specifications" section
   - Handler task: Implement command handler from pseudocode
   - Validator task: Implement validation logic (if contract specifies validators)
   - CLI task: Wire up command to System.CommandLine

2. **Extract From Data Model**: `data-model.md` generates:
   - Model task: Implement each entity as C# record
   - Validation task: Implement validation rules using FluentValidation
   - Factory task: Implement factory methods for complex construction

3. **Infrastructure Tasks**: Derived from research.md technology decisions:
   - Storage provider task: Implement `IMemoryStorageProvider` and `FileSystemStorageProvider`
   - Configuration task: Set up `appsettings.json`, User Secrets, environment variables
   - DI task: Configure service registration in `Program.cs`
   - Logging task: Configure Serilog with console sink
   - Auth task: Implement SSH key discovery and session management
   - LLM task: Implement `ILlmProvider` with OpenAI and Anthropic implementations
   - Prompt task: Create embedded markdown templates with variable substitution

4. **Test Infrastructure Tasks**:
   - Test helpers: Shared test utilities, mocks, fixtures
   - Integration tests: CLI command end-to-end tests
   - Test data: Sample markdown files for testing storage operations

### Task Ordering Principles

Tasks will be ordered to follow **Test-Driven Development** flow and **dependency order**:

1. **Foundation First** [P] (no dependencies - can run in parallel):
   - Data models (entities only, no dependencies)
   - Interfaces (`IMemoryStorageProvider`, `ILlmProvider`, `IPromptTemplateLoader`)
   - Test helpers and fixtures

2. **Infrastructure Second** (depends on interfaces):
   - Configuration setup
   - Logging setup
   - Storage provider implementation + tests [P]
   - LLM provider implementation + tests [P]
   - Prompt template loader + tests [P]
   - Auth service implementation + tests

3. **Feature Slices Third** (depends on infrastructure):
   - For each command (Today, ThisWeek, Search, Auth):
     - Write unit tests FIRST (from contract test specs)
     - Implement command handler
     - Implement validator
     - Wire up CLI command
     - Write integration test

4. **Integration Last**:
   - Program.cs service registration
   - End-to-end CLI tests
   - Cross-feature integration tests (e.g., weekly review depends on daily entries)

### Task Granularity

- **Small, focused tasks**: Each task should take 30-60 minutes
- **Testable increments**: Each task should result in passing tests
- **Independent when possible**: Minimize task dependencies to allow parallel work (marked [P])
- **Acceptance criteria**: Each task includes specific validation steps

### Estimated Task Count

Based on contracts, data model, and infrastructure needs:
- **Data Models**: 6 tasks (1 per entity) [P]
- **Validation**: 4 tasks (validators for command models) [P]
- **Infrastructure**: 8 tasks (storage, LLM, prompts, auth, config, logging, DI, test helpers)
- **Today Feature**: 4 tasks (tests, handler, validator, CLI integration)
- **ThisWeek Feature**: 4 tasks (tests, handler, validator, CLI integration)
- **Search Feature**: 3 tasks (tests, handler, CLI integration)
- **Auth Feature**: 3 tasks (tests, handlers, CLI integration)
- **Integration Tests**: 3 tasks (CLI tests, cross-feature tests, error path tests)

**Total: ~35 tasks**

### Constitution Compliance

All tasks will enforce:
- ✅ TDD: Test tasks always before implementation tasks
- ✅ xUnit: Only xUnit test framework used
- ✅ 80% Coverage: Each feature slice has comprehensive test coverage
- ✅ DRY: Shared utilities extracted to helper classes
- ✅ XML Docs: Public APIs documented
- ✅ Error Handling: Result<T> pattern, structured logging
- ✅ Modern C#: Nullable types, records, pattern matching

---

**⚠️ IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan.  
**✅ Phase 2 COMPLETE**: Strategy documented. Ready for /tasks command to generate tasks.md.



## Phase 3+: Future Implementation
*These phases are beyond the scope of the /plan command*

**Phase 3**: Task execution (/tasks command creates tasks.md)  
**Phase 4**: Implementation (execute tasks.md following constitutional principles)  
**Phase 5**: Validation (run tests, execute quickstart.md, performance validation)

## Complexity Tracking
*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |


## Progress Tracking
*This checklist is updated during execution flow*

**Phase Status**:
- [x] Phase 0: Research complete (/plan command)
- [x] Phase 1: Design complete (/plan command)
- [x] Phase 2: Task planning approach documented (/plan command)
- [x] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation in progress
- [ ] Phase 5: Validation pending

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (none - all checks passed)

**Deliverables Generated**:
- [x] research.md - All technology decisions documented
- [x] data-model.md - Entities, relationships, validation rules
- [x] contracts/CreateDailyEntryCommand.md - Daily reflection contract
- [x] contracts/CreateWeeklyReviewCommand.md - Weekly review contract
- [x] quickstart.md - User onboarding guide
- [x] tasks.md - 70 numbered tasks in dependency order (TDD enforced)

---
*Based on Constitution v1.0.0 - See `.specify/memory/constitution.md`*
