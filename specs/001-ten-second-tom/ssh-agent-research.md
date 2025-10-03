# SSH Agent Integration Research

**Date**: October 3, 2025  
**Task**: T061a - Research SSH Agent Integration for .NET  
**Purpose**: Investigate SSH agent communication for secure authentication without direct key file access

---

## Executive Summary

SSH agent integration provides enhanced security over file-based authentication by:
- Eliminating need to read private keys from disk
- Supporting hardware security keys (YubiKey, Touch ID)
- Enabling modern authentication flows (1Password, Secretive)
- Reducing risk of key theft or exposure

**Recommendation**: Implement SSH agent support as primary authentication method with file-based fallback for v1.1.

---

## SSH Agent Protocol Overview

### Unix/Linux/macOS: SSH_AUTH_SOCK

SSH agents on Unix-like systems communicate via a Unix domain socket:

```bash
$ echo $SSH_AUTH_SOCK
/tmp/ssh-XXXXX/agent.XXXXX
```

**Protocol**: OpenSSH agent protocol (RFC 4251 / draft-ietf-secsh-agent)

**Key Operations**:
1. **SSH_AGENTC_REQUEST_IDENTITIES** (11): List public keys available in agent
2. **SSH_AGENTC_SIGN_REQUEST** (13): Request signature for challenge data
3. **SSH_AGENT_IDENTITIES_ANSWER** (12): Response with key list
4. **SSH_AGENT_SIGN_RESPONSE** (14): Response with signature

### Windows: Pageant / OpenSSH for Windows

**Pageant** (PuTTY agent):
- Uses Windows named pipes or shared memory
- Different protocol from OpenSSH agent

**OpenSSH for Windows** (Windows 10 1809+):
- Uses named pipe `\\.\pipe\openssh-ssh-agent`
- Compatible with OpenSSH agent protocol

---

## .NET Library Options

### Option 1: Direct Socket Communication (Recommended)

**Approach**: Implement OpenSSH agent protocol directly using .NET sockets

**Pros**:
- Full control over authentication flow
- No external dependencies beyond standard .NET libraries
- Cross-platform support (Unix domain sockets in .NET 6+)
- Small, focused implementation

**Cons**:
- Need to implement binary protocol (SSH wire format)
- Requires careful testing of edge cases
- Must handle protocol versioning

**Implementation Complexity**: Medium

**Example Code**:
```csharp
using System.Net.Sockets;
using System.IO;

public class SshAgentClient : IDisposable
{
    private Socket? _socket;
    
    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var socketPath = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        if (string.IsNullOrEmpty(socketPath))
            return false;
            
        try
        {
            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(socketPath);
            await _socket.ConnectAsync(endpoint, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    public async Task<byte[]?> SignDataAsync(byte[] publicKey, byte[] data, CancellationToken cancellationToken = default)
    {
        if (_socket == null || !_socket.Connected)
            return null;
            
        // Build SSH_AGENTC_SIGN_REQUEST message
        var request = BuildSignRequest(publicKey, data);
        
        // Send request
        await _socket.SendAsync(request, SocketFlags.None, cancellationToken);
        
        // Read response
        var response = new byte[4096];
        var bytesRead = await _socket.ReceiveAsync(response, SocketFlags.None, cancellationToken);
        
        // Parse SSH_AGENT_SIGN_RESPONSE
        return ParseSignResponse(response, bytesRead);
    }
    
    private byte[] BuildSignRequest(byte[] publicKey, byte[] data)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        // Message type
        writer.Write((byte)13); // SSH_AGENTC_SIGN_REQUEST
        
        // Public key blob
        WriteSshString(writer, publicKey);
        
        // Data to sign
        WriteSshString(writer, data);
        
        // Flags (0 for default)
        writer.Write((uint)0);
        
        // Prepend length
        var body = ms.ToArray();
        var message = new byte[body.Length + 4];
        BitConverter.GetBytes((uint)body.Length).CopyTo(message, 0);
        Array.Reverse(message, 0, 4); // Network byte order
        body.CopyTo(message, 4);
        
        return message;
    }
    
    private void WriteSshString(BinaryWriter writer, byte[] data)
    {
        var length = BitConverter.GetBytes((uint)data.Length);
        Array.Reverse(length); // Network byte order
        writer.Write(length);
        writer.Write(data);
    }
    
    private byte[]? ParseSignResponse(byte[] response, int length)
    {
        if (length < 5) return null;
        
        var messageType = response[4];
        if (messageType != 14) // SSH_AGENT_SIGN_RESPONSE
            return null;
            
        // Parse signature blob
        using var ms = new MemoryStream(response, 5, length - 5);
        using var reader = new BinaryReader(ms);
        
        return ReadSshString(reader);
    }
    
    private byte[] ReadSshString(BinaryReader reader)
    {
        var lengthBytes = reader.ReadBytes(4);
        Array.Reverse(lengthBytes);
        var length = BitConverter.ToUInt32(lengthBytes, 0);
        
        return reader.ReadBytes((int)length);
    }
    
    public void Dispose()
    {
        _socket?.Dispose();
    }
}
```

