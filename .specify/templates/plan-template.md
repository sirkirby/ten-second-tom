# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]  
**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]  
**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]  
**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]
**Project Type**: [single/web/mobile - determines source structure]  
**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

[Gates determined based on constitution file]

## Project Structure

### Documentation (this feature)

```
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: This project uses Vertical Slice Architecture as defined in
  the constitution (.specify/memory/constitution.md - Project Structure Standards).
  
  For Ten Second Tom, the structure is:
  
  src/Features/[FeatureName]/  - Self-contained vertical slices
  src/Infrastructure/          - Cross-cutting concerns (DI, config, logging)
  src/Shared/                  - Shared domain models and abstractions
  
  Document below how THIS feature will fit into the canonical structure.
  List the specific feature folder(s) and files that will be created/modified.
-->

```
src/
├── Features/
│   └── [YourFeatureName]/
│       ├── Commands/          # [List specific command files]
│       ├── Queries/           # [List specific query files if needed]
│       ├── Handlers/          # [List handler files]
│       ├── Validation/        # [List validators if needed]
│       └── DependencyInjection.cs
├── Infrastructure/            # [Note any infrastructure changes needed]
└── Shared/                    # [Note any shared models/abstractions needed]

tests/
├── TenSecondTom.Tests/
│   └── Features/
│       └── [YourFeatureName]/
│           ├── Commands/      # [List command tests]
│           ├── Queries/       # [List query tests]
│           └── Handlers/      # [List handler tests]
└── TenSecondTom.IntegrationTests/
    └── Features/
        └── [YourFeatureName]/ # [List integration tests]
```

**Structure Decision**: [Document how this feature follows VSA principles and
reference the canonical structure from the constitution. Explain any deviations
with justification.]

## Complexity Tracking

*Fill ONLY if Constitution Check has violations that must be justified*

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
