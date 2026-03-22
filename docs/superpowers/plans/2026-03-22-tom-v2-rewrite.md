# Ten-Second Tom v2.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite Ten-Second Tom from .NET/C# to Node.js/TypeScript as an intelligence-first voice capture and analysis CLI tool.

**Architecture:** pnpm monorepo with two packages — `core` (business logic, services, Agent SDK integration) and `cli` (Ink 5 terminal UI, Commander routing). Storage behind an interface to allow database technology swap. Claude Agent SDK for all LLM work (cloud or local via Ollama).

**Tech Stack:** TypeScript 5 (strict, ESM), Ink 5 + Commander, Claude Agent SDK, Zod, Vitest, pnpm workspaces, SQLite or PGlite (Sprint 1 research decides), Whisper STT (Sprint 1 research decides library)

**Spec:** `docs/superpowers/specs/2026-03-22-tom-v2-rewrite-design.md`

---

## File Map

### Root

| File | Purpose |
|------|---------|
| `package.json` | Root workspace config (private, no direct deps) |
| `pnpm-workspace.yaml` | Declares `packages/*` |
| `tsconfig.base.json` | Shared TS compiler options (spec says `tsconfig.json` — using `tsconfig.base.json` so packages can extend it cleanly) |
| `vitest.config.ts` | Root vitest config |
| `eslint.config.js` | Flat ESLint config |
| `.prettierrc` | Prettier config |
| `.gitignore` | Node.js ignores |
| `CLAUDE.md` | New TypeScript project instructions |

### packages/core/

| File | Purpose |
|------|---------|
| `package.json` | Core package deps |
| `tsconfig.json` | Extends base, sets paths |
| `src/index.ts` | Barrel export |
| `src/types/entry.ts` | Entry, EntryAnalysis Zod schemas + TS types |
| `src/types/config.ts` | AppConfig Zod schema + TS types |
| `src/types/index.ts` | Types barrel export |
| `src/services/storage.ts` | IStorageService interface |
| `src/services/storage-sqlite.ts` | SQLite + sqlite-vec implementation |
| `src/services/transcription.ts` | ITranscriptionService interface + implementation |
| `src/services/embedding.ts` | IEmbeddingService interface + Ollama implementation |
| `src/services/search.ts` | SearchService (hybrid semantic + FTS) |
| `src/services/audio.ts` | IAudioService interface + implementation |
| `src/agent/config.ts` | Agent provider config types |
| `src/agent/tom-agent.ts` | TomAgent — Claude Agent SDK analysis agent |
| `src/config/config-manager.ts` | Read/write `~/.tom/config.json`, ensure directories |

### packages/core/ tests

| File | Purpose |
|------|---------|
| `src/types/__tests__/entry.test.ts` | Schema validation tests |
| `src/types/__tests__/config.test.ts` | Config schema validation tests |
| `src/services/__tests__/storage.test.ts` | Storage interface contract tests |
| `src/services/__tests__/embedding.test.ts` | Embedding service tests |
| `src/services/__tests__/search.test.ts` | Search service tests |
| `src/agent/__tests__/tom-agent.test.ts` | TomAgent tests (mocked SDK) |
| `src/config/__tests__/config-manager.test.ts` | Config manager tests |

### packages/cli/

| File | Purpose |
|------|---------|
| `package.json` | CLI package deps (ink, commander) |
| `tsconfig.json` | Extends base, JSX settings for Ink |
| `src/cli.ts` | Entry point — Commander arg parsing, mounts Ink |
| `src/app.tsx` | Root Ink component |
| `src/commands/setup.tsx` | Setup wizard (LLM, embedding, STT, ~/.tom/) |
| `src/commands/record.tsx` | Record command with streaming transcription |
| `src/commands/note.tsx` | Note command with typed/dictated input |
| `src/commands/search.tsx` | Search command |
| `src/components/RecordingUI.tsx` | Recording display (timer, live transcript) |
| `src/components/SentimentDisplay.tsx` | Analysis results display |
| `src/components/SearchResults.tsx` | Search results list with selection |

### migrations/

| File | Purpose |
|------|---------|
| `local/001_entries.sql` | Entries table + FTS5 virtual table |
| `local/002_vectors.sql` | Vector table (sqlite-vec) or pgvector setup |

---

## Sprint 1 — Foundation + Research (Weeks 1-2)

### Task 1: Create Branch & Wipe .NET Code

**Files:**
- Remove: all `.cs`, `.csproj`, `.sln`, `src/`, `tests/`, `bin/`, `obj/`, `installer/`, `specs/`, `.specify/`, `nuget.config`, `Directory.Build.props`, `Makefile`, `AGENTS.md`, `SECURITY.md`, `example.*` files
- Keep: `LICENSE`, `docs/superpowers/` (specs and plans), `.git/`

- [ ] **Step 1: Create v2 branch**

```bash
git checkout -b v2-rewrite
```

- [ ] **Step 2: Remove .NET files**

Remove all .NET source, build config, and project files. Keep LICENSE and docs/superpowers/.

```bash
rm -rf src/ tests/ bin/ obj/ installer/ specs/ .specify/
rm -f TenSecondTom.sln nuget.config Directory.Build.props Makefile AGENTS.md SECURITY.md GlobalSuppressions.cs
rm -f example.appsettings.json example.config.json example.env
rm -f CLAUDE.md README.md
rm -rf docs/AUDIO.md docs/AUTHENTICATION.md docs/CICD.md docs/CONFIGURATION.md docs/COVERAGE.md docs/ENVIRONMENT.md docs/OBSIDIAN-STORAGE.md
```

- [ ] **Step 3: Commit clean slate**

```bash
git add -A
git commit -m "chore: remove .NET codebase for v2 Node.js rewrite

Preserving LICENSE and design specs. Full .NET history available in git.
Old implementation plan kept at docs/IMPLEMENTATION-PLAN.md for reference."
```

---

### Task 2: Initialize pnpm Monorepo

**Files:**
- Create: `package.json`, `pnpm-workspace.yaml`, `tsconfig.base.json`, `.gitignore`, `.prettierrc`, `eslint.config.js`, `.npmrc`

- [ ] **Step 1: Create root package.json**

