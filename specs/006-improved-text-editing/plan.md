# Implementation Plan: Interactive Console Text Editing Experience

**Branch**: `006-improved-text-editing` | **Date**: 2025-10-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-improved-text-editing/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Implement an interactive, multi-line text editor for console input that replaces the current basic input experience in `/today`. The editor must support cursor navigation (arrows, Home/End), inline editing (insert, backspace, delete), multi-line input with explicit completion gestures (Ctrl+D/Ctrl+Enter), preview with save/edit/cancel options, and be reusable for future editing workflows. The solution must be cross-platform (macOS/Windows), handle multi-byte characters correctly, and provide a graceful fallback for non-interactive terminals. **Technical approach**: Terminal.Gui v2.0.0-alpha.* for primary interactive editing (selected per research.md) with StreamBasedTextEditor fallback for non-interactive terminals.

## Technical Context

**Language/Version**: C# 10+, .NET 9
**Primary Dependencies**:
  - System.CommandLine 2.0.0-rc.1 (CLI framework)
  - Spectre.Console 0.51.1 (currently used for display/formatting)
  - Terminal.Gui v2.0.0-alpha.* (selected for interactive editing per research.md - provides TextView with full multi-line editing, clipboard support, and Unicode/emoji preservation)
  - xUnit (testing framework)
  - FluentAssertions 6+ (test assertions)
  - Moq or NSubstitute (mocking)
  - Serilog 4.3.0 (logging)
  - FluentValidation 12.0.0 (validation)

**Storage**: File-based storage (existing FileSystemStorageProvider)
**Testing**: xUnit with FluentAssertions and Moq/NSubstitute (80% minimum coverage required)
**Target Platform**: macOS (primary), Windows (secondary) - cross-platform CLI
**Project Type**: Single CLI project with Vertical Slice Architecture
**Performance Goals**:
  - Instant perceived response (<100ms) for cursor movements and character input
  - Support up to 10,000 characters without performance degradation
  - Paste operations up to 5,000 characters complete in <200ms

**Constraints**:
  - Must work in standard macOS Terminal.app and Windows Terminal/cmd.exe
  - Cross-platform key handling (some keys may not be available in all environments)
  - Zero web/GUI dependencies (CLI-only per constitution)
  - Must provide non-interactive fallback for piped input
  - Must handle multi-byte Unicode characters correctly
  - Must strip ANSI escape sequences to prevent terminal injection

**Scale/Scope**:
  - Single interactive editor component
  - Reusable across multiple features (Today, future Search edit)
  - ~3-5 new classes (editor, session, sanitizer, tests)
  - Integration with existing CreateDailyEntryHandler in Features/Today

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Modern .NET & Idiomatic C# ✅
- **Status**: PASS
- **Evidence**: Feature uses .NET 9, C# 10+ features (records, nullable reference types, modern patterns)
- **Notes**: Will leverage async/await, pattern matching, and Serilog logging per constitution

### II. CLI-First Interface ✅
- **Status**: PASS
- **Evidence**: Feature is CLI-only text editing in terminal, no web/GUI dependencies
- **Notes**: Enhances existing CLI experience for `/today` command

### III. Test-First (NON-NEGOTIABLE) ⚠️
- **Status**: CONDITIONAL PASS - requires vigilance during implementation
- **Evidence**: Plan includes xUnit test requirements, 80% coverage mandate
- **Action Required**: Tests must be written before implementation code during task execution
- **Coverage Target**: 80% minimum for all new editor components

### IV. DRY & Design Patterns ✅
- **Status**: PASS
- **Evidence**:
  - Feature explicitly designed as reusable component (Today + future Search edit)
  - Will use Vertical Slice Architecture per project standards
  - Will apply Factory pattern for text editor instantiation if multiple implementations needed
- **Notes**: Single editor implementation avoids duplication across features

### V. Semantic Versioning & Automated Releases ✅
- **Status**: PASS (N/A for feature planning)
- **Evidence**: Existing GitHub Actions workflow handles releases on PR merge
- **Notes**: No changes needed - existing CI/CD will handle this feature

### VI. Cross-Platform Distribution ✅
- **Status**: PASS
- **Evidence**:
  - Feature explicitly targets macOS and Windows
  - Uses cross-platform .NET 9 runtime
  - Research phase will evaluate libraries for cross-platform compatibility
