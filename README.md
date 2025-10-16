# Ten Second Tom

```
 _____               ____                          _   _____
|_   _|__ _ __      / ___|  ___  ___ ___  _ __   __| | |_   _|__  _ __ ___
  | |/ _ \ '_ \     \___ \ / _ \/ __/ _ \| '_ \ / _` |   | |/ _ \| '_ ` _ \
  | |  __/ | | |     ___) |  __/ (_| (_) | | | | (_| |   | | (_) | | | | | |
  |_|\___|_| |_|    |____/ \___|\___\___/|_| |_|\__,_|   |_|\___/|_| |_| |_|

                    Your personal memory assistant
```

**Ten Second Tom** is a CLI application for personal memory management that guides you through daily reflection prompts, leverages AI to generate structured summaries, and builds a searchable archive of your experiences. Named after the character from the movie *50 First Dates*, Ten Second Tom helps you remember what matters.

[![PR Validation](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/pr-validation.yml)
[![Build](https://github.com/sirkirby/ten-second-tom/actions/workflows/build.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/build.yml)
[![Release](https://github.com/sirkirby/ten-second-tom/actions/workflows/release.yml/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET Version](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)

---

## ✨ Features

- 🧠 **Guided Daily Reflections**: Answer 3-5 prompts to capture your day
- 📊 **Weekly Reviews**: AI-generated summaries of your week with themes and patterns
- 🔍 **Searchable Archive**: Full-text search across all your memories
- 🤖 **Multiple AI Providers**: Support for OpenAI and Anthropic
- 📝 **Custom Templates**: Create and edit prompt templates to personalize your summaries
- 📁 **Markdown Storage**: Human-readable files in configured `.memory/` directory
- 🔐 **SSH Authentication**: Secure session management with SSH keys
- 🎨 **Beautiful Terminal UI**: Rich formatting with Spectre.Console
- 📤 **JSON Output**: Programmatic access for automation and integrations
- 🔄 **Retry Mechanism**: Recover from failed AI summaries
- ⏰ **Auto-Purge**: Configurable data retention policies

---

## 📋 Prerequisites

- **.NET 9 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **OpenAI API Key** or **Anthropic API Key**
- **SSH Key** (Ed25519 or RSA) in `~/.ssh/`

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

Ten Second Tom supports multiple Large Language Model (LLM) providers and a curated list of production-ready models. You can configure both the provider and model using any of these methods:

1. Interactive setup wizard (`tom setup`)
2. Configuration command (`tom config llm`)
3. Environment variables (advanced / CI)

#### Interactive Selection (Recommended)

Run either the setup wizard or the config command:

```bash
tom setup         # Guided initial configuration (includes provider + model selection)
tom config llm    # Re-run provider/model selection any time
```

You'll first select a provider, then a curated list of models is displayed with cost tier and description:

```
Select an LLM provider:
  ▸ OpenAI
    Anthropic

Select a model (OpenAI):
  ▸ GPT‑4o (gpt-4o) [Premium] - Flagship reasoning & synthesis model
    GPT‑4o Mini (gpt-4o-mini) [Standard] - Balanced cost & quality
    GPT‑3.5 Turbo (gpt-3.5-turbo) [Economy] - Legacy fast inexpensive model
```

Current selection is highlighted when reconfiguring so you can easily compare or switch.

#### Environment Variable Configuration

You can override the configured model (and/or provider) at runtime without modifying user secrets:

```bash
export TenSecondTom__LlmProvider="Anthropic"
export TenSecondTom__Llm__Model="claude-3-5-sonnet"
```

Environment variable values take precedence over user secrets and `appsettings.json`.

#### Supported Providers & Models

| Provider   | Model ID                        | Display Name             | Cost Tier | Default | Description |
|------------|----------------------------------|--------------------------|-----------|---------|-------------|
| OpenAI     | `gpt-4o-mini`                   | GPT-4o Mini              | Budget    | ✅       | Fast, cost-effective model |
| OpenAI     | `gpt-4o`                        | GPT-4o                   | Balanced  |         | High capability, reasonable cost |
| OpenAI     | `chatgpt-4o-latest`             | ChatGPT-4o Latest        | Balanced  |         | Latest ChatGPT-4 Omni, continuously updated |
| Anthropic  | `claude-3-haiku-20240307`       | Claude 3 Haiku           | Budget    | ✅       | Fast and cost-effective, version 3.0 |
| Anthropic  | `claude-3-5-haiku-20241022`     | Claude 3.5 Haiku         | Budget    |         | Improved performance, version 3.5 |
| Anthropic  | `claude-sonnet-4-20250514`      | Claude Sonnet 4          | Balanced  |         | Balanced capability, Claude 4.0 |
| Anthropic  | `claude-sonnet-4-5-20250611`    | Claude Sonnet 4.5        | Balanced  |         | Enhanced version, Claude 4.5 |
| Anthropic  | `claude-opus-4-20250514`        | Claude Opus 4            | Premium   |         | Highest capability, Claude 4.0 |
| Anthropic  | `claude-opus-4-1-20250619`      | Claude Opus 4.1          | Premium   |         | Top-tier model, Claude 4.1 |

**Notes:**

- Default model (per provider) is used automatically if you leave the model blank during setup.
- Validation occurs at startup; an invalid provider/model combination produces a clear error with valid suggestions.
- Model IDs must match the configured provider (e.g., GPT models require OpenAI provider).
- Deprecated models from previous versions are no longer supported - use `tom config llm` to select from current models.
- Additional models may be added over time; run `tom config llm` to view the current curated list.

#### Viewing Current Configuration

```bash
tom config show
```

Example LLM section in output:

```
LLM Configuration
  Provider : OpenAI
  Model    : gpt-4o (Premium)
