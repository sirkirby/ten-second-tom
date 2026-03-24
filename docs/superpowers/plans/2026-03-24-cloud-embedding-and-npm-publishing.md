# Cloud Embedding & NPM Publishing — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add working cloud embedding via OpenAI-compatible API (OpenRouter default) and publish both packages to npm with OIDC trusted publishing.

**Architecture:** Two independent features. Feature 1 (cloud embedding) adds a new `OpenAICompatibleEmbeddingService` class, updates the config schema to replace the `cloud` discriminant with `openrouter` and `custom`, and wires it through the service factory and setup wizard. Feature 2 (npm publishing) renames packages from scoped `@ten-second-tom/*` to flat `ten-second-tom`/`ten-second-tom-core`, adds two GitHub Actions workflows with per-package tagging (`cli/v*`, `core/v*`), and a version sync step back to `v2-rewrite`.

**Tech Stack:** TypeScript, Zod, Vitest, pnpm workspaces, GitHub Actions (OIDC/provenance), OpenAI-compatible `/v1/embeddings` API

**Spec:** `docs/superpowers/specs/2026-03-24-cloud-embedding-and-npm-publishing-design.md`

---

## File Map

### Feature 1: Cloud Embedding

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `packages/core/src/types/config.ts` | Replace `cloud` variant with `openrouter` and `custom` |
| Modify | `packages/core/src/constants.ts` | Add OpenRouter constants, OpenAI model dimensions, remove `DEFAULT_CLOUD_EMBEDDING_MODEL` |
| Modify | `packages/core/src/services/embedding.ts` | Add `OpenAICompatibleEmbeddingService` class |
| Modify | `packages/core/src/services/service-factory.ts` | Wire `openrouter` and `custom` providers |
| Modify | `packages/core/src/index.ts` | Export new class and config type |
| Modify | `packages/cli/src/commands/setup.tsx` | Update embedding wizard (new providers, API key input, model input) |
| Modify | `packages/core/src/types/__tests__/config.test.ts` | Update schema tests |
| Modify | `packages/core/src/services/__tests__/embedding.test.ts` | Add tests for new service |
| Modify | `packages/cli/src/commands/__tests__/setup.test.ts` | Update constant references |

### Feature 2: NPM Publishing

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `packages/core/package.json` | Rename to `ten-second-tom-core` |
| Modify | `packages/cli/package.json` | Rename to `ten-second-tom`, update dependency |
| Modify | 16 files in `packages/cli/src/` | Replace import `@ten-second-tom/core` → `ten-second-tom-core` |
| Create | `.github/workflows/publish-core.yml` | Core package publish workflow |
| Create | `.github/workflows/publish-cli.yml` | CLI package publish workflow |

---

## Task 1: Update Config Schema (core)

**Files:**
- Modify: `packages/core/src/types/config.ts`
- Modify: `packages/core/src/types/__tests__/config.test.ts`

- [ ] **Step 1: Write failing tests for new schema variants**

Add tests for `openrouter` and `custom` embedding providers, and verify `cloud` is rejected:

```typescript
// Add to packages/core/src/types/__tests__/config.test.ts

it('validates openrouter embedding config', () => {
  const config = {
    llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
    stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
    embedding: { provider: 'openrouter' as const, model: 'openai/text-embedding-3-small', apiKey: 'sk-or-test' },
    storage: { dbPath: '/Users/test/.tom/tom.db' },
  };
  const result = AppConfigSchema.safeParse(config);
  expect(result.success).toBe(true);
});

it('validates custom embedding config', () => {
  const config = {
    llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
    stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
    embedding: { provider: 'custom' as const, model: 'bge-m3', endpoint: 'http://localhost:8080' },
    storage: { dbPath: '/Users/test/.tom/tom.db' },
  };
  const result = AppConfigSchema.safeParse(config);
  expect(result.success).toBe(true);
});

it('rejects openrouter embedding without apiKey', () => {
  const config = {
    llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
    stt: { engine: 'whisper', modelPath: '/tmp/model' },
    embedding: { provider: 'openrouter', model: 'openai/text-embedding-3-small' },
    storage: { dbPath: '/tmp/tom.db' },
  };
  const result = AppConfigSchema.safeParse(config);
  expect(result.success).toBe(false);
});

it('rejects custom embedding without endpoint', () => {
  const config = {
    llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
    stt: { engine: 'whisper', modelPath: '/tmp/model' },
    embedding: { provider: 'custom', model: 'bge-m3' },
    storage: { dbPath: '/tmp/tom.db' },
  };
  const result = AppConfigSchema.safeParse(config);
  expect(result.success).toBe(false);
});

it('rejects removed cloud embedding provider', () => {
  const config = {
    llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
    stt: { engine: 'whisper', modelPath: '/tmp/model' },
    embedding: { provider: 'cloud', model: 'voyage-3-lite' },
    storage: { dbPath: '/tmp/tom.db' },
  };
  const result = AppConfigSchema.safeParse(config);
  expect(result.success).toBe(false);
});
```

