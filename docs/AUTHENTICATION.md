# Authentication Setup

This document explains how to configure authentication for Ten Second Tom. The application uses SSH key-based authentication to verify your identity when creating memory entries.

> **Quick Start:** Run `tom setup` and the interactive wizard will automatically detect and configure your SSH keys. This document covers the details and advanced scenarios.

## Overview

Ten Second Tom supports two authentication methods:

1. **SSH Agent** (Recommended) - More secure, keys never touch disk
2. **File-Based Keys** - Traditional SSH key files

The application automatically selects the best available authentication method.

### Commands Requiring Authentication

The following commands create or modify data and require SSH authentication:

- `tom today` - Create daily entry
- `tom thisweek` - Create weekly review
- `tom record` - Record and save audio with transcription
- `tom generate` - Generate content from recordings

**Note:** The `record` command requires authentication because it creates data (recordings and transcriptions) that will be used by other authenticated commands. This ensures consistent identity verification across all data-creating operations.

### Setup Wizard (Recommended)

The easiest way to configure authentication is through the setup wizard:

```bash
# First-time setup (launches automatically)
tom today

# Manual setup
tom setup
```

The wizard will:
- ✅ Automatically detect SSH keys from agents (1Password, Secretive, ssh-agent)
- ✅ Scan your `~/.ssh/` directory for ED25519 keys
- ✅ Present available keys for selection
- ✅ Configure and validate your choice
- ✅ Store configuration securely in User Secrets

**No manual configuration needed!** The wizard handles everything.

---

## Why SSH Key Authentication?

SSH key authentication provides:

- **Strong cryptographic identity** - Each memory entry is signed with your private key
- **Non-repudiation** - Cryptographically prove who created each entry
- **Tamper detection** - Verify memory entries haven't been modified
- **No passwords** - Secure authentication without password management
- **Git integration** - Use the same keys you already use for GitHub, GitLab, etc.

---

## SSH Agent Authentication (Recommended)

SSH agents hold your private keys in memory and perform signing operations without exposing the key material. This is more secure than file-based keys.

### Quick Setup with Wizard

**Recommended:** Use the setup wizard which automatically detects your SSH agent and available keys:

```bash
tom setup
```

The wizard will:
1. Detect if you're using 1Password, Secretive, or system ssh-agent
2. List all available SSH keys from your agent
3. Let you select which key to use
4. Automatically configure everything

**That's it!** Skip to [Testing Your Setup](#step-4-test-your-setup) after running the wizard.

---

### Manual SSH Agent Setup (Advanced)

If you prefer manual configuration or need to troubleshoot, follow these steps:

#### Automatic Agent Detection

**Ten Second Tom automatically detects and connects to popular SSH agents** - no manual configuration required!

**Detection Priority:**
1. **1Password SSH Agent** (macOS, Linux) - Checked first
2. **Secretive SSH Agent** (macOS only) - Hardware key support
3. **System SSH Agent** (ssh-agent, Pageant) - Traditional fallback

**This means:**
- ✅ No need to set `SSH_AUTH_SOCK` manually for 1Password or Secretive
- ✅ Works out of the box once your agent is running
- ✅ Automatic platform-specific socket path detection
- ✅ Seamless switching between different agents

**Advanced Override (optional):**
If you need to force a specific provider:
```bash
# In .env file or as environment variable
TenSecondTom__Auth__SshAgentProvider=Auto        # Default: auto-detect
TenSecondTom__Auth__SshAgentProvider=OnePassword # Force 1Password
TenSecondTom__Auth__SshAgentProvider=Secretive   # Force Secretive
TenSecondTom__Auth__SshAgentProvider=System      # Force system agent
```

### Supported SSH Agents

Ten Second Tom works with any SSH agent that implements the OpenSSH agent protocol:

- **ssh-agent** (built-in on macOS/Linux)
- **1Password SSH Agent** (macOS/Windows/Linux)
- **Secretive** (macOS, hardware key support)
- **Pageant** (Windows)
- **KeeAgent** (Windows, KeePass integration)
- **gpg-agent** (with SSH support enabled)

### Setup Instructions

#### Step 1: Start Your SSH Agent

