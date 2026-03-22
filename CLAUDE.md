# Ten-Second Tom v2 — TypeScript CLI Quick Reference

> **Authority**: This guide supplements the design spec at [`docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md`](./docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md). On any conflict, the design spec wins.

## Project Overview

**Ten-Second Tom v2** is an intelligence-first voice capture and analysis CLI tool. Built from the ground up in **Node.js/TypeScript**, Tom captures voice recordings and text notes, transcribes them, analyzes sentiment and context with the Claude Agent SDK, and makes entries searchable via semantic search backed by vector embeddings. The core value loop is **capture → transcribe → analyze → search**.

This is a complete rewrite from the .NET v1 codebase. Authoritative source of truth for product decisions: `docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md`.

## Core Technology Stack

```text
Language:       TypeScript 5 (strict mode, ESM, verbatimModuleSyntax)
CLI:            Ink 5 + Commander (React for terminal + argument routing)
LLM:            Claude Agent SDK (@anthropic-ai/sdk)
Schema/Types:   Zod (runtime validation + type inference)
Storage:        better-sqlite3 + sqlite-vec (local database)
Embedding:      Ollama (nomic-embed-text) or cloud (FTS5 fallback)
STT:            Whisper (streaming chunked inference)
Testing:        Vitest (globals enabled, 80% coverage target)
Bundling:       tsup (for CLI distribution)
Package Mgr:    pnpm workspaces (monorepo)
Platforms:      macOS, Windows (Linux future)
```

## Monorepo Structure

```
ten-second-tom/
├── package.json                  # Root workspace config
├── pnpm-workspace.yaml
├── tsconfig.json                 # Shared TS config (strict, ESM)
├── vitest.config.ts
├── packages/
│   ├── cli/                      # Ink terminal UI + Commander routing
│   │   ├── src/
│   │   │   ├── cli.ts            # Entry point + Commander setup
│   │   │   ├── app.tsx           # Root Ink component (optional)
│   │   │   ├── commands/         # One file per command
│   │   │   │   ├── setup.tsx
│   │   │   │   ├── record.tsx
│   │   │   │   ├── note.tsx
│   │   │   │   └── search.tsx
│   │   │   └── components/       # Shared Ink UI components
│   │   │       ├── RecordingUI.tsx
│   │   │       ├── SentimentDisplay.tsx
│   │   │       └── SearchResults.tsx
│   │   └── package.json
│   └── core/                     # Business logic + services
│       ├── src/
│       │   ├── types/            # Zod schemas + TS types (colocated, no separate package)
│       │   ├── services/
│       │   │   ├── storage.ts    # IStorageService interface + implementation
│       │   │   ├── transcription.ts  # Whisper STT (streaming/chunked)
│       │   │   ├── sentiment.ts  # Sentiment analysis via TomAgent
│       │   │   ├── embedding.ts  # Vector embedding pipeline
│       │   │   └── search.ts     # Hybrid search (semantic + FTS5)
│       │   ├── agent/            # Claude Agent SDK integration
│       │   │   ├── tom-agent.ts  # TomAgent for analysis
│       │   │   └── config.ts     # Cloud vs local provider config
│       │   └── config/           # Configuration management (ConfigManager)
│       └── package.json
├── migrations/                   # Database schema migrations
│   └── local/
└── docs/
    └── superpowers/
        └── specs/
            └── 2026-03-22-tom-v2-rewrite-design.md  # Design authority
```

## Before Making Changes

1. **Read the design spec** at `docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md` — all architectural decisions live there.
2. **Understand the feature** — check the spec's "Command Flows" section for how data moves through services.
3. **Know the data model** — Entry, EntryAnalysis, and AppConfig are the three core abstractions (defined in Zod).
4. **Check tests first** — TDD is non-negotiable. Write the test before the code.

## Key Architecture Patterns

### Storage Behind an Interface

The `IStorageService` interface ensures database technology (SQLite + sqlite-vec vs PGlite) can be swapped without touching anything above it.

```typescript
// core/src/services/storage.ts
interface IStorageService {
  saveEntry(entry: Entry): Promise<void>;
  getEntry(id: string): Promise<Entry | null>;
  deleteEntry(id: string): Promise<void>;
  searchByVector(embedding: Float32Array, topK: number): Promise<Entry[]>;
  searchByKeyword(query: string): Promise<Entry[]>;
}

// Implementation swappable — start with SQLite, migrate to PGlite later without touching core logic
```