Also update the existing `'validates a cloud config'` test to use `openrouter` instead of `cloud` for embedding:

```typescript
it('validates a cloud config', () => {
  const config = {
    llm: { provider: 'cloud' as const, apiKey: 'sk-ant-test-key' },
    stt: { engine: 'whisper-distil-en', modelPath: '/Users/test/.tom/models/whisper-distil-en' },
    embedding: { provider: 'openrouter' as const, model: 'openai/text-embedding-3-small', apiKey: 'sk-or-test' },
    storage: { dbPath: '/Users/test/.tom/tom.db' },
  };
  const result = AppConfigSchema.safeParse(config);
  expect(result.success).toBe(true);
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm vitest run packages/core/src/types/__tests__/config.test.ts`
Expected: New tests fail (schema doesn't have `openrouter`/`custom` yet), updated test fails (still expects `cloud`)

- [ ] **Step 3: Update the config schema**

In `packages/core/src/types/config.ts`, replace the `cloud` variant:

```typescript
export const EmbeddingConfigSchema = z.discriminatedUnion('provider', [
  z.object({
    provider: z.literal('ollama'),
    model: z.string().min(1),
    endpoint: z.string().url(),
  }),
  z.object({
    provider: z.literal('openrouter'),
    model: z.string().min(1),
    apiKey: z.string().min(1),
  }),
  z.object({
    provider: z.literal('custom'),
    model: z.string().min(1),
    endpoint: z.string().url(),
  }),
  z.object({
    provider: z.literal('none'),
    model: z.literal(''),
  }),
]);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm vitest run packages/core/src/types/__tests__/config.test.ts`
Expected: All tests pass

- [ ] **Step 5: Commit**

```bash
git add packages/core/src/types/config.ts packages/core/src/types/__tests__/config.test.ts
git commit -m "feat: replace cloud embedding config with openrouter and custom providers"
```

---

## Task 2: Update Constants (core)

**Files:**
- Modify: `packages/core/src/constants.ts`

- [ ] **Step 1: Update constants**

In `packages/core/src/constants.ts`:

1. Remove `DEFAULT_CLOUD_EMBEDDING_MODEL`
2. Add OpenRouter constants
3. Add OpenAI embedding model dimensions

```typescript
// Replace line 9:
// export const DEFAULT_CLOUD_EMBEDDING_MODEL = 'voyage-3-lite';
// With:
export const OPENROUTER_BASE_URL = 'https://openrouter.ai/api/v1';
export const DEFAULT_OPENROUTER_EMBEDDING_MODEL = 'openai/text-embedding-3-small';
```

Add to `EMBEDDING_MODEL_DIMENSIONS`:

```typescript
  'openai/text-embedding-3-small': 1536,
  'openai/text-embedding-3-large': 3072,
  'openai/text-embedding-ada-002': 1536,
```

- [ ] **Step 2: Run existing tests to verify nothing breaks**

Run: `pnpm vitest run packages/core/`
Expected: All pass (no tests directly reference `DEFAULT_CLOUD_EMBEDDING_MODEL` in core)

- [ ] **Step 3: Commit**

```bash
git add packages/core/src/constants.ts
git commit -m "feat: add OpenRouter constants and OpenAI embedding dimensions"
```

---

## Task 3: Implement OpenAICompatibleEmbeddingService (core)

**Files:**
- Modify: `packages/core/src/services/embedding.ts`
- Modify: `packages/core/src/services/__tests__/embedding.test.ts`

- [ ] **Step 1: Write failing tests for the new service**

Add to `packages/core/src/services/__tests__/embedding.test.ts`:

```typescript
import { OllamaEmbeddingService, NoopEmbeddingService, OpenAICompatibleEmbeddingService } from '../embedding.js';

// ... existing tests ...

describe('OpenAICompatibleEmbeddingService', () => {
  it('generates embeddings via OpenAI-compatible API', async () => {
    const embeddingValues = Array.from({ length: 1536 }, (_, i) => (i + 1) / 10000);
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: [{ embedding: embeddingValues, index: 0 }],
        model: 'openai/text-embedding-3-small',
        usage: { prompt_tokens: 5, total_tokens: 5 },
      }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'sk-or-test-key',
    });

    const result = await service.embed('Hello, world!');

    expect(result).toBeInstanceOf(Float32Array);
    expect(result.length).toBe(1536);
    expect(mockFetch).toHaveBeenCalledWith('https://openrouter.ai/api/v1/embeddings', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer sk-or-test-key',
      },
      body: JSON.stringify({ input: 'Hello, world!', model: 'openai/text-embedding-3-small' }),
    });
  });

  it('omits Authorization header when no apiKey provided', async () => {
    const embeddingValues = Array.from({ length: 384 }, (_, i) => (i + 1) / 10000);
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: [{ embedding: embeddingValues, index: 0 }],
      }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'http://localhost:8080/v1',
      model: 'all-minilm',
    });

    await service.embed('test');

    expect(mockFetch).toHaveBeenCalledWith('http://localhost:8080/v1/embeddings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ input: 'test', model: 'all-minilm' }),
    });
  });

  it('throws when the API returns a non-ok response', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'bad-key',
    });

    await expect(service.embed('Hello')).rejects.toThrow(
      'Embedding request failed: 401 Unauthorized',
    );
  });

  it('throws when the network request fails', async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error('Network error'));
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'sk-or-test',
    });

    await expect(service.embed('Hello')).rejects.toThrow('Network error');
  });

  it('checks availability with a minimal embed request', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: [{ embedding: [0.1, 0.2, 0.3], index: 0 }],
      }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'sk-or-test',
    });

    const available = await service.isAvailable();
    expect(available).toBe(true);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it('reports unavailable when availability check fails', async () => {
    const mockFetch = vi.fn().mockRejectedValue(new Error('Connection refused'));
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'sk-or-test',
    });

    const available = await service.isAvailable();
    expect(available).toBe(false);
  });

  it('caches availability result for subsequent calls', async () => {
    const mockFetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        data: [{ embedding: [0.1], index: 0 }],
      }),
    });
    vi.stubGlobal('fetch', mockFetch);

    const service = new OpenAICompatibleEmbeddingService({
      baseUrl: 'https://openrouter.ai/api/v1',
      model: 'openai/text-embedding-3-small',
      apiKey: 'sk-or-test',
    });

    await service.isAvailable();
    await service.isAvailable();
    // Only one fetch call — second returns cached
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `pnpm vitest run packages/core/src/services/__tests__/embedding.test.ts`
Expected: Fails — `OpenAICompatibleEmbeddingService` not found in import

- [ ] **Step 3: Implement the service class**

Add to `packages/core/src/services/embedding.ts`:

```typescript
export interface OpenAICompatibleEmbeddingConfig {
  baseUrl: string;
  model: string;
  apiKey?: string;
}

export class OpenAICompatibleEmbeddingService implements IEmbeddingService {
  private readonly baseUrl: string;
  private readonly model: string;
  private readonly apiKey?: string;

  private availabilityCache: [boolean, number] | null = null;

  constructor({ baseUrl, model, apiKey }: OpenAICompatibleEmbeddingConfig) {
    this.baseUrl = baseUrl.replace(/\/+$/, '');
    this.model = model;
    this.apiKey = apiKey;
  }

  async embed(text: string): Promise<Float32Array> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (this.apiKey) {
      headers['Authorization'] = `Bearer ${this.apiKey}`;
    }

    const response = await fetch(`${this.baseUrl}/embeddings`, {
      method: 'POST',
      headers,
      body: JSON.stringify({ input: text, model: this.model }),
    });
    if (!response.ok) {
      throw new Error(`Embedding request failed: ${response.status} ${response.statusText}`);
    }
    const data = (await response.json()) as {
      data: Array<{ embedding: number[]; index: number }>;
    };
    return new Float32Array(data.data[0].embedding);
  }

  async isAvailable(): Promise<boolean> {
    if (this.availabilityCache !== null && Date.now() < this.availabilityCache[1]) {
      return this.availabilityCache[0];
    }

    try {
      const headers: Record<string, string> = {
        'Content-Type': 'application/json',
      };
      if (this.apiKey) {
        headers['Authorization'] = `Bearer ${this.apiKey}`;
      }

      const response = await fetch(`${this.baseUrl}/embeddings`, {
        method: 'POST',
        headers,
        body: JSON.stringify({ input: 'test', model: this.model }),
        signal: AbortSignal.timeout(EMBEDDING_AVAILABILITY_TIMEOUT_MS),
      });
      const available = response.ok;
      this.availabilityCache = [available, Date.now() + EMBEDDING_AVAILABILITY_CACHE_MS];
      return available;
    } catch {
      this.availabilityCache = [false, Date.now() + EMBEDDING_AVAILABILITY_CACHE_MS];
      return false;
    }
  }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `pnpm vitest run packages/core/src/services/__tests__/embedding.test.ts`
Expected: All tests pass (existing Ollama + Noop + new OpenAICompatible)

- [ ] **Step 5: Commit**

```bash
git add packages/core/src/services/embedding.ts packages/core/src/services/__tests__/embedding.test.ts
git commit -m "feat: add OpenAICompatibleEmbeddingService for cloud and custom local embeddings"
```

---

## Task 4: Wire Service Factory and Exports (core)

**Files:**
- Modify: `packages/core/src/services/service-factory.ts`
- Modify: `packages/core/src/index.ts`

- [ ] **Step 1: Update service factory**

In `packages/core/src/services/service-factory.ts`:

1. Add import for `OpenAICompatibleEmbeddingService`
2. Add import for `OPENROUTER_BASE_URL`
3. Replace the embedding construction block (lines 54-63):

```typescript
import { OllamaEmbeddingService, NoopEmbeddingService, OpenAICompatibleEmbeddingService } from './embedding.js';
import { getEmbeddingDimension, OPENROUTER_BASE_URL } from '../constants.js';
```

```typescript
  const embedding =
    config.embedding.provider === 'ollama'
      ? new OllamaEmbeddingService({
          model: config.embedding.model,
          endpoint: config.embedding.endpoint,
        })
      : config.embedding.provider === 'openrouter'
        ? new OpenAICompatibleEmbeddingService({
            baseUrl: OPENROUTER_BASE_URL,
            model: config.embedding.model,
            apiKey: config.embedding.apiKey,
          })
        : config.embedding.provider === 'custom'
          ? new OpenAICompatibleEmbeddingService({
              baseUrl: config.embedding.endpoint,
              model: config.embedding.model,
            })
          : new NoopEmbeddingService();
```

- [ ] **Step 2: Update barrel export**

In `packages/core/src/index.ts`, update the embedding export to include the new class and config type:

```typescript
export {
  type IEmbeddingService,
  OllamaEmbeddingService,
  OpenAICompatibleEmbeddingService,
  NoopEmbeddingService,
  type OllamaEmbeddingConfig,
  type OpenAICompatibleEmbeddingConfig,
} from './services/embedding.js';
```

- [ ] **Step 3: Run full core test suite**

Run: `pnpm vitest run packages/core/`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add packages/core/src/services/service-factory.ts packages/core/src/index.ts
git commit -m "feat: wire OpenAICompatibleEmbeddingService into service factory"
```

---

## Task 5: Update Setup Wizard (cli)

**Files:**
- Modify: `packages/cli/src/commands/setup.tsx`
- Modify: `packages/cli/src/commands/__tests__/setup.test.ts`

- [ ] **Step 1: Update the embedding provider items**

Replace the `embeddingProviderItems` array (line 172-176) with:

```typescript
const embeddingProviderItems = [
  { label: 'OpenRouter (cloud, recommended)', value: 'openrouter' as const },
  { label: 'Custom local (LM Studio, llama.cpp)', value: 'custom' as const },
  { label: 'Ollama (local)', value: 'ollama' as const },
  { label: 'None (keyword search only)', value: 'none' as const },
];
```

- [ ] **Step 2: Update WizardState type**

Change `embeddingProvider` type (line 61):

```typescript
embeddingProvider: 'ollama' | 'openrouter' | 'custom' | 'none' | null;
```

Add new state fields after `embeddingModel` (around line 62):

```typescript
embeddingApiKey: string;
embeddingEndpoint: string;
```

- [ ] **Step 3: Update `deriveInitialState` with initial values for new fields**

The `deriveInitialState` function (around line 296 in setup.tsx) returns a `WizardState` object. Add initial values for the new fields in both the "no existing config" and "existing config" return paths:

```typescript
embeddingApiKey: '',
embeddingEndpoint: '',
```

For the "existing config" path, pre-fill from existing config when available:

```typescript
embeddingApiKey: existing.embedding.provider === 'openrouter' ? existing.embedding.apiKey : '',
embeddingEndpoint: existing.embedding.provider === 'custom' ? existing.embedding.endpoint : '',
```

- [ ] **Step 4: Add new steps to the Step type**

Add to the `Step` type (around line 39):

```typescript
| 'embedding-openrouter-key'
| 'embedding-openrouter-model'
| 'embedding-custom-endpoint'
| 'embedding-custom-model'
```

- [ ] **Step 5: Update the embedding provider handler**

Replace the `handleEmbeddingProviderSelect` function (lines 597-604):

```typescript
function handleEmbeddingProviderSelect(item: { value: 'ollama' | 'openrouter' | 'custom' | 'none' }) {
  setState((s) => ({ ...s, embeddingProvider: item.value }));
  if (item.value === 'ollama') {
    setStep('embedding-model-loading');
  } else if (item.value === 'openrouter') {
    setStep('embedding-openrouter-key');
  } else if (item.value === 'custom') {
    setStep('embedding-custom-endpoint');
  } else {
    setStep('whisper-model');
  }
}
```

- [ ] **Step 6: Add state and handlers for OpenRouter flow**

Add state for the OpenRouter API key text input and model input. Add handlers:

```typescript
function handleOpenRouterKeySubmit(value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) return;
  setState((s) => ({ ...s, embeddingApiKey: trimmed }));
  setStep('embedding-openrouter-model');
}

