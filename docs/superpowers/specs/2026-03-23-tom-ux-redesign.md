# Ten-Second Tom — UX Redesign Spec

**Date:** 2026-03-23
**Status:** Approved

---

## Overview

Redesign Tom from a collection of one-shot CLI commands into a persistent Ink TUI application with a REPL prompt. The app renders as a series of views managed by Ink. `tom` with no args launches the REPL. `tom record`, `tom note`, etc. still work as one-shot commands for scripting.

## Interaction Model

### REPL as Primary Experience

`tom` launches a persistent Ink application. The user types commands at a `tom ❯` prompt. Commands execute within the app, results display inline, and the prompt returns. The app stays running until the user types `quit` or presses Ctrl+C.

One-shot commands (`tom record`, `tom note`, `tom search`, `tom analyze`, `tom setup`) continue to work — they launch the Ink app, execute the command, display results, and exit.

### Ink Rendering Model

A single long-lived `<App>` component manages all screens via internal state. There is one `render()` call for the entire app lifecycle — no multiple render/unmount cycles.

Ink re-renders its output area in place. The app uses Ink's `<Static>` component to push completed command output into the terminal's scroll buffer. The active area (current screen + prompt) renders below the static output. This gives the appearance of additive content: completed results scroll up naturally while the active prompt stays at the bottom.

Pattern:
```
<Static items={completedOutputs}>  ← scrolls up, stays in terminal buffer
  {item => <CompletedResult ... />}
</Static>
<ActiveScreen />                   ← re-renders in place at bottom
```

This is Ink's supported model for REPL-style applications.

## Screens

### Home Screen

```
Ten-Second Tom v2.0
gpt-oss · whisper · bge-m3 · 12 entries
───────────────────────────────
tom ❯ _
```

- Title with version
- Terse config line: model names and entry count, no labels
- Thin divider
- `tom ❯` prompt with text input
- Tab autocomplete for commands
- Shown on launch and after each command completes

### Recording View

```
● RECORDING — 0:34

  Live preview
  Okay, doing another live recording test. Live transcription
  is coming through beautifully...

  Enter to finish · Esc to cancel
```

- Red recording indicator with timer
- Live transcript from sherpa-onnx below, labeled "Live preview" in dim text
- Transcript in a left-bordered block for visual distinction
- Controls hint at bottom
- Replaces the home screen while active — no blank space above

### Processing View (after Enter)

```
✓ Recording saved

  Transcribing...
  ┌─────────────────────────────────────────────┐
  │ Full whisper transcript appears here...      │
  └─────────────────────────────────────────────┘

  Analyzing...
```

- Progressive via React state updates: `phase` state drives what's visible. "Transcribing..." renders first. When transcription completes, `transcript` state is set → Ink re-renders to show the text. Then `phase` moves to "analyzing" → Ink re-renders to add the spinner. Standard React state-driven rendering — no manual stdout writes.
- Each step shows as it completes — not all at once

### Results View

```
✓ Recording saved (0:34)

  ┌─────────────────────────────────────────────┐
  │ Full transcript text...                      │
  └─────────────────────────────────────────────┘

  optimistic and productive (+0.65)  82% confidence
  feature-development, deployment · update
───────────────────────────────
tom ❯ _
```

- Compact results: transcript in a box, sentiment colored (green/yellow/red), topics inline
- Divider, then prompt returns
- Results stay visible above the prompt

### Search View

```
Search: "deploy pipeline"

  ▎ Mar 23  +0.65  Okay, doing another live recording...
  ▎ Mar 22  -0.25  The deploy pipeline broke again...
  ▎ Mar 20  +0.10  We need to think about the deploy...

  ↑↓ navigate · Enter to expand · Esc to close
```

- Left border colored by sentiment (green/red/yellow)
- Date, score, excerpt on one line per result
- Arrow keys navigate (wraps at top/bottom), Enter expands to full detail below the list, Esc returns to prompt
- 0 results: shows "No entries found" message with prompt
- User cannot edit the query from results — Esc to close, then search again
- Expanded detail replaces the list; Esc from detail returns to list