### Agent SDK Integration in One Place

`TomAgent` lives in `core/src/agent/tom-agent.ts` and handles sentiment analysis, context extraction, and future synthesis. It's the single point of contact with the Claude Agent SDK.

```typescript
// core/src/agent/tom-agent.ts
export class TomAgent {
  async analyze(transcript: string): Promise<EntryAnalysis> {
    // One place where Agent SDK is used
    // Returns structured analysis (sentiment, summary, raw output)
  }
}
```

### Zod Schemas Define the Data Model

Schemas live in `core/src/types/` and serve dual purpose: runtime validation + TypeScript type inference.

```typescript
// core/src/types/index.ts
import { z } from 'zod';

export const EntrySchema = z.object({
  id: z.string().uuid(),
  type: z.enum(['recording', 'note']),
  content: z.string(),
  audioPath: z.string().optional(),
  inputMethod: z.enum(['typed', 'dictated', 'recorded']),
  analysis: z.lazy(() => EntryAnalysisSchema).optional(),
  embedding: z.instanceof(Float32Array).optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type Entry = z.infer<typeof EntrySchema>;
```

### Config Manager Handles `~/.tom/`

`ConfigManager` reads/writes `~/.tom/config.json`, downloads models, and manages the local data directory.

```typescript
// core/src/config/config-manager.ts
export class ConfigManager {
  async init(): Promise<AppConfig>;
  async getConfig(): Promise<AppConfig>;
  async updateConfig(updates: Partial<AppConfig>): Promise<void>;
  getDataDir(): string; // ~/.tom/
  getAudioDir(): string; // ~/.tom/audio/
  getModelDir(): string; // ~/.tom/models/
}
```

### Embedding Service Behind an Interface

`IEmbeddingService` supports Ollama (local), cloud providers (future), or noop (fallback to FTS5).

```typescript
// core/src/services/embedding.ts
interface IEmbeddingService {
  embed(text: string): Promise<Float32Array>;
  isAvailable(): Promise<boolean>;
}

export class OllamaEmbeddingService implements IEmbeddingService {
  async embed(text: string): Promise<Float32Array> {
    // Ollama endpoint
  }
}

export class NoopEmbeddingService implements IEmbeddingService {
  async embed(): Promise<Float32Array> {
    return new Float32Array(0); // Fallback for FTS5-only search
  }
}
```

### SearchService Orchestrates Hybrid Search

Semantic search via vector similarity is primary; FTS5 keyword search is fallback.

```typescript
// core/src/services/search.ts
export class SearchService {
  async search(query: string, topK: number = 10): Promise<Entry[]> {
    if (embeddingAvailable) {
      // Embed query → semantic search
      return this.vectorSearch(queryEmbedding, topK);
    } else {
      // Fall back to FTS5 keyword search
      return this.ftsSearch(query, topK);
    }
  }
}
```

## Code Organization

### File Naming

- Feature files: `[noun].ts` for services/utilities, `[verb].tsx` for Ink components
  - Examples: `storage.ts`, `transcription.ts`, `record.tsx`, `search.tsx`
- Test colocated: `src/module/__tests__/module.test.ts`

### Imports & Module Resolution

```typescript
// ✅ DO: Use import type for type-only imports (verbatimModuleSyntax)
import type { Entry } from '../types/index.js';
import { z } from 'zod';

// ✅ DO: Use .js extensions in relative imports (ESM)
import { ConfigManager } from '../config/config-manager.js';

// ❌ DON'T: Omit .js extension
import { ConfigManager } from '../config/config-manager';

// ❌ DON'T: Mix import/export in one statement (verbatimModuleSyntax)
export { type Entry, EntrySchema } from '../types/index.js'; // NO
```

### Type Organization

Types are file-scoped. No separate `types/` subdirectory per entity. Define types near usage.

```typescript
// ✅ DO: Colocate type definitions with service
// core/src/services/storage.ts
interface IStorageService {
  saveEntry(entry: Entry): Promise<void>;
}

export class SQLiteStorageService implements IStorageService {
  // ...
}

// ❌ DON'T: Separate types file
// core/src/services/types/storage.types.ts (unnecessary)
```

