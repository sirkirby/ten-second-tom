# Configuration Guide

This document provides detailed configuration instructions for Ten Second Tom.

## Table of Contents

- [API Keys Setup](#api-keys-setup)
- [Memory Storage](#memory-storage)
- [Data Retention](#data-retention)
- [Logging Configuration](#logging-configuration)
- [LLM Provider Settings](#llm-provider-settings)
- [Environment Variables](#environment-variables)

---

## API Keys Setup

Ten Second Tom requires an API key from either OpenAI or Anthropic to generate AI summaries.

### Option 1: .NET User Secrets (Recommended for Development)

User Secrets keep sensitive data out of your source code and are stored encrypted on your machine.

```bash
cd /path/to/ten-second-tom/src
dotnet user-secrets init
```

**For OpenAI:**
```bash
dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "sk-your-openai-key-here"
```

**For Anthropic:**
```bash
dotnet user-secrets set "TenSecondTom:Anthropic:ApiKey" "sk-ant-your-anthropic-key-here"
```

**View all secrets:**
```bash
dotnet user-secrets list
```

**Remove a secret:**
```bash
dotnet user-secrets remove "TenSecondTom:OpenAI:ApiKey"
```

### Option 2: Environment Variables (Recommended for Production)

Set environment variables in your shell profile or system settings:

**macOS/Linux (bash/zsh):**
```bash
export TenSecondTom__OpenAI__ApiKey="sk-your-key-here"
export TenSecondTom__Anthropic__ApiKey="sk-ant-your-key-here"
```

**Windows (PowerShell):**
```powershell
$env:TenSecondTom__OpenAI__ApiKey="sk-your-key-here"
$env:TenSecondTom__Anthropic__ApiKey="sk-ant-your-key-here"
```

**Windows (Command Prompt):**
```cmd
set TenSecondTom__OpenAI__ApiKey=sk-your-key-here
set TenSecondTom__Anthropic__ApiKey=sk-ant-your-key-here
```

**Note:** Use double underscores (`__`) to separate configuration sections in environment variables.

### Option 3: .env File (Local Development)

Create a `.env` file in your working directory:

```env
TenSecondTom__OpenAI__ApiKey=sk-your-key-here
TenSecondTom__Anthropic__ApiKey=sk-ant-your-key-here
```

⚠️ **Important:** Add `.env` to your `.gitignore` to avoid committing secrets!

### Option 4: appsettings.json (Not Recommended for Secrets)

While you can store API keys in `appsettings.json`, this is **not recommended** as it's easy to accidentally commit secrets to version control.

If you must use this approach:

1. Copy `example.appsettings.json` to `appsettings.json`
2. Fill in your API keys
3. Add `appsettings.json` to `.gitignore`

---

## Memory Storage

### Memory Directory

Configure where your memory files are stored:

**appsettings.json:**
```json
{
  "TenSecondTom": {
    "MemoryDirectory": "~/Documents/ten-second-tom"
  }
}
```

**Environment Variable:**
```bash
export TenSecondTom__MemoryDirectory="~/Documents/ten-second-tom"
```

**Default:** `./.memory` (in current working directory)

### Directory Structure

Ten Second Tom creates the following structure:

```
.memory/
├── today/          # Daily entries
│   └── MM-DD-YYYY_N.md
└── thisweek/       # Weekly reviews
    └── YYYY-WW_N.md
```

---

## Data Retention

Configure how long to keep memory entries:

### Retention Policies

- `Indefinite`: Keep all entries forever (default)
- `Days30`: Keep entries for 30 days
- `Days90`: Keep entries for 90 days
- `OneYear`: Keep entries for 1 year
- `TwoYears`: Keep entries for 2 years

### Configuration

**appsettings.json:**
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

**Environment Variables:**
```bash
export TenSecondTom__DataRetention__DefaultPolicy="OneYear"
export TenSecondTom__DataRetention__AutoPurgeEnabled="true"
```

### Auto-Purge Behavior

When `AutoPurgeEnabled` is `true`:
- Runs automatically on application startup
- Deletes entries older than the retention policy
- Logs the number of entries purged
- Skipped if policy is `Indefinite`

### Manual Purge

To manually purge old entries, run with auto-purge enabled once, then disable it.

---

## Logging Configuration

Ten Second Tom uses Serilog for logging.

### Log Levels

- `Verbose`: Detailed diagnostic information
- `Debug`: Debugging information
- `Information`: General informational messages (default)
- `Warning`: Warning messages
- `Error`: Error messages
- `Fatal`: Critical errors

### Configuration

**appsettings.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/ten-second-tom-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

### Log Files

Logs are written to `logs/ten-second-tom-YYYYMMDD.log` with daily rolling and 7-day retention.

---

## LLM Provider Settings

### Default Provider

Set which LLM provider to use by default:

**appsettings.json:**
```json
{
  "TenSecondTom": {
    "LlmProvider": "OpenAI"
  }
}
```

**Environment Variable:**
```bash
export TenSecondTom__LlmProvider="OpenAI"
```

**Options:** `OpenAI` or `Anthropic`

### OpenAI Configuration

```json
{
  "TenSecondTom": {
    "OpenAI": {
      "Model": "gpt-4",
      "MaxTokens": 2000,
      "Temperature": 0.7
    }
  }
}
```

**Available Models:**
- `gpt-4`
- `gpt-4-turbo`
- `gpt-3.5-turbo`

### Anthropic Configuration

```json
{
  "TenSecondTom": {
    "Anthropic": {
      "Model": "claude-3-5-sonnet-20241022",
      "MaxTokens": 2000
    }
  }
}
```

**Available Models:**
- `claude-3-5-sonnet-20241022`
- `claude-3-opus-20240229`
- `claude-3-haiku-20240307`

### Per-Command Override

Override the default provider for a single command:

```bash
tom today --provider Anthropic
tom thisweek --provider OpenAI
```

---

## Environment Variables

Complete list of environment variables:

### API Keys
```bash
TenSecondTom__OpenAI__ApiKey
TenSecondTom__Anthropic__ApiKey
```

### LLM Provider
```bash
TenSecondTom__LlmProvider                    # OpenAI or Anthropic
TenSecondTom__OpenAI__Model                  # gpt-4
TenSecondTom__OpenAI__MaxTokens              # 2000
TenSecondTom__OpenAI__Temperature            # 0.7
TenSecondTom__Anthropic__Model               # claude-3-5-sonnet-20241022
TenSecondTom__Anthropic__MaxTokens           # 2000
```

### Storage
```bash
TenSecondTom__MemoryDirectory                # ./.memory
```

### Data Retention
```bash
TenSecondTom__DataRetention__DefaultPolicy   # OneYear
TenSecondTom__DataRetention__AutoPurgeEnabled # true
```

### Logging
```bash
TenSecondTom__Serilog__MinimumLevel__Default # Information
```

---

## Configuration Precedence

Ten Second Tom loads configuration in the following order (later sources override earlier ones):

1. `appsettings.json` (in application directory)
2. `appsettings.{Environment}.json` (e.g., `appsettings.Development.json`)
3. User Secrets (development only)
4. Environment Variables
5. Command-line arguments

---

## Troubleshooting

### "API key not found" Error

Check your configuration in order:

1. **User Secrets:**
   ```bash
   dotnet user-secrets list --project src
   ```

2. **Environment Variables:**
   ```bash
   echo $TenSecondTom__OpenAI__ApiKey  # macOS/Linux
   echo %TenSecondTom__OpenAI__ApiKey% # Windows
   ```

3. **Configuration Files:**
   Check `appsettings.json` and `appsettings.Development.json`

### "Invalid API key" Error

- Verify your API key is correct
- Check that you're using the right provider (OpenAI vs Anthropic)
- Ensure your API key has active credits/quota

### "Permission denied" for Memory Directory

Ensure the configured directory is writable:

```bash
ls -la ~/.config/ten-second-tom/  # Check permissions
chmod 755 ~/.config/ten-second-tom/  # Fix if needed
```

---

## Best Practices

1. **Never commit secrets** to version control
2. **Use User Secrets** for local development
3. **Use Environment Variables** for production deployments
4. **Enable auto-purge** if you don't need long-term storage
5. **Set appropriate log levels** (Information for production, Debug for troubleshooting)
6. **Backup your memory directory** regularly if using long-term retention

---

For more information, see:
- [README.md](README.md) - General usage guide
- [ENVIRONMENT.md](ENVIRONMENT.md) - Environment-specific configuration
- [SECURITY.md](SECURITY.md) - Security best practices