```json
{
  "name": "ten-second-tom",
  "private": true,
  "type": "module",
  "engines": {
    "node": ">=20.0.0"
  },
  "packageManager": "pnpm@9.15.0",
  "scripts": {
    "build": "pnpm -r build",
    "test": "vitest run",
    "test:watch": "vitest",
    "lint": "eslint packages/",
    "format": "prettier --write \"packages/**/*.{ts,tsx,json}\"",
    "format:check": "prettier --check \"packages/**/*.{ts,tsx,json}\""
  }
}
```

- [ ] **Step 2: Create pnpm-workspace.yaml**

```yaml
packages:
  - "packages/*"
```

- [ ] **Step 3: Create tsconfig.base.json**

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "NodeNext",
    "moduleResolution": "NodeNext",
    "lib": ["ES2022"],
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "forceConsistentCasingInFileNames": true,
    "resolveJsonModule": true,
    "declaration": true,
    "declarationMap": true,
    "sourceMap": true,
    "outDir": "./dist",
    "rootDir": "./src",
    "isolatedModules": true,
    "verbatimModuleSyntax": true,
    "noUncheckedIndexedAccess": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "exactOptionalPropertyTypes": false
  }
}
```

- [ ] **Step 4: Create .gitignore**

```gitignore
node_modules/
dist/
*.tsbuildinfo
.turbo/
coverage/

# Tom data directory (local dev)
.tom/

# Environment
.env
.env.local
.env.*.local

# OS
.DS_Store
Thumbs.db

# IDE
.idea/
.vscode/
*.swp
*.swo
```

- [ ] **Step 5: Create .prettierrc**

```json
{
  "semi": true,
  "singleQuote": true,
  "trailingComma": "all",
  "printWidth": 100,
  "tabWidth": 2
}
```

- [ ] **Step 6: Create .npmrc**

```ini
auto-install-peers=true
shamefully-hoist=false
```

- [ ] **Step 7: Create eslint.config.js**

```js
import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  eslint.configs.recommended,
  ...tseslint.configs.strict,
  {
    rules: {
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
    },
  },
  {
    ignores: ['**/dist/', '**/node_modules/', '**/*.js'],
  },
);
```

- [ ] **Step 8: Install root dev dependencies**

```bash
pnpm add -D -w typescript@^5.7 vitest@^3.0 @vitest/coverage-v8 eslint@^9 @eslint/js typescript-eslint prettier
```

- [ ] **Step 9: Create vitest.config.ts**

```typescript
import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    globals: true,
    coverage: {
      provider: 'v8',
      reporter: ['text', 'lcov'],
      include: ['packages/*/src/**/*.ts'],
      exclude: ['**/__tests__/**', '**/index.ts'],
      thresholds: {
        lines: 80,
        functions: 80,
        branches: 80,
        statements: 80,
      },
    },
  },
});
```

- [ ] **Step 10: Commit monorepo scaffolding**

```bash
git add -A
git commit -m "chore: initialize pnpm monorepo with TypeScript, ESLint, Prettier, Vitest"
```

---

### Task 3: Core Package Scaffolding

**Files:**
- Create: `packages/core/package.json`, `packages/core/tsconfig.json`, `packages/core/src/index.ts`

- [ ] **Step 1: Create packages/core directory structure**

```bash
mkdir -p packages/core/src/{types,services,agent,config}
mkdir -p packages/core/src/types/__tests__
mkdir -p packages/core/src/services/__tests__
mkdir -p packages/core/src/agent/__tests__
mkdir -p packages/core/src/config/__tests__
```

- [ ] **Step 2: Create packages/core/package.json**

```json
{
  "name": "@ten-second-tom/core",
  "version": "2.0.0",
  "type": "module",
  "main": "./dist/index.js",
  "types": "./dist/index.d.ts",
  "exports": {
    ".": {
      "import": "./dist/index.js",
      "types": "./dist/index.d.ts"
    }
  },
  "scripts": {
    "build": "tsup src/index.ts --format esm --dts",
    "dev": "tsup src/index.ts --format esm --dts --watch"
  },
  "dependencies": {
    "zod": "^3.24.0",
    "uuid": "^11.1.0"
  },
  "devDependencies": {
    "tsup": "^8.4.0",
    "@types/uuid": "^10.0.0"
  }
}
```

Note: Additional deps (better-sqlite3, @anthropic-ai/sdk, etc.) will be added in later tasks as needed.

- [ ] **Step 3: Create packages/core/tsconfig.json**

```json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "outDir": "./dist",
    "rootDir": "./src"
  },
  "include": ["src/**/*.ts"]
}
```

- [ ] **Step 4: Create packages/core/src/index.ts**

```typescript
// Core package barrel export
// Types and services are exported as they are built

export {};
```

- [ ] **Step 5: Install core dependencies**

```bash
cd packages/core && pnpm install
```

- [ ] **Step 6: Verify build works**

```bash
pnpm --filter @ten-second-tom/core build
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold core package with tsup, zod, uuid"
```

---

### Task 4: CLI Package Scaffolding

**Files:**
- Create: `packages/cli/package.json`, `packages/cli/tsconfig.json`, `packages/cli/src/cli.ts`, `packages/cli/src/app.tsx`

- [ ] **Step 1: Create packages/cli directory structure**

```bash
mkdir -p packages/cli/src/{commands,components}
```

- [ ] **Step 2: Create packages/cli/package.json**

```json
{
  "name": "@ten-second-tom/cli",
  "version": "2.0.0",
  "type": "module",
  "bin": {
    "tom": "./dist/cli.js"
  },
  "scripts": {
    "build": "tsup src/cli.ts --format esm",
    "dev": "tsup src/cli.ts --format esm --watch",
    "start": "node dist/cli.js"
  },
  "dependencies": {
    "@ten-second-tom/core": "workspace:*",
    "ink": "^5.1.0",
    "ink-select-input": "^6.0.0",
    "ink-text-input": "^6.0.0",
    "ink-spinner": "^5.0.0",
    "react": "^18.3.0",
    "commander": "^13.1.0"
  },
  "devDependencies": {
    "tsup": "^8.4.0",
    "@types/react": "^18.3.0",
    "ink-testing-library": "^4.0.0"
  }
}
```

- [ ] **Step 3: Create packages/cli/tsconfig.json**

```json
{
  "extends": "../../tsconfig.base.json",
  "compilerOptions": {
    "outDir": "./dist",
    "rootDir": "./src",
    "jsx": "react-jsx",
    "jsxImportSource": "react"
  },
  "include": ["src/**/*.ts", "src/**/*.tsx"]
}
```

- [ ] **Step 4: Create packages/cli/src/cli.ts**

```typescript
#!/usr/bin/env node
import { Command } from 'commander';