- **Constraints**: Must test on both macOS Terminal and Windows Terminal/cmd.exe

### VII. Local Development Excellence ✅
- **Status**: PASS
- **Evidence**: Feature integrates with existing development workflow
- **Notes**: Developers can test interactively with `/today` command locally

### VIII. Secrets Management ✅
- **Status**: PASS (N/A for feature)
- **Evidence**: Feature handles user text input only, no secrets involved
- **Notes**: No secrets management required for text editing

---

**Overall Gate Status**: ✅ **PASS** - Proceed to Phase 0 research

**Violations**: None

**Conditional Items**:
- Test-First discipline must be maintained during implementation (tasks phase)
- Cross-platform testing required on both macOS and Windows before release

---

## Constitution Check (Post-Design Re-Evaluation)

**Date**: 2025-10-14 (After Phase 1 completion)

### Design Decisions Made

1. **Terminal.Gui Integration**: Selected as primary implementation
2. **Shared Location**: Placed in `src/Shared/TextEditing/` (reusable infrastructure)
3. **Fallback Strategy**: `StreamBasedTextEditor` for non-interactive terminals
4. **CQRS Compliance**: Service follows query pattern (read user input)
5. **Dependencies Added**: Terminal.Gui v2.0.0-alpha.*

### Re-Evaluation Against Principles

#### I. Modern .NET & Idiomatic C# ✅
- **Status**: PASS
- **Evidence**:
  - All models use modern C# records and file-scoped namespaces
  - Service uses async/await patterns
  - Nullable reference types enabled throughout
  - Serilog structured logging with correlation IDs
- **Design Alignment**: Full compliance, no concerns

#### II. CLI-First Interface ⚠️ → ✅
- **Status**: PASS WITH JUSTIFICATION
- **Design Decision**: Terminal.Gui creates TUI mode (screen takeover)
- **Initial Concern**: TUI mode conflicts with "CLI-first" principle
- **Justification**:
  - FR-010 (Unicode/emoji preservation) is **non-negotiable** requirement
  - Console.ReadKey fundamentally cannot handle emoji (runtime limitation)
  - Terminal.Gui is a **terminal application framework**, not GUI/web
  - Mode switch is temporary and clearly communicated to user
  - Fallback to stream-based input maintains CLI compatibility for pipes/scripts
  - Alternative (external editor) also exits CLI mode temporarily
- **Mitigation**:
  - Smooth transitions with clear messaging
  - Non-interactive fallback preserves scriptability
  - TUI usage isolated to single feature, doesn't propagate
- **Conclusion**: Accepted as necessary trade-off for functional requirements

#### III. Test-First (NON-NEGOTIABLE) ✅
- **Status**: PASS
- **Evidence**:
  - Quickstart.md mandates TDD with tests written before implementation
  - All phases explicitly require test-first approach
  - Integration test strategy defined for Terminal.Gui
  - Coverage target: 80% minimum
- **Action Required**: Vigilance during task execution (Phase 2)

#### IV. DRY & Design Patterns ✅
- **Status**: PASS
- **Evidence**:
  - Single `IInteractiveTextEditor` interface reused across features
  - Placed in `Shared/` to avoid duplication
  - Service interface pattern (dependency injection)
  - Factory pattern implicit in DI registration (chooses implementation)
  - Vertical Slice Architecture maintained
- **Design Alignment**: Excellent compliance

#### V. Semantic Versioning & Automated Releases ✅
- **Status**: PASS (N/A for design)
- **Evidence**: Existing CI/CD unchanged, will handle release automatically
- **No Action Required**

#### VI. Cross-Platform Distribution ✅
- **Status**: PASS
- **Evidence**:
  - Terminal.Gui explicitly supports macOS and Windows
  - Quickstart.md mandates testing on both platforms
  - Stream-based fallback works universally
- **Testing Requirements**: Manual verification on both platforms before merge

#### VII. Local Development Excellence ✅
- **Status**: PASS
- **Evidence**:
  - Quickstart.md provides clear implementation guide
  - TDD approach ensures fast feedback loops
  - Can test locally with `dotnet run` immediately
  - Integration tests validate full workflow
- **Design Alignment**: Full compliance

#### VIII. Secrets Management ✅
- **Status**: PASS (N/A for feature)
- **Evidence**: No secrets involved in text editing
- **No Action Required**