### Option 2: SSH.NET Extensions

**Package**: `Renci.SshNet` (existing dependency)

**Status**: SSH.NET does NOT natively support SSH agent communication as of version 2023.0.0+

**Approach**: Would require custom extension or fork

**Pros**:
- Familiar library already in use
- Could reuse SSH key parsing utilities

**Cons**:
- No built-in agent support
- Would still need to implement protocol
- Adds complexity to existing library usage

**Recommendation**: NOT RECOMMENDED - better to keep agent code separate

### Option 3: Third-Party Libraries

**Available Options**:
1. **SshNet.Security.Cryptography** (NuGet)
   - Status: Last updated 2017, dormant project
   - Not recommended due to age

2. **OpenSSH.Agent** (hypothetical)
   - No mature .NET library found for OpenSSH agent protocol

**Conclusion**: No suitable third-party library exists for .NET SSH agent communication

---

## Authentication Flow with SSH Agent

### Challenge-Response Pattern

```
┌──────────┐                  ┌──────────┐                ┌────────────┐
│   App    │                  │ SSH Agent│                │ User (HW)  │
└────┬─────┘                  └────┬─────┘                └─────┬──────┘
     │                             │                            │
     │ 1. Check SSH_AUTH_SOCK      │                            │
     ├────────────────────────────>│                            │
     │                             │                            │
     │ 2. Request key list         │                            │
     ├────────────────────────────>│                            │
     │                             │                            │
     │ 3. Return public keys       │                            │
     │<────────────────────────────┤                            │
     │                             │                            │
     │ 4. Generate challenge (32B) │                            │
     │                             │                            │
     │ 5. Request signature        │                            │
     ├────────────────────────────>│                            │
     │                             │ 6. Prompt for approval     │
     │                             ├───────────────────────────>│
     │                             │    (Touch ID, PIN, etc.)   │
     │                             │                            │
     │                             │ 7. Approve                 │
     │                             │<───────────────────────────┤
     │                             │                            │
     │ 8. Return signature         │                            │
     │<────────────────────────────┤                            │
     │                             │                            │
     │ 9. Verify signature         │                            │
     │    with public key          │                            │
     │                             │                            │
     │ 10. Create session          │                            │
     │                             │                            │
```

### Implementation Steps

**Step 1: Check Agent Availability**
```csharp
public bool IsAgentAvailable()
{
    var socketPath = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
    return !string.IsNullOrEmpty(socketPath) && File.Exists(socketPath);
}
```

**Step 2: Connect to Agent**
```csharp
var client = new SshAgentClient();
if (!await client.ConnectAsync())
{
    return Result.Failure("SSH agent not available");
}
```

**Step 3: Load User's Public Key**
```csharp
// Option 1: From configuration
var publicKeyBase64 = _config["TenSecondTom:Auth:PublicKey"];
var publicKey = Convert.FromBase64String(publicKeyBase64);

// Option 2: From file
var publicKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".ssh", "id_ed25519.pub");
var publicKey = ParsePublicKey(await File.ReadAllTextAsync(publicKeyPath));
```

**Step 4: Generate Challenge**
```csharp
var challenge = new byte[32];
using (var rng = RandomNumberGenerator.Create())
{
    rng.GetBytes(challenge);
}
```