const program = new Command();

program
  .name('tom')
  .description('Ten-Second Tom — intelligence-first voice capture and analysis')
  .version('2.0.0');

// Commands will be registered here as they are built
// program.addCommand(setupCommand);
// program.addCommand(recordCommand);
// program.addCommand(noteCommand);
// program.addCommand(searchCommand);

program.parse();
```

- [ ] **Step 5: Create packages/cli/src/app.tsx**

```tsx
import React from 'react';
import { Text } from 'ink';

interface AppProps {
  command: string;
}

export function App({ command }: AppProps) {
  return <Text>Ten-Second Tom v2.0 — {command}</Text>;
}
```

- [ ] **Step 6: Install CLI dependencies**

```bash
cd packages/cli && pnpm install
```

- [ ] **Step 7: Verify CLI builds and runs**

```bash
pnpm --filter @ten-second-tom/cli build
node packages/cli/dist/cli.js --help
```

Expected: Shows help with name, description, version.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: scaffold CLI package with Ink 5, Commander, React"
```

---

### Task 5: Zod Schemas & Types

**Files:**
- Create: `packages/core/src/types/entry.ts`, `packages/core/src/types/config.ts`, `packages/core/src/types/index.ts`
- Test: `packages/core/src/types/__tests__/entry.test.ts`, `packages/core/src/types/__tests__/config.test.ts`

- [ ] **Step 1: Write Entry schema tests**

```typescript
// packages/core/src/types/__tests__/entry.test.ts
import { describe, it, expect } from 'vitest';
import { EntrySchema, EntryAnalysisSchema } from '../entry.js';

describe('EntrySchema', () => {
  it('validates a valid recording entry', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'recording' as const,
      content: 'We shipped the new dashboard today',
      audioPath: '2026-04/2026-04-01-550e8400.wav',
      inputMethod: 'recorded' as const,
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });

  it('validates a valid note entry', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440001',
      type: 'note' as const,
      content: 'Need to follow up on deploy pipeline',
      inputMethod: 'typed' as const,
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });

  it('validates a dictated note entry', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440002',
      type: 'note' as const,
      content: 'Dictated note about standup',
      inputMethod: 'dictated' as const,
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });

  it('rejects invalid entry type', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'invalid',
      content: 'test',
      inputMethod: 'typed',
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(false);
  });

  it('rejects empty content', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'note',
      content: '',
      inputMethod: 'typed',
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(false);
  });

  it('allows optional analysis and embedding', () => {
    const entry = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      type: 'recording' as const,
      content: 'test content',
      inputMethod: 'recorded' as const,
      analysis: {
        sentiment: { score: 0.7, label: 'positive — excited about launch', confidence: 0.9 },
        summary: 'Positive update about dashboard launch',
        raw: { topics: ['dashboard', 'launch'] },
      },
      createdAt: '2026-04-01T10:00:00.000Z',
      updatedAt: '2026-04-01T10:00:00.000Z',
    };
    const result = EntrySchema.safeParse(entry);
    expect(result.success).toBe(true);
  });
});

describe('EntryAnalysisSchema', () => {
  it('validates a valid analysis', () => {
    const analysis = {
      sentiment: { score: -0.3, label: 'mildly frustrated', confidence: 0.85 },
      summary: 'Frustration with deploy pipeline reliability',
      raw: {},
    };
    const result = EntryAnalysisSchema.safeParse(analysis);
    expect(result.success).toBe(true);
  });

  it('rejects sentiment score out of range', () => {
    const analysis = {
      sentiment: { score: 1.5, label: 'positive', confidence: 0.9 },
      summary: 'test',
      raw: {},
    };
    const result = EntryAnalysisSchema.safeParse(analysis);
    expect(result.success).toBe(false);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
pnpm vitest run packages/core/src/types/__tests__/entry.test.ts
```

Expected: FAIL — module `../entry.js` not found.

- [ ] **Step 3: Implement Entry schemas**

```typescript
// packages/core/src/types/entry.ts
import { z } from 'zod';

export const SentimentSchema = z.object({
  score: z.number().min(-1).max(1),
  label: z.string().min(1),
  confidence: z.number().min(0).max(1),
});

export const EntryAnalysisSchema = z.object({
  sentiment: SentimentSchema,
  summary: z.string(),
  raw: z.record(z.unknown()),
});

export const EntrySchema = z.object({
  id: z.string().uuid(),
  type: z.enum(['recording', 'note']),
  content: z.string().min(1),
  audioPath: z.string().optional(),
  inputMethod: z.enum(['typed', 'dictated', 'recorded']),
  analysis: EntryAnalysisSchema.optional(),
  embedding: z.instanceof(Float32Array).optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
});

export type Entry = z.infer<typeof EntrySchema>;
export type EntryAnalysis = z.infer<typeof EntryAnalysisSchema>;
export type Sentiment = z.infer<typeof SentimentSchema>;

export const CreateEntrySchema = EntrySchema.omit({
  id: true,
  analysis: true,
  embedding: true,
  createdAt: true,
  updatedAt: true,
});

export type CreateEntry = z.infer<typeof CreateEntrySchema>;
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
pnpm vitest run packages/core/src/types/__tests__/entry.test.ts
```

Expected: All tests PASS.

- [ ] **Step 5: Write AppConfig schema tests**

