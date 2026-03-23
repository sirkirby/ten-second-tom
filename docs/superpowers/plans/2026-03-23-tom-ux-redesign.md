# Tom UX Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert Tom from one-shot CLI commands into a persistent Ink TUI application with a REPL prompt, using Ink's `<Static>` component for scroll history.

**Architecture:** Single `<App>` component manages all screens via state. `<Static>` pushes completed command output into the terminal's scroll buffer. Active screen re-renders in place below. Services constructed once at startup, shared across commands. One-shot mode detected via argv.

**Tech Stack:** Ink 5 (Static, useInput, useApp), ink-text-input, Commander (one-shot arg parsing only), existing core services

**Spec:** `docs/superpowers/specs/2026-03-23-tom-ux-redesign.md`

---

## File Map

### New Files

| File | Purpose |
|------|---------|
| `packages/cli/src/app.tsx` | Root `<App>` component — screen state, Static history, service lifecycle |
| `packages/cli/src/screens/HomeScreen.tsx` | Header + config line + prompt with autocomplete |
| `packages/cli/src/screens/RecordingScreen.tsx` | Recording view — timer, live transcript, controls |
| `packages/cli/src/screens/ProcessingScreen.tsx` | Post-record — transcribing → analyzing → results |
| `packages/cli/src/screens/SearchScreen.tsx` | Search results with keyboard navigation |
| `packages/cli/src/screens/NoteScreen.tsx` | Text input with dictation toggle |
| `packages/cli/src/components/Prompt.tsx` | Reusable `tom ❯` prompt with autocomplete |
| `packages/cli/src/components/TranscriptBox.tsx` | Bordered transcript display |
| `packages/cli/src/components/ResultsSummary.tsx` | Compact results (sentiment + topics) for history |
| `packages/cli/src/commands/registry.ts` | Command registry (name, description, execute fn) |

### Modified Files

| File | Change |
|------|--------|
| `packages/cli/src/cli.ts` | Detect REPL vs one-shot, single `render(<App />)` |
| `packages/cli/src/hooks/useAutoExit.ts` | Only active in one-shot mode |

### Files to Delete (after migration)

| File | Reason |
|------|--------|
| `packages/cli/src/commands/record.tsx` | Logic moves to `screens/RecordingScreen.tsx` + `ProcessingScreen.tsx` |
| `packages/cli/src/commands/note.tsx` | Logic moves to `screens/NoteScreen.tsx` |
| `packages/cli/src/commands/search.tsx` | Logic moves to `screens/SearchScreen.tsx` |
| `packages/cli/src/commands/analyze.tsx` | Logic moves to `screens/ProcessingScreen.tsx` (reuse) |
| `packages/cli/src/components/RecordingUI.tsx` | Replaced by `screens/RecordingScreen.tsx` |
| `packages/cli/src/components/SearchResults.tsx` | Replaced by `screens/SearchScreen.tsx` |

### Files Unchanged

| File | Reason |
|------|--------|
| All `packages/core/src/**` | Core services untouched |
| `packages/cli/src/commands/setup.tsx` | Setup wizard keeps current UI |
| `packages/cli/src/components/SentimentDisplay.tsx` | Reused in new screens |
| `packages/cli/src/components/ErrorDisplay.tsx` | Reused in new screens |
| `packages/cli/src/components/WarningList.tsx` | Reused in ProcessingScreen and ResultsSummary |
| `packages/cli/src/utils/sentiment.ts` | Reused |

---

## Task 1: App Shell + Home Screen

**Files:**
- Create: `packages/cli/src/app.tsx`
- Create: `packages/cli/src/screens/HomeScreen.tsx`
- Create: `packages/cli/src/components/Prompt.tsx`
- Create: `packages/cli/src/commands/registry.ts`
- Modify: `packages/cli/src/cli.ts`
- Modify: `packages/cli/src/hooks/useAutoExit.ts`

This is the foundation — the single Ink app with screen routing, Static history, and the REPL prompt.

- [ ] **Step 1: Create command registry**

Create `packages/cli/src/commands/registry.ts`:

```typescript
import type { ServiceContainer, ConfigManager } from '@ten-second-tom/core';

export type Screen = 'home' | 'recording' | 'processing' | 'results' | 'search' | 'note' | 'setup';

export interface HistoryEntry {
  id: string;
  content: React.ReactNode;
}

export interface AppContext {
  services: ServiceContainer;
  configManager: ConfigManager;
  setScreen: (screen: Screen) => void;
  pushHistory: (output: HistoryEntry) => void;
  setScreenData: (data: Record<string, unknown>) => void;
  exit: () => void;
}

export interface TomCommand {
  name: string;
  description: string;
  execute: (args: string, context: AppContext) => void;
}

export const COMMANDS: TomCommand[] = [
  { name: 'record', description: 'Record audio with live transcription', execute: (_, ctx) => ctx.setScreen('recording') },
  { name: 'note', description: 'Create a text note (type or dictate)', execute: (_, ctx) => ctx.setScreen('note') },
  { name: 'search', description: 'Search entries by meaning or keyword', execute: (args, ctx) => { ctx.setScreenData({ query: args }); ctx.setScreen('search'); } },
  { name: 'analyze', description: 'Re-run analysis on an entry', execute: (args, ctx) => { ctx.setScreenData({ entryId: args }); ctx.setScreen('processing'); } },
  { name: 'setup', description: 'Configure Tom', execute: (_, ctx) => ctx.setScreen('setup') },
  { name: 'help', description: 'Show available commands', execute: (_, ctx) => {
    const lines = COMMANDS.filter(c => c.name !== 'help' && c.name !== 'quit')
      .map(c => `  ${c.name.padEnd(12)} ${c.description}`).join('\n');
    ctx.pushHistory({ id: `help-${Date.now()}`, content: lines });
  }},
  { name: 'quit', description: 'Exit Tom', execute: (_, ctx) => ctx.exit() },
];
```

Note: `quit` calls `ctx.exit()` which calls Ink's `useApp().exit()`. `help` pushes formatted command list to Static history.

- [ ] **Step 2: Create Prompt component**

Create `packages/cli/src/components/Prompt.tsx` — text input with `tom ❯` prefix, Tab autocomplete from command names, Enter dispatches command.

- [ ] **Step 3: Create HomeScreen**

Create `packages/cli/src/screens/HomeScreen.tsx` — renders header (title, config line, divider) + Prompt. Config line uses the mapping from the spec.

- [ ] **Step 4: Create App shell**

Create `packages/cli/src/app.tsx`:
- `Screen` type union
- `HistoryEntry` type for Static items
- `completedOutputs` state array for `<Static>`
- `screen` state for active view
- Render: `<Static>` with history + switch on `screen` for active view
- Construct `ServiceContainer` once on mount via `useEffect`
- Provide `AppContext` to screens

- [ ] **Step 5: Modify useAutoExit for one-shot mode**

Update `packages/cli/src/hooks/useAutoExit.ts` to accept an `enabled` parameter (default `true`). In REPL mode, `useAutoExit` is called with `enabled: false` so it never exits. In one-shot mode, it behaves as before (exits after delay). The App passes a `oneShot` flag to screens, which use it to conditionally enable auto-exit.

```typescript
export function useAutoExit(shouldExit: boolean, delayMs: number = AUTO_EXIT_DELAY_MS, enabled: boolean = true) {
  // ... existing logic, but early-return if !enabled
}
```

- [ ] **Step 6: Rewrite cli.ts**

Replace Commander routing with:
- If `process.argv` has a recognized command → parse with Commander, launch Ink in one-shot mode
- If no args → launch Ink in REPL mode (home screen)
- `render(<App mode={mode} initialCommand={cmd} />)`

- [ ] **Step 7: Verify**

Build, run `tom` → should show home screen with prompt. Type `help` → command list. Type `quit` → exits. Run `tom --help` → Commander help. All existing tests should still pass (old commands still exist, just not used by REPL yet).

- [ ] **Step 8: Commit**

```bash
git commit -m "feat: add App shell with home screen, REPL prompt, and command registry"
```

---

## Task 2: Recording Screen

**Files:**
- Create: `packages/cli/src/screens/RecordingScreen.tsx`
- Create: `packages/cli/src/components/TranscriptBox.tsx`

Recording as a screen within the app — timer, live transcript, Enter/Esc handling.

- [ ] **Step 1: Create TranscriptBox component**

Shared bordered box for displaying transcripts (single-line border per spec mockup):
```tsx
<Box borderStyle="single" paddingX={1}>
  <Text>{transcript}</Text>
</Box>
```

- [ ] **Step 2: Create RecordingScreen**

**RecordingScreen ONLY handles the recording phase** — mic capture + live preview. It does NOT do transcription or analysis.

Receives `services` and callbacks from App context:
- `onRecordingComplete(audioRelPath: string, liveTranscript: string)` — user pressed Enter, recording saved
- `onCancel()` — user pressed Esc