**Step 5: Request Signature from Agent**
```csharp
var signature = await client.SignDataAsync(publicKey, challenge);
if (signature == null)
{
    return Result.Failure("SSH agent denied signature request");
}
```

**Step 6: Verify Signature**
```csharp
bool isValid = VerifySignature(publicKey, challenge, signature);
if (!isValid)
{
    return Result.Failure("Signature verification failed");
}
```

**Step 7: Create Session**
```csharp
var session = new UserSession
{
    SessionId = Guid.NewGuid(),
    UserId = GetPublicKeyFingerprint(publicKey),
    SshKeyHash = ComputeSHA256(publicKey),
    CreatedAt = DateTimeOffset.UtcNow,
    IsActive = true
};

await _storageProvider.SaveSessionAsync(session);
return Result.Success(session);
```

---

## Public Key Handling

### Public Key Storage Options

**Option 1: Configuration File**
```json
{
  "TenSecondTom": {
    "Auth": {
      "PublicKey": "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGz8... user@host"
    }
  }
}
```

**Option 2: Environment Variable**
```bash
export TENSECONDTOM_AUTH_PUBLICKEY="ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIGz8..."
```

**Option 3: File Path**
```json
{
  "TenSecondTom": {
    "Auth": {
      "PublicKeyPath": "~/.ssh/id_ed25519.pub"
    }
  }
}
```

**Option 4: Auto-Discovery** (Recommended for UX)
```csharp
private async Task<byte[]?> DiscoverPublicKeyAsync()
{
    var sshDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".ssh");
    
    // Try common key files in order of preference
    var keyFiles = new[]
    {
        Path.Combine(sshDir, "id_ed25519.pub"),
        Path.Combine(sshDir, "id_rsa.pub"),
        Path.Combine(sshDir, "id_ecdsa.pub")
    };
    
    foreach (var keyFile in keyFiles)
    {
        if (File.Exists(keyFile))
        {
            try
            {
                var content = await File.ReadAllTextAsync(keyFile);
                return ParsePublicKey(content);
            }
            catch
            {
                // Try next key file
            }
        }
    }
    
    return null;
}
```

### Public Key Parsing

```csharp
private byte[] ParsePublicKey(string publicKeyString)
{
    // Format: "ssh-ed25519 AAAAC3Nza... user@host"
    var parts = publicKeyString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    
    if (parts.Length < 2)
        throw new FormatException("Invalid public key format");
    
    // Second part is base64-encoded key blob
    return Convert.FromBase64String(parts[1]);
}
```

### Signature Verification

```csharp
using System.Security.Cryptography;

private bool VerifySignature(byte[] publicKey, byte[] data, byte[] signature)
{
    // Parse key type from public key blob
    var keyType = GetKeyType(publicKey);
    
    switch (keyType)
    {
        case "ssh-ed25519":
            return VerifyEd25519Signature(publicKey, data, signature);
            
        case "ssh-rsa":
            return VerifyRsaSignature(publicKey, data, signature);
            
        default:
            throw new NotSupportedException($"Key type {keyType} not supported");
    }
}

private bool VerifyEd25519Signature(byte[] publicKeyBlob, byte[] data, byte[] signature)
{
    // Extract Ed25519 public key (32 bytes) from SSH key blob
    var publicKey = ExtractEd25519PublicKey(publicKeyBlob);
    
    // Use BouncyCastle or libsodium for Ed25519 verification
    // .NET does not have built-in Ed25519 until .NET 9+
    
    // For .NET 9+:
    // var key = PublicKey.CreateFromSubjectPublicKeyInfo(publicKey, out _);
    // return key.VerifyData(data, signature, HashAlgorithmName.SHA512);
    
    // For earlier versions, use BouncyCastle:
    // var verifier = new Ed25519Signer();
    // verifier.Init(false, new Ed25519PublicKeyParameters(publicKey, 0));
    // verifier.BlockUpdate(data, 0, data.Length);
    // return verifier.VerifySignature(signature);
    
    // Placeholder for documentation
    return false;
}

private bool VerifyRsaSignature(byte[] publicKeyBlob, byte[] data, byte[] signature)
{
    // Extract RSA parameters from SSH key blob
    var rsaParams = ExtractRsaParameters(publicKeyBlob);
    
    using var rsa = RSA.Create();
    rsa.ImportParameters(rsaParams);
    
    return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
}
```

