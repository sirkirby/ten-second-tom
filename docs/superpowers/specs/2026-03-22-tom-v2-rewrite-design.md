# Ten-Second Tom v2.0 — Design Spec

**Date:** 2026-03-22
**Status:** Approved
**Decision Authority:** Chris Kirby

---

## Overview

Ten-Second Tom v2.0 is a full rewrite from .NET/C# to Node.js/TypeScript. The product shifts from a general-purpose voice journaling CLI to an intelligence-first capture and analysis tool for engineers. The core value loop is **capture → transcribe → analyze → search**, with the differentiator being what Tom does with the data after capture — sentiment analysis, semantic search, and future AI-powered coaching.

### Supersedes

This design spec supersedes the R&D Council Implementation Plan (`docs/IMPLEMENTATION-PLAN.md`). Where the two documents conflict, this spec is authoritative. The existing CLAUDE.md and `.specify/memory/constitution.md` are .NET-specific and will be replaced with a new constitution as part of Sprint 1.

### What Changed From the R&D Council Plan

| Council Recommendation | Decision | Rationale |
|----------------------|----------|-----------|
| Slack bot (Phase 2) | Dropped entirely | Cost concerns with Cloudflare compute; unclear value for text-only interaction; revisit later |
| MCP server (strategic optionality) | Promoted to Phase 2 | Better integration story — agents interact with team data via MCP |
| `tom retro`, `tom decision` | Deferred to team design | Outdated in agentic AI era; revisit when teams are designed |
| `tom digest` | Dropped from MVP | Unclear single-user value; revisit with teams |
| LLM provider abstraction (Claude/OpenAI/Ollama) | Claude Agent SDK only | Agent SDK supports both cloud (Claude API key) and local (Ollama/LM Studio); eliminates need for separate provider abstraction |
| OpenAI-compat API | Dropped | Different prompting/orchestration paradigm than Agent SDK; not worth maintaining two patterns |
| FTS5 search | Downgraded to fallback | Semantic vector search is primary; FTS5 for users without Ollama |
| Batch transcription | Replaced with streaming | Real-time chunked transcription during recording is a requirement (chunked inference with segments up to 5 seconds; transcript updates appear within a few seconds of speech, not sub-second word-level streaming) |
| Audio as DB blob | Replaced with filesystem | Recordings can be 30-60+ minutes; BLOBs not practical at that size |
| Team vault (Phase 1) | Deferred to Phase 2 | Single-user MVP first; team-aware schema where cheap |
| 4 entry types (retro/standup/incident/note) | Simplified to 2 (recording/note) | Retro/standup/incident are team concepts; defer with team features |

---

## MVP Scope

### Commands

| Command | Purpose |
|---------|---------|
| `tom setup` | First-run wizard: choose LLM provider (cloud Claude or local model), download STT model, configure `~/.tom/` |
| `tom record` | Audio capture with real-time streaming transcription, post-recording sentiment/context analysis |
| `tom note` | Text entry with optional voice dictation input (speech-to-text, no audio saved). Notes receive the same analysis and embedding pipeline as recordings. |
| `tom search` | Semantic search via vector similarity; FTS keyword fallback if no embedding provider |

### Explicitly Out of MVP

- `tom retro`, `tom decision` — revisit with team design
- `tom digest` — revisit with team design
- Slack bot — dropped
- Team vault / sharing / push — Phase 2
- MCP server — Phase 2
- Text-to-speech output — future
- Mobile app, web dashboard — out of scope

---

## Architecture

### Monorepo Structure

```
ten-second-tom/
├── package.json                  # Root workspace config (pnpm)
├── pnpm-workspace.yaml
├── tsconfig.json                 # Shared TS config (strict, ESM)
├── vitest.config.ts
├── packages/
│   ├── cli/                      # Ink CLI app
│   │   ├── src/
│   │   │   ├── app.tsx           # Root Ink component
│   │   │   ├── cli.ts            # Entry point + Commander routing
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
│       │   ├── types/            # Zod schemas + TS types (inline, no separate package)
│       │   ├── services/
│       │   │   ├── storage.ts    # DB abstraction (SQLite or PGlite)
│       │   │   ├── transcription.ts  # Whisper STT (streaming)
│       │   │   ├── sentiment.ts  # Sentiment analysis
│       │   │   ├── embedding.ts  # Vector embedding pipeline
│       │   │   └── search.ts     # Hybrid search (semantic + FTS)
│       │   ├── agent/            # Claude Agent SDK integration
│       │   │   ├── tom-agent.ts  # Tom's analysis agent
│       │   │   └── config.ts     # Cloud vs local provider config
│       │   └── config/           # App configuration management
│       └── package.json
├── migrations/                   # Database migrations
│   └── local/
└── docs/
```