```

#### Changing Just the Model Quickly

```bash
tom config llm
```

This re-runs only the provider/model selection flow—other settings remain unchanged.

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
tom today
# → Setup wizard launches automatically if not configured
```

Or manually run the setup wizard:

```bash
tom setup
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

**Option 3: Configuration File**

You can also use an `appsettings.json` file for non-sensitive configuration. **API keys should not be stored here.**

```json
{
  "TenSecondTom": {
    "LlmProvider": "OpenAI",
    "OpenAI": {
      "Model": "gpt-4",
      "MaxTokens": 2000,
      "Temperature": 0.7
    },
    "Anthropic": {
      "Model": "claude-3-5-sonnet-20241022",
      "MaxTokens": 2000
    }
  }
}
```

⚠️ **Never commit API keys to version control!** For installed applications, environment variables are the recommended way to configure secrets. See [SECURITY.md](SECURITY.md) for more details.

### Memory Directory

By default, memories are stored in `./.memory/` in your current directory. To customize:

```json
{
  "TenSecondTom": {
    "MemoryDirectory": "~/Documents/my-memories"
  }
}
```

### Data Retention

Configure automatic cleanup of old entries:

```json
{
  "TenSecondTom": {
    "DataRetention": {
      "DefaultPolicy": "OneYear",
      "AutoPurgeEnabled": true
    }
  }
}
```

**Available Policies**: `Indefinite`, `Days30`, `Days90`, `OneYear`, `TwoYears`

---

## 📖 Usage

### First Run - Automatic Setup

When you run Ten Second Tom for the first time, it will automatically launch the setup wizard:

```bash
$ tom today

 _____               ____                          _   _____
