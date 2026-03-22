# Ten-Second Tom v2.0 — Implementation Plan

**Date:** March 22, 2026
**Prepared by:** R&D Council (Vega, Sentinel, Flux, Volt, Nova)
**Decision Authority:** Chris Kirby, VP of Engineering
**Status:** Ready for Agent Implementation

---

## Executive Summary

Ten-Second Tom is being rebuilt from the ground up as an **async team retrospective engine for distributed engineering teams**. The existing .NET CLI is being replaced with a Node.js + Ink monorepo that includes a local CLI for voice/text capture and a Cloudflare Worker Slack bot for team consumption.

**What we're building:** A CLI tool where engineering teams record voice/text retrospectives locally (privacy-first), run sentiment analysis and AI-powered synthesis via Claude Opus, and share weekly digests + coaching insights to Slack — without requiring every team member to install the CLI.

**Why:** Consumer voice journaling is a commodity market (Otter.ai, Day One, AudioPen). The defensible value is in team aggregation, longitudinal coaching, and temporal sentiment — features no competitor combines with CLI-native, privacy-first architecture.

**For whom:** Distributed engineering teams (5-20 people) who run async retrospectives, track decisions, and want AI-powered coaching on team health patterns.

---

## Product Scope (Vega)

### MVP Features — What's IN

#### Phase 1: CLI + Core Engine (12 weeks, ship by June 2026)

1. **Voice Capture** — `tom record` records audio, transcribes locally via Whisper, stores in SQLite
2. **Text Capture** — `tom note` for quick text entries without AI processing
3. **Team Vault** — shared namespace for team entries with member roles (admin/member/viewer)
4. **Sentiment Analysis** — per-entry sentiment scoring (positive/neutral/negative) with team-level aggregation
5. **Decision Log** — `tom decision` captures structured decisions (title, context, rationale, owner, status)
6. **Weekly Synthesis** — `tom digest` aggregates team entries, uses Claude Opus extended thinking for deep analysis
7. **CLI Search** — `tom search` with full-text search across vault entries
8. **Data Retention** — configurable auto-purge (30/60/90/365 days)

#### Phase 2: Slack Bot + Coaching (Months 4-8)

1. **Slack Bot** — `/tom digest`, `/tom retro`, `/tom sentiment` slash commands via Cloudflare Worker
2. **Team Voting** — emoji reactions on Slack posts aggregate into decision status
3. **Longitudinal Coaching** — Claude Opus extended thinking on 6+ weeks of data identifies patterns
4. **Temporal Sentiment** — week-over-week sentiment trajectories with inflection point detection
5. **Proactive Alerts** — "3 team members flagged frustration this week — consider a check-in"

### What's OUT (Explicitly Dropped)

| Feature | Why |
|---------|-----|
| Basic journaling prompts | Commodity — Day One, Mindsera, AudioPen all have this |
| Template management UI | Commodity — templates are YAML files, edit in any editor |
| Note-taking editor | Obsidian does this better — Tom is for team retros, not notes |
| Search archive GUI | `tom search` CLI is sufficient; no web search dashboard |
| Tag/metadata system | 20% complexity for 2% value |
| Mobile app | Slack mobile is the mobile interface |
| Web dashboard | Slack + CLI is the interface for v1 |

### User Personas

**Tech Lead (Alex):** Runs a 12-person distributed backend team across 3 time zones. Uses Tom to collect async retro entries from the team, gets weekly digest on Slack, tracks decision velocity and incident patterns.

**SRE IC (Morgan):** Records voice retros after incidents. Tom aggregates with past incidents and identifies cascade failure patterns. Coaching feature flags: "You've had 3 cascade failures in the rate limiter this quarter."

**Engineering Manager (Sam):** Manages 3 teams. Gets monthly coaching digest: "Team A sentiment flat (OK). Team B down 20% (investigate hiring friction). Team C up (momentum post-launch)." Focuses attention on early warning signals.

### Core Value Proposition

