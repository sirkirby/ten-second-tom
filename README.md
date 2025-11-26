# Ten Second Tom

```bash
 _____               ____                           _   _____
|_   _|__ _ __      / ___|  ___  ___ ___  _ __   __| | |_   _|__  _ __ ___
  | |/ _ \ '_ \     \___ \ / _ \/ __/ _ \| '_ \ / _` |   | |/ _ \| '_ ` _ \
  | |  __/ | | |     ___) |  __/ (_| (_) | | | | (_| |   | | (_) | | | | | |
  |_|\___|_| |_|    |____/ \___|\___\___/|_| |_|\__,_|   |_|\___/|_| |_| |_|

                    Your personal memory assistant
```

**Ten Second Tom** is a CLI application for personal memory management that guides you through daily reflection prompts, leverages AI to generate structured summaries, and builds a searchable archive of your experiences. Named after the character from the movie *50 First Dates*, Ten Second Tom helps you remember what matters.

[![PR Validation](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml)
[![Release](https://github.com/sirkirby/ten-second-tom/actions/workflows/release.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/sirkirby/ten-second-tom?sort=semver)
[![brew install](https://img.shields.io/badge/brew_install-sirkirby/ten--second--tom/ten--second--tom-informational)](https://github.com/sirkirby/homebrew-ten-second-tom)

---

## ✨ Features

- 🧠 **Simplified Daily Reflections**: Single free-form text entry to capture your thoughts
- 📝 **Quick Capture**: Save notes instantly without AI processing for raw thoughts and meeting notes
- 🎤 **Audio Recording**: Record audio with local-first speech-to-text transcription via `/record`
- 🔁 **Standalone Transcription**: Transcribe audio files with `/transcribe` command
- 📊 **Weekly Reviews**: AI-generated summaries of your week with themes and patterns
- 🔍 **Searchable Archive**: Full-text search across all your memories (including transcripts)
- 🤖 **Multiple AI Providers**: Support for OpenAI, Anthropic, and local LLMs (Ollama, LM Studio, etc.)
- 📝 **Custom Templates**: Create and edit prompt templates to personalize your summaries
- 📁 **Markdown Storage**: Human-readable files in configured memory directory
- 🔐 **SSH Authentication**: Secure session management with SSH keys and local SSH agents
- 🎨 **Beautiful Terminal UI**: Rich formatting with Spectre.Console
- 📤 **JSON Output**: Programmatic access for automation and integrations
- ⏰ **Auto-Purge**: Configurable data retention policies

### 🔒 Privacy-First: Fully Offline Capable

Ten Second Tom can operate **100% offline** with **zero cloud dependencies**:

- **Built-in Speech-to-Text**: Whisper.NET is bundled - no external installation required
- **Local LLM Processing**: Run models via Ollama, LM Studio, or llama.cpp, which support local OpenAI-compatible APIs
- Use advanced open source reasoning models, like `bytedance/seed-oss` and `microsoft/phi-4-reasoning-plus` for your most complex notes or `openai/gpt-oss` and `qwen/qwen3-next-80b` to optimize for speed without compromising quality. Size the model to your hardware capabilities.
- **Your data stays on your device** - no API calls, no internet required

Perfect for sensitive work, offline environments, or privacy-conscious users.

---

## 📋 Prerequisites

### Core Requirements

- **.NET 10 SDK** or later ([Download](https://dotnet.microsoft.com/download)) - for building from source
- **LLM Provider** (choose one):
  - **OpenAI API Key** - for cloud-based summaries with GPT models
  - **Anthropic API Key** - for cloud-based summaries with Claude models
  - **Local LLM** ([Ollama](https://ollama.ai/), [LM Studio](https://lmstudio.ai/), etc.) - for private, offline summaries (no API key needed)
- **SSH Key** (Ed25519 or RSA) in `~/.ssh/` - for secure authentication

### Audio Recording Requirements (Optional)

- **FFmpeg** ([Download](https://ffmpeg.org/)) - **REQUIRED** for audio recording
  - macOS: `brew install ffmpeg` (automatically installed with Homebrew tom package)
  - Linux: `sudo apt install ffmpeg` (Ubuntu/Debian) or `sudo yum install ffmpeg` (RHEL/CentOS)
  - Windows: Download from [ffmpeg.org](https://ffmpeg.org/download.html)
- **Local STT** - Built-in! No external installation required
  - Ten Second Tom includes Whisper.NET for local, privacy-focused transcription
  - Models are downloaded automatically on first use, or via CLI:
    ```bash
    tom transcribe models                     # See available models
    tom transcribe models --download base.en  # Download a specific model (142 MB)
    ```
  - Models are stored in `~/.cache/whisper-net/`

**Note**: Homebrew installation automatically installs FFmpeg as a required dependency. Local STT is built-in - no additional installation needed. OpenAI API is also available as an alternative.

---

## 📋 Command Reference

### note Command

Capture quick notes without AI processing. Perfect for raw thoughts, meeting notes, or anything you want to save immediately without LLM summarization.

```bash
tom note [content] [options]

Arguments:
  content                      Note content (optional). If omitted, opens interactive editor.

Options:
  --no-edit                    Skip interactive editor, use content from command line
  --output-json                Output results in JSON format

Subcommands:
  tom note list                List all available notes

Examples:
  tom note                                            # Interactive mode (opens editor)
  tom note "Quick thought" --no-edit                  # Quick capture mode
  tom note list                                       # List all available notes
  tom note list --output-json                         # List notes in JSON format