Phases: `init → recording` only. On Enter: stops recording, stops live transcription, calls `onRecordingComplete` with the audio path and live transcript text. The App then transitions to ProcessingScreen.

On Esc: stops recording, discards audio, calls `onCancel`. App returns to home.

Key difference from current: does NOT call `exit()`, `useAutoExit`, `transcribeFile`, or `runAnalysisPipeline`. Those happen in ProcessingScreen.

- [ ] **Step 3: Wire into App**

When `screen === 'recording'`, render `<RecordingScreen>`. On complete, push result to `completedOutputs` and set `screen = 'home'`.

- [ ] **Step 4: Verify**

`tom` → type `record` → should record, show live transcript, Enter to finish, results appear, prompt returns.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add RecordingScreen with live transcription as app view"
```

---

## Task 3: Processing / Results Screen

**Files:**
- Create: `packages/cli/src/screens/ProcessingScreen.tsx`
- Create: `packages/cli/src/components/ResultsSummary.tsx`

Handles post-recording processing AND re-analysis (`tom analyze`). Both run the same pipeline — transcribe → analyze → show results.

- [ ] **Step 1: Create ResultsSummary component**

Compact result display used in both active screen and Static history:
```
✓ Recording saved (0:34)
  [transcript box]
  optimistic and productive (+0.65)  82% confidence
  feature-development, deployment · update
```

- [ ] **Step 2: Create ProcessingScreen**

Accepts either:
- A recording result (audioPath + live transcript) for post-record processing
- An entry ID for re-analysis

Phases: `transcribing → analyzing → done`

On done: calls `onComplete(result)` to push to history and return home.

- [ ] **Step 3: Wire into App**

RecordingScreen completion → transitions to ProcessingScreen with the recording data. ProcessingScreen completion → pushes ResultsSummary to Static, returns to home.

- [ ] **Step 4: Verify**

Full flow: `record` → recording → processing → results in history → prompt.

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add ProcessingScreen with progressive transcription and analysis"
```

---

## Task 4: Search Screen

**Files:**
- Create: `packages/cli/src/screens/SearchScreen.tsx`

Search with keyboard navigation — arrow keys, Enter to expand, Esc to close.

- [ ] **Step 1: Create SearchScreen**

- Receives query from command args or shows a text input if no query
- Calls `SearchService.search(query)`
- Renders results with sentiment-colored left borders
- `useInput` for arrow keys (wrapping), Enter (expand), Esc (close)
- Expanded detail replaces list; Esc returns to list
- 0 results: "No entries found"
- On Esc from list: returns to home

- [ ] **Step 2: Wire into App**

`search <query>` → SearchScreen with query. Esc → home.

- [ ] **Step 3: Verify**

`tom` → `search deploy` → results with navigation → Esc → prompt.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add SearchScreen with keyboard navigation and detail view"
```

---

## Task 5: Note Screen

**Files:**
- Create: `packages/cli/src/screens/NoteScreen.tsx`

Text input with dictation toggle — inline within the app.

- [ ] **Step 1: Create NoteScreen**

- Text input below the prompt
- Tab toggles dictation mode (starts live transcription into the input)
- Enter submits → runs analysis pipeline → pushes result to history → returns home
- Esc cancels → returns home

- [ ] **Step 2: Wire into App**

`note` → NoteScreen. On submit: transitions through processing → results → home.

- [ ] **Step 3: Verify**

`tom` → `note` → type text → Enter → analysis → history → prompt.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add NoteScreen with text input and dictation toggle"
```

---

## Task 6: One-Shot Mode

**Files:**
- Modify: `packages/cli/src/cli.ts`
- Modify: `packages/cli/src/app.tsx`

Make `tom record`, `tom note`, etc. work as one-shot commands that exit after completion.

- [ ] **Step 1: Implement one-shot detection**

In `cli.ts`: check `process.argv` for a recognized command. If found, pass `initialCommand` to `<App>`.

In `app.tsx`: if `initialCommand` is set, execute it immediately on mount (skip home screen). After results display, auto-exit after 5 seconds.

- [ ] **Step 2: Keep Commander for --help and analyze args**

Commander still parses `tom analyze <entry-id>` and provides --help. But the action handler launches the unified App, not a standalone Ink render.

- [ ] **Step 3: Verify**