> Async team retrospective engine for distributed engineering teams — capture decisions, incidents, and retros in a private CLI-native tool; get AI-powered coaching on team patterns and sentiment trends via Slack.

### Success Metrics & Kill Criteria

**Kill Gate: September 30, 2026**
- **<10 active teams** OR **<$5K MRR** → Kill the project. Both conditions must be met.

| Phase | Metric | Target |
|-------|--------|--------|
| Phase 1 | CLI installs | 50+ by June 2026 |
| Phase 1 | Beta teams (Shift Digital internal) | 5+ |
| Phase 1 | Weekly active retro entries per team | 20+ |
| Phase 2 | Paid teams | 5+ by Sept 2026 |
| Phase 2 | MRR | $5K+ by Sept 2026 |
| Phase 2 | Team leads using coaching digests | 10+ |

### Competitive Positioning

Tom owns the **"CLI-first, privacy-first, async-retro + coaching"** niche.

| Competitor | Their Strength | Tom's Angle |
|-----------|---------------|------------|
| Parabol | Web-first, real-time, beautiful UI | CLI-first, offline, privacy |
| TeamRetro | Established, Slack/Teams integrations | Free for small teams, local storage |
| Reetro | Lightweight, Slack-integrated | 100% offline, customizable |
| Incident.io | Powerful incident management | Lightweight, CLI-native, focused on retro capture |
| Otter.ai | Market leader transcription | Team-focused, zero cloud, longitudinal coaching |

---

## Technical Architecture (Volt)

### Monorepo Structure

```
ten-second-tom/
├── package.json                    # Root workspace config (pnpm)
├── pnpm-workspace.yaml
├── tsconfig.json                   # Shared TypeScript config (strict)
├── vitest.config.ts
├── packages/
│   ├── cli/                        # Ink CLI app
│   │   ├── src/
│   │   │   ├── app.tsx             # Root Ink component
│   │   │   ├── cli.ts              # Entry point
│   │   │   ├── commands/
│   │   │   │   ├── recordCommand.tsx
│   │   │   │   ├── summaryCommand.tsx
│   │   │   │   ├── retrosCommand.tsx
│   │   │   │   ├── coachingCommand.tsx
│   │   │   │   ├── configCommand.tsx
│   │   │   │   └── pushCommand.tsx
│   │   │   ├── components/
│   │   │   │   ├── RecordingUI.tsx
│   │   │   │   ├── SentimentDisplay.tsx
│   │   │   │   ├── SummaryView.tsx
│   │   │   │   └── ConfigForm.tsx
│   │   │   └── hooks/
│   │   │       ├── useAudioRecorder.ts
│   │   │       ├── useSentimentAnalysis.ts
│   │   │       └── useLocalStorage.ts
│   │   └── package.json
│   ├── core/                       # Shared business logic
│   │   ├── src/
│   │   │   ├── types/
│   │   │   │   ├── entry.ts
│   │   │   │   ├── sentiment.ts
│   │   │   │   ├── digest.ts
│   │   │   │   └── config.ts
│   │   │   ├── services/
│   │   │   │   ├── analysisService.ts
│   │   │   │   ├── summaryService.ts
│   │   │   │   ├── sentimentService.ts
│   │   │   │   ├── transcriptionService.ts
│   │   │   │   └── storageService.ts
│   │   │   ├── llm/
│   │   │   │   ├── provider.ts
│   │   │   │   ├── anthropic.ts
│   │   │   │   ├── openai.ts
│   │   │   │   └── ollama.ts
│   │   │   ├── storage/
│   │   │   │   ├── interface.ts
│   │   │   │   ├── sqlite.ts
│   │   │   │   └── markdown.ts
│   │   │   ├── extensions/
│   │   │   │   └── hooks.ts
│   │   │   └── events/
│   │   │       └── EventBus.ts
│   │   └── package.json
│   ├── slack-bot/                  # Cloudflare Worker
│   │   ├── src/
│   │   │   ├── index.ts            # Hono app entry
│   │   │   ├── handlers/
│   │   │   │   ├── events.ts
│   │   │   │   ├── interactions.ts
│   │   │   │   ├── push.ts
│   │   │   │   └── analytics.ts
│   │   │   ├── services/
│   │   │   │   ├── teamService.ts
│   │   │   │   ├── entryService.ts
│   │   │   │   └── slackApi.ts
│   │   │   └── middleware/
│   │   │       ├── auth.ts
│   │   │       └── errorHandler.ts
│   │   ├── wrangler.toml
│   │   └── package.json
│   └── schemas/                    # Shared Zod validation
│       └── src/
│           ├── entry.ts
│           ├── config.ts
│           └── api.ts
├── migrations/
│   ├── sqlite-local/
│   │   ├── 001_entries.sql
│   │   ├── 002_sentiments.sql
│   │   └── 003_team_config.sql
│   └── d1-team/
│       ├── 001_teams.sql
│       ├── 002_members.sql
│       ├── 003_shared_entries.sql
│       └── 004_retros.sql
└── docs/
```