### Provider-Dependent Config as Discriminated Unions

```typescript
// core/src/types/index.ts
export const LLMConfigSchema = z.discriminatedUnion('provider', [
  z.object({
    provider: z.literal('cloud'),
    apiKey: z.string(),
  }),
  z.object({
    provider: z.literal('local'),
    endpoint: z.string(),
    modelId: z.string(),
  }),
]);

export type LLMConfig = z.infer<typeof LLMConfigSchema>;

// Usage: type-safe pattern matching
if (config.llm.provider === 'cloud') {
  // config.llm.apiKey is available
} else {
  // config.llm.endpoint and modelId are available
}
```

## Testing

### TDD Workflow

1. **Write test** first (RED — test fails)
2. **Minimal implementation** to make test pass (GREEN)
3. **Refactor** while keeping test green
4. **Coverage target:** 80% on core package

### Test Structure (AAA Pattern)

```typescript
// core/src/services/__tests__/storage.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { SQLiteStorageService } from '../storage.js';
import type { Entry } from '../../types/index.js';

describe('SQLiteStorageService', () => {
  let storage: SQLiteStorageService;

  beforeEach(() => {
    // Arrange
    storage = new SQLiteStorageService(':memory:'); // in-memory DB for tests
  });

  it('should save and retrieve an entry', async () => {
    // Arrange
    const entry: Entry = {
      id: 'test-id',
      type: 'note',
      content: 'Hello world',
      inputMethod: 'typed',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    // Act
    await storage.saveEntry(entry);
    const retrieved = await storage.getEntry('test-id');

    // Assert
    expect(retrieved).toEqual(entry);
  });
});
```

### Running Tests

```bash
# Run all tests once
pnpm test

# Run in watch mode (file changes re-run related tests)
pnpm test:watch

# Run with coverage
pnpm test:coverage
```

### Testing Services with Mocks

Use dependency injection so services are testable without the real database/LLM:

```typescript
// ✅ DO: Inject dependencies (easy to mock)
export class TomAgent {
  constructor(private storageService: IStorageService) {}

  async analyze(entry: Entry): Promise<void> {
    // Use injected storage
  }
}

// ✅ In tests: mock the dependency
import { MockedFunction, describe, it, expect, vi } from 'vitest';

describe('TomAgent', () => {
  it('should save analysis', async () => {
    const mockStorage = {
      saveEntry: vi.fn(),
      getEntry: vi.fn(),
      // ... other methods
    } satisfies IStorageService;

    const agent = new TomAgent(mockStorage);
    await agent.analyze(testEntry);

    expect(mockStorage.saveEntry).toHaveBeenCalledWith(
      expect.objectContaining({ analysis: expect.any(Object) })
    );
  });
});
```

## Commands & CLI

### Command Structure (Commander)

Commander handles argument parsing and routing. Each command mounts an Ink component for interactive rendering.

```typescript
// packages/cli/src/cli.ts
import { Command } from 'commander';
import { RecordCommand } from './commands/record.js';
import type { IStorageService } from '@tom/core';

const program = new Command()
  .name('tom')
  .description('Voice capture and analysis')
  .version('2.0.0');

program
  .command('record')
  .description('Record and transcribe audio')
  .action(async () => {
    const storage = new SQLiteStorageService(/* ... */);
    const recordCommand = new RecordCommand(storage);
    await recordCommand.execute();
  });

program.parse();
```

### Ink Components for Interactive UI

Each command is an Ink component. Ink provides React-style rendering for the terminal.

```typescript
// packages/cli/src/commands/record.tsx
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import type { IStorageService } from '@tom/core';

interface RecordCommandProps {
  storage: IStorageService;
}

export const RecordCommand: React.FC<RecordCommandProps> = ({ storage }) => {
  const [transcript, setTranscript] = useState('');
  const [isRecording, setIsRecording] = useState(false);

  useEffect(() => {
    // Start recording, stream STT output
    const startRecording = async () => {
      setIsRecording(true);
      // ... transcription logic
      setTranscript(liveTranscript);
    };
    startRecording();
  }, []);

  return (
    <Box flexDirection="column">
      <Text>{isRecording ? '🔴 Recording...' : 'Press Enter to start'}</Text>
      <Text>{transcript}</Text>
    </Box>
  );
};
```