```typescript
// packages/core/src/types/__tests__/config.test.ts
import { describe, it, expect } from 'vitest';
import { AppConfigSchema } from '../config.js';

describe('AppConfigSchema', () => {
  it('validates a cloud config', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: { provider: 'cloud' as const, model: 'voyage-3-lite' },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('validates a local config with ollama', () => {
    const config = {
      llm: { provider: 'local' as const, localEndpoint: 'http://localhost:11434', modelId: 'qwen2.5:7b' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: { provider: 'ollama' as const, model: 'nomic-embed-text', endpoint: 'http://localhost:11434' },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('validates config with no embedding provider', () => {
    const config = {
      llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
      stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
      embedding: { provider: 'none' as const, model: '' },
      storage: { dbPath: '/Users/test/.tom/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(true);
  });

  it('rejects invalid LLM provider', () => {
    const config = {
      llm: { provider: 'openai' },
      stt: { engine: 'whisper', modelPath: '/tmp/model' },
      embedding: { provider: 'none', model: '' },
      storage: { dbPath: '/tmp/tom.db' },
    };
    const result = AppConfigSchema.safeParse(config);
    expect(result.success).toBe(false);
  });
});
```

- [ ] **Step 6: Run tests to verify they fail**

```bash
pnpm vitest run packages/core/src/types/__tests__/config.test.ts
```

Expected: FAIL — module `../config.js` not found.

- [ ] **Step 7: Implement AppConfig schema**

```typescript
// packages/core/src/types/config.ts
import { z } from 'zod';

export const LlmConfigSchema = z.object({
  provider: z.enum(['cloud', 'local']),
  apiKey: z.string().optional(),
  localEndpoint: z.string().url().optional(),
  modelId: z.string().optional(),
});

export const SttConfigSchema = z.object({
  engine: z.string().min(1),
  modelPath: z.string().min(1),
});

export const EmbeddingConfigSchema = z.object({
  provider: z.enum(['ollama', 'cloud', 'none']),
  model: z.string(),
  endpoint: z.string().url().optional(),
});

export const StorageConfigSchema = z.object({
  dbPath: z.string().min(1),
});

export const AppConfigSchema = z.object({
  llm: LlmConfigSchema,
  stt: SttConfigSchema,
  embedding: EmbeddingConfigSchema,
  storage: StorageConfigSchema,
});

export type AppConfig = z.infer<typeof AppConfigSchema>;
export type LlmConfig = z.infer<typeof LlmConfigSchema>;
export type SttConfig = z.infer<typeof SttConfigSchema>;
export type EmbeddingConfig = z.infer<typeof EmbeddingConfigSchema>;
```

- [ ] **Step 8: Run tests to verify they pass**

```bash
pnpm vitest run packages/core/src/types/__tests__/config.test.ts
```

Expected: All tests PASS.

- [ ] **Step 9: Create types barrel export and update core barrel export**

```typescript
// packages/core/src/types/index.ts
export {
  EntrySchema, EntryAnalysisSchema, SentimentSchema, CreateEntrySchema,
  type Entry, type EntryAnalysis, type Sentiment, type CreateEntry,
} from './entry.js';

export {
  AppConfigSchema, LlmConfigSchema, SttConfigSchema, EmbeddingConfigSchema, StorageConfigSchema,
  type AppConfig, type LlmConfig, type SttConfig, type EmbeddingConfig,
} from './config.js';
```

```typescript
// packages/core/src/index.ts
export * from './types/index.js';
```

- [ ] **Step 10: Run all tests, commit**

```bash
pnpm vitest run
git add -A
git commit -m "feat: add Zod schemas for Entry, EntryAnalysis, AppConfig"
```

---

### Task 6: Storage Interface & SQLite Implementation

**Files:**
- Create: `packages/core/src/services/storage.ts`, `packages/core/src/services/storage-sqlite.ts`, `migrations/local/001_entries.sql`
- Test: `packages/core/src/services/__tests__/storage.test.ts`

- [ ] **Step 1: Write storage interface contract tests**

```typescript
// packages/core/src/services/__tests__/storage.test.ts
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { SqliteStorageService } from '../storage-sqlite.js';
import type { IStorageService } from '../storage.js';
import type { CreateEntry } from '../../types/entry.js';
import { randomUUID } from 'node:crypto';
import { mkdtempSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';

// These tests define the storage contract.
// Any IStorageService implementation must pass them.
describe('IStorageService contract (SQLite)', () => {
  let storage: IStorageService;
  let tempDir: string;

  beforeEach(() => {
    tempDir = mkdtempSync(join(tmpdir(), 'tom-test-'));
    const dbPath = join(tempDir, 'test.db');
    storage = new SqliteStorageService(dbPath);
  });

  afterEach(() => {
    storage.close();
    rmSync(tempDir, { recursive: true, force: true });
  });

  it('saves and retrieves an entry', async () => {
    const input: CreateEntry = { type: 'note', content: 'Test note content', inputMethod: 'typed' };
    const saved = await storage.saveEntry(input);
    expect(saved.id).toBeDefined();
    expect(saved.content).toBe('Test note content');

    const retrieved = await storage.getEntry(saved.id);
    expect(retrieved).toBeDefined();
    expect(retrieved!.content).toBe('Test note content');
  });

  it('returns undefined for non-existent entry', async () => {
    const result = await storage.getEntry(randomUUID());
    expect(result).toBeUndefined();
  });

  it('lists entries in reverse chronological order', async () => {
    await storage.saveEntry({ type: 'note', content: 'First', inputMethod: 'typed' });
    await storage.saveEntry({ type: 'note', content: 'Second', inputMethod: 'typed' });
    await storage.saveEntry({ type: 'recording', content: 'Third', inputMethod: 'recorded', audioPath: '2026-04/test.wav' });

    const entries = await storage.listEntries({ limit: 10 });
    expect(entries).toHaveLength(3);
    expect(entries[0]!.content).toBe('Third');
    expect(entries[2]!.content).toBe('First');
  });

  it('filters entries by type', async () => {
    await storage.saveEntry({ type: 'note', content: 'A note', inputMethod: 'typed' });
    await storage.saveEntry({ type: 'recording', content: 'A recording', inputMethod: 'recorded' });

    const notes = await storage.listEntries({ type: 'note', limit: 10 });
    expect(notes).toHaveLength(1);
    expect(notes[0]!.type).toBe('note');
  });

  it('updates entry with analysis', async () => {
    const saved = await storage.saveEntry({ type: 'note', content: 'Test', inputMethod: 'typed' });
    const analysis = {
      sentiment: { score: 0.5, label: 'positive', confidence: 0.9 },
      summary: 'A positive test note',
      raw: {},
    };
    await storage.updateEntryAnalysis(saved.id, analysis);

    const updated = await storage.getEntry(saved.id);
    expect(updated!.analysis).toBeDefined();
    expect(updated!.analysis!.sentiment.score).toBe(0.5);
  });

  it('searches entries by keyword (FTS)', async () => {
    await storage.saveEntry({ type: 'note', content: 'The deploy pipeline is broken again', inputMethod: 'typed' });
    await storage.saveEntry({ type: 'note', content: 'Lunch was great today', inputMethod: 'typed' });

    const results = await storage.searchByKeyword('deploy pipeline');
    expect(results).toHaveLength(1);
    expect(results[0]!.content).toContain('deploy');
  });

  it('deletes an entry', async () => {
    const saved = await storage.saveEntry({ type: 'note', content: 'Delete me', inputMethod: 'typed' });
    await storage.deleteEntry(saved.id);
    const result = await storage.getEntry(saved.id);
    expect(result).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
pnpm vitest run packages/core/src/services/__tests__/storage.test.ts
```