### Key Dependencies (with versions)

**CLI Package:**
| Package | Version | Purpose |
|---------|---------|---------|
| ink | ^5.0.0 | CLI rendering framework (React for terminal) |
| ink-select-input | ^5.0.0 | Selection menus |
| ink-text-input | ^5.0.0 | Text input fields |
| ink-spinner | ^5.0.0 | Loading indicators |
| commander | ^11.1.0 | CLI argument parsing |
| node-record-lpcm16 | ^0.0.11 | Audio recording |
| fluent-ffmpeg | ^2.1.2 | Audio preprocessing |
| better-sqlite3 | ^9.2.0 | Local data storage |

**Core Package:**
| Package | Version | Purpose |
|---------|---------|---------|
| @anthropic-ai/sdk | ^0.78.0 | Claude Opus integration (extended thinking) |
| openai | ^4.51.0 | OpenAI fallback |
| natural | ^6.10.0 | Local sentiment analysis (AFINN lexicon) |
| @xenova/transformers | ^2.13.0 | Local Whisper STT via transformers.js |
| zod | ^3.22.4 | Schema validation |
| date-fns | ^2.30.0 | Date utilities |

**Slack Bot Package:**
| Package | Version | Purpose |
|---------|---------|---------|
| hono | ^4.0.0 | Cloudflare Worker web framework |
| wrangler | ^3.18.0 | CF Worker development/deployment |

**Whisper Strategy:**
- Primary: `@xenova/transformers` (Distil-Whisper, 6x faster than Large, -1% WER)
- Fallback: `@fugood/whisper.node` (Metal acceleration on macOS, matches .NET performance)
- Both support local-only inference with no cloud dependency

### Core Data Models

```typescript
// Entry — individual voice/text retrospective entry
interface Entry {
  id: string;                    // UUID
  type: 'retro' | 'standup' | 'incident' | 'note';
  teamId: string;
  authorId: string;
  content: string;               // Markdown transcript
  audioPath?: string;            // Local path to WAV (never uploaded)
  sentiment: SentimentScore;
  createdAt: string;             // ISO 8601
  updatedAt: string;
}

// SentimentScore
interface SentimentScore {
  score: number;                 // -1.0 to 1.0
  label: 'positive' | 'neutral' | 'negative';
  confidence: number;            // 0-1
}

// Decision — structured team decision
interface Decision {
  id: string;
  teamId: string;
  title: string;
  context: string;
  decision: string;
  rationale: string;
  ownerId: string;
  status: 'open' | 'closed' | 'archived';
  priority: 'low' | 'medium' | 'high';
  createdAt: string;
}

// TeamVault — team configuration and membership
interface TeamVault {
  id: string;
  name: string;
  members: TeamMember[];
  retentionDays: number;
  slackChannelId?: string;
  createdAt: string;
}

interface TeamMember {
  userId: string;
  role: 'admin' | 'member' | 'viewer';
  joinedAt: string;
}

// WeeklyDigest — AI-generated team synthesis
interface WeeklyDigest {
  id: string;
  teamId: string;
  weekStart: string;
  themes: string[];
  recommendedActions: string[];
  sentimentTrend: SentimentScore[];
  coachingInsight?: string;
  generatedAt: string;
}
```