---

## Cross-Platform Considerations

### macOS

**SSH Agent**: Built-in `ssh-agent` or third-party (1Password, Secretive)

**SSH_AUTH_SOCK**: Automatically set by macOS for default agent

**Touch ID Integration**: Supported via third-party agents (Secretive)

**Compatibility**: Full support via Unix domain sockets

### Linux

**SSH Agent**: `ssh-agent`, `gnome-keyring-daemon`, `gpg-agent`

**SSH_AUTH_SOCK**: Set by session manager or manually

**Hardware Key Support**: YubiKey, Nitrokey via `gpg-agent`

**Compatibility**: Full support via Unix domain sockets

### Windows

**OpenSSH for Windows** (Windows 10 1809+):
- Service: `ssh-agent` (disabled by default)
- Socket: Named pipe `\\.\pipe\openssh-ssh-agent`
- Enable: `Set-Service ssh-agent -StartupType Automatic; Start-Service ssh-agent`

**Pageant**:
- PuTTY's SSH agent
- Different protocol (not OpenSSH-compatible)
- Requires separate implementation or conversion tool

**Recommendation**: Support OpenSSH for Windows, document Pageant limitations

**Implementation Note**:
```csharp
public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        // Try OpenSSH for Windows named pipe
        var pipePath = @"\\.\pipe\openssh-ssh-agent";
        _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        // ... connect to named pipe
    }
    else
    {
        // Unix domain socket
        var socketPath = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        // ... connect to socket
    }
}
```

---

## Configuration Design

### Configuration Schema

```json
{
  "TenSecondTom": {
    "Auth": {
      "PreferredMethod": "SshAgent",  // "SshAgent" | "KeyFile" | "Auto"
      "SshAgent": {
        "Enabled": true,
        "SocketPath": null,  // Auto-detect from SSH_AUTH_SOCK if null
        "FallbackToKeyFile": true
      },
      "KeyFile": {
        "Enabled": true,
        "KeyPath": "~/.ssh/id_ed25519"
      },
      "PublicKey": null,  // Optional: specify exact public key for agent auth
      "PublicKeyPath": null  // Optional: path to .pub file
    }
  }
}
```

### Authentication Method Selection

```csharp
public class AuthenticationServiceFactory
{
    public static IAuthenticationService Create(IConfiguration config, ILogger logger)
    {
        var preferredMethod = config["TenSecondTom:Auth:PreferredMethod"] ?? "Auto";
        
        switch (preferredMethod)
        {
            case "SshAgent":
                return CreateSshAgentService(config, logger);
                
            case "KeyFile":
                return new SshKeyAuthenticationService(logger);
                
            case "Auto":
            default:
                // Try SSH agent first
                if (IsSshAgentAvailable())
                {
                    try
                    {
                        return CreateSshAgentService(config, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to initialize SSH agent, falling back to key file");
                    }
                }
                
                // Fall back to key file
                return new SshKeyAuthenticationService(logger);
        }
    }
    
    private static bool IsSshAgentAvailable()
    {
        var socketPath = Environment.GetEnvironmentVariable("SSH_AUTH_SOCK");
        return !string.IsNullOrEmpty(socketPath) && File.Exists(socketPath);
    }
    
    private static IAuthenticationService CreateSshAgentService(IConfiguration config, ILogger logger)
    {
        var publicKey = LoadPublicKey(config);
        
        if (publicKey == null)
        {
            throw new InvalidOperationException(
                "SSH agent authentication requires public key configuration. " +
                "Set TenSecondTom:Auth:PublicKey or TenSecondTom:Auth:PublicKeyPath");
        }
        
        return new SshAgentAuthenticationService(publicKey, logger);
    }
    
    private static byte[]? LoadPublicKey(IConfiguration config)
    {
        // Try direct public key value
        var publicKeyString = config["TenSecondTom:Auth:PublicKey"];
        if (!string.IsNullOrEmpty(publicKeyString))
        {
            return ParsePublicKey(publicKeyString);
        }
        
        // Try public key file path
        var publicKeyPath = config["TenSecondTom:Auth:PublicKeyPath"];
        if (!string.IsNullOrEmpty(publicKeyPath))
        {
            publicKeyPath = Environment.ExpandEnvironmentVariables(publicKeyPath);
            if (File.Exists(publicKeyPath))
            {
                var content = File.ReadAllText(publicKeyPath);
                return ParsePublicKey(content);
            }
        }
        
        // Try auto-discovery
        return DiscoverPublicKey();
    }
}
```