## MVP Commands

| Command | Purpose | Status |
|---------|---------|--------|
| `tom setup` | First-run wizard: choose LLM (cloud/local), download STT model, configure `~/.tom/` | Sprint 1 |
| `tom record` | Audio capture with real-time streaming transcription, sentiment/context analysis | Sprint 2 |
| `tom note` | Text entry with optional voice dictation (no audio saved), same analysis pipeline | Sprint 3 |
| `tom search` | Semantic search via vector similarity; FTS5 keyword fallback | Sprint 3 |

## Configuration & Options

### AppConfig Data Model

```typescript
// core/src/types/index.ts
export const AppConfigSchema = z.object({
  llm: z.discriminatedUnion('provider', [
    z.object({ provider: z.literal('cloud'), apiKey: z.string() }),
    z.object({
      provider: z.literal('local'),
      endpoint: z.string(),
      modelId: z.string(),
    }),
  ]),
  stt: z.object({
    engine: z.string(), // whisper variant
    modelPath: z.string(), // path to local model file
  }),
  embedding: z.object({
    provider: z.enum(['ollama', 'cloud', 'none']),
    model: z.string(),
    endpoint: z.string().optional(),
  }),
  storage: z.object({
    dbPath: z.string(),
  }),
});

export type AppConfig = z.infer<typeof AppConfigSchema>;
```

### ConfigManager Usage

```typescript
// core/src/config/config-manager.ts
const configManager = new ConfigManager();

// Initialize on first run (tom setup)
const config = await configManager.init();

// Read config
const currentConfig = await configManager.getConfig();

// Update config
await configManager.updateConfig({ embedding: { provider: 'none' } });

// Get paths
const dataDir = configManager.getDataDir(); // ~/.tom/
const audioDir = configManager.getAudioDir(); // ~/.tom/audio/
const modelDir = configManager.getModelDir(); // ~/.tom/models/
```

## Npm Scripts

```bash
# Development
pnpm install          # Install dependencies (uses pnpm workspaces)
pnpm build            # Build all packages (tsup)
pnpm dev              # Run CLI in development mode (ts-node)
pnpm format           # Prettier format all files
pnpm format:check     # Check if files are formatted
pnpm lint             # ESLint check
pnpm test             # Run all tests once
pnpm test:watch       # Run tests in watch mode
pnpm test:coverage    # Run tests with coverage report

# Distribution
pnpm dist             # Package CLI for distribution (SEA, pkg, or npm global)
```

## Code Style & Conventions

### Modern TypeScript Features (Required)

```typescript
// ✅ File-scoped types (no type files)
// core/src/services/storage.ts
interface IStorageService {
  // ...
}

// ✅ Primary constructor parameters
export class StorageService {
  constructor(
    private readonly dbPath: string,
    private readonly logger: ILogger
  ) {}
}

// ✅ Const assertions for literals
const CommandNames = {
  RECORD: 'record',
  NOTE: 'note',
  SEARCH: 'search',
} as const;
type CommandName = (typeof CommandNames)[keyof typeof CommandNames];

// ✅ Discriminated unions for provider config
type EmbeddingConfig =
  | { provider: 'ollama'; endpoint: string }
  | { provider: 'cloud'; apiKey: string }
  | { provider: 'none' };

// ✅ Zod for validation + type inference
export const EntrySchema = z.object({ /* ... */ });
export type Entry = z.infer<typeof EntrySchema>;

// ✅ Collection syntax
const entries: Entry[] = [entry1, entry2, entry3];
```

### Naming Conventions

| Category | Pattern | Example |
|----------|---------|---------|
| Services | `[Noun]Service` | `StorageService`, `TranscriptionService` |
| Interfaces | `I[Service]` | `IStorageService`, `IEmbeddingService` |
| Zod Schemas | `[Noun]Schema` | `EntrySchema`, `AppConfigSchema` |
| Types (inferred) | Same as noun | `type Entry`, `type AppConfig` |
| Config classes | `[Feature]Config` or `ConfigManager` | `LlmConfig`, `ConfigManager` |
| Constants | `SCREAMING_SNAKE_CASE` or const obj | `const CommandNames = { RECORD: 'record' }` |
| Ink components | `[Feature].tsx` (verb-noun) | `record.tsx`, `search.tsx` |
| Test files | `[module].test.ts` | `storage.test.ts` |