function handleOpenRouterModelSubmit(value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) return;
  setState((s) => ({ ...s, embeddingModel: trimmed }));
  setStep('whisper-model');
}
```

- [ ] **Step 7: Add state and handlers for Custom Local flow**

```typescript
function handleCustomEndpointSubmit(value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) return;
  setState((s) => ({ ...s, embeddingEndpoint: trimmed }));
  setStep('embedding-custom-model');
}

function handleCustomModelSubmit(value: string) {
  const trimmed = value.trim();
  if (trimmed.length === 0) return;
  setState((s) => ({ ...s, embeddingModel: trimmed }));
  setStep('whisper-model');
}
```

- [ ] **Step 8: Add JSX for new steps**

Add render blocks for the new steps in the return JSX, after the existing embedding steps and before the whisper steps:

```tsx
{step === 'embedding-openrouter-key' && (
  <Box flexDirection="column">
    <Text>Step 2 of {TOTAL_STEPS}: Enter your OpenRouter API key</Text>
    <Text dimColor>Get one at https://openrouter.ai/keys</Text>
    <Box marginTop={1}>
      <Text>API key: </Text>
      <TextInput value={state.embeddingApiKey} onChange={(v) => setState((s) => ({ ...s, embeddingApiKey: v }))} onSubmit={handleOpenRouterKeySubmit} mask="*" />
    </Box>
  </Box>
)}

