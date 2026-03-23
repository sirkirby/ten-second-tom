# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Ten-Second Tom — intelligence-first voice capture and analysis CLI. Node.js/TypeScript. pnpm monorepo.

## Build & Test

```bash
make check              # CI gate: lint + format + tests. Run before every commit.
make build              # Build all packages
make test               # Run all tests
make tom ARGS="record"  # Run CLI without global linking
make link-dev           # Link `tom` globally (requires PNPM_HOME)
make coverage           # Tests with coverage report
```

Single test: `pnpm vitest run packages/core/src/services/__tests__/storage.test.ts`

## Architecture Rules

### Vertical Slice Pattern

Each feature is a vertical slice through the stack — types, service, tests, and CLI command together. Build features end-to-end, not layer-by-layer.

### Separation of Concerns

- `packages/core/` owns all business logic. CLI is a thin rendering layer. No business logic in CLI components or commands.
- Each service has a single responsibility and communicates through a well-defined interface.
- Keep domains isolated. Storage doesn't know about transcription. Transcription doesn't know about analysis. The command layer orchestrates.

### Dependency Injection

- Services depend on interfaces, not implementations. Accept dependencies through constructors.
- Use factory functions to construct service graphs from configuration.
- Never construct services ad-hoc in command files — use the shared factory.
- All service interfaces live in `core/`. Implementations are swappable.

### No Magic Literals

- No magic strings. Use named constants or enums.
- No magic numbers. Extract to named constants with units in the name (e.g., `MAX_BUFFER_BYTES`, `AUTO_EXIT_DELAY_MS`).
- Configuration values come from config, not hardcoded in logic.

### Functions

- Functions should be idempotent by default. Same inputs → same outputs, no hidden side effects.
- Prefer pure functions. Isolate side effects (I/O, state mutation) to the edges.
- Prefer patterns over patches — when fixing a bug or adding a feature, look for the underlying pattern. Don't patch around symptoms.

### DRY

- Extract shared logic into hooks, utilities, or services. Never copy-paste with slight variation.
- If two components need the same logic, extract it. If three need it, it should already be extracted.
- Shared UI patterns go in `components/`. Shared logic goes in `hooks/` or `utils/`. Shared business logic goes in `core/`.

## Code Rules

### TypeScript

- Strict mode, ESM, `verbatimModuleSyntax` — no exceptions.
- Always use `.js` extensions in relative imports (required for ESM).
- Always use `import type` for type-only imports.
- Zod schemas are the source of truth for types. Infer TypeScript types from schemas with `z.infer<>`. Never manually duplicate a type that a schema defines.
- Use discriminated unions for provider-dependent configuration.

### Error Handling

- Capture always succeeds if the mic works. Post-capture steps (analysis, embedding) degrade gracefully — warn and continue.
- Check prerequisites upfront, not mid-operation.
- Platform-specific error messages for system dependencies.
- Never show raw stack traces to users.

### Testing

- TDD: write the failing test first, then implement.
- Vitest with globals. Tests colocated: `src/module/__tests__/module.test.ts`
- 80% coverage target on core.
- Mock native dependencies — never depend on hardware in tests.
- Use `vi.hoisted()` for mock variables inside `vi.mock()` factories (required for Vitest ESM).

### CJS in ESM

`node-record-lpcm16` is CommonJS. Use default import pattern:
```typescript
import recorder from 'node-record-lpcm16';
const { record } = recorder;
```
Never use named imports from CJS modules.

### CLI Components (Ink)

- Phase pattern: `init → [working phases] → done | error`.
- Yield to the event loop (`setTimeout(0)`) before CPU-intensive native calls so Ink can render.
- Use shared hooks and components — don't duplicate UI patterns across commands.