```

**Storage**: Notes are saved to `<memory-dir>/note/` directory with filename `MM-DD-YYYY_N.md` where N is an incremental counter for that day.

**Use Cases**:

- Quick capture without waiting for AI processing
- Meeting notes that don't need summarization
- Raw thoughts you want to preserve exactly as written

**Note**: For audio recording with transcription, use the `/record` command instead.

### generate Command

Generate AI-powered summaries from notes or recordings using prompt templates. The workflow is interactive: select your source, choose a template, then receive the generated output.

```bash
tom generate <subcommand> [options]

Subcommands:
  tom generate note            Generate from a note (interactive selection)
  tom generate recording       Generate from a recording (interactive selection)

Options (for both subcommands):
  --template <id>              Use specific template by ID (skips template selection)
  --output-json                Output results in JSON format

Examples:
  tom generate note                          # Select note → select template → generate
  tom generate recording                     # Select recording → select template → generate
  tom generate note --template daily-summary # Select note, use specific template
  tom generate recording --template organize # Select recording, use specific template
```

**Workflow**: Each subcommand presents an interactive list to select your source (note or recording), then a template selection, then generates the output using your configured LLM provider.

**Storage**: Output is saved alongside the source file with a `_generated` suffix (e.g., `01-21-2025_1_generated.md`).

**Available Templates**:

- `daily-summary` - Retrospective reflection (Key Events, Themes, To-Do Items)
- `daily-standup` - Forward-looking planning (Priorities, Blockers, Success Criteria)
- `journal` - Emotional reflection and insights
- `organize` - Task extraction and action items
- `weekly-review` - Weekly retrospective
- `business-meeting` - Meeting summaries

### Audio Recording

Use the `/record` command to record audio with automatic speech-to-text transcription:

```bash
# Start recording with transcription
tom record                                # Auto STT selection
tom record --stt local                    # Force local Whisper.NET
tom record --stt openai                   # Force OpenAI Whisper API

# List existing recordings
tom record list                           # List all recordings
tom record list --output-json             # List in JSON format
```

**STT Engine Selection:**

- `auto` (default): Uses configured local provider (whisper-net or whisper-cpp)
- `local`: Forces local provider (privacy-focused, no API calls)
- `openai`: Forces OpenAI Whisper API (cloud-based)

**Workflow:**

1. Use `/record` to capture audio with transcription
2. Use `/generate` to create AI-powered summaries from recordings

**Prerequisites:**

- **FFmpeg** for audio recording ([Download](https://ffmpeg.org/))
- **Local STT** is built-in (Whisper.NET) - no installation needed, OR use **OpenAI API** for cloud transcription

**Storage Note:** Audio recordings are ~940KB/minute. A 5-minute recording uses ~4.7MB.

**Audio Configuration:** Ten Second Tom includes automatic silence removal and noise reduction optimized for laptop microphones. For professional mics or custom settings, see the [Audio Configuration](#audio-configuration) section below.

**Legal Guidance:** Ten Second Tom is designed for single-user personal use on your own device. Do not record conversations without consent.

### record Command

Record and save audio with transcription for later use with the `generate` command:

```bash
tom record [options]

Options:
  --stt <engine>               STT engine: auto (default), local, or openai
  --output-json                Output results in JSON format

  # Per-recording audio overrides (override configured defaults):
  --input-volume <float>       Input volume multiplier (0.0 to 2.0)
  --noise-reduction <bool>     Enable/disable noise reduction
  --frequency-filters <bool>   Enable/disable frequency filters
  --remove-silence <bool>      Enable/disable silence removal
  --silence-threshold-db <int> Silence detection threshold in dB (-60 to -40)
  --min-silence-duration-ms <int> Minimum silence duration to remove (100 to 2000)

Subcommands:
  tom record list   List all available recordings

Examples:
  tom record                           # Start recording with auto STT
  tom record --stt local               # Record with local Whisper.NET
  tom record --noise-reduction false   # Disable noise reduction for this recording
  tom record --remove-silence false    # Disable silence removal for this recording
  tom record --input-volume 1.5        # Boost input volume for this recording
  tom record list                      # List all recordings
  tom record list --output-json        # List recordings in JSON format

Files saved to:
  <memory-dir>/recording/MM-dd-yyyy_N.wav
  <memory-dir>/recording/MM-dd-yyyy_N.md
```

**Note:** The `record` command requires SSH authentication (like other commands that create data). Recordings are saved with metadata in YAML frontmatter including timestamp, duration, STT engine used, and word count.

### transcribe Command

Transcribe any saved audio file—whether it originated from `tom record` or an external `.wav`—without creating a new recording session. Also manages STT model downloads and transcription configuration.

```bash
tom transcribe [options]

Options:
  --recording <name>    Recording base name to re-transcribe (reads from recording/ directory)
  --file <path>         Import an arbitrary .wav file from anywhere on disk
  --name <name>         Override the destination recording name (defaults to source base name)
  --stt <engine>        STT engine: auto (default), local, or openai
  --force               Overwrite existing transcript/audio if the destination already exists
  --output-json         Output results in JSON format

Subcommands:
  tom transcribe list              List available recording audio files
  tom transcribe models            List and download STT models
  tom transcribe config            Configure transcription settings (STT provider, keep files, etc.)