---

## Security Considerations

### Advantages over File-Based Auth

1. **Private Key Never Read**: Agent holds key, app only sees signatures
2. **Hardware Key Support**: YubiKey, TPM, Touch ID via agent
3. **User Approval**: Agent can require interactive approval per signature
4. **Audit Trail**: Agent can log signature requests
5. **Key Isolation**: Keys protected by OS/hardware security

### Potential Risks

1. **Agent Hijacking**: Malicious process could access SSH_AUTH_SOCK
   - Mitigation: OS-level socket permissions, audit agent access
   
2. **Challenge Replay**: Attacker could replay signed challenges
   - Mitigation: Use timestamp + random nonce, short challenge validity
   
3. **Public Key Confusion**: User specifies wrong public key
   - Mitigation: Display key fingerprint, require confirmation
   
4. **Agent Denial**: Agent refuses to sign (user cancels)
   - Mitigation: Clear error messages, fall back to key file

### Best Practices

1. **Challenge Format**:
   ```
   challenge = timestamp (8 bytes) || nonce (24 bytes)
   ```
   
2. **Challenge Validation**:
   ```csharp
   var timestamp = BitConverter.ToInt64(challenge, 0);
   var challengeAge = DateTime.UtcNow - DateTime.FromBinary(timestamp);
   
   if (challengeAge > TimeSpan.FromMinutes(5))
   {
       return Result.Failure("Challenge expired");
   }
   ```
   
3. **Session Binding**: Bind session to public key fingerprint
   
4. **Audit Logging**:
   ```csharp
   _logger.LogInformation(
       "SSH agent authentication attempt: Key={KeyFingerprint}, Result={Result}",
       fingerprint, success);
   ```

---

## Error Handling

### Common Errors and Messages

| Error Condition | User Message | Technical Action |
|----------------|--------------|------------------|
| SSH_AUTH_SOCK not set | "SSH agent not available. Please start your SSH agent or use key file authentication." | Fall back to key file auth |
| Agent socket not found | "SSH agent socket not found at {path}. Is your SSH agent running?" | Display agent setup instructions |
| No keys in agent | "No SSH keys found in agent. Please add your key with 'ssh-add'." | Show ssh-add instructions |
| Public key not configured | "Public key not configured. Set TENSECONDTOM_AUTH_PUBLICKEY or add to config." | Show configuration help |
| Agent denied signature | "SSH agent denied signature request. Did you cancel the approval prompt?" | Retry prompt or fall back |
| Signature verification failed | "Authentication failed: signature verification failed. Is this the correct public key?" | Check public key configuration |
| Network/protocol error | "Failed to communicate with SSH agent: {error}" | Log technical details, retry |

### Error Handling Code

```csharp
public async Task<Result<UserSession>> AuthenticateAsync(CancellationToken cancellationToken)
{
    try
    {
        if (!IsAgentAvailable())
        {
            _logger.LogWarning("SSH agent not available, falling back to key file authentication");
            return await _keyFileAuthService.AuthenticateAsync(cancellationToken);
        }
        
        var client = new SshAgentClient();
        if (!await client.ConnectAsync(cancellationToken))
        {
            return Result.Failure<UserSession>(
                "SSH agent not available. Please start your SSH agent or use key file authentication.");
        }
        
        var publicKey = await LoadPublicKeyAsync();
        if (publicKey == null)
        {
            return Result.Failure<UserSession>(
                "Public key not configured. Set TENSECONDTOM_AUTH_PUBLICKEY environment variable " +
                "or configure TenSecondTom:Auth:PublicKey in appsettings.json");
        }
        
        var challenge = GenerateChallenge();
        var signature = await client.SignDataAsync(publicKey, challenge, cancellationToken);
        
        if (signature == null)
        {
            return Result.Failure<UserSession>(
                "SSH agent denied signature request. Did you cancel the approval prompt?");
        }
        
        if (!VerifySignature(publicKey, challenge, signature))
        {
            return Result.Failure<UserSession>(
                "Authentication failed: signature verification failed. " +
                "Please verify your public key configuration.");
        }
        
        var session = CreateSession(publicKey);
        await SaveSessionAsync(session, cancellationToken);
        
        _logger.LogInformation(
            "SSH agent authentication successful: Key={KeyFingerprint}",
            session.UserId);
        
        return Result.Success(session);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "SSH agent authentication failed unexpectedly");
        return Result.Failure<UserSession>(
            $"Authentication error: {ex.Message}");
    }
}
```