**macOS/Linux (ssh-agent):**
```bash
# Check if agent is already running
echo $SSH_AUTH_SOCK

# If empty, start the agent
eval $(ssh-agent)

# Optional: Add to your shell profile (~/.zshrc, ~/.bashrc)
if [ -z "$SSH_AUTH_SOCK" ]; then
  eval $(ssh-agent) > /dev/null
fi
```

**macOS (1Password):**
1. Open 1Password → Settings → Developer
2. Enable "Use the SSH agent"
3. Restart your terminal

**macOS (Secretive):**
1. Install Secretive from the Mac App Store
2. Launch Secretive - it starts automatically
3. Agent is available at `~/Library/Containers/com.maxgoedjen.Secretive.SecretAgent/Data/socket.ssh`

**Windows (Pageant):**
1. Install PuTTY (includes Pageant)
2. Run `pageant.exe`
3. Add your key via the system tray icon

#### Step 2: Add Your SSH Key to the Agent

**If you have an existing SSH key:**
```bash
# Add your private key to the agent
ssh-add ~/.ssh/id_ed25519

# Or for RSA keys
ssh-add ~/.ssh/id_rsa

# Verify keys are loaded
ssh-add -l
```

**If you need to generate a new SSH key:**
```bash
# Generate Ed25519 key (recommended - faster, smaller signatures)
ssh-keygen -t ed25519 -C "your.email@example.com"

# Or generate RSA key (more compatible)
ssh-keygen -t rsa -b 4096 -C "your.email@example.com"

# Add to agent
ssh-add ~/.ssh/id_ed25519
```

#### Step 3: Configure Your Public Key

**Option A: Use Setup Wizard (Easiest)**

```bash
tom setup
```

