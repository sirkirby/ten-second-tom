# Quickstart Guide: Ten Second Tom

**Version**: 1.0.0  
**Date**: October 1, 2025

## What is Ten Second Tom?

Ten Second Tom is a CLI application that helps you capture and reflect on your daily experiences through guided prompts. It uses AI to summarize your reflections, identify themes, track to-dos, and build a searchable personal memory archive.

---

## Prerequisites

- **.NET 9 SDK** installed
- **OpenAI API Key** or **Anthropic API Key**
- **SSH Key** (Ed25519 or RSA) in `~/.ssh/`

---

## Installation

### macOS (Homebrew)
```bash
brew tap sirkirby/ten-second-tom
brew install ten-second-tom
```

### Windows (winget)
```bash
winget install TenSecondTom
```

### From Source
```bash
git clone https://github.com/sirkirby/ten-second-tom.git
cd ten-second-tom
dotnet build
dotnet run --project src
```

---

## First-Time Setup

### 1. Configure API Keys

**Using .NET User Secrets** (Development):
```bash
cd /path/to/ten-second-tom/src
dotnet user-secrets init
dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "sk-your-key-here"
# OR
dotnet user-secrets set "TenSecondTom:Anthropic:ApiKey" "sk-ant-your-key-here"
```

**Using Environment Variables** (Production):
```bash
export TenSecondTom__OpenAI__ApiKey="sk-your-key-here"
# OR
export TenSecondTom__Anthropic__ApiKey="sk-ant-your-key-here"
```

### 2. First Run - Authentication

When you run Ten Second Tom for the first time, it will prompt for authentication:

```bash
$ tom today

┌────────────────────────────────────────────────┐
│  🧠 Ten Second Tom - Personal Memory Assistant │
└────────────────────────────────────────────────┘

⚠️  No active session found.

Authenticating with SSH key...
📁 Found: ~/.ssh/id_ed25519
🔐 Enter passphrase (leave empty if no passphrase): 

✅ Authentication successful!
Session will remain active until you logout.

---

Let's capture today's reflection.
```

---

## Basic Usage

### Daily Reflection (`/today`)

Capture your day with guided prompts:

```bash
$ tom today

┌────────────────────────────────────────────────┐
│  📅 Daily Reflection - October 1, 2025         │
└────────────────────────────────────────────────┘

❓ What happened today?
> Had a productive meeting with the team about the new feature.
  Made significant progress on the design document.

❓ Anything interesting planned for tomorrow?
> Will finalize the architecture and start implementation.

❓ Is there something you didn't finish that needs attention?
> Need to review John's pull request before end of day.

❓ How are you feeling overall?
> Energized and focused. Good momentum on the project.

⏳ Generating summary...

┌────────────────────────────────────────────────┐
│  ✨ Daily Summary                              │
└────────────────────────────────────────────────┘

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

## Important People
- John (PR review pending)
- Team members from morning meeting

## Notable Tasks
- Design document completion
- Feature planning and architecture

✅ Daily entry saved: .memory/today/10-01-2025_1.md
```

### Weekly Review (`/thisweek`)

Generate a weekly summary:

```bash
$ tom thisweek

┌────────────────────────────────────────────────┐
│  📊 Weekly Review - Week 40, 2025              │
└────────────────────────────────────────────────┘

📖 Analyzing 5 daily entries from Sep 30 - Oct 6...
⏳ Generating weekly review...

┌────────────────────────────────────────────────┐
│  ✨ Your Week at a Glance                      │
└────────────────────────────────────────────────┘

## 🎯 Top 3 Accomplishments
1. Completed feature specification for Ten Second Tom
2. Resolved 8 critical clarifications improving project clarity
3. Designed comprehensive technical architecture

## ⚡ Top 3 Challenges
1. Balancing feature scope with v1 simplicity
2. Selecting appropriate LLM provider SDKs
3. Designing extensible storage without over-engineering

## 🎨 Recurring Themes
- Test-driven development emphasis
- Cross-platform compatibility
- Security and secrets management

## 🤝 Interaction Patterns
- Daily collaborative problem-solving
- Iterative refinement of requirements
- Focus on architectural decisions

## 📅 Suggestions for Next Week
- Begin implementation Phase 1
- Set up CI/CD pipeline
- Create initial project structure

✅ Weekly review saved: .memory/thisweek/2025-40_1.md
```