Examples:
  tom transcribe --recording 10-20-2025_2 --force # Re-run STT on an existing recording
  tom transcribe --file ~/Desktop/meeting.wav     # Import a standalone WAV file
  tom transcribe list                             # List available recordings
  tom transcribe list --output-json               # List in JSON format
  tom transcribe models                           # List available STT models
  tom transcribe models --download base.en        # Download a specific model
  tom transcribe config                           # Configure STT settings interactively
```

**Storage:** Transcripts are written to `<memory-dir>/recording/<name>.md` with YAML frontmatter, matching the files expected by the `/generate` workflow.

### audio Command

View and configure audio recording settings:

```bash
tom audio [options]

Options:
  --output-json         Output results in JSON format

Subcommands:
  tom audio config      Configure audio recording settings interactively

Examples:
  tom audio                   # Show current audio configuration
  tom audio --output-json     # Show config in JSON format
  tom audio config            # Configure audio settings interactively
  tom audio config --input-volume 0.8   # Set input volume directly
```

**Available Config Options:**

- `--record-timeout` - Recording timeout in seconds (60-86400)
- `--input-volume` - Input volume multiplier (0.0 to 2.0)
- `--noise-reduction` - Enable/disable noise reduction (true/false)
- `--frequency-filters` - Enable/disable frequency filters (true/false)

### Audio Configuration

Ten Second Tom provides extensive audio configuration for different microphone types and recording scenarios. Configuration is split between:

1. **Audio recording settings** (`tom audio config`) - Input volume, noise reduction, frequency filters, silence removal
2. **Transcription settings** (`tom transcribe config`) - STT provider selection, model management, keep files option
3. **Environment variables** - For advanced users and CI/CD
4. **Configuration file** (`~/ten-second-tom/config/config.json`) - Automatically managed by setup wizards

#### Key Features

- **STT Provider Selection**: Choose between built-in Whisper.NET, external whisper-cpp, or OpenAI API (configure via `tom transcribe config`)
- **Microphone Optimization**: Presets for laptop/built-in mics, professional dynamic mics, condenser/USB mics, and studio setups
- **Silence Removal**: Automatically compress long silence gaps in recordings (enabled by default)
- **Noise Reduction**: Adaptive noise reduction during recording (enabled by default for laptop mics)
- **Frequency Filters**: High-pass/low-pass filters to remove rumble and hiss (enabled by default)

#### Quick Configuration Examples

**For Laptop/Built-in Microphones (Default):**

```bash
# No configuration needed - optimized by default!
tom record
```

**Per-Recording Overrides (CLI Flags):**

```bash
# Disable noise reduction for this recording only
tom record --noise-reduction false

# Disable silence removal for this recording only
tom record --remove-silence false

# Adjust input volume for quiet microphone
tom record --input-volume 1.5

# Combine multiple overrides
tom record --noise-reduction false --frequency-filters false --remove-silence false
```

**Permanent Configuration (Environment Variables):**

```bash
# For professional dynamic microphones (permanent setting)
export TenSecondTom__Audio__Recorder__InputVolume=0.75
export TenSecondTom__Audio__Recorder__EnableNoiseReduction=false
tom record

# More aggressive silence removal (permanent setting)
export TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-60
tom record
```

#### Available Settings

| Setting | Environment Variable | Default | Description |
|---------|---------------------|---------|-------------|
| STT Provider | `TenSecondTom__Audio__SttProvider` | `whisper-cpp` | Primary STT provider |
| STT Fallback | `TenSecondTom__Audio__SttFallbackEnabled` | `false` | Enable fallback provider |
| Fallback Provider | `TenSecondTom__Audio__SttFallbackProvider` | `null` | Secondary STT provider |
| Input Volume | `TenSecondTom__Audio__Recorder__InputVolume` | `1.0` | Volume multiplier (0.0-2.0) |
| Noise Reduction | `TenSecondTom__Audio__Recorder__EnableNoiseReduction` | `true` | Adaptive noise filter |
| Frequency Filters | `TenSecondTom__Audio__Recorder__EnableFrequencyFilters` | `true` | High/low-pass filters |
| Remove Silence | `TenSecondTom__Audio__Preprocessing__RemoveSilence` | `true` | Compress silence gaps |
| Silence Threshold | `TenSecondTom__Audio__Preprocessing__SilenceThresholdDb` | `-50` | Detection threshold (dB) |
| Min Silence Duration | `TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs` | `500` | Minimum silence (ms) |

**For complete configuration guide, microphone presets, and troubleshooting:** See [docs/AUDIO.md](docs/AUDIO.md)

---

## 🚀 Installation

### macOS (Homebrew)

```bash
brew tap sirkirby/ten-second-tom
brew install ten-second-tom
```

### Windows (winget)

```bash
winget install TenSecondTom
```

### Linux / From Source

```bash
git clone https://github.com/sirkirby/ten-second-tom.git
cd ten-second-tom
dotnet build -c Release
dotnet publish -c Release -o /usr/local/bin/tom
```

---

## ⚙️ Configuration

### API Keys Setup

### LLM Provider & Model Selection

Ten Second Tom supports multiple Large Language Model (LLM) providers including cloud-based and local options. You can configure both the provider and model using any of these methods:

1. Interactive setup wizard (`tom config all`)
2. Configuration command (`tom config llm`)
3. Environment variables (advanced / CI)

#### Interactive Selection (Recommended)

Run either the setup wizard or the config command:

```bash
tom config all    # Guided initial configuration (includes provider + model selection)
tom config llm    # Re-run provider/model selection any time
```

You'll first select a provider, then a curated list of models is displayed with cost tier and description:

```bash
Select an LLM provider:
  ▸ OpenAI
    Anthropic
    Local (OpenAI Compatible)

