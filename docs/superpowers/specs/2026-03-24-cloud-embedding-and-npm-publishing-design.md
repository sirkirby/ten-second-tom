# Cloud Embedding & NPM Publishing — Design Spec

**Date:** 2026-03-24
**Status:** Approved
**Decision Authority:** Chris Kirby

---

## Overview

Two Sprint 4 features that complete the v2 MVP for distribution:

1. **Cloud Embedding Provider** — Replace the stubbed `cloud` embedding option with a working OpenAI-compatible embedding service, defaulting to OpenRouter as the cloud provider.
2. **NPM Publishing** — Package and publish `ten-second-tom` (CLI) and `ten-second-tom-core` (library) to npm with OIDC trusted publishing, per-package tagging, and GitHub Releases.

---

## Feature 1: Cloud Embedding Provider

### Problem

The setup wizard offers a "Cloud (Voyage AI)" embedding option, but selecting it falls through to `NoopEmbeddingService`. Users who don't run Ollama locally have no way to get semantic search — they're stuck on FTS5 keyword fallback.

### Solution

Implement an OpenAI-compatible embedding service that works with any provider exposing the standard `/v1/embeddings` endpoint. Offer OpenRouter as the default cloud provider (one API key covers OpenAI, Voyage, and others). Add a "custom local" option for self-hosted servers (LM Studio, llama.cpp) that use the same protocol without requiring an API key.

### Config Schema

Replace the current `cloud` variant in `EmbeddingConfigSchema` with two new providers:

```typescript
EmbeddingConfigSchema = z.discriminatedUnion('provider', [
  // Existing — unchanged
  z.object({
    provider: z.literal('ollama'),
    model: z.string().min(1),
    endpoint: z.string().url(),
  }),
  // NEW: OpenRouter (known endpoint, API key required)
  z.object({
    provider: z.literal('openrouter'),
    model: z.string().min(1),
    apiKey: z.string().min(1),
  }),
  // NEW: Custom local (user-provided endpoint, no API key)
  z.object({
    provider: z.literal('custom'),
    model: z.string().min(1),
    endpoint: z.string().url(),
  }),
  // Existing — unchanged
  z.object({
    provider: z.literal('none'),
    model: z.literal(''),
  }),
]);
```

The OpenRouter base URL is a constant, not user-configurable. Custom local endpoints are user-provided.

### Constants

```typescript
// OpenRouter
export const OPENROUTER_BASE_URL = 'https://openrouter.ai/api/v1';
export const DEFAULT_OPENROUTER_EMBEDDING_MODEL = 'openai/text-embedding-3-small';
```

Remove the existing `DEFAULT_CLOUD_EMBEDDING_MODEL` constant (replaced by `DEFAULT_OPENROUTER_EMBEDDING_MODEL`).

Update the embedding dimensions map with OpenRouter model IDs:

```typescript
EMBEDDING_MODEL_DIMENSIONS: Record<string, number> = {
  // Existing Ollama models
  'nomic-embed-text': 768,
  'bge-m3': 1024,
  'mxbai-embed-large': 1024,
  'all-minilm': 384,
  'snowflake-arctic-embed': 1024,
  'qwen3-embedding': 1536,
  'jina-embeddings': 768,
  // OpenRouter / OpenAI models (NEW)
  'openai/text-embedding-3-small': 1536,
  'openai/text-embedding-3-large': 3072,
  'openai/text-embedding-ada-002': 1536,
  // voyage-3-lite (512) already exists in the map — no change needed
};
```

### Service Implementation

One new class: `OpenAICompatibleEmbeddingService` — used by both `openrouter` and `custom` providers. Same `IEmbeddingService` interface as `OllamaEmbeddingService`.

```typescript
interface OpenAICompatibleEmbeddingConfig {
  baseUrl: string;      // e.g., "https://openrouter.ai/api/v1"
  model: string;        // e.g., "openai/text-embedding-3-small"
  apiKey?: string;      // Required for OpenRouter, absent for custom local
}
```