### Note View

```
tom ❯ note
  Type your note (Enter to save, Tab for dictation):
  > _
```

- Inline text input, no full-screen takeover
- Tab toggles dictation mode (shows live transcription in the input area)
- On submit: shows analysis results same as recording results view

### Setup Wizard

Keeps current Ink-based multi-step wizard UI. No changes to setup — it already works well as a full-screen interactive form.

## Architecture

### Single Ink App

One `<App>` component manages all views via a `screen` state:

```typescript
type Screen = 'home' | 'recording' | 'processing' | 'results' | 'search' | 'note' | 'setup';
```

The app component renders the active screen. Screen transitions are state changes, not process exits.

### REPL Prompt Component

A `<Prompt>` component handles:
- Text input with `ink-text-input`
- Command parsing (split on first space: command + args)
- Tab autocomplete from command registry
- Command dispatch → screen transition

### Command Registry

Commands are registered as objects:

```typescript
interface TomCommand {
  name: string;
  description: string;
  execute: (args: string, context: AppContext) => void;
}
```

`AppContext` provides access to services, screen transitions, and output rendering.

### Service Lifecycle

Services are constructed once on app startup (not per-command). The `ServiceContainer` is created from config and passed through context. Storage connection stays open for the app's lifetime, closed on exit.

### One-Shot Mode

When `tom record` is invoked (with a subcommand), the app launches, skips the home screen, executes the command, displays results, and exits automatically after 5 seconds (using `useAutoExit`). No user interaction required to exit.

Detection: `process.argv` has a recognized command → one-shot mode. No args → REPL mode. `tom record --help` shows command help via Commander (before Ink launches).

One-shot commands do not accept additional positional args beyond what's defined (e.g., `tom analyze <entry-id>`). Commander handles arg parsing before the Ink app starts.

## Native Output Suppression

The whisper.cpp native logging issue is addressed by:

1. `toggleNativeLog(false)` before all whisper operations
2. The Ink app owns the terminal — native writes to stderr are captured by Ink's output management
3. The `ggml_metal_free: deallocating` message on exit: suppress by calling whisper context release explicitly before process exit, or accept it as a known cosmetic issue

## What Changes

| Component | Current | New |
|-----------|---------|-----|
| `cli.ts` | Commander routes to separate Ink renders | Single Ink app with internal routing |
| Commands | Each is a standalone Ink component + Commander action | Each is a view within the app |
| Services | Constructed per-command, closed on exit | Constructed once, shared across commands |
| Prompt | None (Commander handles args) | Ink TextInput with autocomplete |
| Screen management | Each command owns the full terminal | App component manages screen state |
| One-shot | Only mode | Detected via argv, app exits after command |

## Error Handling

- **Transcription fails mid-recording**: recording continues (audio is still being captured). On stop, entry is saved without transcript. Warning shown in results view.
- **Analysis/embedding fails**: entry saved without enrichment. Warning shown inline in results view (same as current behavior).
- **Crash during recording**: audio buffer is lost (not yet saved to disk). This is acceptable — `stopRecording()` is what triggers the WAV save.
- **Service unavailable at startup**: home screen shows degraded config line (e.g., missing model name replaced with "unavailable"). Commands that need the missing service show an error when invoked, not at startup.

## Config Display Mapping

```
config.llm.provider === 'cloud' → "Claude"
config.llm.provider === 'local' → config.llm.modelId (e.g., "gpt-oss")
"whisper" → always shown (hardcoded, all users use whisper)
config.embedding.provider === 'none' → omitted from config line
config.embedding.provider !== 'none' → config.embedding.model (e.g., "bge-m3")
entry count → SELECT COUNT(*) from entries
```

## What Doesn't Change

- Core package services (storage, transcription, agent, embedding, search)
- Setup wizard (keeps current Ink UI)
- Service factory (`buildServicesFromConfig`)
- Data model, config, constants
- Tests for core services
