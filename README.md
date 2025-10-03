# Ten Second Tom

```
╔════════════════════════════════════════════════════════════════════════════╗
║  ████████╗███████╗███╗   ██╗    ███████╗███████╗ ██████╗                ║
║  ╚══██╔══╝██╔════╝████╗  ██║    ██╔════╝██╔════╝██╔════╝                ║
║     ██║   █████╗  ██╔██╗ ██║    ███████╗█████╗  ██║                     ║
║     ██║   ██╔══╝  ██║╚██╗██║    ╚════██║██╔══╝  ██║                     ║
║     ██║   ███████╗██║ ╚████║    ███████║███████╗╚██████╗                ║
║     ╚═╝   ╚══════╝╚═╝  ╚═══╝    ╚══════╝╚══════╝ ╚═════╝                ║
║                   TOM - Your personal memory assistant               ║
╚════════════════════════════════════════════════════════════════════════════╝
```

**Ten Second Tom** is a CLI application for personal memory management that guides you through daily reflection prompts, leverages AI to generate structured summaries, and builds a searchable archive of your experiences. Named after the character from the movie *50 First Dates*, Ten Second Tom helps you remember what matters.

[![Build Status](https://github.com/sirkirby/ten-second-tom/workflows/build/badge.svg)](https://github.com/sirkirby/ten-second-tom/actions)
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

**Option 3: Configuration File**

Create `appsettings.json` in your working directory:

```json
{
  "TenSecondTom": {
    "LlmProvider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-your-key-here",
      "Model": "gpt-4",
      "MaxTokens": 2000,
      "Temperature": 0.7
    },
    "Anthropic": {
      "ApiKey": "sk-ant-your-key-here",
      "Model": "claude-3-5-sonnet-20241022",
      "MaxTokens": 2000
    }
  }
}
```

⚠️ **Never commit API keys to version control!**

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

### First Run - Authentication

When you run Ten Second Tom for the first time, it will prompt for SSH authentication:

```bash
$ tom login

╔════════════════════════════════════════════════════════════════════════════╗
║  ████████╗███████╗███╗   ██╗    ███████╗███████╗ ██████╗                ║
║  ╚══██╔══╝██╔════╝████╗  ██║    ██╔════╝██╔════╝██╔════╝                ║
║     ██║   █████╗  ██╔██╗ ██║    ███████╗█████╗  ██║                     ║
║     ██║   ██╔══╝  ██║╚██╗██║    ╚════██║██╔══╝  ██║                     ║
║     ██║   ███████╗██║ ╚████║    ███████║███████╗╚██████╗                ║
║     ╚═╝   ╚══════╝╚═╝  ╚═══╝    ╚══════╝╚══════╝ ╚═════╝                ║
║                   TOM - Your personal memory assistant               ║
╚════════════════════════════════════════════════════════════════════════════╝

→ Authenticating with SSH key...

✓ Successfully authenticated!

Session will remain active until you logout.
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

## 📁 File Structure

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

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Guidelines

1. Follow the [AI Agent Instructions](AGENTS.md) and [Project Constitution](.github/copilot-instructions.md)
2. Write tests first (TDD approach)
3. Maintain 80%+ code coverage
4. Use conventional commits
5. Update documentation for user-facing changes

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

- **Documentation**: [GitHub Wiki](https://github.com/sirkirby/ten-second-tom/wiki)
- **Issues**: [GitHub Issues](https://github.com/sirkirby/ten-second-tom/issues)
- **Discussions**: [GitHub Discussions](https://github.com/sirkirby/ten-second-tom/discussions)

---

**Happy memory building! 🧠✨**

Made with ☕ and .NET 9