### Local SQLite Schema

```sql
-- 001_entries.sql
CREATE TABLE entries (
  id TEXT PRIMARY KEY,
  type TEXT NOT NULL CHECK(type IN ('retro','standup','incident','note')),
  team_id TEXT NOT NULL,
  author_id TEXT NOT NULL,
  content TEXT NOT NULL,
  audio_path TEXT,
  sentiment_score REAL,
  sentiment_label TEXT,
  sentiment_confidence REAL,
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX idx_entries_team ON entries(team_id);
CREATE INDEX idx_entries_created ON entries(created_at);

-- 002_decisions.sql
CREATE TABLE decisions (
  id TEXT PRIMARY KEY,
  team_id TEXT NOT NULL,
  title TEXT NOT NULL,
  context TEXT,
  decision TEXT NOT NULL,
  rationale TEXT,
  owner_id TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'open',
  priority TEXT NOT NULL DEFAULT 'medium',
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  updated_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 003_team_config.sql
CREATE TABLE team_config (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL UNIQUE,
  retention_days INTEGER DEFAULT 90,
  slack_channel_id TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE team_members (
  team_id TEXT NOT NULL REFERENCES team_config(id),
  user_id TEXT NOT NULL,
  role TEXT NOT NULL DEFAULT 'member',
  joined_at TEXT NOT NULL DEFAULT (datetime('now')),
  PRIMARY KEY (team_id, user_id)
);

-- 004_digests.sql
CREATE TABLE digests (
  id TEXT PRIMARY KEY,
  team_id TEXT NOT NULL,
  week_start TEXT NOT NULL,
  themes TEXT NOT NULL,           -- JSON array
  actions TEXT NOT NULL,          -- JSON array
  sentiment_trend TEXT NOT NULL,  -- JSON array
  coaching_insight TEXT,
  generated_at TEXT NOT NULL DEFAULT (datetime('now'))
);
```

### Cloudflare D1 Schema (Team/Slack Data)

```sql
-- 001_teams.sql
CREATE TABLE teams (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  slack_workspace_id TEXT NOT NULL,
  slack_channel_id TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

-- 002_shared_entries.sql
CREATE TABLE shared_entries (
  id TEXT PRIMARY KEY,
  team_id TEXT NOT NULL REFERENCES teams(id),
  author_name TEXT NOT NULL,
  type TEXT NOT NULL,
  content TEXT NOT NULL,
  sentiment_score REAL,
  sentiment_label TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);
-- Note: audio_path is NEVER stored in D1 — audio stays local

-- 003_retros.sql
CREATE TABLE retros (
  id TEXT PRIMARY KEY,
  team_id TEXT NOT NULL REFERENCES teams(id),
  week_start TEXT NOT NULL,
  status TEXT DEFAULT 'open',
  themes TEXT,                    -- JSON
  actions TEXT,                   -- JSON
  coaching_insight TEXT,
  created_at TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE retro_votes (
  retro_id TEXT NOT NULL REFERENCES retros(id),
  user_id TEXT NOT NULL,
  theme TEXT NOT NULL,
  vote TEXT NOT NULL CHECK(vote IN ('agree','disagree','discuss')),
  created_at TEXT NOT NULL DEFAULT (datetime('now')),
  PRIMARY KEY (retro_id, user_id, theme)
);
```

### Cloudflare Worker Architecture

```
POST /slack/events        — Slack event webhook (app_mention, message)
POST /slack/interactions   — Slash commands, button clicks, modal submissions
POST /api/push            — CLI pushes entries/digests to team (authenticated)
GET  /api/analytics/:id   — Team analytics endpoint
GET  /health              — Health check
```

