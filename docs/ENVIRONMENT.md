# Environment Configuration

Ten Second Tom uses environment variables for configuration. You can set these in multiple ways:

## Option 1: .env File (Recommended for Development)

Create a `.env` file in the project root:

```bash
# Copy the example file
cp .env.example .env

# Edit with your settings
DOTNET_ENVIRONMENT=Development
```

The `.env` file is automatically loaded at startup and is already in `.gitignore` to keep your local settings private.

### Available Environment Variables

```bash
# Environment (Development, Staging, Production)
DOTNET_ENVIRONMENT=Development

# Memory storage directory
TenSecondTom__MemoryDirectory=./.memory

# LLM Provider Configuration
TenSecondTom__LlmProvider=OpenAI

# OpenAI Configuration
OPENAI_API_KEY=your-api-key-here

# Anthropic Configuration
ANTHROPIC_API_KEY=your-api-key-here

# SSH Agent Authentication (Phase 3.11a)
TenSecondTom__Auth__PublicKey=ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAA...
TenSecondTom__Auth__PublicKeyPath=~/.ssh/id_ed25519.pub

# Logging
Serilog__MinimumLevel__Default=Debug
```

## Option 2: Shell Export (Session-wide)

```bash
export DOTNET_ENVIRONMENT=Development
export OPENAI_API_KEY=your-key
dotnet run -- today
```

## Option 3: Inline (One-time)

```bash
DOTNET_ENVIRONMENT=Development dotnet run -- today
```

## Option 4: appsettings.json

Edit `src/appsettings.json` or `src/appsettings.Development.json`:

```json
{
  "TenSecondTom": {
    "MemoryDirectory": "./.memory",
    "LlmProvider": "OpenAI"
  }
}
```

## Configuration Hierarchy

Settings are loaded in this order (later sources override earlier ones):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User Secrets (dotnet user-secrets)
4. **Environment Variables** (from .env or shell)
5. Command-line arguments

## Development Mode

Set `DOTNET_ENVIRONMENT=Development` to enable:

- **Mock Authentication**: Bypasses SSH key requirements
- **Debug Logging**: More verbose logging output
- **Development Warnings**: Visible warnings in CLI

### Example Development Setup

```bash
# Create .env file
cat > .env << EOF
DOTNET_ENVIRONMENT=Development
OPENAI_API_KEY=sk-your-key-here
TenSecondTom__MemoryDirectory=./.memory
EOF

# Run the app (no need to set env vars manually)
dotnet run -- today
```

You'll see:
```
⚠ Development Mode: Authentication bypassed
[WRN] Using MockAuthenticationService - authentication bypassed for development
```

## Security Notes

- **Never commit `.env` files** - Already in `.gitignore`
- **Use `.env.example`** - Template for sharing config structure
- **API Keys**: Store in environment variables, not in code
- **Production**: Use proper secrets management (Azure Key Vault, etc.)

## Troubleshooting

**Q: Changes to .env not taking effect?**
A: Restart the application - `.env` is loaded once at startup.

**Q: Which environment am I in?**
A: Check the log output - it shows which environment is loaded.

**Q: .env not loading?**
A: Ensure the file is in the same directory where you run `dotnet run`.