{step === 'embedding-openrouter-model' && (
  <Box flexDirection="column">
    <Text>Step 2 of {TOTAL_STEPS}: Enter embedding model</Text>
    <Text dimColor>Default: {DEFAULT_OPENROUTER_EMBEDDING_MODEL}</Text>
    <Box marginTop={1}>
      <Text>Model: </Text>
      <TextInput
        value={state.embeddingModel || DEFAULT_OPENROUTER_EMBEDDING_MODEL}
        onChange={(v) => setState((s) => ({ ...s, embeddingModel: v }))}
        onSubmit={handleOpenRouterModelSubmit}
      />
    </Box>
  </Box>
)}

{step === 'embedding-custom-endpoint' && (
  <Box flexDirection="column">
    <Text>Step 2 of {TOTAL_STEPS}: Enter embedding server URL</Text>
    <Text dimColor>e.g., http://localhost:1234/v1</Text>
    <Box marginTop={1}>
      <Text>Endpoint: </Text>
      <TextInput value={state.embeddingEndpoint} onChange={(v) => setState((s) => ({ ...s, embeddingEndpoint: v }))} onSubmit={handleCustomEndpointSubmit} />
    </Box>
  </Box>
)}

{step === 'embedding-custom-model' && (
  <Box flexDirection="column">
    <Text>Step 2 of {TOTAL_STEPS}: Enter embedding model name</Text>
    <Box marginTop={1}>
      <Text>Model: </Text>
      <TextInput value={state.embeddingModel} onChange={(v) => setState((s) => ({ ...s, embeddingModel: v }))} onSubmit={handleCustomModelSubmit} />
    </Box>
  </Box>
)}
```

- [ ] **Step 9: Update the save handler**

Replace the embedding config construction in `handleSave` (lines 827-836):

```typescript
const embedding: EmbeddingConfig =
  state.embeddingProvider === 'ollama'
    ? {
        provider: 'ollama',
        model: state.embeddingModel,
        endpoint: ollamaEndpoint,
      }
    : state.embeddingProvider === 'openrouter'
      ? {
          provider: 'openrouter',
          model: state.embeddingModel || DEFAULT_OPENROUTER_EMBEDDING_MODEL,
          apiKey: state.embeddingApiKey,
        }
      : state.embeddingProvider === 'custom'
        ? {
            provider: 'custom',
            model: state.embeddingModel,
            endpoint: state.embeddingEndpoint,
          }
        : { provider: 'none', model: '' };
