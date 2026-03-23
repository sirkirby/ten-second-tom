# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Ten-Second Tom — intelligence-first voice capture and analysis CLI. Node.js/TypeScript.

## Build & Test

```bash
make check              # CI gate: lint + format + tests. Run before every commit.
make build              # Build all packages
make test               # Run all tests
make tom ARGS="record"  # Run CLI without global linking
make link-dev           # Link `tom` globally (requires PNPM_HOME)
make coverage           # Tests with coverage report
```

Run a single test: `pnpm vitest run packages/core/src/services/__tests__/storage.test.ts`

## Rules

### TypeScript

- Strict mode, ESM, `verbatimModuleSyntax` — no exceptions.
- Always use `.js` extensions in relative imports. This is required for ESM.
- Always use `import type` for type-only imports.
- Use Zod schemas as the source of truth for types. Infer TypeScript types from schemas with `z.infer<>`. Never manually duplicate a type that a schema already defines.
- Use discriminated unions for provider-dependent configuration. Each provider variant must require only the fields relevant to it.

### Architecture

- `packages/core/` owns all business logic. The CLI package is a thin rendering layer over core services. No business logic in CLI components.
- All database access goes through the `IStorageService` interface. Never import SQLite directly in CLI code.
- All LLM interaction goes through `TomAgent`. No direct SDK/API calls from CLI commands.
- All service construction uses `buildServicesFromConfig()`. Never construct services ad-hoc in command files.
- All commands use `checkSetupComplete()` from `useSetupGuard` before accessing config or services.

### Error Handling

- **Capture always succeeds if the mic works.** Analysis, embedding, and other post-capture steps degrade gracefully — show a warning, save the entry without the enrichment, move on.
- Check prerequisites upfront, not mid-operation: setup complete → model exists → SoX available → then start recording.
- Platform-specific error messages for mic/SoX issues (macOS: System Settings path, Windows: Settings path).
- Never show raw stack traces to the user. Catch, extract the message, show in `<ErrorDisplay>`.

### Testing

- TDD: write the failing test first, then implement.
- Vitest with globals enabled. Tests colocated: `src/module/__tests__/module.test.ts`
- 80% coverage target on the core package.
- Mock native dependencies (whisper.node, node-record-lpcm16) in tests — never depend on hardware.
- Use `vi.hoisted()` for mock variables that need to be captured inside `vi.mock()` factory functions. This is required for Vitest ESM mocking.

### CLI Components (Ink)

- Every command component follows the phase pattern: `init → [working phases] → done | error`.
- Every command uses `useAutoExit(shouldExit)` to exit after completion.
- Every command uses `<ErrorDisplay message={error} />` for errors.
- Sentiment colors and thresholds come from `utils/sentiment.ts` — never hardcode thresholds in components.
- Use `setTimeout(resolve, 0)` to yield to the event loop before CPU-intensive native calls (model loading, transcription) so Ink can render status updates.

### CJS Modules in ESM

`node-record-lpcm16` is CommonJS. Import pattern:
```typescript
import recorder from 'node-record-lpcm16';
const { record } = recorder;
```
Never use named imports from CJS modules — they will fail at runtime even if TypeScript allows them.

### Audio

- Record as raw PCM (16kHz, mono, 16-bit). Write WAV header on save.
- Audio files go to `~/.tom/audio/{YYYY-MM}/{YYYY-MM-DD-{id}}.wav`.
- Buffer is capped at 100MB (~55 min). Auto-stop on overflow.
- SoX is a system dependency (`brew install sox` on macOS). Check before recording.

### LLM Integration

- Cloud: Anthropic SDK with `messages.create` and `system` parameter.
- Local: Ollama native `/api/chat` endpoint directly. The Anthropic SDK does NOT work with Ollama.
- Setup wizard queries Ollama `/api/tags` for installed models — never hardcode model lists.
- Suppress whisper.cpp stderr logging via `GGML_LOG_LEVEL=0` env var before native calls.

## References

- Design spec: `docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md`
- Implementation plan: `docs/superpowers/plans/2026-03-22-tom-v2-rewrite.md`
- STT research: `docs/research/stt-evaluation.md`
- Database research: `docs/research/database-evaluation.md`