**Key constraints:**
- Must acknowledge Slack events within 3 seconds (return 200 immediately, process async)
- No native modules (no Whisper, no ffmpeg, no local LLM)
- D1 for persistent team state, KV for token caching
- Hono framework for routing (Cloudflare-native)

### Build Tooling

| Tool | Config | Purpose |
|------|--------|---------|
| TypeScript | 5.3.2, strict mode, ESM | Type safety |
| pnpm | 9.1.0, workspaces | Package management |
| tsup | 8.0.1 | CLI bundling |
| wrangler | 3.18.0 | Worker dev/deploy |
| vitest | 1.0.0 | Testing |
| eslint | 8.55.0 | Linting |
| prettier | 3.1.0 | Formatting |

### What to Port from .NET

| .NET Feature | Node.js Action |
|-------------|---------------|
| Audio recording (FFmpeg integration) | Port → node-record-lpcm16 + fluent-ffmpeg |
| Whisper.NET transcription | Rebuild → @xenova/transformers or @fugood/whisper.node |
| LLM provider abstraction | Port pattern → Anthropic/OpenAI/Ollama SDK wrappers |
| Weekly summary generation | Port prompts → Claude extended thinking |
| Markdown storage | Rebuild → better-sqlite3 + markdown export |
| SSH authentication | Drop → use API keys for team auth instead |
| Template management | Drop entirely (commodity) |
| OS notifications | Drop (Slack replaces this) |
| Search (full-text) | Rebuild simpler → SQLite FTS5 |

---

## User Experience (Flux)

### CLI Commands & UX

#### `tom record [title]`
Records voice, transcribes locally, shows real-time sentiment.
```
┌─────────────────────────────────────────────────┐
│  🎙️  RECORDING: "Friday Standup"                │
├─────────────────────────────────────────────────┤
│  ⏱  0:45 seconds                                │
│  📊 Confidence: 94%  Sentiment: Positive (+0.7) │
│  [Transcription streaming...]                   │
│  "We shipped the new dashboard today..."        │
│  ◀ Esc to cancel  ▶ Enter to finish             │
└─────────────────────────────────────────────────┘
```

#### `tom retro --team [name]`
Guided retro capture with theme selection (Standard, Sailboat, Mad/Sad/Glad).

#### `tom digest --team [name]`
Weekly synthesis via Claude Opus extended thinking with progress bar.
```
┌─────────────────────────────────────────────────┐
│  📊 WEEKLY DIGEST: Platform Team (March 15-22)  │
├─────────────────────────────────────────────────┤
│  THEMES: Incident response (4x), Onboarding (3x)│
│  ACTIONS: 1. Document runbook 2. Onboarding audit│
│  SENTIMENT: Mon +0.4 → Fri +0.8 ▲ Positive     │
│  💾 Post to Slack? (y/n)                        │
└─────────────────────────────────────────────────┘
```

#### `tom decision --title "..." --team [name]`
Structured decision capture with context, rationale, and owner.

#### `tom sentiment --team [name] --weeks 6`
6-week sentiment timeline with anomaly detection.

#### `tom setup`
3-step first-run: choose LLM provider → download Whisper model → create team vault. Under 2 minutes.

### Slack Bot UX

**Slash commands:**
- `/tom retro` — opens modal for team retro input (theme selection + prompts)
- `/tom digest` — posts weekly synthesis to channel with Block Kit formatting
- `/tom sentiment` — shows team sentiment trend chart

**Interactive elements:**
- Voting buttons on retro themes (agree/disagree/discuss)
- Action item assignment dropdowns
- Thread-based discussion on digest posts

### Onboarding

**CLI Users:**
1. `npm install -g @ten-second-tom/cli` (or `brew install ten-second-tom`)
2. `tom setup` → choose provider, download model, create vault
3. `tom record` → first recording with live transcription
4. `tom digest` → see first AI synthesis