```

- [ ] **Step 10: Update imports**

In setup.tsx, replace the `DEFAULT_CLOUD_EMBEDDING_MODEL` import with `DEFAULT_OPENROUTER_EMBEDDING_MODEL`:

```typescript
import {
  DEFAULT_OLLAMA_ENDPOINT,
  DEFAULT_LOCAL_MODEL_ID,
  DEFAULT_OLLAMA_EMBEDDING_MODEL,
  DEFAULT_OPENROUTER_EMBEDDING_MODEL,
  ANTHROPIC_API_KEY_PREFIX,
  WHISPER_MODELS,
  getDefaultWhisperModel,
  SHERPA_MODELS,
} from '@ten-second-tom/core';
```

- [ ] **Step 11: Update setup test mock**

In `packages/cli/src/commands/__tests__/setup.test.ts`, update the mock (line 34):

```typescript
// Replace:
DEFAULT_CLOUD_EMBEDDING_MODEL: 'voyage-3-lite',
// With:
DEFAULT_OPENROUTER_EMBEDDING_MODEL: 'openai/text-embedding-3-small',
```

- [ ] **Step 12: Run tests**

Run: `pnpm vitest run`
Expected: All pass

- [ ] **Step 13: Build and verify**

Run: `pnpm -r build`
Expected: Clean build, no errors

- [ ] **Step 14: Commit**

```bash
git add packages/cli/src/commands/setup.tsx packages/cli/src/commands/__tests__/setup.test.ts
git commit -m "feat: update setup wizard with openrouter and custom local embedding providers"
```

---

## Task 6: Rename Packages (publishing prep)

**Files:**
- Modify: `packages/core/package.json`
- Modify: `packages/cli/package.json`
- Modify: 18 files in `packages/cli/src/` (import paths)

- [ ] **Step 1: Rename core package**

In `packages/core/package.json`, change line 2:

```json
"name": "ten-second-tom-core",
```

- [ ] **Step 2: Rename CLI package and update dependency**

In `packages/cli/package.json`:

```json
"name": "ten-second-tom",
```

And in dependencies, replace:

```json
"@ten-second-tom/core": "workspace:*",
```

with:

```json
"ten-second-tom-core": "workspace:*",
```

- [ ] **Step 3: Replace all imports across CLI source files**

Find and replace `@ten-second-tom/core` → `ten-second-tom-core` in all these files:

- `packages/cli/src/app.tsx` (2 imports)
- `packages/cli/src/pipeline.ts` (1 import)
- `packages/cli/src/pipeline.test.ts` (2 references — mock path and import)
- `packages/cli/src/reindex.tsx` (1 import)
- `packages/cli/src/commands/registry.tsx` (1 import)
- `packages/cli/src/commands/setup.tsx` (3 imports)
- `packages/cli/src/commands/__tests__/setup.test.ts` (1 mock path)
- `packages/cli/src/components/SentimentDisplay.tsx` (1 import)
- `packages/cli/src/components/ResultsSummary.tsx` (1 import)
- `packages/cli/src/hooks/useSetupGuard.ts` (2 imports)
- `packages/cli/src/screens/HomeScreen.tsx` (1 import)
- `packages/cli/src/screens/RecordingScreen.tsx` (2 imports)
- `packages/cli/src/screens/ProcessingScreen.tsx` (2 imports)
- `packages/cli/src/screens/NoteScreen.tsx` (2 imports)
- `packages/cli/src/screens/SearchScreen.tsx` (1 import)
- `packages/cli/src/screens/ListScreen.tsx` (1 import)

- [ ] **Step 4: Reinstall dependencies**

Run: `pnpm install`
Expected: Lockfile updates, workspace link resolves to new package name

- [ ] **Step 5: Build and test**

Run: `pnpm -r build && pnpm vitest run`
Expected: Clean build, all tests pass

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "chore: rename packages for npm publishing (ten-second-tom, ten-second-tom-core)"
```

