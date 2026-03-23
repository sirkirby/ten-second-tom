# Ten-Second Tom v2 — Project Reference

> **Design spec (authority)**: [`docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md`](./docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md)

## What This Is

Intelligence-first voice capture and analysis CLI. Node.js/TypeScript rewrite from .NET v1. Core loop: **capture → transcribe → analyze → search**.

## Stack

```
TypeScript 5 (strict, ESM, verbatimModuleSyntax) | Ink 5 + Commander | @anthropic-ai/sdk
Zod | better-sqlite3 + FTS5 | @fugood/whisper.node | Vitest | pnpm workspaces | tsup
```

## Commands

```bash
make check          # Lint + format + tests (CI gate)
make build          # Build all packages
make test           # Run tests
make coverage       # Tests with coverage report
make link-dev       # Build + link `tom` globally (requires PNPM_HOME)
make unlink-dev     # Remove global link
make tom ARGS="..." # Run without linking (e.g., make tom ARGS="record")
make dev            # Watch mode
make clean          # Remove build artifacts
```

## Structure

```
packages/
  cli/src/
    cli.ts                  # Entry point — Commander routing
    commands/
      setup.tsx             # First-run wizard (LLM, embedding, model download)
      record.tsx            # Audio capture + streaming STT + analysis
      note.tsx              # Text/dictation input + analysis
      search.tsx            # Semantic + FTS search
      analyze.tsx           # Re-run analysis on existing entry
    components/
      RecordingUI.tsx       # Timer, live transcript, controls
      SentimentDisplay.tsx  # Color-coded analysis results
      SearchResults.tsx     # Result list with detail view
      ErrorDisplay.tsx      # Shared error display
    hooks/
      useSetupGuard.ts      # checkSetupComplete() — shared across commands
      useAutoExit.ts        # useAutoExit(shouldExit, delayMs) — auto-exit hook
    utils/
      sentiment.ts          # getSentimentColor(), getSentimentEmoji(), thresholds
  core/src/
    types/
      entry.ts              # EntrySchema, EntryAnalysisSchema, CreateEntrySchema
      config.ts             # AppConfigSchema (discriminated unions for providers)
      index.ts              # Barrel export
    services/
      storage.ts            # IStorageService interface
      storage-sqlite.ts     # SqliteStorageService (better-sqlite3, FTS5, prepared statements)
      audio.ts              # AudioService, checkAudioPrerequisites(), checkModelExists()
      transcription.ts      # WhisperTranscriptionService (@fugood/whisper.node)
      embedding.ts          # OllamaEmbeddingService, NoopEmbeddingService
      search.ts             # SearchService (semantic + FTS fallback)
    agent/
      config.ts             # getModelId(), getBaseUrl()
      tom-agent.ts          # TomAgent — Claude SDK analysis
    config/
      config-manager.ts     # ConfigManager — ~/.tom/ management
```

## Key APIs

### ConfigManager

```typescript
const cm = new ConfigManager();       // defaults to ~/.tom/
cm.ensureDirectories();               // creates ~/.tom/, audio/, models/
cm.save(config: AppConfig): void;     // validates with Zod, writes config.json
cm.load(): AppConfig | undefined;     // reads + validates (cached after first call)
cm.isSetupComplete(): boolean;        // load() !== undefined
cm.homePath / cm.audioPath / cm.modelsPath  // readonly path getters
```

### IStorageService

```typescript
saveEntry(input: CreateEntry): Promise<Entry>;        // generates UUID + timestamps
getEntry(id: string): Promise<Entry | undefined>;
listEntries(options: ListEntriesOptions): Promise<Entry[]>;
updateEntryAnalysis(id: string, analysis: EntryAnalysis): Promise<void>;
updateEntryEmbedding(id: string, embedding: Float32Array): Promise<void>;
searchByKeyword(query: string, limit?: number): Promise<Entry[]>;
searchByVector(embedding: Float32Array, limit: number): Promise<Entry[]>;
deleteEntry(id: string): Promise<void>;
close(): void;
```

### TomAgent

```typescript
const agent = new TomAgent(config.llm);  // accepts LlmConfig directly (discriminated union)
const analysis = await agent.analyze(text);  // returns EntryAnalysis
// Uses system message for prompt, validates JSON response, clamps scores
```

### Shared CLI Patterns

```typescript
// Setup guard — use in all commands that need config
import { checkSetupComplete } from '../hooks/useSetupGuard.js';
const guard = checkSetupComplete(); // { ok, config, configManager } or { ok: false, error }

// Auto-exit — use in all command components
import { useAutoExit } from '../hooks/useAutoExit.js';
useAutoExit(phase === 'done' || phase === 'error');

// Error display
import { ErrorDisplay } from '../components/ErrorDisplay.js';
<ErrorDisplay message={error} />

// Service construction — shared factory in record.tsx
import { buildServicesFromConfig } from './record.js';
const services = buildServicesFromConfig(config, configManager);
```

## Config (Discriminated Unions)

```typescript
// LLM — cloud requires apiKey, local requires endpoint + modelId
config.llm.provider === 'cloud'  → config.llm.apiKey
config.llm.provider === 'local'  → config.llm.localEndpoint, config.llm.modelId

// Embedding — ollama requires endpoint, none enforces empty model
config.embedding.provider === 'ollama' → config.embedding.model, config.embedding.endpoint
config.embedding.provider === 'cloud'  → config.embedding.model
config.embedding.provider === 'none'   → config.embedding.model === ''
```

## Testing

- TDD: red → green → refactor
- Vitest with globals enabled
- 80% coverage target on core (currently 88%)
- Tests colocated: `src/module/__tests__/module.test.ts`

## Gotchas

- **ESM imports**: always use `.js` extensions in relative imports
- **`import type`**: required for type-only imports (`verbatimModuleSyntax`)
- **`node-record-lpcm16`**: CJS module — must use `import recorder from 'node-record-lpcm16'` then `const { record } = recorder`, NOT named import
- **Vitest ESM mocks**: use `vi.hoisted()` for mock variables referenced inside `vi.mock()` factories
- **`pnpm link --global`**: requires `PNPM_HOME` set — run `pnpm setup` first if missing
- **SoX**: required system dependency for mic recording (`brew install sox` on macOS)
- **Whisper model**: downloaded during `tom setup` to `~/.tom/models/ggml-distil-small.en.bin` (~380MB)
- **`searchByVector` stub**: currently throws (not yet implemented) — SearchService catches and falls back to FTS5
- **Audio buffer**: capped at 100MB (~55 min) to prevent OOM; auto-stops recording

## Architecture Rules

- `core/` owns all business logic — CLI is a thin rendering layer
- Storage behind `IStorageService` interface (swappable database)
- TomAgent is the single point of contact with the Claude SDK
- Capture always succeeds if mic works — analysis/embedding degrade gracefully
- Commands check prerequisites upfront: setup complete → model exists → SoX available