Expected: FAIL — modules not found.

- [ ] **Step 3: Create storage interface**

```typescript
// packages/core/src/services/storage.ts
import type { Entry, CreateEntry, EntryAnalysis } from '../types/entry.js';

export interface ListEntriesOptions {
  type?: 'recording' | 'note';
  limit: number;
  offset?: number;
}

export interface IStorageService {
  saveEntry(input: CreateEntry): Promise<Entry>;
  getEntry(id: string): Promise<Entry | undefined>;
  listEntries(options: ListEntriesOptions): Promise<Entry[]>;
  updateEntryAnalysis(id: string, analysis: EntryAnalysis): Promise<void>;
  updateEntryEmbedding(id: string, embedding: Float32Array): Promise<void>;
  searchByKeyword(query: string): Promise<Entry[]>;
  searchByVector(embedding: Float32Array, limit: number): Promise<Entry[]>;
  deleteEntry(id: string): Promise<void>;
  close(): void;
}
```

- [ ] **Step 4: Create SQL migration**

```sql
-- migrations/local/001_entries.sql
CREATE TABLE IF NOT EXISTS entries (
  id TEXT PRIMARY KEY,
  type TEXT NOT NULL CHECK(type IN ('recording', 'note')),
  content TEXT NOT NULL,
  audio_path TEXT,
  input_method TEXT NOT NULL CHECK(input_method IN ('typed', 'dictated', 'recorded')),
  analysis TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS idx_entries_type ON entries(type);
CREATE INDEX IF NOT EXISTS idx_entries_created ON entries(created_at DESC);

CREATE VIRTUAL TABLE IF NOT EXISTS entries_fts USING fts5(content, content='entries', content_rowid='rowid');

CREATE TRIGGER IF NOT EXISTS entries_ai AFTER INSERT ON entries BEGIN
  INSERT INTO entries_fts(rowid, content) VALUES (new.rowid, new.content);
END;
CREATE TRIGGER IF NOT EXISTS entries_ad AFTER DELETE ON entries BEGIN
  INSERT INTO entries_fts(entries_fts, rowid, content) VALUES('delete', old.rowid, old.content);
END;
CREATE TRIGGER IF NOT EXISTS entries_au AFTER UPDATE ON entries BEGIN
  INSERT INTO entries_fts(entries_fts, rowid, content) VALUES('delete', old.rowid, old.content);
  INSERT INTO entries_fts(rowid, content) VALUES (new.rowid, new.content);
END;
```

- [ ] **Step 5: Install better-sqlite3**

```bash
pnpm --filter @ten-second-tom/core add better-sqlite3
pnpm --filter @ten-second-tom/core add -D @types/better-sqlite3
```

- [ ] **Step 6: Implement SQLite storage service**

Implement `SqliteStorageService` in `packages/core/src/services/storage-sqlite.ts`:
- Constructor takes `dbPath`, opens database with WAL mode
- Runs migrations on init (inline SQL as fallback if migration files not found)
- Maps between camelCase TS types and snake_case SQL columns
- `saveEntry`: generates UUID, inserts row, returns full Entry
- `getEntry`: SELECT by id, parse analysis JSON
- `listEntries`: SELECT with optional type filter, ORDER BY created_at DESC, LIMIT/OFFSET
- `updateEntryAnalysis`: UPDATE analysis column with JSON.stringify
- `updateEntryEmbedding`: placeholder (vector storage pending Sprint 1 research)
- `searchByKeyword`: JOIN with FTS5 virtual table, ORDER BY rank
- `searchByVector`: placeholder returning empty array (pending research)
- `deleteEntry`: DELETE by id
- `close`: close database connection

- [ ] **Step 7: Run tests to verify they pass**

```bash
pnpm vitest run packages/core/src/services/__tests__/storage.test.ts
```

Expected: All tests PASS.

- [ ] **Step 8: Update core barrel export, commit**

```bash
git add -A
git commit -m "feat: add storage interface and SQLite implementation with FTS5"
```

---

### Task 7: Config Manager

**Files:**
- Create: `packages/core/src/config/config-manager.ts`
- Test: `packages/core/src/config/__tests__/config-manager.test.ts`

- [ ] **Step 1: Write config manager tests**

Test that ConfigManager:
- Creates `~/.tom/`, `~/.tom/audio/`, `~/.tom/models/` directories on `ensureDirectories()`
- Saves config to `config.json` and loads it back with validation
- Returns `undefined` when no config file exists
- Throws on invalid config JSON
- Reports `isSetupComplete()` based on config existence
- Exposes `homePath`, `audioPath`, `modelsPath` getters

Use a temp directory for tests (mkdtempSync), clean up in afterEach.

- [ ] **Step 2: Run tests to verify they fail**
- [ ] **Step 3: Implement ConfigManager**

Constructor takes optional `homePath` (defaults to `~/.tom`). Provides:
- `ensureDirectories()`: mkdirSync recursive for home, audio, models
- `save(config)`: validate with Zod, write JSON
- `load()`: read file, parse, validate, return (or undefined if missing)
- `isSetupComplete()`: try load, return boolean

- [ ] **Step 4: Run tests to verify they pass**
- [ ] **Step 5: Update core barrel export, commit**

```bash
git add -A
git commit -m "feat: add ConfigManager for ~/.tom/ directory and config.json management"
```

---

### Task 8: Agent SDK Integration (TomAgent Skeleton)

