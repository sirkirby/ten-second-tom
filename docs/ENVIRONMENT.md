# Environment Configuration

This document explains environment-based configuration for Ten Second Tom, including environment variables, development modes, and advanced configuration scenarios.

> **Quick Start:** Most users should use `tom setup` for initial configuration. This document covers advanced environment-based configuration for special use cases.

> **Related Documentation:**
> - [Configuration Guide](CONFIGURATION.md) - Primary configuration via setup wizard
> - [Authentication Setup](AUTHENTICATION.md) - SSH key configuration
> - [Security Policy](../SECURITY.md) - Security best practices

---

## Primary Configuration Method

**For most users:** Use the interactive setup wizard:

```bash
# First-time setup (launches automatically)
tom today

# Manual setup/reconfiguration
tom setup

# View current configuration
tom config show
```

**Configuration is stored in:**
- **Primary:** User configuration file (`~/ten-second-tom/config/config.json`)
- **Fallback:** `appsettings.json` (framework defaults only)

This document covers **environment variable overrides** and **advanced scenarios**.

---

## Configuration Hierarchy

Settings are loaded in this order (later sources override earlier ones):

1. `appsettings.json` (framework defaults)
2. `appsettings.{Environment}.json` (environment-specific)
3. **User Configuration** (`~/ten-second-tom/config/config.json`)
4. **Environment Variables** (runtime overrides)
5. Command-line arguments (highest priority)

---

## Environment Variables

### When to Use Environment Variables

- **Production deployments** - Server/container environments
- **CI/CD pipelines** - Automated testing and deployment
- **One-time overrides** - Testing different settings temporarily
- **Cross-platform scripts** - Portable configuration

**For local development:** Use `tom setup` instead of manual environment variables.

### Naming Convention

Use double underscores (`__`) to specify nested configuration keys:

```bash
# Maps to: TenSecondTom:Llm:ApiKey
TenSecondTom__Llm__ApiKey=your-key

# Maps to: Llm:Provider
Llm__Provider=Anthropic

# Maps to: Serilog:MinimumLevel:Default
Serilog__MinimumLevel__Default=Debug
```

### Common Environment Variables

**LLM Configuration:**
```bash
# Provider selection
Llm__Provider=OpenAI              # or Anthropic
Llm__ApiKey=sk-your-key-here
Llm__Model=gpt-4o-mini            # Optional, uses default if not set

# Legacy format (still supported)
TenSecondTom__Llm__Provider=OpenAI
TenSecondTom__Llm__ApiKey=sk-your-key-here
TenSecondTom__Llm__Model=gpt-4o-mini
```

**Storage Configuration:**
```bash
TenSecondTom__MemoryDirectory=~/ten-second-tom
```

**Audio Configuration:**
```bash
# STT Provider Selection
TenSecondTom__Audio__SttProvider=whisper-cpp  # or openai
TenSecondTom__Audio__SttApiKey=sk-your-api-key-here

# STT Fallback Configuration
TenSecondTom__Audio__SttFallbackEnabled=true
TenSecondTom__Audio__SttFallbackProvider=openai
TenSecondTom__Audio__SttFallbackApiKey=sk-fallback-api-key-here

# Recording Settings
TenSecondTom__Audio__Recorder__InputVolume=1.0
TenSecondTom__Audio__Recorder__EnableNoiseReduction=true
TenSecondTom__Audio__Recorder__EnableFrequencyFilters=true

# Preprocessing Settings
TenSecondTom__Audio__Preprocessing__RemoveSilence=true
TenSecondTom__Audio__Preprocessing__SilenceThresholdDb=-50
TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs=500
```

**SSH Configuration:**
```bash
Ssh__KeyPath=~/.ssh/id_ed25519
Ssh__KeySource=ManualPath
```

**Logging:**
```bash
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Default=Debug           # More verbose
```

**Application Environment:**
```bash
DOTNET_ENVIRONMENT=Development    # or Production, Staging
```