**Slack-Only Team Members:**
1. Admin installs Tom bot to workspace
2. `/tom help` shows commands
3. `/tom retro` to participate in first retro (no CLI needed)
4. Weekly digest appears automatically in channel

### CLI ↔ Slack Relationship

CLI users capture locally → optionally push to team via `tom push`. Slack bot reads from D1, posts digests. Slack-only members participate via slash commands without CLI.

---

## Risk Assessment (Sentinel)

### Critical Risks

**1. Whisper Performance in Node.js**
- .NET had Metal acceleration via Whisper.NET
- Node.js: @fugood/whisper.node maintains Metal parity on macOS
- Fallback: @xenova/transformers (Distil-Whisper, 6x faster, English-only)
- Kill criterion: If transcription >10 min for 1-hour audio on M1, escalate

**2. Cloudflare Worker Constraints**
- 128MB memory (free), 1GB (paid) — no Whisper inference on Worker
- 30s CPU time (default), 5 min with upgrade
- No native modules, no filesystem
- Architectural rule: Worker is stateless command router only. All heavy processing stays on CLI.
- Slack requires <3s acknowledgment — Worker must respond immediately, process async

**3. Node.js Runtime Distribution**
- .NET shipped as self-contained binary; Node.js requires runtime
- Mitigation: `pkg` bundles Node.js + app into single executable (~80-100MB)
- Alternative: Homebrew tap with pre-built binaries

**4. Market Timing (12-18 month window)**
- Apple Siri + Memory, Google Recorder + Gemini, Anthropic Claude Voice all shipping 2026
- Wearable AI (Memoro, Limitless) shifts from intentional → ambient capture
- Tom's moat: team aggregation + coaching (not individual capture)

**5. Privacy/Compliance for Slack Bot**
- CLI data stays local (audio never leaves device)
- When shared to Slack, transcripts live in Cloudflare D1
- GDPR: Cloudflare acts as data processor; standard DPA available
- SOC 2: Cloudflare is certified
- Audit logging: deferred to Q3 2026

### Kill Criteria Summary

| Criterion | Deadline | Action |
|-----------|----------|--------|
| <10 active teams | Sept 30, 2026 | KILL project |
| <$5K MRR | Sept 30, 2026 | KILL project |
| Node.js rewrite not shipped | June 30, 2026 | KILL project |
| >50% churn after 30 days | Q3 2026 | Overhaul UX or pivot |
| @fugood/whisper.node abandoned (6mo+) | Ongoing | Switch to WASM fallback |
| Slack bot >5% retry rate | Q2 2026 | Redesign bot logic |

### Dependency Risk Matrix

| Package | Risk | Mitigation |
|---------|------|-----------|
| @fugood/whisper.node | MEDIUM — community maintained | Pin version, monitor monthly, WASM fallback |
| ink | LOW — stable, used by Gatsby/Yarn/Shopify | Lock to 5.0.0 |
| @anthropic-ai/sdk | LOW — official, TypeScript first-class | Lock version |
| hono | LOW — Cloudflare-native, 25K stars | Active maintenance |

---

## Innovation Roadmap (Nova)

### Phase 1 Extension Points (Build Now, Use Later)

**Extension Registry:** Hooks for transcription, sentiment, synthesis, persist, and export events. Phase 2 integrations plug in without touching core.

```typescript
interface IExtensionHook {
  onTranscriptionComplete: (entry: Entry, transcript: string) => Promise<void>
  onSentimentAnalyzed: (entry: Entry, sentiment: SentimentScore) => Promise<void>
  onTeamSynthesisGenerated: (synthesis: WeeklyDigest, team: TeamVault) => Promise<void>
}
```

**Event Bus:** In-process event-driven architecture. CLI, Slack bot, and future web dashboard all react to the same domain events (EntryCreated, SentimentAnalyzed, SynthesisCompleted).

**Storage Abstraction:** `IStorageProvider` interface — SQLite today, potential migration to Turso/LibSQL for edge sync later.

### Team Coaching (The Killer Feature)