The wizard automatically handles public key configuration. Skip to [Step 4](#step-4-test-your-setup).

**Option B: Manual Configuration**

Ten Second Tom needs your public key to verify signatures. You can provide it in three ways:

**Method 1: .env File (Recommended for Development)**

The recommended approach is to use a `.env` file for local development:

1. Copy `.env.example` to `.env`:
   ```bash
   cp .env.example .env
   ```

2. Edit `.env` and add your public key using one of these methods:

   **Method A: Public key content (copy entire line from .pub file)**
   ```bash
   TenSecondTom__Auth__PublicKey=ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKeyDataHere your.email@example.com
   ```

   **Method B: Path to public key file (supports ~ expansion)**
   ```bash
   TenSecondTom__Auth__PublicKeyPath=~/.ssh/id_ed25519.pub
   ```

3. Get your public key content:
   ```bash
   # Display your public key
   cat ~/.ssh/id_ed25519.pub
   
   # Copy the entire output and paste into .env file
   ```

The `.env` file is automatically loaded and is already in `.gitignore` to protect your keys.

**Option 2: Environment Variables (Alternative)**

For shell-level configuration:
```bash
# Export your public key as an environment variable
export TENSECONDTOM_AUTH_PUBLICKEY="ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKeyDataHere your.email@example.com"

# Or read directly from your .pub file
export TENSECONDTOM_AUTH_PUBLICKEY="$(cat ~/.ssh/id_ed25519.pub)"

# Or use path-based configuration
export TENSECONDTOM_AUTH_PUBLICKEYPATH="~/.ssh/id_ed25519.pub"

# Add to your shell profile for persistence (~/.zshrc, ~/.bashrc)
echo 'export TENSECONDTOM_AUTH_PUBLICKEYPATH="~/.ssh/id_ed25519.pub"' >> ~/.zshrc
```

**Option 3: Configuration File (Not for Secrets)**

You can add non-sensitive authentication settings to `appsettings.json`. **Never store private keys or other secrets in this file.**

```json
{
  "TenSecondTom": {
    "Auth": {
      "PublicKeyPath": "~/.ssh/id_ed25519.pub"
    }
  }
}
```

Or via environment variable:
```bash
export TENSECONDTOM_AUTH_PUBLICKEYPATH="~/.ssh/id_ed25519.pub"
```

#### Step 4: Test Your Setup

```bash
# Try creating a daily entry
tom today

# If successful, you'll see authentication confirmation
# If it fails, check the error message for specific guidance
```

### SSH Agent Authentication Flow

When you run a command:

1. Application checks if `SSH_AUTH_SOCK` environment variable is set
2. Application loads your public key from configuration
3. Application connects to the SSH agent via the socket
4. Agent lists available keys and matches your public key
5. Application generates a challenge (random data + timestamp)
6. Agent signs the challenge with your private key
7. Application verifies the signature using your public key
8. If valid, you're authenticated!

Your private key **never leaves the agent** - only signatures are transmitted.

### Cryptographic Signature Verification

Ten Second Tom uses **NSec.Cryptography** (built on libsodium) to perform cryptographic verification of Ed25519 signatures during SSH agent authentication.

#### Verification Process

When authenticating with an SSH agent:

1. **Challenge Generation**: Application creates a cryptographically random 32-byte challenge
2. **Agent Signing**: SSH agent signs the challenge with your private key
3. **Signature Return**: Agent returns a 64-byte Ed25519 signature
4. **Cryptographic Verification**: Application verifies the signature using NSec.Cryptography
   - Extracts your 32-byte Ed25519 public key from SSH format
   - Verifies signature matches challenge and public key
   - Only cryptographically valid signatures grant authentication

#### Security Properties

The Ed25519 signature verification provides:

- **RFC 8032 Compliance**: Implements the official Ed25519 standard
- **Tamper Detection**: Modified signatures are immediately rejected
- **Constant-Time Operations**: Prevents timing attacks
- **128-bit Security Level**: Industry-standard cryptographic strength
- **No Signature Malleability**: Ed25519 signatures cannot be forged
- **Audited Implementation**: Built on libsodium, widely audited and trusted

#### Library Choice

**NSec.Cryptography** was selected for:

- RFC 8032 compliant Ed25519 implementation
- Built on libsodium (audited C library with 10+ years of security history)
- Modern .NET API design with strong typing
- Lightweight dependency with no platform-specific requirements
- Embedded libsodium (no system library dependencies)
- Active maintenance and security updates

**Performance**: Signature verification completes in < 1ms on modern hardware.

#### Signature Validation

All signatures must meet these requirements:

- **Length**: Exactly 64 bytes (Ed25519 signature size)
- **Public Key**: Exactly 32 bytes (Ed25519 key size)
- **Cryptographic Validity**: Must pass NSec.Cryptography verification
- **No Bypasses**: Simplified validation is never used (security-first design)

Failed verifications are logged as security events for audit purposes.

## File-Based Key Authentication

If SSH agent is not available, Ten Second Tom automatically falls back to file-based authentication.

### Quick Setup with Wizard

**Recommended:** The setup wizard handles file-based keys too:

```bash
tom setup
```

The wizard will:
- Scan `~/.ssh/` for ED25519 keys
- List available keys for selection
- Automatically configure file paths

**That's it!** Skip to [Step 3](#step-3-test-your-setup) after running the wizard.

---

### Manual File-Based Setup (Advanced)

If you prefer manual configuration:

#### Step 1: Generate SSH Key Pair (if needed)

```bash
# Generate Ed25519 key (recommended)
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519

# Or generate RSA key
ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa
```

#### Step 2: Configure Key Paths

Ten Second Tom looks for keys in standard locations by default:

**Default Key Locations:**
- `~/.ssh/id_ed25519` (Ed25519 private key)
- `~/.ssh/id_ed25519.pub` (Ed25519 public key)
- `~/.ssh/id_rsa` (RSA private key)
- `~/.ssh/id_rsa.pub` (RSA public key)

**Custom Key Paths:**

Via `.env` file (recommended for development):
```bash
TenSecondTom__Auth__PrivateKeyPath=~/custom/path/my_key
TenSecondTom__Auth__PublicKeyPath=~/custom/path/my_key.pub
```

Via environment variables:
```bash
export TENSECONDTOM_AUTH_PRIVATEKEYPATH="~/custom/path/my_key"
export TENSECONDTOM_AUTH_PUBLICKEYPATH="~/custom/path/my_key.pub"
```

Via configuration file (for production):
```json
{
  "TenSecondTom": {
    "Auth": {
      "PrivateKeyPath": "~/custom/path/my_key",
      "PublicKeyPath": "~/custom/path/my_key.pub"
    }
  }
}
```

#### Step 3: Test Your Setup

```bash
# Try creating a daily entry
tom today

# If successful, file-based authentication is working
```

## Configuration Reference

### Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `SSH_AUTH_SOCK` | SSH agent socket path (set automatically) | `/tmp/ssh-agent.sock` |
| `TENSECONDTOM_AUTH_PUBLICKEY` | Your public key (full content) | `ssh-ed25519 AAAAC3...` |
| `TENSECONDTOM_AUTH_PUBLICKEYPATH` | Path to public key file | `~/.ssh/id_ed25519.pub` |
| `TENSECONDTOM_AUTH_PRIVATEKEYPATH` | Path to private key file | `~/.ssh/id_ed25519` |

### Configuration File Settings

```json
{
  "TenSecondTom": {
    "Auth": {
      "PublicKey": "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKey...",
      "PublicKeyPath": "~/.ssh/id_ed25519.pub",
      "PrivateKeyPath": "~/.ssh/id_ed25519"
    }
  }
}
```

### Configuration Priority

The authentication method is selected automatically:

1. **SSH Agent** (if `SSH_AUTH_SOCK` is set AND public key is configured)
2. **File-Based Keys** (fallback if agent not available or not configured)

Public key sources (highest priority first):

1. `TENSECONDTOM_AUTH_PUBLICKEY` environment variable
2. `TenSecondTom:Auth:PublicKey` configuration setting
3. `TENSECONDTOM_AUTH_PUBLICKEYPATH` environment variable
4. `TenSecondTom:Auth:PublicKeyPath` configuration setting
5. Default locations (`~/.ssh/id_ed25519.pub`, `~/.ssh/id_rsa.pub`)

## Troubleshooting

### Error: "SSH agent not available"

**Symptoms:**
```
✗ Authentication failed: SSH agent not available: SSH_AUTH_SOCK environment variable is not set
```

**Solutions:**

1. **Check if agent is running:**
   ```bash
   echo $SSH_AUTH_SOCK
   # Should print a socket path like /tmp/ssh-agent.sock
   ```

2. **Start the agent:**
   ```bash
   # macOS/Linux
   eval $(ssh-agent)
   
   # Or for persistent setup, add to ~/.zshrc or ~/.bashrc:
   if [ -z "$SSH_AUTH_SOCK" ]; then
     eval $(ssh-agent) > /dev/null
   fi
   ```

3. **For 1Password:** Ensure SSH agent is enabled in Settings → Developer

4. **For Secretive:** Launch the Secretive app

### Error: "No SSH keys found in agent"

**Symptoms:**
```
✗ Authentication failed: No SSH keys found in agent matching configured public key
```

**Solutions:**

1. **Check which keys are loaded:**
   ```bash
   ssh-add -l
   ```

2. **Add your key to the agent:**
   ```bash
   ssh-add ~/.ssh/id_ed25519
   ```

3. **Verify public key configuration matches:**
   ```bash
   # Compare your configured public key with actual key
   echo $TENSECONDTOM_AUTH_PUBLICKEY
   cat ~/.ssh/id_ed25519.pub
   # These should match!
   ```

4. **For 1Password:** Import your SSH key into 1Password

### Error: "Public key not configured"

**Symptoms:**
```
✗ Authentication failed: SSH agent authentication requires a configured public key
```

**Solutions:**

1. **Set via environment variable:**
   ```bash
   export TENSECONDTOM_AUTH_PUBLICKEY="$(cat ~/.ssh/id_ed25519.pub)"
   ```

2. **Or configure public key path:**
   ```bash
   export TENSECONDTOM_AUTH_PUBLICKEYPATH="~/.ssh/id_ed25519.pub"
   ```

3. **Or add to appsettings.json:**
   ```json
   {
     "TenSecondTom": {
       "Auth": {
         "PublicKeyPath": "~/.ssh/id_ed25519.pub"
       }
     }
   }
   ```

### Error: "Invalid public key format"

**Symptoms:**
```
✗ Authentication failed: Invalid public key format in configuration
```

**Solutions:**

1. **Ensure public key includes all parts:**
   ```bash
   # Correct format: algorithm base64data comment
   ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKey... your.email@example.com
   ```

2. **Copy entire line from .pub file:**
   ```bash
   cat ~/.ssh/id_ed25519.pub
   # Copy the entire output
   ```

3. **Avoid line breaks in configuration:**
   ```json
   // WRONG - line break in middle of key
   {
     "PublicKey": "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAA
                   IMockPublicKey..."
   }
   
   // CORRECT - single line
   {
     "PublicKey": "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIMockPublicKey..."
   }
   ```

### Error: "Permission denied" (File-Based Keys)

**Symptoms:**
```
✗ Authentication failed: Could not read private key file
```

**Solutions:**

1. **Check file permissions:**
   ```bash
   ls -la ~/.ssh/id_ed25519
   # Should be: -rw------- (600)
   ```

2. **Fix permissions:**
   ```bash
   chmod 600 ~/.ssh/id_ed25519
   chmod 644 ~/.ssh/id_ed25519.pub
   ```

3. **Check file ownership:**
   ```bash
   # Files should be owned by you
   ls -la ~/.ssh/
   
   # Fix if needed
   chown $USER:$USER ~/.ssh/id_ed25519*
   ```

### Debugging Tips

1. **Enable verbose logging:**
   ```bash
   export ASPNETCORE_ENVIRONMENT=Development
   tom today
   ```

2. **Check agent communication:**
   ```bash
   # Verify agent is responding
   ssh-add -l
   
   # If this fails, the agent itself has issues
   ```

3. **Test SSH key directly:**
   ```bash
   # Test SSH connection (if using GitHub key)
   ssh -T git@github.com
   
   # Should succeed if key is valid and loaded
   ```

4. **Check configuration loading:**
   ```bash
   # Print all configuration (for debugging)
   dotnet run --project src/TenSecondTom.csproj -- config
   ```

## Security Considerations

### SSH Agent Security

✅ **Advantages:**
- Private keys never touch disk (except initial storage)
- Keys are encrypted in memory
- Agent can require confirmation for each signature (with some agents)
- Agent auto-locks after timeout (configurable)
- Hardware key support (YubiKey, etc.) via some agents

⚠️ **Recommendations:**
- Use hardware keys (YubiKey) for maximum security (via Secretive or other agents)
- Enable touch-to-sign if your agent supports it
- Set agent timeouts to auto-remove keys after inactivity
- Don't use agent forwarding (`ssh -A`) unnecessarily
- Regularly audit loaded keys with `ssh-add -l`

### File-Based Key Security

✅ **Best Practices:**
- Always use passphrase-protected keys
- Use strong key types (Ed25519, RSA 4096)
- Keep private keys in `~/.ssh/` with 600 permissions
- Never commit private keys to version control
- Rotate keys periodically (annually recommended)
- Use different keys for different purposes

⚠️ **Warnings:**
- Private keys on disk can be stolen if system is compromised
- Unencrypted keys (no passphrase) are especially vulnerable
- Backup keys securely (encrypted backup media)

### Key Management

**Key Rotation:**
```bash
# Generate new key
ssh-keygen -t ed25519 -f ~/.ssh/id_ed25519_new

# Test with new key
export TENSECONDTOM_AUTH_PRIVATEKEYPATH="~/.ssh/id_ed25519_new"
tom today

# Once verified, replace old key
mv ~/.ssh/id_ed25519 ~/.ssh/id_ed25519.old
mv ~/.ssh/id_ed25519_new ~/.ssh/id_ed25519
```

**Key Revocation:**

If your key is compromised:
1. Generate a new key immediately
2. Update Ten Second Tom configuration
3. Archive old memory entries (they were signed with compromised key)
4. Remove compromised key from all systems

**Hardware Key Support:**

For maximum security, use a hardware key (YubiKey):
1. Install Secretive (macOS) or use gpg-agent with SSH support
2. Generate key on hardware device (cannot be extracted)
3. Configure Ten Second Tom to use agent authentication
4. Every signature requires physical touch of device

## Platform-Specific Notes

### macOS

- **Built-in ssh-agent:** macOS includes ssh-agent, but it's not started by default
- **Keychain integration:** Use `ssh-add --apple-use-keychain` to store passphrases
- **1Password:** Native integration available, very smooth experience
- **Secretive:** Excellent open-source agent with hardware key support

**Persistent Agent Setup (macOS):**

Add to `~/.zshrc`:
```bash
# Start SSH agent if not running
if [ -z "$SSH_AUTH_SOCK" ]; then
  eval $(ssh-agent) > /dev/null
fi

# Load key from macOS Keychain (requires prior ssh-add --apple-use-keychain)
ssh-add --apple-load-keychain 2>/dev/null
```

### Linux

- **Built-in ssh-agent:** Available on all distributions
- **systemd integration:** Many distros start ssh-agent via systemd user session
- **gpg-agent:** Can act as SSH agent with `enable-ssh-support` in `gpg-agent.conf`

**Persistent Agent Setup (systemd):**

Create `~/.config/systemd/user/ssh-agent.service`:
```ini
[Unit]
Description=SSH key agent

[Service]
Type=simple
Environment=SSH_AUTH_SOCK=%t/ssh-agent.socket
ExecStart=/usr/bin/ssh-agent -D -a $SSH_AUTH_SOCK

[Install]
WantedBy=default.target
```

Enable:
```bash
systemctl --user enable ssh-agent
systemctl --user start ssh-agent
export SSH_AUTH_SOCK="${XDG_RUNTIME_DIR}/ssh-agent.socket"
```

### Windows

- **Pageant (PuTTY):** Most common SSH agent for Windows
- **1Password:** Cross-platform support includes Windows
- **OpenSSH (Windows 10+):** Built-in ssh-agent service available

**Windows OpenSSH Agent:**

```powershell
# Start SSH Agent service
Start-Service ssh-agent

# Set to start automatically
Set-Service ssh-agent -StartupType Automatic

# Add key
ssh-add $env:USERPROFILE\.ssh\id_ed25519
```

## Advanced Configuration

### Multiple Key Support

If you have multiple SSH keys for different purposes:

```bash
# List all keys in agent
ssh-add -l

# Configure Ten Second Tom to use specific key
export TENSECONDTOM_AUTH_PUBLICKEY="$(cat ~/.ssh/id_ed25519_tom.pub)"
```

### CI/CD Integration

For automated environments without SSH agents:

```yaml
# GitHub Actions example
env:
  TENSECONDTOM_AUTH_PRIVATEKEYPATH: ${{ secrets.SSH_PRIVATE_KEY }}
  TENSECONDTOM_AUTH_PUBLICKEYPATH: ${{ secrets.SSH_PUBLIC_KEY }}
```

Store keys in CI/CD secrets, inject as environment variables.

### Docker/Container Environments

**SSH Agent Forwarding:**
```bash
# Forward host agent to container
docker run -v $SSH_AUTH_SOCK:/ssh-agent \
           -e SSH_AUTH_SOCK=/ssh-agent \
           -e TENSECONDTOM_AUTH_PUBLICKEY="$(cat ~/.ssh/id_ed25519.pub)" \
           tensecondtom
```

**File-Based Keys in Container:**
```bash
# Mount keys as read-only volume
docker run -v ~/.ssh:/root/.ssh:ro \
           -e TENSECONDTOM_AUTH_PRIVATEKEYPATH=/root/.ssh/id_ed25519 \
           tensecondtom
```

## Related Documentation

- [Configuration Setup](CONFIGURATION.md) - General configuration and API keys
- [Environment Variables](ENVIRONMENT.md) - Complete environment variable reference

## Getting Help

If you encounter authentication issues:

1. Check the error message - it includes specific setup guidance
2. Review this troubleshooting section
3. Enable verbose logging (Development environment)
4. Verify your SSH setup works with other tools (`ssh -T git@github.com`)
5. Open an issue on GitHub with error messages and environment details

## Summary

**Recommended Setup:**

```bash
# Easiest: Use the setup wizard (handles everything automatically)
tom setup
```

The wizard automatically:
- ✅ Detects SSH agents (1Password, Secretive, ssh-agent)
- ✅ Scans for SSH keys in `~/.ssh/`
- ✅ Presents available keys for selection
- ✅ Validates and configures your choice
- ✅ Stores configuration securely

**Alternative: Manual Setup (Advanced Users)**

If you prefer manual configuration or need specific customization:

1. Start your SSH agent (or use file-based keys)
2. Add your SSH key to the agent with `ssh-add`
3. Configure via environment variables or config files
4. Test with `tom today`

**Quick Start Example:**

```bash
# Option 1: Interactive wizard (recommended)
tom setup

# Option 2: Manual with agent
eval $(ssh-agent)
ssh-add ~/.ssh/id_ed25519
export TENSECONDTOM_AUTH_PUBLICKEYPATH=~/.ssh/id_ed25519.pub
tom today
```

---

**For Production:**
Use `appsettings.json` or environment variables instead of `.env` file.

That's it! You're ready to use Ten Second Tom with secure SSH authentication.