|_   _|__ _ __      / ___|  ___  ___ ___  _ __   __| | |_   _|__  _ __ ___
  | |/ _ \ '_ \     \___ \ / _ \/ __/ _ \| '_ \ / _` |   | |/ _ \| '_ ` _ \
  | |  __/ | | |     ___) |  __/ (_| (_) | | | | (_| |   | | (_) | | | | | |
  |_|\___|_| |_|    |____/ \___|\___\___/|_| |_|\__,_|   |_|\___/|_| |_| |_|

                    Your personal memory assistant

Welcome to Ten Second Tom! Let's get you set up.

Step 1 of 5: SSH Key Configuration
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
tom setup
```

Or view/update individual settings:

```bash
tom config show               # View current configuration
tom config set api-key "..."  # Update specific setting
```

### Daily Reflection

Capture your day with guided prompts:

```bash
$ tom today
```

**Example Session:**

```
📅 Daily Reflection - October 3, 2025

❓ What happened today?
> Had a productive meeting with the team about the new feature.
  Made significant progress on the design document.

❓ Anything interesting planned for tomorrow?
> Will finalize the architecture and start implementation.

❓ Is there something you didn't finish that needs attention?
> Need to review John's pull request before end of day.

⏳ Generating summary...

✨ Daily Summary

## Key Events
- Productive team meeting about new feature
- Significant progress on design document

## Themes
- Collaboration & teamwork
- Feature development momentum

## To-Do Items
- [ ] Review John's pull request
- [ ] Finalize architecture design
- [ ] Start implementation tomorrow

✅ Daily entry saved: .memory/today/10-03-2025_1.md
```

### Weekly Review

Generate a weekly summary from your daily entries:

```bash
$ tom thisweek
```

**Custom Date Range:**

```bash
$ tom thisweek --from-date 2025-09-15 --to-date 2025-09-22
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
$ tom today --output-json
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

```text
 _____               ____                          _   _____
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
| `/today` | Capture today's reflection | `/today` |
| `/thisweek` | Generate weekly review | `/thisweek` |
| `/search` | Search memory entries | `/search meeting` |
| `/login` | Authenticate with SSH key | `/login` |
| `/logout` | End current session | `/logout` |
| `/help` | Display available commands | `/help` |
| `/quit` or `/exit` | Exit the shell | `/quit` |

### Shell Features

✨ **Autocomplete**: Press Tab to see command suggestions

- Type `/to` + Tab → shows `/today`
- Works with partial command names

🕐 **Command History**: Navigate previous commands with arrow keys

- Arrow Up/Down to scroll through history
- History persists during session only (not saved between launches)

⚡ **Fast Execution**: No re-authentication between commands

- Session remains active throughout shell lifetime
- Commands execute immediately

🛑 **Graceful Interruption**: Press Ctrl+C to cancel running commands

- First Ctrl+C: Cancels current command, returns to prompt
- Second Ctrl+C: Exits shell
- Partial results displayed when available

📄 **Smart Pagination**: Long output is automatically paginated

- Short output displays fully inline
- Long output uses interactive pager (Space = next page, Q = quit)

### Shell vs Single Command Mode

**Shell Mode** (no arguments):

```bash
tom           # Launches interactive shell
```

**Single Command Mode** (with arguments):

```bash
tom today     # Executes command and exits
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

## �📁 File Structure

Your memories are stored as plain markdown files:

```
.memory/
├── templates/              # Prompt templates (customizable!)
│   ├── daily-summary.md   # Default daily template
│   ├── weekly-review.md   # Default weekly template
│   └── my-custom.md       # Your custom templates
├── today/
│   ├── 10-01-2025_1.md
│   ├── 10-01-2025_2.md    # Multiple entries per day supported
│   ├── 10-02-2025_1.md
│   └── 10-03-2025_1.md
└── thisweek/
    ├── 2025-40_1.md        # Week 40 of 2025
    └── 2025-41_1.md
```

**File Format:**

```markdown
---
command: today
timestamp: 2025-10-03T14:30:00Z
entry-number: 1
llm-provider: OpenAI
llm-model: gpt-4
---

# User Input

## What happened today?
Had a productive meeting with the team...

# LLM Summary

## Key Events
- Productive team meeting...
```

---

## 📝 Custom Templates

Ten Second Tom allows you to customize the prompt templates used for generating daily and weekly summaries. Templates are stored in `.memory/templates/` and can be edited with any text editor.

### Creating a Custom Template

Templates use **YAML front matter** for metadata and **Markdown** for the prompt content. Here's how to create one:

**1. Create a new file in `.memory/templates/`**

Example: `.memory/templates/my-daily-standup.md`

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

When you run `tom today` or `tom thisweek`, Ten Second Tom will:

1. **Auto-select** if only one template is available (no prompt shown)
2. **Show selection prompt** if multiple templates exist:
   ```
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
3. Run `tom today` or `tom thisweek`
4. Changes take effect immediately ✨

### Restoring Default Templates

If you delete or modify default templates and want them back:

1. Delete the `.memory/templates/` directory
2. Run any command - templates will be automatically restored

Default templates are never overwritten, so feel free to customize them!

### Troubleshooting Templates

**Template not appearing in selection?**
- Check YAML front matter is valid (use a YAML validator)
- Ensure `templateType` matches command type (`daily` for `tom today`)
- Check file extension is `.md`
- Review logs for validation errors

**Template selection not showing?**
- If only one template exists, it's auto-selected (no prompt)
- Check `.memory/templates/` directory exists
- Verify at least one valid template for the command type

**Want to see all templates?**
```bash
ls -la .memory/templates/
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

- **Language**: C# with .NET 9
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

- .NET 9 SDK
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
dotnet run --project src -- today
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
- Follow C# and .NET 9 best practices

### Reporting Issues

Found a bug or have a feature request? [Open an issue](https://github.com/sirkirby/ten-second-tom/issues/new).

---

## 📜 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 Acknowledgments

- Named after "Ten Second Tom" from *50 First Dates*
- Built with ❤️ using .NET 9 and modern C# practices
- Inspired by the need for better personal memory management tools

---

## 📞 Support

- **Documentation**: 
  - [Configuration Guide](docs/CONFIGURATION.md)
  - [Authentication Setup](docs/AUTHENTICATION.md)
  - [Security Policy](SECURITY.md)
  - [CI/CD Documentation](docs/CICD.md)
  - [Environment Setup](docs/ENVIRONMENT.md)
  - [Code Coverage](docs/COVERAGE.md)
- **Issues**: [GitHub Issues](https://github.com/sirkirby/ten-second-tom/issues)
- **Discussions**: [GitHub Discussions](https://github.com/sirkirby/ten-second-tom/discussions)

---

**Happy memory building! 🧠✨**

Made with ☕ and .NET 9