- Temporal sentiment trajectories: week-over-week, month-over-month mood tracking
- Pattern recognition: "Your team's morale dips every sprint planning week"
- Proactive alerts: "3 team members flagged frustration — consider a check-in"
- Claude Opus extended thinking synthesizes months of retro data into actionable coaching

### MCP Server (Strategic Optionality)

Tom could expose an MCP interface for agent integration:
- Tools: `get_team_sentiment`, `get_weekly_digest`, `search_retros`, `get_coaching_insight`
- Resources: `tom://team/{id}/sentiment-timeline`, `tom://team/{id}/decisions`
- Integration with Myco for cross-product knowledge memory
- Evaluate after team features ship and validate

### Voice-as-Interface Expansion

- Standup capture: "Tom, here's what I did yesterday..." → structured standup
- Rubber duck debugging: voice out a problem, get structured analysis
- PR dictation: describe changes verbally, get PR description
- Meeting notes: record a meeting, extract action items

### The Moonshot (12 months)

Real-time team sentiment dashboard, AI coach that proactively participates in Slack, predictive burnout scoring, and voice-first engineering management.

---

## Implementation Phases

### Phase 1: Foundation (Weeks 1-4, April 2026)

**Sprint 1 (Weeks 1-2): Monorepo + Core**
- [ ] Initialize pnpm workspace with packages/cli, packages/core, packages/slack-bot, packages/schemas
- [ ] Configure TypeScript (strict), ESLint, Prettier, Vitest
- [ ] Implement core types (Entry, Decision, TeamVault, SentimentScore, WeeklyDigest)
- [ ] Implement StorageService with better-sqlite3 + SQLite migrations
- [ ] Implement LLM provider abstraction (Anthropic primary, OpenAI/Ollama fallback)
- [ ] Implement SentimentService using `natural` (AFINN lexicon)
- [ ] Unit tests for all core services (>80% coverage target)

**Sprint 2 (Weeks 3-4): CLI + Audio**
- [ ] Build Ink CLI shell (app.tsx, command routing)
- [ ] Implement `tom record` with audio capture (node-record-lpcm16)
- [ ] Integrate Whisper STT (@xenova/transformers or @fugood/whisper.node)
- [ ] Test Metal acceleration on macOS M-series
- [ ] Implement `tom note` for text capture
- [ ] Implement `tom setup` first-run wizard
- [ ] Implement `tom config` for settings management
- [ ] Build RecordingUI, SentimentDisplay Ink components

### Phase 2: Team Features (Weeks 5-8, May 2026)

**Sprint 3 (Weeks 5-6): Team Vault + Synthesis**
- [ ] Implement TeamVault with member roles
- [ ] Implement `tom retro` with guided prompts and theme selection
- [ ] Implement `tom decision` for structured decision capture
- [ ] Implement `tom digest` with Claude Opus extended thinking
- [ ] Implement `tom search` with SQLite FTS5
- [ ] Implement data retention and auto-purge

**Sprint 4 (Weeks 7-8): Slack Bot Skeleton**
- [ ] Set up Cloudflare Worker with Hono
- [ ] Implement D1 schema (teams, shared_entries, retros, retro_votes)
- [ ] Implement Slack event webhook with signature verification
- [ ] Implement `/tom digest` slash command
- [ ] Implement `tom push` CLI command (push entries to team API)
- [ ] Test Slack acknowledgment <3 seconds

### Phase 3: Integration & Polish (Weeks 9-12, June 2026)

**Sprint 5 (Weeks 9-10): Full Slack Integration**
- [ ] Implement `/tom retro` with Slack modal
- [ ] Implement `/tom sentiment` with Block Kit charts
- [ ] Implement voting buttons on retro posts
- [ ] Implement weekly auto-posting of digests
- [ ] Build `pkg` binary distribution for macOS/Linux/Windows
- [ ] Set up Homebrew tap

