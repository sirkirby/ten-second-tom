# Ten-Second Tom

An intelligence-first voice capture and analysis CLI for engineers.

## What It Does

Ten-Second Tom captures voice recordings and text notes, transcribes them locally with Whisper, analyzes sentiment and context with Claude AI (or local models), and provides semantic search across all your entries. Privacy-first — all data stays on your machine.

## Features

- **Voice recording** with real-time streaming transcription
- **Text notes** with optional voice dictation
- **AI-powered analysis** using Claude or local models (Ollama)
- **Semantic search** across all entries with vector embeddings
- **Local-first architecture** — your data never leaves your machine

## Prerequisites

- **Node.js 20+**
- **SoX** (for microphone capture): `brew install sox` on macOS, `choco install sox` on Windows
- **Anthropic API key** (for cloud analysis with Claude) OR **Ollama** (for local analysis)
- **Whisper model** (automatically downloaded on first use)

## Installation

### From npm

```bash
npm install -g @ten-second-tom/cli
```

Or with pnpm:

```bash
pnpm add -g @ten-second-tom/cli
```

### From Source

```bash
git clone https://github.com/your-org/ten-second-tom.git
cd ten-second-tom
pnpm install
pnpm build
pnpm -C packages/cli start
```

## Quick Start

```bash
# Configure your LLM, embedding, and STT preferences
tom setup

# Record audio with live transcription
tom record

# Create a text note (type or dictate)
tom note

# Search your entries
tom search

# View help for any command
tom --help
tom record --help
```

## Commands

### `tom setup`

First-run configuration wizard. Choose your LLM provider (cloud Claude via Anthropic API or local model via Ollama), configure embeddings, and download the Whisper STT model.

### `tom record`

Record audio from your microphone. Transcription happens in real-time. After recording, Tom analyzes the entry for sentiment and context, creates a vector embedding, and saves everything to your local database.

### `tom note`

Create a text-based note. You can type freely or use voice dictation (speech-to-text). Notes go through the same analysis and embedding pipeline as recordings.

### `tom search`

Search your entries using natural language. Uses semantic search (vector similarity) if embeddings are enabled, falls back to keyword search otherwise. Results show relevant entries ranked by similarity.

## Configuration

Tom stores all configuration and data in `~/.tom/`:

```
~/.tom/
├── config.json          # LLM, embedding, and STT settings
├── data.db             # SQLite database with entries and embeddings
└── recordings/         # Audio files from `tom record`
```

Run `tom setup` to configure your LLM provider and embedding strategy. The setup wizard walks through:

1. **LLM Provider**: Claude via Anthropic API (cloud) or local model via Ollama
2. **Embedding Provider**: Ollama (local vectors), Voyage AI (cloud), or keyword-only search
3. **STT Model**: Whisper (downloaded automatically)

For cloud analysis, you'll need an [Anthropic API key](https://console.anthropic.com/). For local analysis, [install Ollama](https://ollama.ai) and pull a model (e.g., `ollama pull qwen2.5:7b`).

## Development

### Install Dependencies

```bash
pnpm install
```

### Run Tests

```bash
pnpm test       # Run all tests once
pnpm test:watch # Watch mode
```

### Build

```bash
pnpm build   # Build all packages
```

### Lint & Format

```bash
pnpm lint        # Check linting
pnpm format      # Format code
pnpm format:check # Verify formatting
```

### Dev Mode

```bash
pnpm -C packages/cli dev   # Watch + rebuild CLI
pnpm -C packages/core dev  # Watch + rebuild core
```

## Architecture

Tom follows a monorepo structure:

- **`packages/cli/`** — Terminal UI (Ink + React) and command routing (Commander)
- **`packages/core/`** — Business logic: transcription, analysis, embedding, search, and storage

See [CLAUDE.md](./CLAUDE.md) for detailed architecture and coding standards.

## License

MIT. See [LICENSE](./LICENSE) for details.