### What to Avoid

```typescript
// ❌ DON'T: Direct LLM provider instantiation outside core/agent/
import { Anthropic } from '@anthropic-ai/sdk';
const client = new Anthropic({ apiKey: process.env.ANTHROPIC_API_KEY });
// ^ Wrong location — belongs in TomAgent

// ❌ DON'T: Magic strings
if (config.provider === 'ollama') { }
// ^ Use const enum or const object

// ❌ DON'T: Import from CLI in core services
import { RecordUI } from '@tom/cli';
// ^ Core must not know about CLI

// ❌ DON'T: Throw raw Error — return Result type or throw domain-specific exception
throw new Error('Storage failed');
// ^ Better: return Result<T, E> or throw StorageException

// ❌ DON'T: Omit type annotations for public APIs
export function search(query) { } // Infer return type only
// ^ Should be: export function search(query: string): Promise<Entry[]>

// ❌ DON'T: Mix ESM and CommonJS
module.exports = { /* ... */ };
// ^ Use ESM consistently

// ❌ DON'T: Omit .js extension in relative imports (ESM requires it)
import { foo } from './foo';
// ^ Should be: import { foo } from './foo.js';
```

## Dependency Injection Pattern

Services depend on interfaces, not concrete implementations. This makes code testable and allows swapping providers.

```typescript
// ✅ Core service with DI
export class SearchService {
  constructor(
    private readonly storage: IStorageService,
    private readonly embedding: IEmbeddingService
  ) {}

  async search(query: string): Promise<Entry[]> {
    // Uses injected dependencies
  }
}

// ✅ CLI command with DI
export async function runRecord(
  storage: IStorageService,
  transcription: ITranscriptionService,
  agent: TomAgent
) {
  // Uses injected services
}

// ✅ In setup (dependency container)
const storage = new SQLiteStorageService(dbPath);
const embedding = new OllamaEmbeddingService(ollama);
const search = new SearchService(storage, embedding);
```

## Data Flow Examples

### `tom record` Command Flow

1. CLI mounts `RecordCommand` Ink component
2. Component calls TranscriptionService.startStreaming()
3. Audio chunks stream from microphone → Whisper
4. Transcription updates render live in Ink
5. User presses Enter → audio saved to `~/.tom/audio/{date}-{id}.wav`
6. In parallel:
   - TomAgent.analyze(transcript) → EntryAnalysis (sentiment, summary)
   - EmbeddingService.embed(transcript) → Float32Array
7. StorageService.saveEntry(entry) → persisted to DB
8. Ink renders final transcript, sentiment score, summary

### `tom search` Command Flow

1. CLI prompts for query string
2. SearchService.search(query) evaluates:
   - If EmbeddingService.isAvailable() → embed query vector
   - Perform semantic search via StorageService.searchByVector()
   - Rank results by cosine similarity
3. If no embeddings available → FTS5 fallback
4. Render results: ranked entries with timestamps, sentiment badges, excerpt

## Error Handling

### Graceful Degradation

- **Microphone unavailable**: Exit with clear error + platform-specific instructions
- **STT model not downloaded**: Prompt user to run `tom setup`
- **Ollama not running**: Skip embedding, search falls back to FTS5, warning displayed (non-blocking)
- **Claude API unavailable/rate-limited**: Skip analysis, entry saved without it (analysis can be retried later)
- **Database corrupted**: Display error and exit (no silent data loss)

**Design principle**: Capture always succeeds if the mic works. Analysis and embedding are enhancements that degrade gracefully.

## References

- **Design Spec** (authority): `docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md`
- **Zod**: https://zod.dev
- **TypeScript ESM**: https://nodejs.org/api/esm.html
- **Ink**: https://github.com/vadimdemedes/ink
- **Commander**: https://github.com/tj/commander.js
- **Vitest**: https://vitest.dev
- **better-sqlite3**: https://github.com/WiseLibs/better-sqlite3
- **Claude Agent SDK**: https://github.com/anthropics/anthropic-sdk-python (TypeScript docs: https://sdk.anthropic.com)

---

**Document Version**: 1.0.0 | **Last Updated**: 2026-03-22