---

## Setting Environment Variables

### Option 1: Shell Export (Session-wide)

**macOS/Linux (bash/zsh):**
```bash
export Llm__Provider=Anthropic
export Llm__ApiKey=sk-ant-your-key-here
export DOTNET_ENVIRONMENT=Development

# Run application
tom today
```

**Windows (PowerShell):**
```powershell
$env:Llm__Provider="Anthropic"
$env:Llm__ApiKey="sk-ant-your-key-here"
$env:DOTNET_ENVIRONMENT="Development"

# Run application
tom today
```

**Windows (Command Prompt):**
```cmd
set Llm__Provider=Anthropic
set Llm__ApiKey=sk-ant-your-key-here
set DOTNET_ENVIRONMENT=Development

tom today
```

### Option 2: Inline (One-time Override)

```bash
# Test with different model temporarily
Llm__Model=gpt-4o tom today

# Use different memory directory
TenSecondTom__Storage__MemoryDirectory=/tmp/test-memory tom today
```

### Option 3: Shell Profile (Persistent)

**macOS/Linux (~/.zshrc or ~/.bashrc):**
```bash
# Add to end of file
export DOTNET_ENVIRONMENT=Development
export Llm__Provider=Anthropic
export Llm__ApiKey=sk-ant-your-key-here

# Reload shell
source ~/.zshrc  # or source ~/.bashrc
```

**Windows (PowerShell Profile):**
```powershell
# Open profile
notepad $PROFILE

# Add lines:
$env:DOTNET_ENVIRONMENT="Development"
$env:Llm__Provider="Anthropic"
$env:Llm__ApiKey="sk-ant-your-key-here"
```

### Option 4: .env File (Development Only)

Create a `.env` file in your working directory:

```bash
# Copy the example
cp .env.example .env

# Edit with your settings
DOTNET_ENVIRONMENT=Development
Llm__Provider=Anthropic
Llm__ApiKey=sk-ant-your-key-here
```