`tom record` → records → shows results → exits. `tom` → REPL stays open.

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add one-shot mode for tom record, tom note, etc."
```

---

## Task 7: Cleanup Old Commands

**Files:**
- Create: `packages/cli/src/pipeline.ts` (if `runAnalysisPipeline` is still in `record.tsx`)
- Delete: `packages/cli/src/commands/record.tsx`
- Delete: `packages/cli/src/commands/note.tsx`
- Delete: `packages/cli/src/commands/search.tsx`
- Delete: `packages/cli/src/commands/analyze.tsx`
- Delete: `packages/cli/src/components/RecordingUI.tsx`
- Delete: `packages/cli/src/components/SearchResults.tsx`
- Create: `packages/cli/src/screens/__tests__/RecordingScreen.test.tsx`
- Create: `packages/cli/src/screens/__tests__/SearchScreen.test.tsx`

- [ ] **Step 1: Move `runAnalysisPipeline` if still in record.tsx**

Check if `runAnalysisPipeline` and `PipelineResult`/`PipelineOptions` types are still exported from `record.tsx`. If so, move them to `packages/cli/src/pipeline.ts` (or `packages/core/src/services/`). Update all imports. This must happen BEFORE deleting `record.tsx`.

`buildServicesFromConfig` is already in `packages/core/src/services/service-factory.ts` — no action needed.

- [ ] **Step 2: Delete old command files**

Remove the standalone command files. Verify no remaining imports point to them.

- [ ] **Step 2: Delete replaced components**

Remove `RecordingUI.tsx` and `SearchResults.tsx` — replaced by screen components.

- [ ] **Step 3: Create new test files for screens**

Create test files:
- `packages/cli/src/screens/__tests__/RecordingScreen.test.tsx` — mock services, test: init starts recording + live transcription, Enter calls onRecordingComplete with audioPath, Esc calls onCancel
- `packages/cli/src/screens/__tests__/SearchScreen.test.tsx` — mock SearchService, test: renders results, arrow keys change selection, Enter expands detail, Esc from detail returns to list, Esc from list calls onClose, 0 results shows message, search with no query shows input
- `packages/cli/src/screens/__tests__/NoteScreen.test.tsx` — mock services, test: text submit triggers pipeline, empty input rejected, Tab toggle calls dictation start

Core pipeline orchestration (`runAnalysisPipeline`) is already tested in core — screen tests focus on user interaction and screen transitions, not business logic.

Delete old test files: `commands/__tests__/record.test.ts`, `note.test.ts`, `search.test.ts`, `analyze.test.ts`.

- [ ] **Step 4: Run `make check`**

Ensure lint, format, and all tests pass.

- [ ] **Step 5: Commit**

```bash
git commit -m "refactor: remove old standalone command files, migrate to screen components"
```

---

## Task 8: Polish

**Files:**
- Various screen and component files

- [ ] **Step 1: Native output suppression**

Ensure `toggleNativeLog(false)` is called during app init (before any whisper operations). Since the Ink app owns the terminal, native stderr should be less disruptive — but still call the suppression.

For `ggml_metal_free: deallocating` on exit: add a cleanup handler that releases the whisper context before Ink unmounts.

- [ ] **Step 2: Help command**

When user types `help` at the prompt, show a formatted list of commands with descriptions. Push to Static history.

- [ ] **Step 3: Config display**

Implement the config line mapping from the spec. Handle edge cases (no embedding, cloud vs local LLM). Add entry count from storage.

- [ ] **Step 4: Tab autocomplete**

In the Prompt component, intercept Tab key. If the current input partially matches a command name, complete it. If multiple matches, show options.

- [ ] **Step 5: Final `make check`**

Lint, format, all tests pass.

- [ ] **Step 6: Commit**

```bash
git commit -m "feat: polish — help command, config display, autocomplete, native log suppression"
```

---

## Verification Checklist

- [ ] `tom` — launches REPL with header, config line, prompt
- [ ] Type `record` → recording with live transcript → Enter → transcribing → analyzing → results in history → prompt
- [ ] Type `note` → text input → Enter → analysis → results → prompt
- [ ] Type `note` + Tab → dictation mode → live text → Enter → results → prompt
- [ ] Type `search deploy` → results with arrow nav → Enter to expand → Esc → Esc → prompt
- [ ] Type `search` (no args) → shows text input for query → type query → Enter → results
- [ ] Search results: arrow down past last item wraps to first
- [ ] Type `analyze <id>` → processing → results → prompt
- [ ] Type `help` → command list → prompt
- [ ] Type `quit` → exits
- [ ] Ctrl+C → exits cleanly
- [ ] `tom record` (one-shot) → records → results → auto-exits
- [ ] `tom --help` → Commander help text
- [ ] No whisper log spam during any operation
- [ ] No blank space above content
- [ ] Results from previous commands visible in scroll history
