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
- 📁 **Markdown Storage**: Human-readable files in `.memory/` directory
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