**Notes:**
- `.env` files are automatically loaded at startup
- Already in `.gitignore` to prevent committing secrets
- Supplementary to user configuration (doesn't replace config.json)
- Environment variables in `.env` override configuration file settings

⚠️ **Security Warning:** The `.env` file approach stores secrets in plain text. For production, use proper secrets management.

---

## Development Mode

Enable development mode for local testing and debugging:

```bash
export DOTNET_ENVIRONMENT=Development
tom today
```

### Development Mode Features

When `DOTNET_ENVIRONMENT=Development`:

- **Mock Authentication** - Bypasses SSH key requirements (for testing)
- **Debug Logging** - More verbose output
- **Development Warnings** - Visible CLI warnings
- **Relaxed Validation** - Some checks are loosened

You'll see:
```
⚠ Development Mode: Authentication bypassed
[WRN] Using MockAuthenticationService - authentication bypassed for development
```

### Example Development Setup

```bash
# Create .env file (optional, supplementary to config.json)
cat > .env << EOF
DOTNET_ENVIRONMENT=Development
EOF

# Run setup wizard (stores config in ~/ten-second-tom/config/config.json)
tom setup

# Run the app
tom today
```

---

## Production Configuration

### Recommended Approach

1. **Install via package manager** (Homebrew, Winget, etc.)
2. **Use environment variables** for any runtime overrides
3. **Use configuration file** (`~/ten-second-tom/config/config.json`) for persistent settings
4. **Use shipped configuration files** only for framework defaults (logging)

### CI/CD Example (GitHub Actions)

```yaml
env:
  DOTNET_ENVIRONMENT: Production
  Llm__Provider: OpenAI
  Llm__ApiKey: ${{ secrets.OPENAI_API_KEY }}
  Llm__Model: gpt-4o-mini
```

---

## Advanced Scenarios

### Override Single Setting Temporarily

```bash
# Test different model without changing saved config
Llm__Model=gpt-4o tom today

# Use different memory directory for testing
TenSecondTom__Storage__MemoryDirectory=/tmp/test tom today
```

### Multiple Environment Configurations

Create environment-specific config files:

**appsettings.Development.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

**appsettings.Production.json:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```

Switch environments:
```bash
DOTNET_ENVIRONMENT=Development tom today  # Uses appsettings.Development.json
DOTNET_ENVIRONMENT=Production tom today   # Uses appsettings.Production.json
```

### Script-Friendly Configuration

```bash
#!/bin/bash
# backup-memories.sh

export DOTNET_ENVIRONMENT=Production
export Llm__Provider=OpenAI
export Llm__ApiKey="${OPENAI_API_KEY}"  # From parent environment
export TenSecondTom__Storage__MemoryDirectory="${MEMORY_DIR:-~/.tom/memory}"

tom today
tom thisweek
```

---

## Troubleshooting

### Changes Not Taking Effect

**Problem:** Environment variable changes don't seem to apply.

**Solutions:**

1. **Restart your shell** - Environment variables are set at shell startup
2. **Check spelling** - Variable names are case-sensitive
3. **Verify double underscores** - Use `__` not `:` in env vars
4. **Check hierarchy** - Configuration file may be overriding your environment variables

```bash
# View all environment variables
env | grep -i tensecondtom
env | grep -i llm

# Check what the app sees
tom config show
```

### .env File Not Loading

**Problem:** Settings in `.env` file are ignored.

**Solutions:**

1. **Check file location** - `.env` must be in the working directory where you run `tom`
2. **Verify format** - No spaces around `=`, one variable per line
3. **Restart application** - `.env` is loaded once at startup
4. **Check for quotes** - Usually don't need quotes unless value has spaces

```bash
# Correct format
Llm__Provider=OpenAI
Llm__ApiKey=sk-test123

# Incorrect
Llm__Provider = OpenAI   # ❌ spaces around =
Llm__ApiKey="sk-test"    # ⚠️ quotes usually not needed
```

### Which Configuration Source Is Active?

**Problem:** Not sure which configuration source is being used.

**Solution:** Check the effective configuration:

```bash
tom config show

# Look for the storage location at the bottom:
# "Configuration loaded from: ~/ten-second-tom/config/config.json"
```

### Environment vs Configuration File Priority

**Remember:** Environment variables **override** configuration file settings.

```bash
# config.json has: Llm__Provider=OpenAI
# Environment sets:
export Llm__Provider=Anthropic

# Result: Anthropic is used (environment wins)
tom config show
```

---

## Security Best Practices

### ✅ DO

- Use `tom setup` for local development (stores in `~/ten-second-tom/config/config.json`)
- Use environment variables for production deployments
- Store secrets in CI/CD secret managers
- Keep `.env` in `.gitignore`
- Protect `config.json` with proper file permissions
- Regularly rotate API keys
- Use minimal permission scopes

### ❌ DO NOT

- Commit secrets to version control
- Share `.env` files
- Store secrets in `appsettings.json`
- Use hardcoded secrets in scripts
- Expose secrets in logs or error messages
- Reuse secrets across environments

---

## Summary

### For Local Development
✅ **Recommended:** `tom setup` → `~/ten-second-tom/config/config.json`
- Interactive wizard
- Centralized configuration
- Easy to update with `tom config`

### For Production
✅ **Recommended:** Environment variables
- No files to manage
- Works with containers
- Integrates with secret managers

### For Testing/Overrides
✅ **Recommended:** Inline environment variables
- Temporary changes
- No config pollution
- Easy to script

---

**Need More Help?**

- Configuration Guide: [CONFIGURATION.md](CONFIGURATION.md)
- Authentication Setup: [AUTHENTICATION.md](AUTHENTICATION.md)
- Security Policy: [../SECURITY.md](../SECURITY.md)
- Report Issues: [GitHub Issues](https://github.com/sirkirby/ten-second-tom/issues)
