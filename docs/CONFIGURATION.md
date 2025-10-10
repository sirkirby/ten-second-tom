# Configuration Guide

This document explains how to configure Ten Second Tom using the built-in setup wizard and configuration commands.

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
Default: ~/.memory/ten-second-tom

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
│ Memory Directory    │ /Users/you/.memory/ten-second-tom        │
│ Log Level           │ Information                              │
│ Retention Days      │ Unlimited (never delete)                 │
└─────────────────────┴──────────────────────────────────────────┘

Save this configuration? (Y/n): _
```

### Step 8: Save Configuration

Your configuration is securely saved to .NET User Secrets (or `appsettings.json` as a fallback).

```
Step 8 of 8: Saving Configuration

Saving configuration...
✓ Setup complete!
Configuration saved to: /Users/you/.microsoft/usersecrets/...

You can view your configuration anytime with: tom config --show
To change individual settings, use: tom config --set <setting-name> <value>
```

## Configuration Commands

### View Current Configuration

Display all current settings (API keys are masked):

```bash
tom config --show
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
│ Memory Directory    │ /Users/you/.memory/ten-second-tom        │
│ Log Level           │ Information                              │
│ Retention Days      │ Unlimited                                │
└─────────────────────┴──────────────────────────────────────────┘
```

**Show API keys (unmasked):**
```bash
tom config --show --show-secrets
```

### Update Individual Settings

Change a single configuration setting without running the full setup wizard:

#### Change LLM Provider

```bash
tom config --set llm-provider Anthropic
# Valid values: OpenAI, Anthropic
```

#### Update API Key

```bash
tom config --set api-key "sk-ant-your-new-key-here"
# Format is validated before saving
```

#### Change Memory Directory

```bash
tom config --set memory-directory "~/Documents/tom-memories"
# Path is resolved and validated
```

#### Update SSH Key Path

```bash
tom config --set ssh-key-path "~/.ssh/id_ed25519.pub"
# File must exist, validated before saving
```

#### Change Log Level

```bash
tom config --set log-level Debug
# Valid values: Debug, Information, Warning, Error
```

#### Update Data Retention

```bash
tom config --set retention-days 90
# Must be a positive integer (days)
```

### Validate Configuration

Check if your current configuration is valid:

```bash
tom config --validate
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

### Reconfigure Everything

Run the setup wizard again to walk through all settings with current values as defaults:

```bash
tom setup
# or
tom setup --force
```

The wizard will show your current values and allow you to change any setting.

## Configuration Storage

### .NET User Secrets (Primary)

Ten Second Tom uses [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) to securely store sensitive configuration like API keys. User Secrets are stored outside the repository in your user profile directory.

**User Secrets location:**

- **macOS/Linux**: `~/.microsoft/usersecrets/<user-secrets-id>/secrets.json`
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user-secrets-id>\secrets.json`

**Advantages:**
- ✅ Secrets are stored outside the project directory
- ✅ Not accidentally committed to version control
- ✅ Per-user configuration on shared machines
- ✅ Encrypted by the operating system

### appsettings.json (Fallback)

If .NET User Secrets cannot be written (e.g., permissions issues), configuration is saved to `appsettings.json` in the application directory.

**Security warning:** The fallback method stores API keys in plain text. Use User Secrets whenever possible.

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

Ten Second Tom uses the following configuration priority (highest to lowest):

1. **Command-line arguments** (highest priority)
2. **Environment variables**
3. **User Secrets** (development/local)
4. **appsettings.Development.json** (development environment)
5. **appsettings.json** (defaults, lowest priority)

## Timeout Configuration

SSH key detection and API validation operations have configurable timeouts to prevent the setup wizard from hanging.

**Default timeouts (in `appsettings.json`):**

```json
{
  "Setup": {
    "SshKeyDetectionTimeoutSeconds": 5,
    "ApiValidationTimeoutSeconds": 10,
    "TotalSetupTimeoutSeconds": 120
  }
}
```

**Adjust timeouts:**

Edit `appsettings.json` in the application directory:

```json
{
  "Setup": {
    "SshKeyDetectionTimeoutSeconds": 10,  // Increase if SSH detection times out
    "ApiValidationTimeoutSeconds": 20,    // Increase for slow networks
    "TotalSetupTimeoutSeconds": 180       // Overall setup timeout
  }
}
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
   tom config --set ssh-key-path "~/.ssh/id_ed25519.pub"
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

