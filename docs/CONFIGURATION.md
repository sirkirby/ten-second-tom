# Configuration Guide

This document explains how to configure Ten Second Tom using the built-in setup wizard and configuration commands.

> **Related Documentation:**
> - [Authentication Setup](AUTHENTICATION.md) - SSH key configuration and agent setup
> - [Audio Configuration](AUDIO.md) - Microphone settings, silence removal, and recording optimization
> - [Security Policy](../SECURITY.md) - Security best practices and key management
> - [Environment Setup](ENVIRONMENT.md) - Environment variables and deployment configuration

## Quick Start

The first time you run Ten Second Tom, it will automatically launch a guided setup wizard that walks you through all configuration steps:

```bash
tom today
# → Setup wizard launches automatically
```

Or manually start the setup wizard:

```bash
tom setup
```

## Setup Wizard

The setup wizard is an interactive, 8-step process that collects all necessary configuration:

### Step 1: SSH Key Configuration

The wizard automatically detects ED25519 SSH keys from:
- System SSH agent (`ssh-agent`)
- 1Password SSH agent (macOS)
- Secretive SSH agent (macOS)
- File system (`~/.ssh/*.pub`)

**What you'll see:**
```
Step 1 of 8: SSH Key Configuration

Detecting SSH keys...
✓ Found 3 SSH keys:

1. [System Agent] id_ed25519
2. [1Password] work_key
3. [File] ~/.ssh/personal_ed25519

Select SSH key to use: _
```

**If no keys are found:**
The wizard provides guidance on how to generate a new SSH key or add an existing one to your SSH agent.

**Generate a new SSH key:**
```bash
ssh-keygen -t ed25519 -C "your-email@example.com"
```