**Sprint 6 (Weeks 11-12): Beta Launch**
- [ ] End-to-end integration testing
- [ ] Performance benchmarks (transcription, synthesis, Slack response)
- [ ] Documentation: README, getting-started, team-setup, CLI reference
- [ ] Deploy Worker to production
- [ ] Onboard 5 beta teams from Shift Digital
- [ ] Set up monitoring and error tracking

### Phase 4: Traction Validation (Q3 2026)

- [ ] Expand to 10+ active teams
- [ ] Implement longitudinal coaching (6+ weeks data)
- [ ] Implement temporal sentiment trajectories
- [ ] Add compliance audit logging
- [ ] Define pricing model
- [ ] **September 30 kill gate: evaluate metrics**

---

## Debate Notes

### Key Disagreements Resolved

**1. Full Rewrite vs. Hybrid (Chris Override)**
- Council originally recommended hybrid (keep .NET CLI, add Node.js team layer)
- Chris overrode: full Node.js rewrite, drop .NET
- Resolution: Since we're stripping to a subset of features, the rewrite is manageable. Node.js ecosystem is richer for LLM SDKs and Slack integration. The .NET codebase's primary advantage (Metal-accelerated Whisper.NET) can be matched by @fugood/whisper.node.

**2. Whisper Strategy**
- Volt recommended @xenova/transformers (Distil-Whisper)
- Sentinel flagged Metal acceleration loss as risk
- Resolution: Use @xenova/transformers as primary (fastest, cross-platform), with @fugood/whisper.node as fallback for Metal on macOS. Both are tested in CI.

**3. Slack Bot Framework**
- Options: @slack/bolt (not CF Worker compatible), workers-slack (unofficial), Hono (CF-native), custom
- Resolution: Hono framework — Cloudflare-native, TypeScript, well-maintained. Custom implementation is simpler but Hono's middleware pattern handles auth/error handling cleanly.

**4. Sentiment Analysis Approach**
- Volt: `natural` (fast AFINN lexicon, local)
- Nova: Claude API for deeper analysis
- Resolution: `natural` for real-time per-entry sentiment (fast, local). Claude for weekly synthesis coaching (deeper, cloud). Two-tier approach.

**5. Phase 1 Scope**
- Vega wanted team vault + decision log + digest in Phase 1
- Sentinel warned scope creep risks June deadline
- Resolution: Phase 1 includes all three, but with strict scope: no voting, no Slack auto-posting, no coaching. Those are Phase 2.

---

## Agent Handoff — Start Here

A coding agent picking up this plan should:

### Task 1: Initialize Monorepo
```bash
mkdir ten-second-tom && cd ten-second-tom
pnpm init
# Create pnpm-workspace.yaml, tsconfig.json, vitest.config.ts
# Create packages/cli, packages/core, packages/slack-bot, packages/schemas
# Set up TypeScript strict mode, ESLint, Prettier
```

### Task 2: Build Core Package
Implement types (Entry, Decision, TeamVault, SentimentScore, WeeklyDigest), StorageService (better-sqlite3), SentimentService (natural), LLM provider abstraction (Anthropic SDK), and TranscriptionService (@xenova/transformers).

### Task 3: Build CLI Shell
Ink app with command routing. Start with `tom setup` and `tom record`. Use commander for arg parsing, Ink for rendering.

### Task 4: Add Audio + Transcription
node-record-lpcm16 for capture, @xenova/transformers for Whisper STT. Test on macOS with Metal fallback.

### Task 5: Build Team Features
TeamVault, `tom retro`, `tom decision`, `tom digest` with Claude extended thinking.

### Priority Order
1. Core types + storage (foundation)
2. CLI shell + setup wizard (developer can interact)
3. Audio recording + transcription (core feature)
4. Sentiment analysis (value-add)
5. Team vault + retro + digest (team features)
6. Slack bot (Phase 2 start)

---

*Prepared by the R&D Council: Vega (Product), Sentinel (Risk), Flux (UX), Volt (Architecture), Nova (Innovation)*
*Decision authority: Chris Kirby*
*Date: March 22, 2026*