### Design Principles

- **`core` owns all business logic.** The CLI package is a thin rendering layer over core services. No business logic in CLI components.
- **Storage is behind an interface** so the database technology (SQLite + sqlite-vec vs PGlite) can be swapped without touching anything above it.
- **Agent SDK lives in `core/agent/`.** One TomAgent handles sentiment, analysis, and future synthesis. Not scattered across services.
- **Schemas live in `core/types/`** for MVP simplicity. Extract to a separate package only if a future consumer (MCP server, team backend) needs them independently.
- **Intelligence is the differentiator.** Many apps capture notes and do speech-to-text. Tom's value is what happens after capture — analysis, semantic search, and future coaching.

---

## Data Model

### Entry

The universal capture record. All content Tom stores flows through this.

```typescript
interface Entry {
  id: string;                      // UUID
  type: 'recording' | 'note';
  content: string;                 // transcript or note text
  audioPath?: string;              // relative path within ~/.tom/audio/ (recordings only)
  inputMethod: 'typed' | 'dictated' | 'recorded'; // how content was captured — useful for future analysis patterns (e.g., do dictated notes have different sentiment profiles than typed?)
  analysis?: EntryAnalysis;        // null until agent analyzes
  embedding?: Float32Array;        // null until embedded
  createdAt: string;               // ISO 8601
  updatedAt: string;
}
```

### EntryAnalysis

Rich analysis output from TomAgent. The schema is intentionally flexible — the agent produces structured JSON, and this will evolve as analysis capabilities grow. The initial framing includes sentiment scoring but will expand to emotion detection, topic extraction, linguistic markers, and more.

```typescript
interface EntryAnalysis {
  sentiment: {
    score: number;                 // -1.0 to 1.0
    label: string;                 // richer than pos/neg/neutral
    confidence: number;            // 0-1
  };
  summary: string;                 // brief AI-generated context
  raw: Record<string, unknown>;    // full agent output for future use
}
```

### AppConfig

Persisted user configuration created during `tom setup`.

```typescript
interface AppConfig {
  llm: {
    provider: 'cloud' | 'local';
    apiKey?: string;               // for cloud (Claude)
    localEndpoint?: string;        // for local (Ollama/LM Studio)
    modelId?: string;              // approved local model
  };
  stt: {
    engine: string;                // whisper variant, resolved at setup
    modelPath: string;             // local model file path
  };
  embedding: {
    provider: 'ollama' | 'cloud' | 'none'; // ollama = local embeddings, cloud = Voyage AI / Anthropic embeddings, none = FTS5 fallback
    model: string;                 // nomic-embed-text, bge-m3, voyage-3-lite, etc.
    endpoint?: string;             // for ollama or custom endpoint
  };
  storage: {
    dbPath: string;                // path to local database
  };
}
```

---

## Local Storage Layout

```
~/.tom/
├── config.json          # AppConfig
├── tom.db               # SQLite or PGlite database
├── audio/               # Audio files: {date}-{id}.wav
│   ├── 2026-04/
│   │   ├── 2026-04-01-abc12345.wav
│   │   └── 2026-04-03-def67890.wav
│   └── 2026-05/
└── models/              # Downloaded STT model files
```

Database stores entry metadata, transcripts, embeddings, and analysis. Audio files live in `~/.tom/audio/` with the entry ID linking them. One `audioPath` field in the entry references the file.

---

## Service Architecture

### Service Dependency Graph

```
CLI Commands (thin UI layer)
    │
    ▼
Core Services
    ├── TranscriptionService    ← Whisper (local, streaming)
    ├── EmbeddingService        ← Ollama (local) or skip
    ├── TomAgent                ← Claude Agent SDK (cloud or local)
    │   ├── analyze sentiment + context
    │   ├── (future: synthesize, coach)
    ├── StorageService          ← SQLite or PGlite (behind interface)
    │   ├── save/retrieve entries
    │   ├── vector search
    │   └── FTS5 fallback
    └── SearchService           ← orchestrates hybrid search
        ├── semantic (vectors)
        └── keyword (FTS5)
```

