# Project Rules

## Project Identity

Ten-Second Tom — intelligence-first voice capture and analysis CLI. Node.js/TypeScript. pnpm monorepo.

## Non-Goals

- This is NOT a general-purpose audio or AI framework. Do not add extensibility points or plugin systems beyond what the CLI requires.
- The CLI is NOT a business logic layer. All business logic MUST live in `packages/core/`.

## Architecture Invariants

### Vertical Slice Pattern

Each feature MUST be built as a vertical slice through the stack — types, service, tests, and CLI command together. Build features end-to-end, not layer-by-layer.

### Separation of Concerns

- `packages/core/` owns all business logic. The CLI is a thin rendering layer. No business logic in CLI components or commands.
- Each service MUST have a single responsibility and communicate through a well-defined interface.
- Domains MUST stay isolated. Storage does not know about transcription. Transcription does not know about analysis. The command layer orchestrates.

### Dependency Injection

- Services MUST depend on interfaces, not implementations. Accept dependencies through constructors.
- Use factory functions to construct service graphs from configuration.
- MUST NOT construct services ad-hoc in command files — use the shared factory.
- All service interfaces MUST live in `core/`. Implementations are swappable.

### No Magic Literals

- MUST NOT use magic strings. Use named constants or enums.
- MUST NOT use magic numbers. Extract to named constants with units in the name (e.g., `MAX_BUFFER_BYTES`, `AUTO_EXIT_DELAY_MS`).
- Configuration values MUST come from config, not hardcoded in logic.

### Functions

- Functions MUST be idempotent by default. Same inputs → same outputs, no hidden side effects.
- Prefer pure functions. Isolate side effects (I/O, state mutation) to the edges.
- Prefer patterns over patches — when fixing a bug or adding a feature, look for the underlying pattern. Do not patch around symptoms.

### DRY

- MUST NOT copy-paste logic with slight variation. Extract shared logic into hooks, utilities, or services.
- Shared UI patterns MUST go in `packages/cli/src/components/`. Shared logic goes in `hooks/` or `utils/`. Shared business logic goes in `packages/core/`.

## Code Rules

### TypeScript

- Strict mode, ESM, `verbatimModuleSyntax` — no exceptions.
- MUST use `.js` extensions in all relative imports (required for ESM).
- MUST use `import type` for type-only imports.
- Zod schemas are the source of truth for types. Infer TypeScript types from schemas with `z.infer<>`. MUST NOT manually duplicate a type that a schema defines.
- Use discriminated unions for provider-dependent configuration.

### Error Handling

- Capture MUST always succeed if the mic works. Post-capture steps (analysis, embedding) MUST degrade gracefully — warn and continue.
- Check prerequisites upfront, not mid-operation.
- Use platform-specific error messages for system dependencies.
- MUST NOT show raw stack traces to users.

### Testing

- TDD: write the failing test first, then implement.
- Vitest with globals. Tests MUST be colocated: `src/module/__tests__/module.test.ts`
- 80% coverage target on core.
- MUST mock native dependencies — never depend on hardware in tests.
- MUST use `vi.hoisted()` for mock variables inside `vi.mock()` factories (required for Vitest ESM).

### CJS in ESM

`node-record-lpcm16` is CommonJS. MUST use default import pattern:
```typescript
import recorder from 'node-record-lpcm16';
const { record } = recorder;
```
MUST NOT use named imports from CJS modules.

### CLI Components (Ink)

- MUST use phase pattern: `init → [working phases] → done | error`.
- MUST yield to the event loop (`setTimeout(0)`) before CPU-intensive native calls so Ink can render.
- MUST use shared hooks and components — do not duplicate UI patterns across commands.

## Quality Gates

Run before every commit:
```bash
make check   # lint + format + tests
```

Other useful commands:
```bash
make build              # Build all packages
make test               # Run all tests
make tom ARGS="record"  # Run CLI without global linking
make link-dev           # Link `tom` globally (requires PNPM_HOME)
make coverage           # Tests with coverage report
```

Single test: `pnpm vitest run packages/core/src/services/__tests__/storage.test.ts`


<!-- myco:managed:start -->
## Myco Managed Guidance

- When `capture.ignore_plan_dirs_in_git` is enabled, custom directories in `capture.plan_dirs` may be intentionally gitignored after capture into Myco.
- Do not force-add files from intentionally gitignored custom plan directories unless the user explicitly asks.
<!-- myco:managed:end -->