---

## Testing Strategy

### Unit Tests

```csharp
public class SshAgentAuthenticationServiceTests
{
    [Fact]
    public async Task Authenticate_WithValidAgent_ReturnsSession()
    {
        // Arrange
        var mockAgentClient = new Mock<ISshAgentClient>();
        mockAgentClient
            .Setup(c => c.ConnectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        mockAgentClient
            .Setup(c => c.SignDataAsync(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[64]); // Mock signature
        
        var service = new SshAgentAuthenticationService(mockAgentClient.Object, publicKey, logger);
        
        // Act
        var result = await service.AuthenticateAsync();
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
    
    [Fact]
    public async Task Authenticate_WhenAgentUnavailable_FallsBackToKeyFile()
    {
        // Test fallback behavior
    }
    
    [Fact]
    public async Task Authenticate_WithInvalidSignature_ReturnsError()
    {
        // Test signature verification failure
    }
}
```

### Integration Tests

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task AuthenticateWithRealAgent_Succeeds()
{
    // Requires SSH agent running in test environment
    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SSH_AUTH_SOCK")))
    {
        return; // Skip if no agent
    }
    
    var service = new SshAgentAuthenticationService(publicKey, logger);
    var result = await service.AuthenticateAsync();
    
    result.IsSuccess.Should().BeTrue();
}
```

---

## Implementation Roadmap

### Phase 1: Core SSH Agent Protocol (T061b, T061c)

**Tasks**:
1. Implement ISshAgentClient interface
2. Implement SshAgentClient with Unix socket support
3. Implement SSH agent protocol messages (request identities, sign request)
4. Implement signature verification (Ed25519, RSA)
5. Unit tests with mocked agent

**Deliverables**:
- `ISshAgentClient.cs`
- `SshAgentClient.cs`
- `SshAgentAuthenticationService.cs`
- Unit tests

### Phase 2: Authentication Service Integration (T061d)

**Tasks**:
1. Create AuthenticationServiceFactory
2. Implement auto-selection logic (agent vs key file)
3. Implement fallback mechanism
4. Configuration schema updates

**Deliverables**:
- `AuthenticationServiceFactory.cs`
- Updated configuration schema
- Integration tests

### Phase 3: CLI and UX (T061e)

**Tasks**:
1. Update CLI error messages
2. Add setup instructions to error output
3. Add public key configuration prompts
4. Update help text

**Deliverables**:
- Updated `TodayCommandHandler.cs`
- Updated `ThisWeekCommandHandler.cs`
- Enhanced error messages

### Phase 4: Documentation (T061f)

**Tasks**:
1. Create AUTHENTICATION.md guide
2. Update README.md
3. Add troubleshooting section
4. Platform-specific instructions

**Deliverables**:
- `docs/AUTHENTICATION.md`
- Updated `README.md`
- Troubleshooting guide

---

## Recommended Dependencies

### Cryptography Libraries

**For Ed25519 Support** (.NET 8 and earlier):
```xml
<PackageReference Include="BouncyCastle.Cryptography" Version="2.3.1" />
```

**For .NET 9+**: Use built-in `System.Security.Cryptography` Ed25519 support

### No Additional Dependencies Needed

The implementation can use standard .NET libraries:
- `System.Net.Sockets` (Unix domain sockets, .NET 6+)
- `System.Security.Cryptography` (RSA, SHA256)
- `System.IO` (file operations)
- `System.Runtime.InteropServices` (platform detection)

---

## Migration Path for Existing Users

### Backward Compatibility

1. **Existing key file authentication remains default** until user opts in to SSH agent
2. **Sessions remain valid** regardless of authentication method
3. **Configuration is additive**: New SSH agent settings don't break existing config

### User Migration Flow

**Step 1: Add public key to config**
```bash
export TENSECONDTOM_AUTH_PUBLICKEY="$(cat ~/.ssh/id_ed25519.pub)"
```

**Step 2: Verify SSH agent is running**
```bash
echo $SSH_AUTH_SOCK
ssh-add -l
```

**Step 3: Log in with new method**
```bash
tom logout
tom login
# → App detects agent and uses it automatically
```

**Step 4: Remove passphrase dependency**
```bash
# No more passphrase prompts! Agent handles authentication
```

### Configuration Update Script

```bash
#!/bin/bash
# migrate-to-ssh-agent.sh