### Command Flows

**`tom record`:**
1. CLI starts audio capture (microphone → WAV stream)
2. Audio chunks stream to TranscriptionService (Whisper)
3. Ink renders real-time: timer, live transcript appearing as user speaks
4. User presses Enter → audio file saved to `~/.tom/audio/`
5. Final transcription pass for accuracy (if needed)
6. In parallel: EmbeddingService generates vector + TomAgent analyzes sentiment/context
7. StorageService persists entry + embedding + analysis
8. CLI renders result (final transcript, sentiment, insights)

**`tom note`:**
1. CLI presents text input (Ink text component)
2. User types or toggles to voice dictation mode
3. If dictation: TranscriptionService streams Whisper → text appears live (no audio saved)
4. On submit: EmbeddingService generates vector + TomAgent analyzes
5. StorageService persists entry + embedding + analysis

**`tom search`:**
1. User enters natural language query
2. If embeddings available: SearchService embeds query → vector similarity search
3. If no embeddings: FTS5 keyword search
4. Results ranked by relevance, rendered with timestamps, sentiment indicators, and transcript excerpts
5. User can select an entry to view full transcript

---

## Technology Choices

### Confirmed

| Choice | Technology | Notes |
|--------|-----------|-------|
| Language | TypeScript (strict, ESM) | |
| CLI framework | Ink 5 + Commander | React for terminal |
| Package manager | pnpm workspaces | Monorepo |
| LLM integration | Claude Agent SDK | Cloud (API key) or local (Ollama/LM Studio) with approved models |
| Analysis | TomAgent via Agent SDK | Rich analysis beyond simple sentiment |
| Embeddings | Ollama (nomic-embed-text or bge-m3), cloud (Voyage AI), or none | FTS5 fallback when no embedding provider configured |
| Testing | Vitest | 80% coverage target |
| Bundling | tsup | CLI distribution |
| Audio storage | Local filesystem (`~/.tom/audio/`) | Recordings too large for DB blobs |
| Config storage | JSON file (`~/.tom/config.json`) | |

### Research Spikes (Sprint 1)

| Question | Options | Decision Criteria |
|----------|---------|-------------------|
| Database | SQLite + sqlite-vec vs PGlite | Vector support quality, maturity, bundle size, migration tooling, developer experience |
| STT engine | Whisper variants in Node.js | Must support chunked inference (segments up to 5s, transcript updates within a few seconds of speech). Metal acceleration on macOS, Windows support. Fallback: batch transcription after recording if no streaming library is viable. |
| Approved local models | Models for Agent SDK via Ollama | Quality of analysis output, speed, memory footprint |

---

## Streaming Transcription

"Real-time transcription" means **chunked inference**: audio is split into segments (up to ~5 seconds) and each segment is transcribed sequentially. Transcript text appears in the Ink UI after each chunk is processed, giving the appearance of live transcription with a few seconds of latency. This is not sub-second word-level streaming.

If the Sprint 1 STT research finds that no Node.js Whisper library supports chunked inference reliably, the fallback is **batch transcription with progress indication**: recording completes, then Whisper processes the full audio with a progress bar. This is how v1 worked and is acceptable if streaming proves infeasible.

---

## Failure Modes

| Scenario | Behavior |
|----------|----------|
| Microphone unavailable or permission denied | CLI displays clear error with platform-specific instructions to grant mic access. `tom record` and dictation mode in `tom note` exit gracefully. |
| STT model not downloaded (setup skipped) | CLI prompts user to run `tom setup` first. Commands that need STT refuse to start. |
| Ollama configured but not running | Embedding is skipped, entry saved without vector. Search falls back to FTS5. Warning displayed but not blocking. |
| Claude API key invalid or rate-limited | Analysis is skipped, entry saved without analysis. Warning displayed. Analysis can be retried later. |
| LLM provider completely unavailable (offline cloud user, Ollama down) | Entry is saved with transcript only (no analysis, no embedding). Tom degrades to a capture + FTS5 search tool. |
| Database corrupted or inaccessible | CLI displays error and exits. No silent data loss. |

