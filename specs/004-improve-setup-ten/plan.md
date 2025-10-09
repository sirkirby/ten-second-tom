
# Implementation Plan: Guided Setup and Configuration Management

**Branch**: `004-improve-setup-ten` | **Date**: October 9, 2025 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-improve-setup-ten/spec.md`

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
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, `GEMINI.md` for Gemini CLI, `QWEN.md` for Qwen Code, or `AGENTS.md` for all other agents).
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

This feature implements a comprehensive guided setup wizard and configuration management system for Ten Second Tom. The primary requirement is to eliminate user confusion during first-time setup by automatically detecting when no configuration exists and launching an interactive wizard that collects all necessary settings: SSH key selection/validation, LLM provider choice, API key entry, memory storage location, and optional settings.

The technical approach includes:
- Auto-detection of first-run state and launch of guided setup before any command execution
- Comprehensive SSH key discovery across multiple sources (SSH agents: system, 1Password, Secretive; file system)
- Secure storage using .NET User Secrets by default with fallback to appsettings.json
- A `/setup` command for manual reconfiguration that walks through all steps with current values as defaults
- A `/config` command for granular, individual setting updates with validation
- Timeout-based operation limits for SSH key detection, API validation, and total setup duration
- Retry logic with exponential backoff for network operations
- Clear, actionable error messages and step-by-step guidance for users

## Technical Context

**Language/Version**: C# 12 with .NET 9.0  
**Primary Dependencies**: System.CommandLine 2.0 (CLI), Spectre.Console 0.51 (interactive UI), FluentValidation 12.0, Serilog 4.3, Microsoft.Extensions.Configuration.UserSecrets 9.0, SSH.NET 2025.0, NSec.Cryptography 25.4  
**Storage**: .NET User Secrets (primary, secure), appsettings.json (fallback), file system (memory directory)  
**Testing**: xUnit with FluentAssertions and Moq  
**Target Platform**: macOS and Windows (cross-platform CLI, self-contained executables)  
**Project Type**: Single CLI project with Vertical Slice Architecture  
**Performance Goals**: Setup wizard response time <500ms per step, SSH key detection <5s, API validation <10s per attempt, total setup <2min  
**Constraints**: No manual file editing required, secure secret storage, graceful degradation for network failures, accessible to non-expert users  
**Scale/Scope**: Single-user CLI application, ~10-15 configuration settings, 3-5 SSH key sources, 2 LLM providers initially

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Modern .NET & Idiomatic C#
- [x] **C# with .NET 9**: Feature uses C# 12 with .NET 9 exclusively
- [x] **Modern patterns**: Leverages nullable reference types, pattern matching, records for DTOs
- [x] **Async/await**: All I/O operations (file access, SSH detection, API validation) use async patterns
- [x] **Serilog**: Uses existing Serilog infrastructure for structured logging throughout setup process

### II. CLI-First Interface
- [x] **Terminal-based**: All interaction via System.CommandLine and Spectre.Console
- [x] **Standard CLI patterns**: `/setup` and `/config` commands follow Unix conventions
- [x] **Scriptable**: Configuration values can be provided via command-line arguments
- [x] **Clear errors**: Validation failures provide specific, actionable error messages

### III. Test-First (NON-NEGOTIABLE)
- [x] **TDD mandatory**: Tests will be written before implementation for all features
- [x] **xUnit framework**: Using existing xUnit test infrastructure
- [x] **80% coverage**: All setup wizard logic, SSH detection, validation, and configuration management must be tested
- [x] **Test structure**: Tests organized in `tests/Unit/Features/Setup/` and `tests/Integration/Features/Setup/`

### IV. DRY & Design Patterns
- [x] **No duplication**: Configuration logic centralized, validation rules reusable
- [x] **CQRS**: Setup operations modeled as commands (SetupCommand, ConfigCommand), queries for reading current state
- [x] **Factory pattern**: SSH key detector factory for different providers (1Password, Secretive, file system)
- [x] **VSA**: Setup feature organized as vertical slice with all layers self-contained

### V. Semantic Versioning & Automated Releases
- [x] **SemVer**: This is a new feature (MINOR version bump)
- [x] **Automated releases**: No changes to release process required
- [x] **Conventional commits**: All commits will follow `feat:` prefix

### VI. Cross-Platform Distribution
- [x] **Self-contained**: Works within existing single-file deployment model
- [x] **Package managers**: No changes to Homebrew/Chocolatey distribution required
- [x] **Platform-specific**: SSH agent detection handles macOS/Windows differences

### VII. Local Development Excellence
- [x] **Easy setup**: No additional dependencies beyond existing project requirements
- [x] **Fast iteration**: Setup wizard can be tested locally without external services
- [x] **IDE support**: Full debugging support in VS Code and Visual Studio

### VIII. Secrets Management
- [x] **Never in source**: All secrets stored in .NET User Secrets or environment variables
- [x] **No hardcoding**: No default API keys or credentials in code
- [x] **Clear documentation**: Setup wizard guides users through secure secret storage

**GATE STATUS**: ✅ PASS - All constitutional principles satisfied

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

```text
src/
├── Features/
│   └── Setup/              # NEW: Setup feature vertical slice
│       ├── Commands/       # SetupCommand, ConfigCommand
│       ├── Handlers/       # SetupCommandHandler, ConfigCommandHandler
│       ├── Queries/        # GetCurrentConfigQuery, ValidateSshKeyQuery
│       ├── Validation/     # SetupCommandValidator, ConfigCommandValidator
│       └── Models/         # SetupProgress, SshKeyInfo, ConfigurationSettings
├── Infrastructure/
│   ├── Auth/               # Existing SSH infrastructure
│   │   └── SshProviders/   # EXTEND: Add provider discovery/selection
│   ├── Cli/                # EXTEND: Add setup/config commands
│   ├── Configuration/      # EXTEND: Add User Secrets management
│   └── Storage/            # Existing storage infrastructure
└── Shared/
    ├── Models/             # Shared configuration models
    └── Results/            # Result<T> types

