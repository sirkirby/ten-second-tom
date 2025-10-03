# Configuration Setup

This document explains how to configure Ten Second Tom with your API keys and secrets.

## User Secrets (Development)

For local development, use .NET User Secrets to store sensitive configuration like API keys. User Secrets are stored outside the repository in your user profile directory.

### Setting Up User Secrets

1. **Navigate to the source directory:**
   ```bash
   cd src
   ```

2. **Set your OpenAI API key:**
   ```bash
   dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "your-openai-api-key-here"
   ```

3. **Set your Anthropic API key:**
   ```bash
   dotnet user-secrets set "TenSecondTom:Anthropic:ApiKey" "your-anthropic-api-key-here"
   ```

4. **Verify your secrets (optional):**
   ```bash
   dotnet user-secrets list
   ```

### Configuration Hierarchy

Ten Second Tom uses the following configuration priority (highest to lowest):

1. **Command-line arguments** (highest priority)
   ```bash
   tom today --llm-provider Anthropic
   ```

2. **Environment variables**
   ```bash
   export TenSecondTom__OpenAI__ApiKey="your-key"
   ```
   Note: Use double underscores (`__`) to represent nested configuration sections.

3. **User Secrets** (development only)
   ```bash
   dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "your-key"
   ```

4. **appsettings.Development.json** (development environment)

5. **appsettings.json** (defaults, lowest priority)

## Environment Variables (Production)

For production deployments, use environment variables instead of User Secrets:

```bash
# Linux/macOS
export TenSecondTom__OpenAI__ApiKey="your-openai-api-key"
export TenSecondTom__Anthropic__ApiKey="your-anthropic-api-key"
export TenSecondTom__MemoryDirectory="/var/tom/memory"

# Windows (PowerShell)
$env:TenSecondTom__OpenAI__ApiKey="your-openai-api-key"
$env:TenSecondTom__Anthropic__ApiKey="your-anthropic-api-key"
$env:TenSecondTom__MemoryDirectory="C:\tom\memory"
```

## Configuration Options

### Application Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `TenSecondTom:MemoryDirectory` | `./.memory` | Directory where memory entries are stored |
| `TenSecondTom:LlmProvider` | `OpenAI` | Default LLM provider (`OpenAI` or `Anthropic`) |

### OpenAI Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `TenSecondTom:OpenAI:ApiKey` | *(required)* | Your OpenAI API key |
| `TenSecondTom:OpenAI:Model` | `gpt-4` | Model to use for completions |
| `TenSecondTom:OpenAI:MaxTokens` | `2000` | Maximum tokens for responses |

### Anthropic Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `TenSecondTom:Anthropic:ApiKey` | *(required)* | Your Anthropic API key |
| `TenSecondTom:Anthropic:Model` | `claude-3-sonnet-20240229` | Model to use for completions |
| `TenSecondTom:Anthropic:MaxTokens` | `2000` | Maximum tokens for responses |

### Data Retention Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `TenSecondTom:DataRetention:DefaultPolicy` | `Indefinite` | Retention policy: `Indefinite`, `Days30`, `Days90`, `OneYear`, `TwoYears` |
| `TenSecondTom:DataRetention:AutoPurgeEnabled` | `false` | Automatically purge old entries on startup |

## Security Best Practices

- ✅ **DO** use User Secrets for local development
- ✅ **DO** use environment variables for production
- ✅ **DO** use a secrets manager (Azure Key Vault, AWS Secrets Manager, etc.) for production deployments
- ❌ **DO NOT** commit API keys to version control
- ❌ **DO NOT** put secrets in `appsettings.json` or `appsettings.Development.json`

## Troubleshooting

### User Secrets Not Found

If you get an error about missing User Secrets:

1. Verify User Secrets are initialized:
   ```bash
   cd src
   dotnet user-secrets list
   ```

2. If empty, set your API keys as shown above.

### API Key Not Working

1. Check your configuration priority - a higher-priority source may be overriding your key
2. Verify the key format matches your provider's requirements
3. Check the application logs in `.logs/` for authentication errors

### Configuration Not Loading

Ensure `appsettings.json` is copied to the output directory:
```bash
cd src
dotnet build
# Check that bin/Debug/net9.0/appsettings.json exists
```

## Related Documentation

- [.NET User Secrets Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [.NET Configuration Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [OpenAI API Keys](https://platform.openai.com/api-keys)
- [Anthropic API Keys](https://console.anthropic.com/settings/keys)