**Design principle:** Capture always succeeds if the mic works. Analysis and embedding are enhancements that degrade gracefully. The user's content is never lost because an optional service is unavailable.

---

## Testing Strategy

- **Core services:** Unit tested via interface mocks. StorageService tested against a real in-memory database instance.
- **Audio/STT:** TranscriptionService tested via interface mock in unit tests. Integration tests with real audio files in CI (small test fixtures).
- **TomAgent:** Tested via Agent SDK mock/stub. Integration tests against real Claude API gated behind an env flag.
- **Ink components:** Tested with `ink-testing-library` for render output assertions.
- **Coverage target:** 80% across core package. CLI package coverage lower is acceptable (UI rendering is hard to unit test meaningfully).

---

## CLI Framework Integration

Commander parses arguments and flags at the entry point (`cli.ts`). Each command handler mounts an Ink component for interactive rendering. Commander handles the "which command was invoked" routing; Ink handles everything the user sees and interacts with.

---

## Sprint Plan

### Sprint 1 — Weeks 1-2: Foundation + Research

- Initialize pnpm monorepo (cli, core packages — schemas inline in core for MVP)
- TypeScript strict, ESLint, Prettier, Vitest configuration
- Replace CLAUDE.md and constitution with new TypeScript/Node.js equivalents
- Define Zod schemas for Entry, AppConfig, EntryAnalysis
- Storage interface + two spike implementations (SQLite + sqlite-vec, PGlite)
- STT library research: evaluate streaming/chunked Whisper options in Node.js
- Agent SDK wiring: connect to Claude cloud, test with Ollama locally
- `tom setup` wizard: choose cloud/local LLM, configure embedding provider, download STT model, configure `~/.tom/`
- **Exit criteria:** `tom setup` works end-to-end, database choice committed, STT library chosen

### Sprint 2 — Weeks 3-4: `tom record`

- Audio capture from microphone
- Streaming STT: real-time transcription rendering in Ink as user speaks
- Ink RecordingUI component (timer, live transcript, controls)
- Post-recording: TomAgent sentiment/context analysis
- Embedding generation (if Ollama configured)
- Persist entry + audio file to `~/.tom/`
- **Exit criteria:** Full record pipeline — speak, see live transcript, get analysis, saved to DB and filesystem

### Sprint 3 — Weeks 5-6: `tom note` + Search

- `tom note` with typed text input (Ink text component)
- Voice dictation mode toggle: streaming Whisper → text, no audio saved
- Embedding + analysis pipeline (same as record, minus audio storage)
- `tom search`: semantic search via vector similarity
- FTS5 fallback for users without Ollama
- Search results UI: ranked entries with timestamps, sentiment, transcript excerpts
- **Exit criteria:** Both capture modes work, search finds entries semantically

### Sprint 4 — Weeks 7-8: Polish + Distribution

- Edge cases: mic permissions, missing dependencies, error handling
- Cross-platform testing (macOS + Windows)
- Performance benchmarking: STT speed, embedding generation, search latency
- `tom --help`, command documentation
- Distribution: evaluate Node.js SEA (single executable application, built-in since Node 20), `@yao-pkg/pkg` (community fork of discontinued `pkg`), or npm global install + Homebrew tap. STT models downloaded on first run via `tom setup`, not bundled.
- Test coverage to 80%
- **Exit criteria:** Shippable single-user CLI

### Phase 2 (Future — Not Scoped)

- Team vault + shared backend (Cloudflare hosted)
- MCP server on top of team data
- `tom digest` with synthesis
- `tom retro`, `tom decision`
- Richer coaching (temporal patterns, proactive alerts)
- Text-to-speech output

---

## Repo Strategy

Branch off `main` in the existing ten-second-tom repository. Remove all .NET code on the new branch. Initialize Node.js monorepo from scratch. Git history preserves the full .NET codebase.

---

## Open Questions for Phase 2 Design

These are explicitly deferred and will need their own design cycle:

- How does team data flow? Local CLI → push to where? Cloudflare D1, R2, Durable Objects?
- MCP server architecture: what tools does it expose? Where does it run?
- Team vault design: roles, permissions, data isolation
- How do retros and decisions work in a team context?
- Pricing model for team features