Select a model (OpenAI):
  ▸ GPT-5.1 (gpt-5.1) [Premium] - Flagship model for coding, reasoning, and agentic tasks
    GPT-5 Mini (gpt-5-mini) [Balanced] - Faster, cost-efficient version for well-defined tasks
    GPT-5 Nano (gpt-5-nano) [Budget] - Fastest, cheapest model for summarization and classification
```

For local LLMs, you'll be prompted to configure your server URL (e.g., `http://localhost:11434/v1` for Ollama) and select from available models.

Current selection is highlighted when reconfiguring so you can easily compare or switch.

#### Environment Variable Configuration

You can override the configured model (and/or provider) at runtime without modifying user secrets:

```bash
export TenSecondTom__LlmProvider="Anthropic"
export TenSecondTom__Llm__Model="claude-sonnet-4-5"
```

Environment variable values take precedence over user secrets and `appsettings.json`.

#### Supported Providers & Models

| Provider   | Model ID              | Display Name          | Cost Tier | Default | Description |
|------------|-----------------------|-----------------------|-----------|---------|-------------|
| OpenAI     | `gpt-5-nano`         | GPT-5 Nano           | Budget    |         | Fastest, cheapest model for summarization and classification |
| OpenAI     | `gpt-5-mini`         | GPT-5 Mini           | Balanced  | ✅       | Faster, cost-efficient version for well-defined tasks |
| OpenAI     | `gpt-5`              | GPT-5 Standard       | Premium   |         | Flagship model for coding, reasoning, and agentic tasks |
| Anthropic  | `claude-haiku-4-5`   | Claude Haiku 4.5     | Budget    | ✅       | Fast and compact model for near-instant responsiveness |
| Anthropic  | `claude-sonnet-4-5`  | Claude Sonnet 4.5    | Balanced  |         | Best model for complex agents and coding with highest intelligence |
| Anthropic  | `claude-opus-4-5`    | Claude Opus 4.5      | Premium   |         | Exceptional model for specialized complex tasks requiring advanced reasoning |
| Local      | Any model ID         | User-defined         | Free      | N/A     | Run local models via Ollama, LM Studio, or any OpenAI-compatible server |

**Notes:**

- Default model (per provider) is used automatically if you leave the model blank during setup.
- Validation occurs at startup; an invalid provider/model combination produces a clear error with valid suggestions.
- Model IDs must match the configured provider (e.g., GPT models require OpenAI provider).
- **Local LLMs**: Model names are not validated - use any model ID from your local server (e.g., `gpt-oss:latest`, `phi4:latest`, `qwen3:8b`, `gemma3:latest`).
- **Local LLMs**: Requires an OpenAI-compatible API server (Ollama, LM Studio, etc.) running locally.
- Deprecated models from previous versions are no longer supported - use `tom config llm` to select from current models.
- Additional models may be added over time; run `tom config llm` to view the current curated list.

#### Viewing Current Configuration

```bash
tom config show
```

Example LLM section in output:

```bash
LLM Configuration
  Provider : OpenAI
  Model    : gpt-5-mini (Balanced)
```

#### Changing Just the Model Quickly

```bash
tom config llm
```

This re-runs only the provider/model selection flow—other settings remain unchanged.

#### Local LLM Configuration

Ten Second Tom supports running **completely offline** using local LLM servers that provide an OpenAI-compatible API.

**Supported Local Servers:**