---

### Post-Design Overall Assessment

**Gate Status**: ✅ **PASS** - Proceed to Phase 2 (Tasks Generation)

**Key Changes from Initial Check**:
1. **Terminal.Gui decision finalized** - Principle II concern resolved with justification
2. **Architecture solidified** - Shared location confirmed, DRY compliance verified
3. **Testing strategy defined** - Integration tests address Terminal.Gui testing challenges

**Remaining Conditional Items**:
- ⚠️ **Test-First discipline** - Must be enforced during implementation (tasks.md execution)
- ⚠️ **Cross-platform testing** - Required on macOS and Windows before PR merge

**New Dependencies Introduced**:
- `Terminal.Gui` v2.0.0-alpha.* (~5MB, 2-5 transitive dependencies)
- **Justification**: Only viable solution for FR-010 (Unicode/emoji) with acceptable complexity
- **Risk**: Alpha version, but v2 recommended for new projects per Terminal.Gui docs
- **Mitigation**: Extensive integration testing, fallback to StreamBasedTextEditor available

**Constitutional Violations**: **None**

**Complexity Justification**: Not required - no violations present

---

### Approval to Proceed

Based on post-design Constitution Check:
- ✅ All 8 core principles satisfied or justified
- ✅ Design artifacts complete (research.md, data-model.md, contracts/, quickstart.md)
- ✅ Integration points identified and documented
- ✅ Testing strategy defined and comprehensive
- ✅ No unresolved architectural concerns

**Status**: **APPROVED for Phase 2 (Task Generation)**

Next command: `/speckit.tasks` to generate detailed implementation tasks

## Project Structure

### Documentation (this feature)

```
specs/006-improved-text-editing/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```
src/
├── Features/                   # Vertical Slice Architecture
│   ├── Today/                  # Existing feature to be enhanced
│   │   ├── Commands/
│   │   ├── Handlers/          # CreateDailyEntryHandler (integration point)
│   │   └── Validation/
│   └── TextEditing/           # NEW: This feature (Shared/TextEditing preferred if truly shared)
│       ├── Models/            # TextEditingSession, EditorResult
│       ├── Services/          # InteractiveTextEditor, InputSanitizer
│       ├── Handlers/          # (if using CQRS pattern for editor commands)
│       └── Validation/        # Input validation rules
├── Shared/                    # Cross-cutting concerns (alternative location for editor)
│   ├── Console/               # Existing console utilities
│   └── TextEditing/           # ALTERNATIVE: Reusable editor here if not feature-specific
└── Infrastructure/
    ├── Prompts/
    └── Storage/

tests/
├── TenSecondTom.Tests/               # Unit tests
│   ├── Unit/
│   │   └── Features/
│   │       ├── TextEditing/          # NEW: Editor unit tests
│   │       │   ├── InteractiveTextEditorTests.cs
│   │       │   ├── InputSanitizerTests.cs
│   │       │   └── TextEditingSessionTests.cs
│   │       └── Today/
│   │           └── CreateDailyEntryHandlerTests.cs  # Updated for editor integration
│   └── TestHelpers/
│       ├── Builders/
│       └── Fixtures/
└── TenSecondTom.IntegrationTests/   # Integration tests
    └── Integration/
        ├── Cli/
        │   └── TodayCommandTests.cs  # Test full /today with editor
        └── Features/
            └── TextEditing/
                └── EditorWorkflowTests.cs  # Multi-scenario editor tests
```

**Structure Decision**: **Vertical Slice Architecture with Shared option**

This feature follows the existing TenSecondTom structure:
- **Primary Location**: `src/Features/TextEditing/` OR `src/Shared/TextEditing/`
  - Use **Features/TextEditing** if the editor is tightly coupled to specific features
  - Use **Shared/TextEditing** if the editor is truly reusable infrastructure (RECOMMENDED based on spec requirements)
- **Integration Point**: `src/Features/Today/Handlers/CreateDailyEntryHandler.cs`
- **Testing**: Mirrors source structure in `tests/TenSecondTom.Tests/Unit/` and `tests/TenSecondTom.IntegrationTests/`

The final decision between Features vs Shared will be made during Phase 1 design based on coupling analysis.

## Complexity Tracking

*Fill ONLY if Constitution Check has violations that must be justified*

**No violations** - Complexity Tracking table not needed.