Behavior:
- `embed(text)` — `POST {baseUrl}/embeddings` with `{input: text, model}`. If `apiKey` provided, sets `Authorization: Bearer {apiKey}`. Returns `Float32Array` from `response.data[0].embedding`.
- `isAvailable()` — Sends a minimal embed request (single-word input) and caches the result. Unlike the Ollama service which probes the root URL, OpenAI-compatible endpoints have no standard health check path. A small real request is the most reliable test. Cache TTL matches `EMBEDDING_AVAILABILITY_CACHE_MS` (30s).

### Service Factory

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

### Setup Wizard

Embedding provider selection:

```
Embedding provider:
  ● OpenRouter (cloud, recommended)
  ○ Custom local (LM Studio, llama.cpp)
  ○ Ollama (local)
  ○ None (keyword search only)
```

Flows:
- **OpenRouter** → ask for API key → user enters model name (default: `openai/text-embedding-3-small`). Use free text input rather than fetching from a models endpoint, since OpenRouter's model discovery API may not have a dedicated embeddings-only listing.
- **Custom local** → ask for endpoint URL → ask for model name (free text)
- **Ollama** → existing flow (endpoint, model selection from running instance)
- **None** → skip

### OpenRouter API

Verified endpoint details:

| | |
|---|---|
| **Endpoint** | `POST https://openrouter.ai/api/v1/embeddings` |
| **Auth** | `Authorization: Bearer <openrouter-api-key>` |
| **Request** | `{ input: string, model: string }` |
| **Response** | `{ data: [{ embedding: number[], index: 0 }], model, usage }` |
| **Model discovery** | Not used — model entered as free text in setup wizard |
| **Compatibility** | Fully OpenAI-compatible request/response format |

### Config Migration

Existing users with `{ provider: 'cloud' }` in `~/.tom/config.json` will fail Zod validation on load since the `cloud` discriminant is removed. This is acceptable pre-release breakage — the app has not been published yet and there are no external users. `ConfigManager.load()` will throw a validation error; users re-run `tom setup` to regenerate their config. No migration code needed.

### Existing Code Changes

| File | Change |
|------|--------|
| `core/src/types/config.ts` | Replace `cloud` variant with `openrouter` and `custom` variants |
| `core/src/constants.ts` | Add OpenRouter constants, remove `DEFAULT_CLOUD_EMBEDDING_MODEL`, update dimensions map |
| `core/src/services/embedding.ts` | Add `OpenAICompatibleEmbeddingService` class |
| `core/src/services/service-factory.ts` | Wire up new providers |
| `core/src/index.ts` | Export new service class and config type |
| `cli/src/commands/setup.tsx` | Update embedding provider wizard flow |
| `core/src/services/__tests__/embedding.test.ts` | Tests for `OpenAICompatibleEmbeddingService` |
| `core/src/types/__tests__/config.test.ts` | Updated schema tests for `openrouter` and `custom` variants |
| `cli/src/commands/__tests__/setup.test.ts` | Update `DEFAULT_CLOUD_EMBEDDING_MODEL` references |

### Graceful Degradation

Same principle as existing Ollama path:
- If OpenRouter is unreachable or key is invalid, embedding is skipped
- Entry saved without vector, search falls back to FTS5
- Warning displayed, not blocking

---

## Feature 2: NPM Publishing

### Problem

The CLI has no distribution mechanism. Users can only run it from the monorepo source via `make tom` or `make link-dev`.

### Solution

Publish two npm packages with OIDC trusted publishing (no npm tokens), per-package git tags, independent GitHub Releases, and version sync back to the working branch.

### Package Naming

| Current Internal Name | Published npm Name | Purpose |
|---|---|---|
| `@ten-second-tom/core` | `ten-second-tom-core` | Business logic library |
| `@ten-second-tom/cli` | `ten-second-tom` | CLI binary (`tom`) |

The internal workspace package names change to match the npm names. All source imports of `@ten-second-tom/core` update to `ten-second-tom-core`.

### Per-Package Tagging

Following the per-package tagging pattern used in the unifi-mcp monorepo:

