
# Implementation Plan: GitHub Actions CI/CD Pipeline

**Branch**: `002-as-per-the` | **Date**: 2025-10-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-as-per-the/spec.md`

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
Implement comprehensive GitHub Actions CI/CD workflows to automate testing, coverage enforcement, cross-platform builds, and package distribution. The system will validate all pull requests with unit and integration tests while enforcing 80% minimum coverage threshold, build self-contained executables for macOS and Windows on main branch merges, and automatically publish releases to Homebrew, Winget, and Chocolatey when semantic version tags are pushed. This infrastructure enables developers to maintain high code quality standards while ensuring end users can easily install the application via their preferred package manager.

## Technical Context
**Language/Version**: C# with .NET 9 SDK  
**Primary Dependencies**: GitHub Actions, dotnet CLI, xUnit, coverage tools (coverlet/ReportGenerator)  
**Storage**: GitHub Actions artifacts and cache, GitHub Releases  
**Testing**: xUnit framework with FluentAssertions, 80% minimum coverage via coverlet  
**Target Platform**: GitHub-hosted runners (ubuntu-latest, macos-latest, windows-latest)  
**Project Type**: Single CLI project (existing structure at src/)  
**Performance Goals**: 
  - PR validation: ≤10 minutes total
  - Test execution: ≤5 minutes
  - Coverage calculation: ≤2 minutes
  - Cross-platform build: ≤15 minutes
  - Package publication: ≤30 minutes  
**Constraints**: 
  - Executable size: <50MB per platform
  - Coverage threshold: ≥80%
  - Zero compiler warnings
  - GitHub Actions concurrency limits
  - Package manager API requirements  
**Scale/Scope**: 
  - 3 workflow files (.github/workflows/)
  - 3 package managers (Homebrew, Winget, Chocolatey)
  - 3 platforms (macOS x64/ARM64, Windows x64)
  - CODEOWNERS-based approval for releases
**Research Required**:
  - Code signing requirements for Homebrew (macOS)
  - Code signing requirements for Winget/Chocolatey (Windows)
  - Package manager publication APIs and authentication

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Core Principles Compliance

- [x] **I. Modern .NET & Idiomatic C#**: Workflows use .NET 9 SDK, dotnet CLI commands follow modern patterns
- [x] **II. CLI-First Interface**: No change to CLI interface; workflows support CLI application
- [x] **III. Test-First (NON-NEGOTIABLE)**: Workflows enforce 80% coverage minimum, run tests before allowing merge
- [x] **IV. DRY & Design Patterns**: Reusable workflow components via composite actions or workflow_call
- [x] **V. Semantic Versioning & Automated Releases**: Implements automated releases on semantic version tags
- [x] **VI. Cross-Platform Distribution**: Implements Homebrew, Winget, Chocolatey publication
- [x] **VII. Local Development Excellence**: No change to local dev; CI/CD supports development workflow
- [x] **VIII. Secrets Management**: Package manager credentials stored as GitHub Secrets

### Architecture & Design Standards

- [x] **Code Organization**: Workflows organized by purpose (PR validation, build, release)
- [x] **Naming Conventions**: Workflow files descriptive (pr-validation.yml, build.yml, release.yml)
- [x] **Error Handling**: Workflows fail fast with clear error messages, retryable operations

### Quality & Testing Standards

- [x] **Test Coverage**: Enforces 80% minimum via coverage checks
- [x] **Test Organization**: Runs unit and integration tests separately, reports results
- [x] **Code Quality**: Enforces zero compiler warnings, runs static analysis

### Development & Operations Standards

- [x] **Version Control**: All workflows version controlled in .github/workflows/
- [x] **CI/CD Pipeline**: Implements complete GitHub Actions pipeline per requirements
- [x] **Release Process**: Automates semantic versioning, publishing, documentation
- [x] **Documentation**: Will update README with CI/CD badge and workflow documentation
- [x] **Logging Standards**: Workflows log all operations for audit (FR-034)

### Complexity Justification

**No constitutional violations detected.** This feature implements infrastructure required by constitution principles V and VI.

## Project Structure

### Documentation (this feature)
```
specs/002-as-per-the/
├── spec.md              # Feature specification (completed)
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (to be generated)
├── data-model.md        # Phase 1 output (to be generated)
├── quickstart.md        # Phase 1 output (to be generated)
├── contracts/           # Phase 1 output (to be generated)
│   ├── pr-validation-workflow.yml
│   ├── build-workflow.yml
│   └── release-workflow.yml
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
.github/
└── workflows/
    ├── pr-validation.yml       # Pull request validation workflow
    ├── build.yml               # Main branch build workflow
    └── release.yml             # Release and package publication workflow

src/
├── TenSecondTom.csproj         # Existing project file (no changes)
├── Program.cs                   # Existing entry point (no changes)
├── Features/                    # Existing features (no changes)
└── Infrastructure/              # Existing infrastructure (no changes)

tests/
├── TenSecondTom.Tests/          # Existing unit tests (no changes)
└── TenSecondTom.IntegrationTests/ # Existing integration tests (no changes)