**Files:**
- Create: `packages/core/src/agent/config.ts`, `packages/core/src/agent/tom-agent.ts`
- Test: `packages/core/src/agent/__tests__/tom-agent.test.ts`

- [ ] **Step 1: Install Agent SDK**

```bash
pnpm --filter @ten-second-tom/core add @anthropic-ai/sdk
```

- [ ] **Step 2: Write TomAgent tests (mocked SDK)**

Test that TomAgent:
- Calls Anthropic SDK with analysis prompt and content
- Parses structured JSON response into EntryAnalysis
- Clamps sentiment score to [-1, 1] range
- Throws on empty content
- Returns properly typed EntryAnalysis

Mock `@anthropic-ai/sdk` to return a canned JSON response.

- [ ] **Step 3: Run tests to verify they fail**

- [ ] **Step 4: Create agent config types**

`packages/core/src/agent/config.ts`:
- `AgentConfig` interface: provider, apiKey, localEndpoint, modelId
- `getModelId(config)`: returns cloud model name or local model name
- `getBaseUrl(config)`: returns local endpoint URL or undefined for cloud

- [ ] **Step 5: Implement TomAgent**

`packages/core/src/agent/tom-agent.ts`:
- Constructor takes AgentConfig, creates Anthropic client
- `analyze(content: string): Promise<EntryAnalysis>` — sends analysis prompt, parses JSON response
- Analysis prompt instructs model to return structured JSON with sentiment (score, descriptive label, confidence), summary, topics, emotions
- Clamps score/confidence to valid ranges

- [ ] **Step 6: Run tests to verify they pass**
- [ ] **Step 7: Update core barrel export, commit**

```bash
git add -A
git commit -m "feat: add TomAgent skeleton with Claude Agent SDK integration"
```

---

### Task 9: Embedding Service

**Files:**
- Create: `packages/core/src/services/embedding.ts`
- Test: `packages/core/src/services/__tests__/embedding.test.ts`

- [ ] **Step 1: Write embedding service tests**

Test `OllamaEmbeddingService`:
- Calls Ollama `/api/embeddings` endpoint with model and prompt
- Returns Float32Array of embeddings
- Throws when Ollama is unavailable
- `isAvailable()` returns true/false based on health check

Test `NoopEmbeddingService`:
- `isAvailable()` always returns false
- `embed()` throws "No embedding provider configured"

Mock `fetch` for Ollama API calls.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement embedding service**

`IEmbeddingService` interface: `embed(text): Promise<Float32Array>`, `isAvailable(): Promise<boolean>`

`OllamaEmbeddingService`: calls Ollama REST API, returns Float32Array

`NoopEmbeddingService`: always unavailable, throws on embed

- [ ] **Step 4: Run tests to verify they pass**
- [ ] **Step 5: Update core barrel export, commit**

```bash
git add -A
git commit -m "feat: add embedding service with Ollama and noop implementations"
```

---

### Task 10: Search Service

**Files:**
- Create: `packages/core/src/services/search.ts`
- Test: `packages/core/src/services/__tests__/search.test.ts`

- [ ] **Step 1: Write search service tests**

Test SearchService:
- Uses semantic search (embed query + searchByVector) when embedding is available
- Falls back to FTS (searchByKeyword) when embedding is unavailable
- Falls back to FTS when embedding.embed() throws

Mock IStorageService and IEmbeddingService.

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement SearchService**

Constructor takes IStorageService and IEmbeddingService. `search(query, limit)`:
1. Check embedding.isAvailable()
2. If yes: embed query, call storage.searchByVector
3. If no (or on error): call storage.searchByKeyword

- [ ] **Step 4: Run tests to verify they pass**
- [ ] **Step 5: Update core barrel export, commit**

```bash
git add -A
git commit -m "feat: add SearchService with semantic search and FTS fallback"
```

---

### Task 11: Setup Wizard Command

**Files:**
- Create: `packages/cli/src/commands/setup.tsx`
- Modify: `packages/cli/src/cli.ts` — register setup command

**Depends on:** Tasks 13-14 (research spikes) should complete first so the wizard knows which STT engine/model to offer.

- [ ] **Step 1: Write setup wizard tests**

Test with ink-testing-library:
- Renders LLM provider selection on start
- Navigating to cloud shows API key input
- Navigating to local shows endpoint input
- Shows embedding provider selection after LLM config
- Shows STT model download step after embedding config
- Saves valid config via ConfigManager on completion
- Creates `~/.tom/` directory structure

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Create setup command**

Ink wizard component with steps:
1. Choose LLM provider (cloud Claude / local Ollama)
2. If cloud: enter API key. If local: enter endpoint URL.
3. Choose embedding provider (ollama / cloud / none)
4. Download STT model — show progress bar, save to `~/.tom/models/`. Use the STT library chosen in Task 13's research spike.
5. Save config via ConfigManager, create `~/.tom/` directories

Use `ink-select-input` for choices, `ink-text-input` for text fields, `ink-spinner` for download progress.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Register in cli.ts**

```typescript
import { setupCommand } from './commands/setup.js';
program.addCommand(setupCommand);
```

- [ ] **Step 6: Build and verify**

```bash
pnpm --filter @ten-second-tom/cli build
node packages/cli/dist/cli.js setup --help
```

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add tom setup wizard with LLM, embedding, and STT configuration"
```

---

### Task 12: New CLAUDE.md

**Files:**
- Create: `CLAUDE.md`

- [ ] **Step 1: Write new CLAUDE.md**

Cover:
- Project overview (Tom v2, Node.js/TypeScript rewrite)
- Tech stack (TypeScript, Ink 5, Commander, Claude Agent SDK, Zod, Vitest, pnpm)
- Monorepo structure (packages/core, packages/cli)
- Key patterns: storage interface, Agent SDK in core/agent, Zod schemas in core/types
- Testing: TDD, Vitest, 80% coverage, AAA pattern
- Commands: `pnpm test`, `pnpm build`, `pnpm lint`
- Reference to design spec

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: add new CLAUDE.md for TypeScript v2 project"
```

---

### Task 13: Research Spike — STT Library & Approved Local Models

**Files:**
- Create: `docs/research/stt-evaluation.md`

This is a research task, not a code task.