- `core/v1.0.0` → triggers `publish-core.yml` → publishes `ten-second-tom-core@1.0.0`
- `cli/v2.0.0` → triggers `publish-cli.yml` → publishes `ten-second-tom@2.0.0`

Packages version independently. Both packages currently have `2.0.0` in their `package.json`, but neither has been published before. Core starts at `1.0.0` for its first public release (it's a new library with no prior npm presence). CLI starts at `2.0.0` to match the v2 rewrite branding and the version users see in the app.

### Workflow: `publish-core.yml`

Triggers on push of tags matching `core/v[0-9]+.[0-9]+.[0-9]+`.

```yaml
permissions:
  contents: write
  id-token: write

jobs:
  validate-tag:
    # Extract version from "core/vX.Y.Z" → "X.Y.Z"

  build-and-test:
    needs: validate-tag
    steps:
      - checkout
      - setup-node (22) with pnpm
      - pnpm install
      - Set version in packages/core/package.json from tag
      - pnpm -r build
      - pnpm vitest run

  create-release:
    needs: [validate-tag, build-and-test]
    steps:
      - checkout with fetch-depth: 0
      - Find previous core/v* tag
      - gh release create with scoped changelog (--notes-start-tag)

  publish:
    needs: [validate-tag, create-release]
    environment: npm-publish
    steps:
      - checkout
      - setup-node (22) with registry-url: https://registry.npmjs.org
      - npm install -g npm@latest  # OIDC support
      - pnpm install
      - Set version in packages/core/package.json
      - pnpm -r build
      - cd packages/core && pnpm publish --provenance --access public --no-git-checks

  sync-version:
    needs: [validate-tag, publish]
    steps:
      - checkout ref: v2-rewrite
      - Set version in packages/core/package.json
      - Commit "chore: sync core version to X.Y.Z [skip ci]"
      - Push to v2-rewrite
```

### Workflow: `publish-cli.yml`

Same structure as `publish-core.yml` but:
- Triggers on `cli/v[0-9]+.[0-9]+.[0-9]+`
- Sets version in `packages/cli/package.json`
- Publishes from `packages/cli/`
- GitHub Release title: `Ten-Second Tom vX.Y.Z`

### Release Flow

1. Make changes, commit, push to `v2-rewrite`
2. Tag the package: `git tag core/v1.0.0` or `git tag cli/v2.0.0`
3. Push the tag: `git push origin core/v1.0.0`
4. Workflow runs: build → test → release → publish → sync version

**Ordering when both packages change:** Tag and push `core/v*` first. Wait for the workflow to complete **and the package to appear on the npm registry** (npm propagation can take a few minutes). Then tag `cli/v*`. The CLI's `workspace:*` dependency gets resolved to whatever version is in core's `package.json` at publish time — that version must exist on npm or `npm install -g ten-second-tom` will fail for users.

### User Installation

```bash
npm install -g ten-second-tom
tom setup
tom record
```

### Required One-Time Setup

| Step | Where |
|------|-------|
| Register `ten-second-tom` on npm | npmjs.com |
| Register `ten-second-tom-core` on npm | npmjs.com |
| Configure OIDC trusted publishing for both | npm package settings → Provenance → link GitHub repo |
| Create `npm-publish` environment | GitHub repo Settings → Environments |

### Repo Changes

| File | Change |
|------|--------|
| `packages/core/package.json` | `name` → `ten-second-tom-core` |
| `packages/cli/package.json` | `name` → `ten-second-tom`, dep → `ten-second-tom-core` |
| `packages/cli/src/**/*.ts(x)` | Import path `@ten-second-tom/core` → `ten-second-tom-core` |
| `.github/workflows/publish-core.yml` | New workflow |
| `.github/workflows/publish-cli.yml` | New workflow |

---

## What This Spec Does NOT Cover

- Homebrew tap — future distribution channel, not part of this spec
- Node.js SEA (single executable) — evaluated and deferred; native dependencies make this impractical
- `@yao-pkg/pkg` bundling — same native dependency constraint
- CLI `--help` polish — separate task
- Test coverage gap analysis — separate task
- Cross-platform testing — separate task
