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

### No Screen Clearing

Content is additive. When a command completes, its results stay visible above the next prompt. Ink manages rendering without full-screen clears. The terminal scrolls naturally.

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

- Progressive: "Transcribing..." appears first, transcript fills in, then "Analyzing..." appears
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
- Arrow keys navigate, Enter expands to full detail, Esc returns to prompt

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

When `tom record` is invoked (with a subcommand), the app launches, executes the command, displays results, and exits after a short delay. Detection: `process.argv` has a command → one-shot mode. No args → REPL mode.

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

## What Doesn't Change

- Core package services (storage, transcription, agent, embedding, search)
- Setup wizard (keeps current Ink UI)
- Service factory (`buildServicesFromConfig`)
- Data model, config, constants
- Tests for core services