- [ ] **Step 1: Evaluate Node.js STT libraries**

Research and test:
- `@xenova/transformers` (transformers.js) — Distil-Whisper, WASM/WebGPU
- `@fugood/whisper.node` — Native bindings, Metal on macOS
- `whisper-node` — Another native binding option
- `sherpa-onnx-node` — ONNX runtime, supports streaming
- Any new entrants since March 2026

Criteria (from spec):
1. Chunked/streaming inference — can it process audio chunks and return partial transcripts? (REQUIRED)
2. Metal acceleration on macOS Apple Silicon
3. Windows support
4. Model download size
5. Accuracy (WER on English speech)
6. Node.js API ergonomics

- [ ] **Step 2: Evaluate approved local models for Agent SDK via Ollama**

Test candidate models for analysis quality when used as TomAgent's LLM provider:
- `qwen2.5:7b`, `llama3.2:8b`, `mistral:7b`, and any other strong local models
- Run each against sample transcripts, evaluate sentiment/analysis output quality
- Measure speed and memory footprint
- Document which models are approved for `tom setup` to offer

Criteria: analysis quality (must produce coherent sentiment + summaries), speed (< 10s for a paragraph), memory (< 8GB VRAM)

- [ ] **Step 3: Write evaluation documents**

Write STT findings to `docs/research/stt-evaluation.md` and local model findings to `docs/research/local-model-evaluation.md`.

- [ ] **Step 4: Commit**

```bash
git add docs/research/
git commit -m "docs: STT library and local model evaluation"
```

---

### Task 14: Research Spike — Database (SQLite vs PGlite)

**Files:**
- Create: `docs/research/database-evaluation.md`

This is a research task, not a code task.

- [ ] **Step 1: Evaluate SQLite + sqlite-vec vs PGlite**

Test both with:
1. Basic CRUD (entries table)
2. FTS (full-text search)
3. Vector storage + similarity search
4. Migration tooling
5. Bundle size impact
6. Node.js API ergonomics

- [ ] **Step 2: Write evaluation document to `docs/research/database-evaluation.md`**

- [ ] **Step 3: If PGlite wins, implement PGlite storage service**

Create `packages/core/src/services/storage-pglite.ts` implementing IStorageService. Must pass all existing storage contract tests from Task 6.

- [ ] **Step 4: Commit**

```bash
git add docs/research/
git commit -m "docs: database evaluation — SQLite vs PGlite for vector search"
```

---

## Sprint 2 — `tom record` (Weeks 3-4)

> **Note:** Tasks in this sprint depend on Sprint 1 research outcomes. STT library and database technology will be known. Code examples use placeholder names — substitute the chosen library.

### Task 15: Audio Capture Service

**Files:**
- Create: `packages/core/src/services/audio.ts`
- Test: `packages/core/src/services/__tests__/audio.test.ts`

- [ ] **Step 1: Write IAudioService interface + tests**

Interface methods:
- `startRecording(): void` — begins mic capture
- `stopRecording(): Promise<string>` — stops, saves WAV to `~/.tom/audio/{date}-{id}.wav`, returns file path
- `getAudioStream(): Readable` — returns live audio stream for chunked STT
- `isRecording(): boolean`

Test with mocked mic input.

- [ ] **Step 2: Implement audio capture**

Evaluate and use one of these Node.js audio libraries:
- `node-record-lpcm16` — cross-platform mic recording via SoX/arecord
- `mic` — simpler API, also uses SoX/arecord under the hood
- `portaudio` / `naudiodon` — native PortAudio bindings (lower-level, more control)

Key requirements: must work on macOS (CoreAudio) and Windows (WASAPI/DirectSound), output PCM/WAV format compatible with the chosen Whisper library.

- [ ] **Step 3: Run tests, verify pass**
- [ ] **Step 4: Commit**

---

### Task 16: Transcription Service Implementation

**Files:**
- Create: `packages/core/src/services/transcription.ts` (full implementation)
- Test: `packages/core/src/services/__tests__/transcription.test.ts`

- [ ] **Step 1: Write ITranscriptionService interface + tests**

Interface:
- `transcribeStream(audioStream: Readable, onChunk: (text: string) => void): Promise<string>` — chunked transcription, calls onChunk with partial results
- `transcribeFile(audioPath: string): Promise<string>` — batch fallback
- `isModelLoaded(): boolean`
- `loadModel(modelPath: string): Promise<void>`

- [ ] **Step 2: Implement with chosen STT library**
- [ ] **Step 3: Test with real audio fixture**
- [ ] **Step 4: Commit**

---

### Task 17: RecordingUI Component

**Files:**
- Create: `packages/cli/src/components/RecordingUI.tsx`

- [ ] **Step 1: Build Ink component**

Props: `transcript: string`, `duration: number`, `isRecording: boolean`

Renders: timer, live transcript text, controls hint (Esc to cancel, Enter to finish)

- [ ] **Step 2: Test with ink-testing-library**
- [ ] **Step 3: Commit**

---

### Task 18: SentimentDisplay Component

**Files:**
- Create: `packages/cli/src/components/SentimentDisplay.tsx`

- [ ] **Step 1: Build Ink component**

Renders: sentiment score (color-coded), label, summary, confidence

- [ ] **Step 2: Test with ink-testing-library**
- [ ] **Step 3: Commit**

---

### Task 19: `tom record` Command — Full Pipeline

**Files:**
- Create: `packages/cli/src/commands/record.tsx`
- Test: `packages/cli/src/commands/__tests__/record.test.ts`
- Modify: `packages/cli/src/cli.ts`

- [ ] **Step 1: Write record pipeline tests**

Test the orchestration logic (mock all services):
- Checks `ConfigManager.isSetupComplete()` before starting — if not setup, displays "Run `tom setup` first" and exits
- Checks `TranscriptionService.isModelLoaded()` — if not, displays "STT model not found. Run `tom setup`" and exits
- On successful recording: calls AudioService, TranscriptionService, then TomAgent + EmbeddingService in parallel, then StorageService
- When TomAgent is unavailable: saves entry without analysis, displays warning
- When EmbeddingService is unavailable: saves entry without embedding, displays warning
- When mic is unavailable: displays platform-specific error and exits

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement record command**