.github/
└── CODEOWNERS                   # Release approval configuration (to be created)
```

**Structure Decision**: Single project structure (Option 1). This feature adds CI/CD workflows to the existing project without changing the source code organization. All workflow files will be placed in `.github/workflows/` following GitHub Actions conventions.

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

The /tasks command will load `.specify/templates/tasks-template.md` and generate ordered, testable tasks from the Phase 1 design artifacts:

**From research.md**:
- Task group for workflow file creation (PR validation, build, release)
- Task group for supporting files (CODEOWNERS, documentation)

**From contracts/**:
- One task per workflow contract to create YAML file
- One task per workflow contract to create validation test
- Tasks ensure workflow conforms to contract specifications

**From data-model.md**:
- No model creation tasks (workflows are configuration, not code)
- Validation tasks to ensure workflows produce expected entities

**From quickstart.md**:
- Manual validation tasks for each scenario
- Integration test tasks for automated workflow validation

**Ordering Strategy**:
1. Create supporting files first (CODEOWNERS)
2. Create PR validation workflow (most critical, blocks merges)
3. Create build workflow (depends on PR validation pattern)
4. Create release workflow (depends on build workflow pattern)
5. Create documentation updates
6. Create validation tests
7. Execute quickstart scenarios

**Parallel Execution Opportunities** [P]:
- Workflow file creation can be parallelized (independent YAML files)
- Documentation updates can be parallelized
- Platform-specific testing can be parallelized

**Estimated Output**: 15-20 numbered, ordered tasks in tasks.md covering:
- 1-3 tasks for supporting files
- 3 tasks for workflow creation (one per workflow)
- 3 tasks for workflow validation tests
- 2-3 tasks for documentation
- 5-8 tasks for quickstart scenario validation

**IMPORTANT**: This phase is executed by the /tasks command, NOT by /plan

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
  - [x] research.md: All unknowns resolved
  - [x] Decision: Code signing deferred (not required initially)
  - [x] Decision: Homebrew in Phase 1, Winget/Chocolatey in Phase 2
  - [x] Decision: Three-workflow structure (PR, build, release)
- [x] Phase 1: Design complete (/plan command)
  - [x] data-model.md: 9 entities defined
  - [x] contracts/pr-validation-workflow.md: Complete
  - [x] contracts/build-workflow.md: Complete
  - [x] contracts/release-workflow.md: Complete
  - [x] quickstart.md: 4 validation scenarios
  - [x] Agent context updated
- [x] Phase 2: Task planning complete (/plan command - describe approach only)
  - [x] Task generation strategy documented
  - [x] Ordering strategy defined
  - [x] Estimated 15-20 tasks
- [ ] Phase 3: Tasks generated (/tasks command)
- [ ] Phase 4: Implementation complete
- [ ] Phase 5: Validation passed

**Gate Status**:
- [x] Initial Constitution Check: PASS
- [x] Post-Design Constitution Check: PASS (see below)
- [x] All NEEDS CLARIFICATION resolved
- [x] Complexity deviations documented (NONE)

### Post-Design Constitution Check

**Principle I: Modern .NET & Idiomatic C#** ✅ PASS
- All workflow definitions use .NET 9 SDK (specified in research.md)
- dotnet CLI commands follow modern patterns
- Async/await not applicable (CI/CD workflows, not application code)
- Serilog logging requirement preserved in implementation (out of scope for workflows)

**Principle II: CLI-First Interface** ✅ PASS
- No changes to CLI interface
- Workflows automate existing CLI testing patterns
- Text-based output maintained (logs, test results, coverage reports)

**Principle III: Test-First (NON-NEGOTIABLE)** ✅ PASS
- PR validation workflow enforces xUnit test execution (FR-001, FR-002)
- Build workflow includes test jobs (FR-014)
- Coverage enforcement at 80% threshold (FR-008, NFR-007)
- No implementation without passing tests (workflow gates)

**Principle IV: DRY & Design Patterns** ✅ PASS
- Reusable workflow components planned (PR validation pattern used in build workflow)
- No duplication across workflow contracts
- CQRS/Factory patterns not applicable (infrastructure code, not application logic)
- VSA preserved (workflows organize by feature: test, build, release)

**Principle V: Semantic Versioning & Automated Releases** ✅ PASS
- Release workflow implements semantic versioning (FR-019)
- Automated GitHub releases on merge to main (FR-021)
- Release notes generation automated (FR-022)
- Git tag creation automated (FR-023)

**Principle VI: Cross-Platform Distribution** ✅ PASS
- Self-contained executables for macOS (osx-x64, osx-arm64) and Windows (win-x64) (FR-013, FR-015, FR-016)
- Homebrew publication automated (FR-024, FR-025)
- Winget and Chocolatey planned for Phase 2 (research.md decision)
- Package manager automation via GitHub Actions (FR-026 through FR-034)

**Principle VII: Local Development Excellence** ✅ PASS
- Workflows do not impact local development
- quickstart.md provides manual validation scenarios for contributors
- Build artifacts downloadable from GitHub Actions for local testing
- Fast feedback loops maintained (≤10 min PR validation per NFR-001)

**Principle VIII: Secrets Management** ✅ PASS
- GitHub Secrets used for package manager tokens (FR-029, FR-032, FR-034)
- No secrets in workflow files (parameterized via secrets)
- Environment protection rules for release approval (FR-025)
- CODEOWNERS file enforces approval gates

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
