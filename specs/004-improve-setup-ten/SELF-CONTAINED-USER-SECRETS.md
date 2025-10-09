# Self-Contained Distribution - User Secrets Support

## Overview

Ten Second Tom is distributed as a **self-contained .NET binary** with:
- ✅ Embedded .NET runtime (no SDK installation required)
- ✅ Trimmed binary (reduced size, reflection disabled)
- ✅ Cross-platform (macOS, Linux, Windows)
- ✅ User Secrets support without reflection

## User Secrets Path

Configuration is stored in User Secrets at a platform-specific location:

### macOS / Linux
```
~/.microsoft/usersecrets/ten-second-tom-secrets/secrets.json
```

### Windows
```
%APPDATA%\Microsoft\UserSecrets\ten-second-tom-secrets\secrets.json
```

## How It Works

### Traditional Approach (Doesn't Work with Trimming)

The standard .NET approach uses reflection:

```csharp
// ❌ Doesn't work in trimmed binaries
configuration.AddUserSecrets<object>(optional: true)
```

This relies on:
- Assembly attributes to find `UserSecretsId`
- Reflection to read assembly metadata
- **Fails in trimmed/self-contained builds**

### Our Solution (Works with Trimming)

We explicitly resolve the User Secrets path:

```csharp
// ✅ Works in trimmed binaries
string userSecretsId = "ten-second-tom-secrets";
string userSecretsPath = GetUserSecretsPath(userSecretsId);
if (File.Exists(userSecretsPath))
{
    configurationBuilder.AddJsonFile(userSecretsPath, optional: true, reloadOnChange: true);
}
```

The `GetUserSecretsPath` helper:
1. Detects the operating system
2. Resolves the correct base path
3. Combines with the secrets ID
4. Returns the full path to `secrets.json`

No reflection required! ✅

## JSON Serialization

User Secrets are JSON files, so we need JSON serialization that works without reflection.

### Source Generation

We use **System.Text.Json source generation**:

```csharp
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class ConfigurationJsonContext : JsonSerializerContext
{
}
```

This generates serialization code at **compile-time** instead of using reflection at runtime.

### Usage

All JSON operations use the context:

```csharp
// Serialize
var json = JsonSerializer.Serialize(configData, JsonOptions);

// Deserialize
var configData = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions);
```

Where `JsonOptions` includes:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    TypeInfoResolver = ConfigurationJsonContext.Default  // Source generation
};
```

## Benefits

### For Users
- ✅ No .NET SDK installation required
- ✅ Single executable file
- ✅ Secure configuration storage (User Secrets)
- ✅ Cross-platform compatibility

### For Developers
- ✅ Smaller binary size (trimming removes unused code)
- ✅ Faster startup (no reflection overhead)
- ✅ AOT-ready (ahead-of-time compilation compatible)
- ✅ Compile-time safety (serialization errors caught early)

## Testing

### Verify Self-Contained Binary

```bash
# Publish self-contained binary
dotnet publish src/TenSecondTom.csproj \
  -c Release \
  --self-contained \
  -r osx-arm64 \
  -o /tmp/tom-test

# Test configuration loading
/tmp/tom-test/TenSecondTom config show

# Output should show your configuration from User Secrets
```

### Verify Cross-Platform

```bash
# macOS (ARM64)
dotnet publish -r osx-arm64 --self-contained

# macOS (Intel)
dotnet publish -r osx-x64 --self-contained

# Linux (x64)
dotnet publish -r linux-x64 --self-contained

# Windows (x64)
dotnet publish -r win-x64 --self-contained
```

## Distribution

Users receive a single binary that:
1. Contains the .NET runtime
2. Reads configuration from User Secrets
3. Works on any machine (no dependencies)

### Example: Homebrew Distribution

```ruby
class TenSecondTom < Formula
  desc "Your personal memory assistant"
  homepage "https://github.com/yourusername/ten-second-tom"
  
  # Each platform gets its own self-contained binary
  if OS.mac? && Hardware::CPU.arm?
    url "https://github.com/.../ten-second-tom-osx-arm64.tar.gz"
  elsif OS.mac? && Hardware::CPU.intel?
    url "https://github.com/.../ten-second-tom-osx-x64.tar.gz"
  elsif OS.linux?
    url "https://github.com/.../ten-second-tom-linux-x64.tar.gz"
  end

  def install
    bin.install "TenSecondTom" => "tom"
  end
end
```

## Security

User Secrets are stored in the user's home directory:
- ✅ Not in the application directory
- ✅ Not committed to source control
- ✅ Not in the working directory
- ✅ Standard .NET location (shared with other .NET tools)

The secrets file is just JSON, but it's stored in a location that:
- Is user-specific (not shared across users)
- Has appropriate file permissions
- Is separate from the application binary

For production secrets (API keys), users should still use environment variables or proper secret management, but User Secrets are perfect for local development and CLI tools.

## Troubleshooting

### Configuration Not Loading

Check if the secrets file exists:

```bash
# macOS/Linux
ls -la ~/.microsoft/usersecrets/ten-second-tom-secrets/secrets.json

# Windows (PowerShell)
ls $env:APPDATA\Microsoft\UserSecrets\ten-second-tom-secrets\secrets.json
```

### Binary Too Large

The self-contained binary includes the .NET runtime, which adds ~60-70MB. This is normal and expected.

To reduce size:
- ✅ Already using trimming (`PublishTrimmed = true`)
- ✅ Already using single-file publishing
- Consider: Compression (e.g., UPX for further reduction)

### Runtime Errors

If you see reflection errors in the published binary:
1. Check that `ConfigurationJsonContext` is properly registered
2. Verify all `JsonSerializer` calls use `JsonOptions`
3. Ensure `TypeInfoResolver` is set in `JsonOptions`

## References

- [.NET Self-Contained Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/deploy-with-cli#self-contained-deployment)
- [.NET App Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [System.Text.Json Source Generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [User Secrets in .NET](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