1. **Check User Secrets location:**
   ```bash
   # The setup wizard shows the save location at the end
   # Verify the file exists:
   ls ~/.microsoft/usersecrets/*/secrets.json
   ```

2. **Check file permissions:**
   ```bash
   # Ensure you have write permissions
   ls -la ~/.microsoft/usersecrets/
   ```

3. **Use fallback storage:**
   If User Secrets fails, configuration falls back to `appsettings.json`. Check for warnings in the setup output.

4. **Re-run setup:**
   ```bash
   tom setup --force
   ```

### Can't Find Configuration File

**Problem:** Want to manually edit or backup configuration but can't find the file.

**Solutions:**

1. **Show configuration:**
   ```bash
   tom config --show
   # The output includes the storage location
   ```

2. **Find User Secrets directory:**
   ```bash
   # macOS/Linux
   find ~/.microsoft/usersecrets -name "secrets.json"
   
   # Windows (PowerShell)
   Get-ChildItem -Path $env:APPDATA\Microsoft\UserSecrets -Recurse -Filter secrets.json
   ```

3. **Check appsettings.json fallback:**
   If User Secrets failed, check the application directory:
   ```bash
   # macOS/Linux (Homebrew install)
   cat /usr/local/bin/TenSecondTom/appsettings.json
   
   # Windows (default install)
   type "C:\Program Files\TenSecondTom\appsettings.json"
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
   tom config --set llm-provider OpenAI
   tom config --set api-key "sk-your-old-key"
   # ... etc.
   ```

3. **Manual User Secrets restore:**
   ```bash
   # Find User Secrets location
   tom config --show  # Shows path at bottom
   
   # Copy backup over existing file
   cp config-backup.json ~/.microsoft/usersecrets/<id>/secrets.json
   ```

### Start Fresh

To completely reset configuration and start over:

1. **Delete User Secrets file:**
   ```bash
   # macOS/Linux
   rm ~/.microsoft/usersecrets/*/secrets.json
   
   # Windows (PowerShell)
   Remove-Item -Path $env:APPDATA\Microsoft\UserSecrets\*\secrets.json
   ```

2. **Re-run setup:**
   ```bash
   tom setup
   ```

## Advanced Configuration

### Manual User Secrets Management (For Developers)

If you're developing Ten Second Tom, you can use `dotnet user-secrets` commands directly:

```bash
cd src

# Set secrets
dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "your-key"
dotnet user-secrets set "TenSecondTom:Anthropic:ApiKey" "your-key"

# List secrets
dotnet user-secrets list

# Clear all secrets
dotnet user-secrets clear
```

**Note:** For regular users, always use `tom setup` and `tom config` instead of manual commands.

## Related Documentation

- [.NET User Secrets Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [.NET Configuration Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [OpenAI API Keys](https://platform.openai.com/api-keys)
- [Anthropic API Keys](https://console.anthropic.com/settings/keys)
- [GitHub SSH Documentation](https://docs.github.com/en/authentication/connecting-to-github-with-ssh)

## Security Best Practices

- ✅ **DO** use the built-in setup wizard (`tom setup`)
- ✅ **DO** use User Secrets for local development
- ✅ **DO** use environment variables for production/CI/CD
- ✅ **DO** regularly rotate API keys
- ✅ **DO** back up configuration before making changes
- ❌ **DO NOT** commit API keys to version control
- ❌ **DO NOT** share User Secrets files
- ❌ **DO NOT** store secrets in plain text files
- ❌ **DO NOT** put secrets in `appsettings.json` (use as fallback only)

---

**Need Help?**

If you encounter issues not covered in this guide, please:
1. Check the logs in `.logs/` directory
2. Run `tom config --validate` to check configuration validity
3. Report issues at: [https://github.com/sirkirby/ten-second-tom/issues](https://github.com/sirkirby/ten-second-tom/issues)