tests/
├── Unit/
│   └── Features/
│       └── Setup/          # NEW: Setup feature unit tests
│           ├── Commands/
│           ├── Handlers/
│           ├── Queries/
│           └── Validation/
└── Integration/
    └── Features/
        └── Setup/          # NEW: Setup feature integration tests
            ├── SetupWizardTests.cs
            ├── SshKeyDetectionTests.cs
            ├── ApiKeyValidationTests.cs
            └── ConfigCommandTests.cs
```

**Structure Decision**: Single CLI project using Vertical Slice Architecture. The new Setup feature is organized as a self-contained vertical slice in `src/Features/Setup/` containing all necessary layers (commands, handlers, queries, validation, models). Existing infrastructure components (Auth, Configuration, Storage) will be extended where needed to support setup functionality, maintaining separation of concerns while avoiding duplication.

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

**Task Generation Strategy**:

1. **Load Design Artifacts**:
   - Parse data-model.md for entities, commands, queries
   - Parse contracts/*.contract.md for behavior specifications
   - Parse quickstart.md for user scenarios

2. **Generate Test Tasks First (TDD)**:
   - Each entity → unit test task for creation, validation, state transitions
   - Each command/query → unit test task for handler logic
   - Each contract behavior → integration test task
   - Each quickstart scenario → end-to-end test task
   - All test tasks marked [P] for parallel execution where independent

3. **Generate Implementation Tasks**:
   - Core entities and value objects (records, enums)
   - Validators (FluentValidation rules)
   - Service interfaces and implementations (ISshKeyDetector, IApiKeyValidator, etc.)
   - Command/query handlers (SetupCommandHandler, ConfigCommandHandler)
   - CLI commands (setup command, config command with System.CommandLine)
   - Configuration writers (User Secrets, appsettings.json fallback)

4. **Task Dependencies**:
   - Tests MUST be created before implementation (TDD)
   - Value objects before entities that use them
   - Interfaces before implementations
   - Core services before handlers that depend on them
   - Handlers before CLI commands

5. **Estimated Task Count**: 45-55 tasks
   - Phase 0 (Setup): 3-5 tasks (project setup, dependencies)
   - Phase 1 (Entities & Validation): 10-12 tasks (models, validators, tests)
   - Phase 2 (Services): 12-15 tasks (SSH detection, API validation, config I/O, tests)
   - Phase 3 (Handlers): 8-10 tasks (command/query handlers, tests)
   - Phase 4 (CLI): 6-8 tasks (setup wizard, config command, UI, tests)
   - Phase 5 (Integration): 6-8 tasks (end-to-end scenarios from quickstart)

**Ordering Strategy**:
- TDD order: Tests before implementation (RED-GREEN-REFACTOR)
- Dependency order: Foundation (value objects) → Core (entities, services) → Application (handlers) → Presentation (CLI)
- Mark [P] for tasks that can execute in parallel:
  - Independent entity tests
  - Independent service implementations
  - Independent validator tests
- Sequential tasks: Handlers depend on services, CLI depends on handlers

**Task Template Example**:
```
### Task N: [Component] - [Action] [TEST FIRST]

**Type**: Unit Test / Implementation / Integration  
**Dependencies**: Tasks X, Y  
**Parallel**: [P] or Sequential  
**Estimated Time**: Xh

**Description**: [What to build/test]

**Acceptance Criteria**:
- [ ] Criterion 1
- [ ] Criterion 2

**Files to Create/Modify**:
- `path/to/file.cs`
- `path/to/test.cs`

**Test Coverage Target**: 80% minimum
```

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan. The /plan command STOPS HERE.

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
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS
- [x] All NEEDS CLARIFICATION resolved
- [ ] Complexity deviations documented

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