- [Ollama](https://ollama.ai/) - Easy-to-use local LLM runner
- [LM Studio](https://lmstudio.ai/) - GUI-based local model manager
- [llama.cpp](https://github.com/ggerganov/llama.cpp) - Direct model inference
- Any OpenAI-compatible API server

**Quick Start with Ollama:**

1. **Install Ollama and pull a model:**
   - Follow the [Ollama Quickstart Guide](https://github.com/ollama/ollama#quickstart) for installation and model setup
   - Server runs at `http://localhost:11434` by default

2. **Configure Tom:**

   ```bash
   tom config llm --provider LocalOpenAiCompatible --model gpt-oss:latest
   # Or run interactively:
   tom config llm
   # Select "Local (OpenAI Compatible)"
   # Enter API URL: http://localhost:11434/v1
   # Select your model
   ```

3. **Use Tom offline:**

   ```bash
   tom record  # Record with local STT + local LLM = 100% offline!
   tom generate --recording 10-21-2025_1  # Generate summary from recording
   ```

**Configuration via Command Line:**

```bash
# Quick model switch (preserves your BaseUrl)
tom config llm --provider LocalOpenAiCompatible --model gpt-oss:latest

# Change both server and model
tom config llm  # Use interactive mode for full configuration
```

**Configuration via Environment:**

```bash
export TenSecondTom__Llm__Provider="LocalOpenAiCompatible"
export TenSecondTom__Llm__Model="gpt-oss:latest"
export TenSecondTom__Llm__Providers__LocalOpenAiCompatible__BaseUrl="http://localhost:11434/v1"
```

**Popular Model Recommendations:**

| Model | Size | Best For | Notes |
|-------|------|----------|-------|
| `gpt-oss:latest` | ~2GB | **Recommended** - All tasks | Best tested model for Tom - excellent quality and speed ⭐ |
| `phi4:latest` | 1.6GB | Summaries, daily entries | Fast and efficient, great for quick tasks |
| `qwen3:8b` | 4.9GB | Reasoning, weekly reviews | Strong analytical capabilities |
| `gemma3:latest` | 2-9GB | General purpose | Reliable across different tasks |
| `llama3:latest` | 4.7GB | General purpose | Solid balance of speed and quality |

**Performance Tips:**

- **First run is slow**: Models load into memory (30-60 seconds for 7B models)
- **Subsequent runs are fast**: Models stay loaded in memory
- **Adjust timeout if needed**: Long recordings may need more time (already configured to 15 minutes)
- **Check model size**: Ensure you have enough RAM (8GB+ recommended for 7B models)

**Switching Back to Cloud:**

```bash
tom config llm --provider OpenAI --model gpt-5-mini
# Or Anthropic
tom config llm --provider Anthropic --model claude-haiku-4-5
```

---

**Option 1: .NET User Secrets** (recommended for development)

```bash
cd /path/to/ten-second-tom/src
dotnet user-secrets init
dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "sk-your-openai-key"
# OR
dotnet user-secrets set "TenSecondTom:Anthropic:ApiKey" "sk-ant-your-anthropic-key"
```

**Option 2: Environment Variables** (recommended for production)

```bash
export TenSecondTom__OpenAI__ApiKey="sk-your-openai-key"
# OR
export TenSecondTom__Anthropic__ApiKey="sk-ant-your-anthropic-key"
```

**Option 3: Interactive Setup Wizard** (recommended for end users)

On first run, Ten Second Tom will automatically launch a guided setup wizard:

```bash
tom record
# → Setup wizard launches automatically if not configured
```

Or manually run the setup wizard:

```bash
tom config all
```

The setup wizard will guide you through:

1. SSH key selection (auto-detected from your system)
2. LLM provider selection (OpenAI or Anthropic)
3. API key configuration with validation
4. Memory storage location
5. Optional settings (logging, data retention)

**Configuration Management:**

View your current configuration:

```bash
tom config show
```

Update individual settings:

```bash
tom config set llm-provider Anthropic
tom config set api-key "your-new-key"
```

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for complete configuration guide.

**Configuration Storage:**

All user configuration is stored in `~/ten-second-tom/config/config.json` and managed automatically by the setup wizard. The shipped application files contain only logging configuration.

Configuration precedence (highest to lowest):

1. **Environment variables** - Runtime overrides
2. **User configuration** (`~/ten-second-tom/config/config.json`) - Managed by `tom config`
3. **Shipped defaults** (logging only)

⚠️ **Never commit API keys to version control!** Use the setup wizard or environment variables for secrets. See [SECURITY.md](SECURITY.md) for more details.

### Storage Providers

Ten Second Tom supports **multiple storage providers** to fit your workflow. Choose where to store your memories during the setup wizard.

#### Available Providers

**Default File System** (Recommended for new users)

- TST-native hierarchical structure optimized for organization
- Stores entries in: `note/`, `thisweek/`, `templates/`, `config/`
- Perfect for standalone use

**Obsidian Vault Integration**

- Store entries directly in your Obsidian vault
- Bidirectional sync: changes in either app appear in both
- Obsidian-friendly naming: `"2025-10-28 Entry 1.md"`
- Optional subdirectory isolation (e.g., `ten-second-tom/` within vault)
- Perfect for users who already manage notes in Obsidian

📚 **[Read the Obsidian Integration Guide](docs/OBSIDIAN-STORAGE.md)** for detailed setup instructions and migration steps.

#### Selecting a Storage Provider

During initial setup (`tom config all`), you'll be prompted to choose your storage provider:

```bash
$ tom config all

Step 4 of 10: Storage Provider Selection

ℹ️  Storage Provider:
   Choose where to store your memory entries:
   • Default: TST-native file structure (recommended for new users)
   • Obsidian: Store entries in your Obsidian vault for seamless note integration

? Select storage provider:
  > Default File System - Stores memory entries in a hierarchical directory...
    Obsidian Vault - Store entries in an Obsidian vault for seamless integration...
```

#### Changing Storage Provider

To switch providers or reconfigure storage:

```bash
tom config storage
# run the storage configuration wizard and select a different provider
```

**Note**: Switching providers doesn't automatically migrate existing entries. See the [Obsidian Integration Guide](docs/OBSIDIAN-STORAGE.md#migrating-from-default-storage) for migration instructions.

### Memory Directory (Default Provider)

By default, memories and configuration are stored in `~/ten-second-tom/` in your home directory:

```bash
~/ten-second-tom/
├── config/
│   └── config.json        # Your configuration (from setup wizard)
├── templates/              # Prompt templates
├── note/                   # Quick notes and daily entries (Default provider)
├── thisweek/              # Weekly reviews (Default provider)
└── recording/             # Voice recordings
```

To customize the root directory location, use the setup wizard or set via environment variable:

```bash
export TenSecondTom__RootDirectory="~/Documents/my-memories"
```

### Data Retention

Configure automatic cleanup of old entries via the setup wizard:

```bash
tom config
# Select "Optional Settings" → "Data Retention"
```

**Available Policies**: `Indefinite`, `Days30`, `Days90`, `OneYear`, `TwoYears`

Automatic purging can be enabled/disabled independently from the retention policy.

---

## 📖 Usage

### First Run - Automatic Setup

When you run Ten Second Tom for the first time, it will automatically launch the setup wizard:

```bash
$ tom record

 _____               ____                           _   _____
|_   _|__ _ __      / ___|  ___  ___ ___  _ __   __| | |_   _|__  _ __ ___
  | |/ _ \ '_ \     \___ \ / _ \/ __/ _ \| '_ \ / _` |   | |/ _ \| '_ ` _ \
  | |  __/ | | |     ___) |  __/ (_| (_) | | | | (_| |   | | (_) | | | | | |
  |_|\___|_| |_|    |____/ \___|\___\___/|_| |_|\__,_|   |_|\___/|_| |_| |_|

                    Your personal memory assistant

Welcome to Ten Second Tom! Let's get you set up.

Step 1 of 6: SSH Key Configuration
...
```

The setup wizard will guide you through:

- SSH key selection (auto-detected from your system, 1Password, Secretive, etc.)
- LLM provider selection (OpenAI or Anthropic)
- API key configuration with validation
- Memory storage location
- Optional settings (logging level, data retention)

Once setup is complete, you can start using Ten Second Tom immediately!

For detailed authentication configuration (SSH agents, key management, etc.), see [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md).

### Re-running Setup

To reconfigure your settings at any time:

```bash
tom config all
```

Or view/update individual settings:

```bash
tom config show               # View current configuration
tom config set api-key "..."  # Update specific setting
```

### Recording with AI Summary

Capture your thoughts with audio recording and generate AI-powered summaries:

```bash
$ tom record           # Record audio with transcription
$ tom generate --recording 10-03-2025_1  # Generate AI summary
```

**Example Workflow:**

1. Start a recording session:
```bash
$ tom record
🎤 Recording started... Press Enter to stop.
✅ Recording saved: ~/ten-second-tom/recording/10-03-2025_1.wav
⏳ Transcribing audio...
✅ Transcription saved: ~/ten-second-tom/recording/10-03-2025_1.md
```

2. Generate an AI summary from the recording:
```bash
$ tom generate --recording 10-03-2025_1 --template daily-summary
⏳ Generating summary...
✅ Summary saved: ~/ten-second-tom/recording/10-03-2025_1_generated.md
```

### Quick Note Capture

Save notes instantly with the `/note` command:

```bash
# Quick note without editor
$ tom note "Completed OAuth integration. Fixed rate limiting issues." --no-edit

# Interactive note (opens editor)
$ tom note

# List all notes
$ tom note list
```

### Multi-line Notes from CLI

You can include formatted multi-line notes directly:

```bash
# Using quotes with line breaks (bash/zsh)
$ tom note "Line 1: Completed task A
Line 2: Working on task B
Line 3: Blocked on task C" --no-edit

# Using echo with pipe
$ echo -e "Today's highlights:\n- Fixed critical bug\n- Deployed to production" | tom note --no-edit
```

### Search Memories

Search your memory archive:

```bash
$ tom search "meeting"
```

**With Date Filters:**

```bash
$ tom search "project" --from-date 2025-09-01 --to-date 2025-09-30
```

### JSON Output

All commands support `--output-json` for programmatic consumption:

```bash
$ tom note list --output-json
$ tom record list --output-json
$ tom search "meeting" --output-json
```

### Retry Failed Summaries

If an AI summary fails, you can retry it later:

```bash
$ tom retry                    # Retry all failed summaries
$ tom retry <entry-id>         # Retry specific entry
```

### Logout

End your session:

```bash
$ tom logout
```

---

## � Shell Mode

**New!** Run Tom in interactive shell mode for a persistent session:

```bash
$ tom
```

This launches an interactive shell where you can execute multiple commands without re-authentication:

```bash
 _____               ____                           _   _____
|_   _|__ _ __      / ___|  ___  ___ ___  _ __   __| | |_   _|__  _ __ ___
  | |/ _ \ '_ \     \___ \ / _ \/ __/ _ \| '_ \ / _` |   | |/ _ \| '_ ` _ \
  | |  __/ | | |     ___) |  __/ (_| (_) | | | | (_| |   | | (_) | | | | | |
  |_|\___|_| |_|    |____/ \___|\___\___/|_| |_|\__,_|   |_|\___/|_| |_| |_|

Version 1.0.0 - Your personal memory assistant

Type /help for available commands, /quit to exit

>
```

### Shell Commands

All commands in shell mode use a slash prefix:

| Command | Description | Example |
|---------|-------------|---------|
| `/note` | Capture quick note without AI processing | `/note` |
| `/record` | Record audio with transcription | `/record` |
| `/generate` | Generate AI summary from note or recording | `/generate note` |
| `/transcribe` | Transcribe audio files or manage STT models | `/transcribe --file meeting.wav` |
| `/audio` | Audio configuration and management | `/audio config` |
| `/search` | Search memory entries | `/search meeting` |
| `/config` | View and manage configuration | `/config` |
| `/llm` | Manage LLM models and configuration | `/llm config` |
| `/auth` | Authentication management | `/auth` |
| `/storage` | Storage management | `/storage` |
| `/login` | Authenticate with SSH key | `/login` |
| `/logout` | End current session | `/logout` |
| `/help` | Display available commands | `/help` |
| `/quit` or `/exit` | Exit the shell | `/quit` |

### Shell Features

⚡ **Fast Execution**: No re-authentication between commands

- Session remains active throughout shell lifetime
- Commands execute immediately

⎋ **Escape Key Support**: Press Escape to cancel wizards and prompts

- Cancels multi-step configuration wizards (e.g., `/audio config`, `/config all`)
- Returns to the REPL prompt without exiting the shell
- Works in selection prompts, text inputs, and confirmation dialogs

🛑 **Graceful Interruption**: Press Ctrl+C to cancel running commands

- First Ctrl+C: Cancels current command, returns to prompt
- Second Ctrl+C: Exits shell
- Partial results displayed when available

### Shell vs Single Command Mode

**Shell Mode** (no arguments):

```bash
tom           # Launches interactive shell
```

**Single Command Mode** (with arguments):

```bash
tom record    # Executes command and exits
tom note      # Executes command and exits
```

Use shell mode for:

- Multiple operations in sequence
- Exploring commands interactively
- Avoiding repeated authentication

Use single command mode for:

- Scripting and automation
- One-off commands
- CI/CD pipelines

---

## 📁 File Structure

Your memories are stored as plain markdown files in your configured memory directory:

```bash
~/ten-second-tom/
├── config/
│   └── config.json         # Your configuration (SSH, LLM, Audio settings)
├── templates/              # Prompt templates (customizable!)
│   ├── daily-summary.md   # Default daily template
│   ├── weekly-review.md   # Default weekly template
│   └── my-custom.md       # Your custom templates
├── note/
│   ├── 10-01-2025_1.md           # Quick note (from /note command)
│   ├── 10-01-2025_2.md           # Another quick note
│   └── 10-02-2025_1.md           # Quick note
├── thisweek/
│   ├── 2025-40-Mon-1.md          # Week 40 of 2025, Monday, entry 1
│   └── 2025-41-Fri-1.md          # Week 41 of 2025, Friday, entry 1
└── recording/                     # Audio recordings (from /record command)
    ├── 10-21-2025_1.wav          # Audio file
    ├── 10-21-2025_1.md           # Transcription with metadata
    ├── 10-21-2025_1_generated.md # AI-processed from /generate command
    └── 10-21-2025_2.wav          # Multiple recordings per day supported
```

**File Format (Recording):**

```markdown
---
command: record
timestamp: 2025-10-03T14:30:00Z
entry-number: 1
duration: 120
stt-provider: whisper-net
word-count: 250
---

Had a productive meeting with the team...
```

**File Format (Generated Summary):**

```markdown
---
command: generate
timestamp: 2025-10-03T14:35:00Z
source: 10-03-2025_1
llm-provider: OpenAI
llm-model: gpt-5-mini
template: daily-summary
---

## Key Events
- Productive team meeting...

## Themes
- Collaboration & teamwork...
```

---

## 📝 Custom Templates

Ten Second Tom allows you to customize the prompt templates used for generating daily and weekly summaries. Templates are stored in `~/ten-second-tom/templates/` and can be edited with any text editor.

### Creating a Custom Template

Templates use **YAML front matter** for metadata and **Markdown** for the prompt content. Here's how to create one:

**1. Create a new file in `~/ten-second-tom/templates/`**

Example: `~/ten-second-tom/templates/my-daily-standup.md`

**2. Add YAML front matter at the top**

```yaml
---
templateType: daily       # Required: "daily" or "weekly"
title: My Daily Standup   # Required: Display name in selection
description: A focused template for daily standup format  # Optional
version: 1.0              # Optional: For tracking updates
author: Your Name         # Optional: Template creator
---
```

**3. Add your custom prompt below the front matter**

```markdown
# Daily Standup - {{DATE}}

Please create a standup-style summary from today's entries.

## What I Did Today
{{TODAY_ENTRIES}}

## Focus Areas
- Identify key accomplishments
- List any blockers or challenges
- Suggest priorities for tomorrow

## Format
- Use bullet points
- Keep it concise (3-5 items per section)
- Highlight any urgent items with ⚠️
```

### Template Metadata Fields

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `templateType` | ✅ Yes | Template type: `daily` or `weekly` | `daily` |
| `title` | ✅ Yes | Display name (max 200 chars) | `My Daily Standup` |
| `description` | ❌ No | Description shown in selection (max 500 chars) | `A focused template for...` |
| `version` | ❌ No | Semantic version for tracking | `1.0` |
| `author` | ❌ No | Template creator (max 100 chars) | `Your Name` |
| `tags` | ❌ No | Categorization tags (future use) | `["work", "agile"]` |

### Template Variables

Use these variables in your template content:

- `{{DATE}}` - Current date
- `{{TODAY_ENTRIES}}` - User's daily entries
- `{{WEEK_ENTRIES}}` - User's weekly entries
- `{{ENTRIES}}` - Generic entries placeholder

### Template Selection

When you run `tom generate`, Ten Second Tom will:

1. **Auto-select** if only one template is available (no prompt shown)
2. **Show selection prompt** if multiple templates exist:
   ```bash
   Select a template for daily summary:
   ▸ Daily Summary - Default template for daily journal entries [Default]
     My Daily Standup - A focused template for daily standup format
   ```
3. **Fall back to embedded template** if no valid templates are found

### Template Examples

**Daily Gratitude Journal:**

```yaml
---
templateType: daily
title: Gratitude Journal
description: Focus on positive moments and gratitude
version: 1.0
---

# Daily Gratitude - {{DATE}}

From today's entries ({{TODAY_ENTRIES}}), create a gratitude-focused summary:

## Three Good Things
Identify three positive moments or achievements from today.

## Gratitude
What am I grateful for today?

## Lessons Learned
What did I learn today that I can apply tomorrow?

Keep the tone warm and encouraging.
```

**Weekly Sprint Review:**

```yaml
---
templateType: weekly
title: Sprint Review
description: Agile sprint-focused weekly summary
version: 1.0
---

# Sprint Review - Week of {{DATE}}

Based on this week's entries ({{WEEK_ENTRIES}}), create a sprint review:

## Sprint Goals Completed
- What goals were achieved?
- What features were delivered?

## Sprint Retrospective
- What went well?
- What could be improved?

## Next Sprint Planning
- What should be prioritized?
- Any blockers to address?

Format as a structured agile sprint review.
```

### Template Best Practices

✅ **DO:**

- Keep templates under 1MB (soft limit)
- Use clear, descriptive titles
- Add helpful descriptions for selection
- Test templates after creating them
- Use semantic versioning for tracking
- Include specific instructions for the LLM
- Use variables for dynamic content

❌ **DON'T:**

- Use parent directory references (`..`) in filenames
- Include path separators (`/`, `\`) in template IDs
- Store sensitive information in templates
- Create templates larger than 1MB

### Template Validation

Ten Second Tom validates templates automatically:

- ✅ **Valid YAML** front matter
- ✅ **Required fields** present (`templateType`, `title`)
- ✅ **File size** under 1MB
- ✅ **Filename** follows kebab-case convention
- ✅ **UTF-8 encoding**

Invalid templates are skipped with warnings in the logs.

### Editing Templates

Templates are reloaded on every command run - **no restart required!**

1. Edit the template file with any text editor
2. Save your changes
3. Run `tom generate note` or `tom generate recording`
4. Changes take effect immediately ✨

### Restoring Default Templates

If you delete or modify default templates and want them back:

1. Delete the `~/ten-second-tom/templates/` directory
2. Run any command - templates will be automatically restored

Default templates are never overwritten, so feel free to customize them!

### Troubleshooting Templates

**Template not appearing in selection?**

- Check YAML front matter is valid (use a YAML validator)
- Ensure `templateType` matches command type (`daily` for `tom generate`)
- Check file extension is `.md`
- Review logs for validation errors

**Template selection not showing?**

- If only one template exists, it's auto-selected (no prompt)
- Check `~/ten-second-tom/templates/` directory exists
- Verify at least one valid template for the command type

**Want to see all templates?**

```bash
ls -la ~/ten-second-tom/templates/
```

---

## 🏗️ Architecture

Ten Second Tom follows modern software architecture principles:

- **Vertical Slice Architecture**: Features organized as self-contained slices
- **CQRS Pattern**: Separation of commands and queries
- **Provider Pattern**: Pluggable storage and LLM providers
- **Dependency Injection**: .NET's built-in DI container
- **Test-First Development**: 80%+ test coverage with xUnit

### Technology Stack

- **Language**: C# with .NET 10
- **CLI Framework**: System.CommandLine
- **LLM Providers**: OpenAI SDK, Anthropic.SDK
- **Markdown**: Markdig
- **Terminal UI**: Spectre.Console
- **SSH Authentication**: SSH.NET
- **Configuration**: Microsoft.Extensions.Configuration
- **Logging**: Serilog
- **Testing**: xUnit, FluentAssertions, Moq

---

## 🧪 Development

### Prerequisites

- .NET 10 SDK
- Git

### Building from Source

```bash
git clone https://github.com/sirkirby/ten-second-tom.git
cd ten-second-tom
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

**With Coverage:**

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Running Locally

```bash
dotnet run --project src -- record
```

---

## 🤝 Contributing

Contributions are welcome! To contribute:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Follow the coding guidelines in [AGENTS.md](AGENTS.md)
4. Write tests for your changes (maintain 80%+ coverage)
5. Commit your changes using conventional commits
6. Push to your branch
7. Open a Pull Request

### Development Guidelines

- Follow the [AI Agent Instructions](AGENTS.md) and [Project Constitution](.github/copilot-instructions.md)
- Write tests first (TDD approach)
- Maintain 80%+ code coverage
- Use conventional commits (`feat:`, `fix:`, `docs:`, `test:`, `refactor:`)
- Update documentation for user-facing changes
- Follow C# and .NET 10 best practices

### Reporting Issues

Found a bug or have a feature request? [Open an issue](https://github.com/sirkirby/ten-second-tom/issues/new).

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Named after "Ten Second Tom" from *50 First Dates*
- Built with ❤️ using .NET 10 and modern C# practices
- Inspired by the need for better personal memory management and journaling tools

---

## 📞 Support

- **Documentation**:
  - [Configuration Guide](docs/CONFIGURATION.md) - Primary configuration via setup wizard
  - [Environment Setup](docs/ENVIRONMENT.md) - Environment variables and advanced configuration
  - [Obsidian Storage Integration](docs/OBSIDIAN-STORAGE.md) - Detailed Obsidian vault setup guide
  - [Audio Configuration](docs/AUDIO.md) - Voice recording and transcription settings
  - [Authentication Setup](docs/AUTHENTICATION.md) - SSH key configuration and management
  - [Security Policy](SECURITY.md) - Security best practices and secrets management
  - [CI/CD Documentation](docs/CICD.md) - Continuous integration and deployment setup
  - [Code Coverage](docs/COVERAGE.md) - Test coverage reports and metrics
- **Issues**: [GitHub Issues](https://github.com/sirkirby/ten-second-tom/issues)
- **Discussions**: [GitHub Discussions](https://github.com/sirkirby/ten-second-tom/discussions)

---

**Happy memory building! 🧠✨**

Made with ☕ and .NET 10