### Search Memories (`/search`)

Search your memory archive:

```bash
$ tom search --query "meeting"

┌────────────────────────────────────────────────┐
│  🔍 Search Results for "meeting"               │
└────────────────────────────────────────────────┘

Found 3 entries:

📅 10-01-2025 (Daily Entry #1)
   "...productive meeting with the team about the new feature..."
   
📅 09-28-2025 (Daily Entry #2)
   "...weekly status meeting covered project timeline..."
   
📊 Week 39, 2025 (Weekly Review #1)
   "...recurring meeting patterns with stakeholders..."

💡 Tip: Use --from and --to to filter by date range
```

### Logout (`/logout`)

End your session:

```bash
$ tom logout

✅ Successfully logged out.
Next run will require re-authentication.
```

---

## Configuration

### Memory Directory

By default, memories are stored in `./.memory/` in your current directory.

To customize:

**appsettings.json**:
```json
{
  "TenSecondTom": {
    "MemoryDirectory": "~/Documents/my-memories"
  }
}
```

### LLM Provider Selection

**Default provider** (appsettings.json):
```json
{
  "TenSecondTom": {
    "LlmProvider": "OpenAI",
    "OpenAI": {
      "Model": "gpt-4",
      "MaxTokens": 2000
    }
  }
}
```

**Override per command**:
```bash
tom today --provider Anthropic
tom thisweek --provider OpenAI
```

### Data Retention Policy

Configure automatic data cleanup:

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

Options: `Indefinite`, `Days30`, `Days90`, `OneYear`, `TwoYears`

---

## File Structure

Your memory archive is organized as markdown files:

```
.memory/
├── today/
│   ├── 10-01-2025_1.md
│   ├── 10-01-2025_2.md    # Multiple entries same day
│   └── 10-02-2025_1.md
└── thisweek/
    ├── 2025-40_1.md        # Week 40 of 2025
    └── 2025-41_1.md
```

Each file is human-readable markdown with metadata:

```markdown
---
command: today
timestamp: 2025-10-01T14:30:00Z
entry-number: 1
llm-provider: OpenAI
llm-model: gpt-4
---

# User Input
...

# LLM Summary
...
```

---

## Tips & Tricks

### Multiple Daily Entries
You can run `/today` multiple times per day - each creates a separate timestamped entry:
```bash
tom today  # Morning reflection
tom today  # Evening reflection
```

### Custom Weekly Range
Review a specific date range instead of the last 7 days:
```bash
tom thisweek --from 2025-09-15 --to 2025-09-22
```

### Viewing Raw Files
All entries are plain markdown - view or edit them directly:
```bash
cat .memory/today/10-01-2025_1.md
code .memory/today/  # Open in VS Code
```

### Backup Your Memories
Simply copy the `.memory/` directory:
```bash
cp -r .memory/ ~/Dropbox/backup/
```

### Version Control
You can commit your memories to git (just ensure API keys are not included):
```bash
cd .memory
git init
git add .
git commit -m "My memories"
```

---

## Troubleshooting

### "Authentication required" Error
Your session may have expired or been cleared. Run any command to re-authenticate:
```bash
tom today
```

### "LLM provider error"
Check your API key configuration:
```bash
dotnet user-secrets list --project src
```

Verify your API key is valid and has credits/quota available.

### "No daily entries found"
For `/thisweek`, ensure you have daily entries in the target date range:
```bash
ls -la .memory/today/
```

### Permission Denied (SSH Key)
Ensure your SSH key has correct permissions:
```bash
chmod 600 ~/.ssh/id_ed25519
```

---

## Getting Help

### CLI Help
```bash
tom --help
tom today --help
tom thisweek --help
tom search --help
```

### Documentation
- GitHub: https://github.com/sirkirby/ten-second-tom
- Issues: https://github.com/sirkirby/ten-second-tom/issues

### Community
- Discussions: https://github.com/sirkirby/ten-second-tom/discussions

---

## Next Steps

After your first successful run:

1. **Establish a routine**: Run `tom today` each evening
2. **Review weekly**: Run `tom thisweek` every Sunday
3. **Explore your memories**: Use `tom search` to find patterns
4. **Customize prompts**: Edit template files in `.memory/templates/` (optional)
5. **Backup regularly**: Set up automatic backups of `.memory/`

---

**Happy memory building! 🧠✨**