Learn more: [GitHub SSH Documentation](https://docs.github.com/en/authentication/connecting-to-github-with-ssh)

### Step 2: LLM Provider Selection

Choose between OpenAI (GPT-4, GPT-3.5) or Anthropic (Claude 3.5).

```
Step 2 of 8: LLM Provider Selection

Choose your AI provider:

1. OpenAI (GPT-4, GPT-3.5)
2. Anthropic (Claude 3.5)

Select provider: _
```

### Step 3: API Key Configuration

Enter your API key for the selected provider. The key is masked as you type for security.

```
Step 3 of 8: API Key Configuration

Enter your OpenAI API key: ****************************************

Validating API key...
✓ Format valid
✓ Network validation successful
```

**Get your API keys:**
- OpenAI: [https://platform.openai.com/api-keys](https://platform.openai.com/api-keys)
- Anthropic: [https://console.anthropic.com/settings/keys](https://console.anthropic.com/settings/keys)

### Step 4: Memory Storage Location

Specify where Ten Second Tom should store your daily entries and memories.

```
Step 4 of 8: Memory Storage Location

Where should I store your memories?
Default: ~/ten-second-tom

Directory path [default]: _
```

Press Enter to accept the default, or provide a custom path.

### Step 5: Logging Level (Optional)

Choose how verbose you want the application logs to be:

- **Debug**: Verbose output for troubleshooting
- **Information**: Standard output (recommended)
- **Warning**: Quiet mode, only warnings and errors
- **Error**: Silent mode, only errors

### Step 6: Data Retention (Optional)

Choose how long to keep your memories before automatic deletion:

- **Unlimited**: Keep all memories forever (recommended)
- **Custom days**: Automatically delete memories older than X days

```
Step 6 of 8: Data Retention

How long should memories be kept? (enter 'unlimited' or number of days)
Default: unlimited
```

### Step 7: Configuration Summary

Review all your settings before saving:

```
Step 7 of 8: Configuration Summary

┌─────────────────────┬──────────────────────────────────────────┐
│ Setting             │ Value                                     │
├─────────────────────┼──────────────────────────────────────────┤
│ SSH Key             │ id_ed25519                               │
│ LLM Provider        │ OpenAI                                   │
│ API Key             │ ****************************************7890 │
│ Memory Directory    │ /Users/you/ten-second-tom                │
│ Log Level           │ Information                              │
│ Retention Days      │ Unlimited (never delete)                 │
└─────────────────────┴──────────────────────────────────────────┘

Save this configuration? (Y/n): _
```

### Step 8: Save Configuration

Your configuration is securely saved to `~/ten-second-tom/config/config.json`.

```
Step 8 of 8: Saving Configuration

Saving configuration...
✓ Setup complete!
Configuration saved to: ~/ten-second-tom/config/config.json

You can view your configuration anytime with: tom config show
To change individual settings, use: tom config set <setting-name> <value>
```

## Configuration Commands

### View Current Configuration

Display all current settings (API keys are masked):

```bash
tom config show
```

**Output:**
```
Current Configuration:

┌─────────────────────┬──────────────────────────────────────────┐
│ Setting             │ Value                                     │
├─────────────────────┼──────────────────────────────────────────┤
│ SSH Key             │ id_ed25519                               │
│ LLM Provider        │ OpenAI                                   │
│ API Key             │ ****************************************7890 │
│ Memory Directory    │ /Users/you/ten-second-tom                │
│ Log Level           │ Information                              │
│ Retention Days      │ Unlimited                                │
└─────────────────────┴──────────────────────────────────────────┘
```

**Show API keys (unmasked):**
```bash
tom config show --show-secrets
```

### Update Individual Settings

Change a single configuration setting without running the full setup wizard:

#### Change LLM Provider

```bash
tom config set llm-provider Anthropic
# Valid values: OpenAI, Anthropic
```

#### Configure LLM Model

You can select or change the specific AI model used by Ten Second Tom:

```bash
# Interactive model selection (recommended)
tom config llm
# → Shows provider and model selection prompts with descriptions
```

**Via environment variables:**

```bash
# macOS/Linux
export TenSecondTom__Llm__Model="gpt-4o-mini"
export TenSecondTom__Llm__Model="claude-3-haiku-20240307"

# Windows (PowerShell)
$env:TenSecondTom__Llm__Model="gpt-4o-mini"
$env:TenSecondTom__Llm__Model="claude-3-haiku-20240307"
```

**Supported models:**

**OpenAI (Budget):**
- `gpt-4o-mini` - Fast, cost-effective model optimized for speed and value (default)

**OpenAI (Balanced):**
- `gpt-4o` - GPT-4 Omni, high capability with reasonable cost
- `chatgpt-4o-latest` - Latest ChatGPT-4 Omni, continuously updated

**Anthropic (Budget):**
- `claude-3-haiku-20240307` - Fast and cost-effective, version 3.0 (default)
- `claude-3-5-haiku-20241022` - Improved version, better performance, version 3.5

**Anthropic (Balanced):**
- `claude-sonnet-4-20250514` - Balanced capability and cost, Claude 4.0 Sonnet
- `claude-sonnet-4-5-20250929` - Enhanced version, Claude 4.5 Sonnet

**Anthropic (Premium):**
- `claude-opus-4-20250514` - Highest capability, premium cost, Claude 4.0 Opus
- `claude-opus-4-1-20250805` - Top-tier model, Claude 4.1 Opus

**Notes:**

- If no model is specified, a default model for your provider is automatically used
- Model validation occurs at startup - invalid models will trigger an error with suggestions
- Model IDs must match the configured provider (e.g., GPT models require OpenAI provider)
- Use `tom config llm` for an interactive selection with descriptions and cost tiers
- Deprecated models from previous versions are no longer supported - the CLI will suggest alternatives

#### Update API Key

```bash
tom config set api-key "sk-ant-your-new-key-here"
# Format is validated before saving
```

#### Change Memory Directory

```bash
tom config set memory-directory "~/Documents/tom-memories"
# Path is resolved and validated
```

#### Update SSH Key Path

```bash
tom config set ssh-key-path "~/.ssh/id_ed25519.pub"
# File must exist, validated before saving
```

#### Change Log Level

```bash
tom config set log-level Debug
# Valid values: Debug, Information, Warning, Error
```

#### Update Data Retention

```bash
tom config set retention-days 90
# Must be a positive integer (days)
```

### Validate Configuration

Check if your current configuration is valid:

```bash
tom config validate
```

**Output on success:**
```
✓ Configuration is valid
```

**Output on failure:**
```
✗ Configuration validation failed: Required fields are missing or invalid
Run 'tom setup' to reconfigure.
```

### Reset Configuration

**Note:** The `reset` action is implemented in the backend but not currently exposed as a CLI command. To reset your configuration, you can:

1. Delete the configuration file manually and re-run setup:
   ```bash
   # Delete user configuration
   rm ~/ten-second-tom/config/config.json

   # Re-run setup wizard
   tom setup
   ```

2. Or use `tom setup --force` to reconfigure all settings.

**Output on success:**
```
✓ Configuration is valid
```

**Output on failure:**
```
✗ Configuration validation failed: Required fields are missing or invalid
Run 'tom setup' to reconfigure.
```

### Reconfigure Everything

Run the setup wizard again to walk through all settings with current values as defaults:

```bash
tom setup
# or
tom setup --force
```

The wizard will show your current values and allow you to change any setting.

## Configuration Storage

### User Configuration File (Primary)

Ten Second Tom stores all user configuration in `~/ten-second-tom/config/config.json`. This file is automatically created and managed by the setup wizard (`tom config`).

**Configuration file location:**

- **macOS/Linux**: `~/ten-second-tom/config/config.json`
- **Windows**: `%USERPROFILE%\ten-second-tom\config\config.json`

**Advantages:**
- ✅ All settings in one predictable location
- ✅ Easy to backup and restore
- ✅ Supports nested configuration sections
- ✅ Automatically created by setup wizard
- ✅ Protected by file system permissions

### Shipped Configuration (Framework Settings Only)

The application ships with `appsettings.json` containing **only framework-level configuration** (Serilog logging). All application-specific settings are managed through `config.json`.

**Shipped files contain:**
- ✅ Serilog logging configuration
- ✅ Framework defaults
- ❌ **NO** application secrets or user settings

**Security:** User configuration is separated from shipped files to prevent accidental commits and maintain clean separation of concerns.

### Environment Variables (Advanced)

For production deployments or CI/CD pipelines, you can use environment variables:

```bash
# macOS/Linux
export TenSecondTom__OpenAI__ApiKey="your-openai-api-key"
export TenSecondTom__Anthropic__ApiKey="your-anthropic-api-key"
export TenSecondTom__MemoryDirectory="/var/tom/memory"

# Windows (PowerShell)
$env:TenSecondTom__OpenAI__ApiKey="your-openai-api-key"
$env:TenSecondTom__Anthropic__ApiKey="your-anthropic-api-key"
$env:TenSecondTom__MemoryDirectory="C:\tom\memory"
```

**Note:** Use double underscores (`__`) to represent nested configuration sections.

## Configuration Hierarchy

Ten Second Tom follows standard .NET configuration patterns with a hierarchical override system.

### Priority Order (Highest to Lowest)

1. **Command-line arguments** - Highest priority, runtime overrides
2. **Environment variables** - System or session configuration
3. **User Configuration** - User-specific settings (`~/ten-second-tom/config/config.json`)
4. **appsettings.{Environment}.json** - Environment-specific settings (Development, Production, etc.)
5. **appsettings.json** - Framework defaults (logging only), lowest priority

### Configuration Keys

Ten Second Tom stores configuration using a structured hierarchy:

```json
{
  "TenSecondTom": {
    "MemoryDirectory": "~/ten-second-tom",
    "Auth": {
      "SshAgentProvider": "Auto",
      "PublicKeyPath": "~/.ssh/id_ed25519.pub"
    },
    "Llm": {
      "Provider": "OpenAI",
      "ApiKey": "your-api-key",
      "Model": "gpt-5-nano",
      "MaxInputTokens": 50000,
      "SpeechToTextModel": "whisper-1"
    },
    "Audio": {
      "SttProvider": "built-in-local",
      "SttApiKey": null,
      "KeepFiles": true,
      "Recorder": {
        "FfmpegPath": "ffmpeg",
        "InputVolume": 1.0,
        "EnableNoiseReduction": true,
        "EnableFrequencyFilters": true
      },
      "LocalWhisper": {
        "BinaryPath": "whisper-cli",
        "ModelPath": "~/.cache/whisper/ggml-base.en.bin"
      },
      "Preprocessing": {
        "RemoveSilence": true,
        "SilenceThresholdDb": -50,
        "MinimumSilenceDurationMs": 500
      },
      "Timeouts": {
        "TodaySeconds": 180,
        "RecordSeconds": 900
      }
    },
    "DataRetention": {
      "DefaultPolicy": "Indefinite",
      "AutoPurgeEnabled": false
    },
    "Setup": {
      "SshKeyDetectionTimeoutSeconds": 5,
      "ApiValidationTimeoutSeconds": 10,
      "TotalSetupTimeoutSeconds": 120
    }
  }
}
```

### Environment Variable Naming

Following .NET conventions, use double underscores (`__`) to specify nested keys:

```bash
# LLM Configuration
TenSecondTom__Llm__Provider=Anthropic
TenSecondTom__Llm__ApiKey=your-api-key-here
TenSecondTom__Llm__Model=claude-sonnet-4-20250514

# Memory Directory
TenSecondTom__MemoryDirectory=~/ten-second-tom

# Audio Configuration (STT)
TenSecondTom__Audio__SttProvider=built-in-local
TenSecondTom__Audio__SttApiKey=sk-...

# Audio Recording Settings
TenSecondTom__Audio__Recorder__InputVolume=1.0
TenSecondTom__Audio__Recorder__EnableNoiseReduction=true
TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true

# SSH/Auth Configuration
TenSecondTom__Auth__SshAgentProvider=Auto
TenSecondTom__Auth__PublicKeyPath=~/.ssh/id_ed25519.pub
```

The double underscore (`__`) in environment variables maps to a colon (`:`) in configuration keys,
which is the standard .NET convention for nested configuration.

### Viewing Effective Configuration

```bash
# Show current configuration (secrets masked)
tom config show

# Show with secrets visible (use caution!)
tom config show --show-secrets
```

## Configuration Standard for Developers

> **Audience:** This section is for developers contributing to Ten Second Tom.  
> **Status:** MANDATORY - Must be enforced in all features

All configuration in Ten Second Tom uses the **TenSecondTom:** namespace for consistency. This section establishes the canonical pattern that **MUST** be followed in all current and future features.

### Configuration Key Naming Convention

Ten Second Tom uses standard .NET configuration patterns:

**Configuration Files (config.json, appsettings.json)** - Use colons (`:`) for hierarchy:
```json
{
  "TenSecondTom": {
    "MemoryDirectory": "~/ten-second-tom",
    "Audio": {
      "KeepFiles": true
    },
    "Llm": {
      "Provider": "OpenAI",
      "ApiKey": "sk-..."
    }
  }
}
```

**Environment Variables (.env file, shell exports)** - Use double underscores (`__`) for hierarchy:
```bash
# .env file
TenSecondTom__MemoryDirectory=~/ten-second-tom
TenSecondTom__Audio__KeepFiles=true
TenSecondTom__Llm__Provider=OpenAI
TenSecondTom__Llm__ApiKey=sk-...
```

## Timeout Configuration

SSH key detection and API validation operations have configurable timeouts to prevent the setup wizard from hanging.

**Default timeouts (in `~/ten-second-tom/config/config.json`):**

```json
{
  "TenSecondTom": {
    "Setup": {
      "SshKeyDetectionTimeoutSeconds": 5,
      "ApiValidationTimeoutSeconds": 10,
      "TotalSetupTimeoutSeconds": 120
    }
  }
}
```

**Adjust timeouts:**

Edit `~/ten-second-tom/config/config.json` or use environment variables:

```json
{
  "TenSecondTom": {
    "Setup": {
      "SshKeyDetectionTimeoutSeconds": 10,  // Increase if SSH detection times out
      "ApiValidationTimeoutSeconds": 20,    // Increase for slow networks
      "TotalSetupTimeoutSeconds": 180       // Overall setup timeout
    }
  }
}
```

Or via environment variables:
```bash
export TenSecondTom__Setup__SshKeyDetectionTimeoutSeconds=10
export TenSecondTom__Setup__ApiValidationTimeoutSeconds=20
export TenSecondTom__Setup__TotalSetupTimeoutSeconds=180
```

## Troubleshooting

### Setup Wizard Doesn't Launch

**Problem:** Running `tom today` doesn't trigger the setup wizard.

**Solution:** Configuration already exists. To reconfigure, run:
```bash
tom setup --force
```

### No SSH Keys Detected

**Problem:** Setup wizard reports "No SSH keys detected."

**Solutions:**

1. **Generate a new ED25519 key:**
   ```bash
   ssh-keygen -t ed25519 -C "your-email@example.com"
   ```

2. **Add existing key to SSH agent:**
   ```bash
   # macOS/Linux
   eval "$(ssh-agent -s)"
   ssh-add ~/.ssh/id_ed25519
   ```

3. **Manually specify key path:**
   During setup, when no keys are detected, you can exit and later set the path manually:
   ```bash
   tom config set ssh-key-path "~/.ssh/id_ed25519.pub"
   ```

4. **Check SSH agent is running:**
   ```bash
   # macOS/Linux
   echo $SSH_AUTH_SOCK
   # Should output a path like /tmp/ssh-xxxx/agent.12345
   ```

### API Key Validation Fails

**Problem:** Setup wizard reports "Invalid API key format" or "Network validation failed."

**Solutions:**

1. **Verify key format:**
   - OpenAI: `sk-[a-zA-Z0-9]{48,}` (starts with `sk-`)
   - Anthropic: `sk-ant-[a-zA-Z0-9\-]{32,}` (starts with `sk-ant-`)

2. **Check network connectivity:**
   ```bash
   # Test OpenAI connectivity
   curl https://api.openai.com/v1/models
   
   # Test Anthropic connectivity
   curl https://api.anthropic.com/v1/messages
   ```

3. **Increase timeout:**
   Edit `appsettings.json` and increase `ApiValidationTimeoutSeconds`.

4. **Generate a new key:**
   - OpenAI: [https://platform.openai.com/api-keys](https://platform.openai.com/api-keys)
   - Anthropic: [https://console.anthropic.com/settings/keys](https://console.anthropic.com/settings/keys)

### Configuration Not Saved

**Problem:** Setup completes but running commands says "No configuration found."

**Solutions:**

1. **Check configuration file location:**
   ```bash
   # The setup wizard shows the save location at the end
   # Verify the file exists:
   ls ~/ten-second-tom/config/config.json
   ```

2. **Check file permissions:**
   ```bash
   # Ensure you have write permissions
   ls -la ~/ten-second-tom/config/

   # Fix if needed
   mkdir -p ~/ten-second-tom/config
   chmod 755 ~/ten-second-tom/config
   ```

3. **Check for errors:**
   Review the setup output for any error messages about file creation or permissions.

4. **Re-run setup:**
   ```bash
   tom setup --force
   ```

### Can't Find Configuration File

**Problem:** Want to manually edit or backup configuration but can't find the file.

**Solutions:**

1. **Standard location:**
   ```bash
   # macOS/Linux
   cat ~/ten-second-tom/config/config.json

   # Windows
   type %USERPROFILE%\ten-second-tom\config\config.json
   ```

2. **Show configuration:**
   ```bash
   tom config show
   # The output includes the storage location
   ```

3. **Backup configuration:**
   ```bash
   # Create backup
   cp ~/ten-second-tom/config/config.json ~/ten-second-tom/config/config.backup.json

   # Or backup to another location
   cp ~/ten-second-tom/config/config.json ~/backups/tom-config-$(date +%Y%m%d).json
   ```

## Rollback and Recovery

### View Current Configuration

Before making changes, always view the current configuration:

```bash
tom config --show --show-secrets > config-backup.txt
```

### Restore from Backup

If you need to restore a previous configuration:

1. **Re-run setup wizard:**
   ```bash
   tom setup --force
   ```
   Enter values from your backup.

2. **Update individual settings:**
   ```bash
   tom config set llm-provider OpenAI
   tom config set api-key "sk-your-old-key"
   # ... etc.
   ```

3. **Manual configuration restore:**
   ```bash
   # Restore from backup
   cp ~/backups/tom-config-20251026.json ~/ten-second-tom/config/config.json

   # Or restore from local backup
   cp ~/ten-second-tom/config/config.backup.json ~/ten-second-tom/config/config.json
   ```

### Start Fresh

To completely reset configuration and start over:

1. **Delete configuration file:**
   ```bash
   # macOS/Linux
   rm ~/ten-second-tom/config/config.json

   # Windows (PowerShell)
   Remove-Item -Path $env:USERPROFILE\ten-second-tom\config\config.json
   ```

2. **Re-run setup:**
   ```bash
   tom setup
   ```

## Advanced Configuration

### Manual Configuration File Editing

You can directly edit the configuration file if needed:

```bash
# Open in your preferred editor
nano ~/ten-second-tom/config/config.json
# or
vim ~/ten-second-tom/config/config.json
# or
code ~/ten-second-tom/config/config.json
```

**Important:** The configuration file must be valid JSON. Invalid JSON will cause the application to fail on startup.

**Recommended:** Use `tom config` commands instead of manual editing to ensure proper validation and formatting.

### Configuration File Structure

The configuration file uses the TenSecondTom namespace for all application settings:

```json
{
  "TenSecondTom": {
    "MemoryDirectory": "~/ten-second-tom",
    "Auth": { "PublicKeyPath": "~/.ssh/id_ed25519.pub" },
    "Llm": { "Provider": "OpenAI", "ApiKey": "sk-..." },
    "Audio": { "SttProvider": "whisper-cpp", ... }
  }
}
```

## Related Documentation

- [.NET Configuration Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [OpenAI API Keys](https://platform.openai.com/api-keys)
- [Anthropic API Keys](https://console.anthropic.com/settings/keys)
- [GitHub SSH Documentation](https://docs.github.com/en/authentication/connecting-to-github-with-ssh)
- [Audio Configuration](AUDIO.md) - Microphone and STT settings
- [Authentication Setup](AUTHENTICATION.md) - SSH key configuration
- [Environment Variables](ENVIRONMENT.md) - Environment-based configuration

## Security Best Practices

- ✅ **DO** use the built-in setup wizard (`tom setup`)
- ✅ **DO** protect `~/ten-second-tom/config/config.json` with proper file permissions
- ✅ **DO** use environment variables for production/CI/CD
- ✅ **DO** regularly rotate API keys
- ✅ **DO** back up configuration before making changes
- ✅ **DO** add `config.json` to `.gitignore` if versioning your memory directory
- ❌ **DO NOT** commit API keys to version control
- ❌ **DO NOT** share configuration files containing secrets
- ❌ **DO NOT** store configuration in publicly accessible locations
- ❌ **DO NOT** put secrets in shipped `appsettings.json` files

---

**Need Help?**

If you encounter issues not covered in this guide, please:
1. Check the logs in `.logs/` directory
2. Run `tom config validate` to check configuration validity
3. Report issues at: [https://github.com/sirkirby/ten-second-tom/issues](https://github.com/sirkirby/ten-second-tom/issues)