---

## Task 7: Create publish-core.yml Workflow

**Files:**
- Create: `.github/workflows/publish-core.yml`

- [ ] **Step 1: Create the workflow file**

Reference pattern: `~/Repos/unifi-mcp/.github/workflows/publish-relay.yml`

```yaml
name: "Core: Publish to npm"

on:
  push:
    tags:
      - 'core/v[0-9]+.[0-9]+.[0-9]+'

permissions:
  contents: write
  id-token: write

jobs:
  build-test-publish:
    name: Build, test, and publish
    runs-on: ubuntu-latest
    environment:
      name: npm-publish
      url: https://www.npmjs.com/package/ten-second-tom-core

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          fetch-tags: true

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 22
          registry-url: https://registry.npmjs.org

      - name: Setup pnpm
        uses: pnpm/action-setup@v4

      - name: Install dependencies
        run: pnpm install

      - name: Extract version from tag
        id: version
        run: |
          TAG="${GITHUB_REF#refs/tags/}"
          VERSION="${TAG#core/v}"
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"
          echo "Publishing ten-second-tom-core@$VERSION"

      - name: Set package version
        run: |
          cd packages/core
          npm version "${{ steps.version.outputs.version }}" --no-git-tag-version --allow-same-version

      - name: Build
        run: pnpm -r build

      - name: Test
        run: pnpm vitest run

      - name: Update npm for OIDC support
        run: npm install -g npm@latest

      - name: Publish to npm
        run: |
          cd packages/core
          pnpm publish --provenance --access public --no-git-checks

      - name: Create GitHub release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          TAG="${GITHUB_REF#refs/tags/}"
          VERSION="${TAG#core/v}"

          PREV_TAG=$(git tag --list "core/v*" --sort=-version:refname | sed -n '2p')

          NOTES_START_FLAG=""
          if [ -n "$PREV_TAG" ]; then
            NOTES_START_FLAG="--notes-start-tag $PREV_TAG"
          fi

          gh release create "$TAG" \
            --title "ten-second-tom-core v${VERSION}" \
            --generate-notes \
            $NOTES_START_FLAG \
            --notes "Install or upgrade:
          \`\`\`bash
          npm install ten-second-tom-core@${VERSION}
          \`\`\`" \
            --latest=false \
            --verify-tag

  sync-version:
    name: Sync version to branch
    runs-on: ubuntu-latest
    needs: build-test-publish
    steps:
      - name: Checkout branch
        uses: actions/checkout@v4
        with:
          ref: v2-rewrite
          token: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract version from tag
        id: version
        run: |
          TAG="${GITHUB_REF#refs/tags/}"
          VERSION="${TAG#core/v}"
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"

      - name: Update package.json version
        run: |
          cd packages/core
          npm version "${{ steps.version.outputs.version }}" --no-git-tag-version --allow-same-version

      - name: Commit version bump
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add packages/core/package.json
          git diff --cached --quiet && exit 0
          git commit -m "chore: sync core version to ${{ steps.version.outputs.version }} [skip ci]"
          git push
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/publish-core.yml
git commit -m "ci: add publish workflow for ten-second-tom-core package"
```