echo "Ten Second Tom - SSH Agent Migration"
echo

# Check SSH agent
if [ -z "$SSH_AUTH_SOCK" ]; then
    echo "ERROR: SSH agent not running"
    echo "Start with: eval \$(ssh-agent) && ssh-add"
    exit 1
fi

# Check for keys in agent
if ! ssh-add -l > /dev/null 2>&1; then
    echo "ERROR: No keys in SSH agent"
    echo "Add your key with: ssh-add ~/.ssh/id_ed25519"
    exit 1
fi

# Get public key
PUBLIC_KEY_PATH="$HOME/.ssh/id_ed25519.pub"
if [ ! -f "$PUBLIC_KEY_PATH" ]; then
    echo "ERROR: Public key not found at $PUBLIC_KEY_PATH"
    exit 1
fi

PUBLIC_KEY=$(cat "$PUBLIC_KEY_PATH")

echo "Found public key:"
echo "  $PUBLIC_KEY"
echo

# Offer to add to environment
echo "Add this to your shell profile (~/.bashrc or ~/.zshrc):"
echo
echo "export TENSECONDTOM_AUTH_PUBLICKEY=\"$PUBLIC_KEY\""
echo

# Offer to update appsettings
echo "Or add to appsettings.json:"
echo
cat <<EOF
{
  "TenSecondTom": {
    "Auth": {
      "PublicKey": "$PUBLIC_KEY"
    }
  }
}
EOF

echo
echo "After configuration, run:"
echo "  tom logout"
echo "  tom login"
```

---

## Conclusion

### Chosen Approach: Direct Socket Communication

**Rationale**:
- No mature .NET libraries exist for SSH agent protocol
- Direct implementation provides full control and transparency
- Unix domain socket support in .NET 6+ makes this straightforward
- Small protocol surface area (2 message types needed)
- Cross-platform support with platform-specific adaptations

### Implementation Plan

1. **T061b**: Test SSH agent client and authentication service (TDD)
2. **T061c**: Implement `SshAgentClient` and `SshAgentAuthenticationService`
3. **T061d**: Create factory pattern for auth service selection
4. **T061e**: Update CLI error messages and UX
5. **T061f**: Document SSH agent setup and configuration

### Expected Benefits

- **Security**: Private keys never exposed to application
- **UX**: Touch ID / hardware key support via agent
- **Compatibility**: Works with 1Password, Secretive, YubiKey, etc.
- **Fallback**: Existing key file auth remains available

### Estimated Effort

- **Research**: ✅ COMPLETE (T061a)
- **Development**: ~8-12 hours (T061b-T061e)
- **Documentation**: ~2-3 hours (T061f)
- **Total**: ~10-15 hours

---

## References

- [OpenSSH Agent Protocol (draft-ietf-secsh-agent)](https://tools.ietf.org/html/draft-miller-ssh-agent-14)
- [SSH Protocol Architecture (RFC 4251)](https://tools.ietf.org/html/rfc4251)
- [.NET Unix Domain Sockets](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.unixdomainsocketendpoint)
- [BouncyCastle Cryptography](https://www.bouncycastle.org/csharp/)
- [SSH Agent Forwarding](https://developer.github.com/v3/guides/using-ssh-agent-forwarding/)
- [Secretive - SSH Agent for macOS](https://github.com/maxgoedjen/secretive)
- [1Password SSH Agent](https://developer.1password.com/docs/ssh/)

---

**Document Status**: ✅ COMPLETE  
**Ready for Implementation**: YES  
**Next Task**: T061b - Test SSH Agent Authentication Service