Wires together: setup guard → AudioService → TranscriptionService (streaming) → RecordingUI → TomAgent + EmbeddingService (parallel) → StorageService → SentimentDisplay

- [ ] **Step 4: Run tests to verify they pass**
- [ ] **Step 5: Register in cli.ts with help text**

```typescript
import { recordCommand } from './commands/record.js';
program.addCommand(recordCommand);
```

Ensure command has `.description('Record audio with live transcription and AI analysis')`.

- [ ] **Step 6: Manual end-to-end test**
- [ ] **Step 7: Commit**

---

## Sprint 3 — `tom note` + Search (Weeks 5-6)

### Task 20: `tom note` Command — Typed Input

**Files:**
- Create: `packages/cli/src/commands/note.tsx`
- Test: `packages/cli/src/commands/__tests__/note.test.ts`
- Modify: `packages/cli/src/cli.ts`

- [ ] **Step 1: Write note command tests**

Test:
- Checks `ConfigManager.isSetupComplete()` before starting — if not, shows "Run `tom setup` first"
- Submitting text triggers EmbeddingService + TomAgent in parallel, then StorageService
- When TomAgent unavailable: saves without analysis, warns
- When EmbeddingService unavailable: saves without embedding, warns
- Empty input is rejected

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Implement note with text input**

Ink text input. Submit → EmbeddingService + TomAgent (parallel) → StorageService.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Register in cli.ts with help text**

```typescript
import { noteCommand } from './commands/note.js';
program.addCommand(noteCommand);
```

Ensure `.description('Create a text note (type or dictate)')`.

- [ ] **Step 6: Commit**

---

### Task 21: `tom note` — Voice Dictation Mode

**Files:**
- Modify: `packages/cli/src/commands/note.tsx`
- Modify: `packages/cli/src/commands/__tests__/note.test.ts`

- [ ] **Step 1: Write dictation mode tests**

Test:
- Tab key toggles between typed and dictated mode
- In dictation mode: AudioService starts, TranscriptionService streams text
- No audio file is saved in dictation mode (inputMethod = 'dictated')
- Dictation mode requires STT model — if not loaded, shows warning and stays in typed mode
- Toggling back to typed stops audio capture

- [ ] **Step 2: Run tests to verify they fail**

- [ ] **Step 3: Add dictation toggle**

Key press (Tab) switches between typed and dictated. Dictation: AudioService captures, TranscriptionService streams to text (no audio saved). Text appears in input field.

- [ ] **Step 4: Run tests to verify they pass**
- [ ] **Step 5: Commit**

---

### Task 22: `tom search` Command

**Files:**
- Create: `packages/cli/src/commands/search.tsx`, `packages/cli/src/components/SearchResults.tsx`
- Modify: `packages/cli/src/cli.ts`

- [ ] **Step 1: Implement SearchResults component**

Renders entries list: timestamp, type, sentiment indicator, content excerpt. Selectable — arrow keys + Enter to view full entry.

- [ ] **Step 2: Implement search command**

Text input → SearchService.search() → SearchResults. On select, show full entry + analysis.

- [ ] **Step 3: Register in cli.ts with `.description('Search entries by meaning or keyword')`**
- [ ] **Step 4: End-to-end test**
- [ ] **Step 5: Commit**

---

## Sprint 4 — Polish + Distribution (Weeks 7-8)

### Task 23: Re-analyze Command

**Files:**
- Create: `packages/cli/src/commands/analyze.tsx` (or add as a subcommand)
- Modify: `packages/cli/src/cli.ts`

Per spec: "Analysis can be retried later" when the LLM was unavailable at capture time.

- [ ] **Step 1: Write tests**

Test that:
- `tom analyze <entry-id>` retrieves the entry, runs TomAgent + EmbeddingService, updates the entry
- Entries that already have analysis get re-analyzed (overwritten)
- Non-existent entry ID shows error
- LLM unavailable shows error (this is an explicit user action, not silent degradation)

- [ ] **Step 2: Implement analyze command**
- [ ] **Step 3: Register in cli.ts with `.description('Re-run AI analysis on an entry')`**
- [ ] **Step 4: Commit**

---

### Task 25: Error Handling & Failure Modes

**Files:**
- Modify: all command files, service files as needed

- [ ] **Step 1: Implement all failure modes from spec**

Per Failure Modes table: mic unavailable, STT missing, Ollama down, Claude invalid, fully offline, DB corrupted.

- [ ] **Step 2: Test each failure mode**
- [ ] **Step 3: Commit**

---

### Task 26: Cross-Platform Testing

- [ ] **Step 1: Test on macOS**
- [ ] **Step 2: Test on Windows**

Verify all commands, directory creation, audio files.

- [ ] **Step 3: Fix platform-specific issues**
- [ ] **Step 4: Commit**

---

### Task 27: Distribution Setup

**Files:**
- Modify: `packages/cli/package.json`

- [ ] **Step 1: Evaluate distribution options** (Node.js SEA, @yao-pkg/pkg, npm global, Homebrew)
- [ ] **Step 2: Implement chosen method**
- [ ] **Step 3: Test install on clean machine**
- [ ] **Step 4: Commit**

---

### Task 28: Documentation & Coverage

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write README** (overview, install, commands, configuration)

- [ ] **Step 2: Run coverage report**

```bash
pnpm vitest run --coverage
```

Target: 80% on core package.

- [ ] **Step 3: Fill coverage gaps**
- [ ] **Step 4: Commit**

---

## Verification Checklist

Before declaring MVP complete:

- [ ] `tom setup` — configures LLM, embedding, STT, creates `~/.tom/`
- [ ] `tom record` — captures audio, shows live transcript, analyzes, saves entry + audio file
- [ ] `tom note` — accepts typed text, analyzes, saves entry
- [ ] `tom note` (dictation) — speech-to-text input, no audio saved
- [ ] `tom search` — semantic search returns relevant entries (or FTS fallback)
- [ ] `tom analyze <id>` — re-runs analysis on an entry that was saved without it
- [ ] Failure modes — graceful degradation when services unavailable
- [ ] Setup guard — commands that need STT/LLM refuse to start if setup incomplete
- [ ] macOS — all commands work
- [ ] Windows — all commands work
- [ ] Test coverage — 80%+ on core package
- [ ] `tom --help` — shows all commands with descriptions