---

## Task 8: Create publish-cli.yml Workflow

**Files:**
- Create: `.github/workflows/publish-cli.yml`

- [ ] **Step 1: Create the workflow file**

```yaml
name: "CLI: Publish to npm"

on:
  push:
    tags:
      - 'cli/v[0-9]+.[0-9]+.[0-9]+'

permissions:
  contents: write
  id-token: write

jobs:
  build-test-publish:
    name: Build, test, and publish
    runs-on: ubuntu-latest
    environment:
      name: npm-publish
      url: https://www.npmjs.com/package/ten-second-tom

    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          fetch-tags: true

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 22
          registry-url: https://registry.npmjs.org

      - name: Setup pnpm
        uses: pnpm/action-setup@v4

      - name: Install dependencies
        run: pnpm install

      - name: Extract version from tag
        id: version
        run: |
          TAG="${GITHUB_REF#refs/tags/}"
          VERSION="${TAG#cli/v}"
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"
          echo "Publishing ten-second-tom@$VERSION"

      - name: Set package version
        run: |
          cd packages/cli
          npm version "${{ steps.version.outputs.version }}" --no-git-tag-version --allow-same-version

      - name: Build
        run: pnpm -r build

      - name: Test
        run: pnpm vitest run

      - name: Update npm for OIDC support
        run: npm install -g npm@latest

      - name: Publish to npm
        run: |
          cd packages/cli
          pnpm publish --provenance --access public --no-git-checks

      - name: Create GitHub release
        env:
          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: |
          TAG="${GITHUB_REF#refs/tags/}"
          VERSION="${TAG#cli/v}"

          PREV_TAG=$(git tag --list "cli/v*" --sort=-version:refname | sed -n '2p')

          NOTES_START_FLAG=""
          if [ -n "$PREV_TAG" ]; then
            NOTES_START_FLAG="--notes-start-tag $PREV_TAG"
          fi

          gh release create "$TAG" \
            --title "Ten-Second Tom v${VERSION}" \
            --generate-notes \
            $NOTES_START_FLAG \
            --notes "Install or upgrade:
          \`\`\`bash
          npm install -g ten-second-tom@${VERSION}
          tom setup
          \`\`\`" \
            --latest=true \
            --verify-tag

  sync-version:
    name: Sync version to branch
    runs-on: ubuntu-latest
    needs: build-test-publish
    steps:
      - name: Checkout branch
        uses: actions/checkout@v4
        with:
          ref: v2-rewrite
          token: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract version from tag
        id: version
        run: |
          TAG="${GITHUB_REF#refs/tags/}"
          VERSION="${TAG#cli/v}"
          echo "version=$VERSION" >> "$GITHUB_OUTPUT"

      - name: Update package.json version
        run: |
          cd packages/cli
          npm version "${{ steps.version.outputs.version }}" --no-git-tag-version --allow-same-version

      - name: Commit version bump
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"
          git add packages/cli/package.json
          git diff --cached --quiet && exit 0
          git commit -m "chore: sync cli version to ${{ steps.version.outputs.version }} [skip ci]"
          git push
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/publish-cli.yml
git commit -m "ci: add publish workflow for ten-second-tom CLI package"
```

---

## Task 9: Final Verification

- [ ] **Step 1: Run full build and test suite**

Run: `pnpm -r build && pnpm vitest run`
Expected: Clean build, all tests pass

- [ ] **Step 2: Run lint and format check**

Run: `make check`
Expected: All checks pass (lint + format + tests)

- [ ] **Step 3: Verify the CLI runs end-to-end**

Run: `make tom` (launches REPL mode)
Expected: App launches, home screen shows, `/help` lists commands, `/quit` exits cleanly

- [ ] **Step 4: Verify setup wizard shows new embedding options**

Run: `make setup`
Expected: Embedding provider step shows: OpenRouter, Custom local, Ollama, None
