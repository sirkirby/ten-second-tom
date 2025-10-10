# JSON Serialization Fix for Configuration Save

## Problem

When attempting to save configuration through the `/setup` wizard, the application failed with:

```text
System.InvalidOperationException: Reflection-based serialization has been disabled for this application.
```

This occurred in `UserSecretsStorageService.SaveAsync()` at the line:

```csharp
var json = JsonSerializer.Serialize(configData, JsonOptions);
```

## Root Cause

The project is configured with **trimming enabled** (`PublishTrimmed = true` in `.csproj`), which automatically disables reflection-based serialization in System.Text.Json for performance and size optimization.

The `JsonOptions` used in `UserSecretsStorageService` lacked a `TypeInfoResolver`, which is required when reflection is disabled. The code was trying to serialize:

- `Dictionary<string, string?>` - for User Secrets storage
- `Dictionary<string, object>` - for appsettings.json fallback

## Solution

Created a **JsonSerializerContext** with source generation to enable serialization without reflection:

### 1. Created `ConfigurationJsonContext.cs`

```csharp
using System.Text.Json.Serialization;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// JSON serialization context for configuration storage.
/// Enables source generation for trimmed/AOT scenarios.
/// </summary>
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
internal partial class ConfigurationJsonContext : JsonSerializerContext
{
}
```

This tells the JSON source generator to create serialization code at compile-time for these types.

### 2. Updated `UserSecretsStorageService.cs`

Added the `TypeInfoResolver` to the `JsonOptions`:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    TypeInfoResolver = ConfigurationJsonContext.Default  // ✅ Added this line
};
```

Also updated all `JsonSerializer.Deserialize` calls to use `JsonOptions`:

- Line 107: `LoadAsync` method
- Line 160: `FallbackToAppSettingsAsync` method

### 3. Updated `Program.cs` for Self-Contained Distribution

Replaced `.AddUserSecrets<object>(optional: true)` with explicit path resolution:

```csharp
// Add User Secrets explicitly (for self-contained/trimmed binaries)
// This doesn't rely on assembly reflection like AddUserSecrets<T>()
string userSecretsId = "ten-second-tom-secrets";
string userSecretsPath = GetUserSecretsPath(userSecretsId);
if (File.Exists(userSecretsPath))
{
    configurationBuilder.AddJsonFile(userSecretsPath, optional: true, reloadOnChange: true);
}
```

Added helper method to resolve User Secrets path without reflection:

```csharp
private static string GetUserSecretsPath(string userSecretsId)
{
    string userSecretsBasePath;
    
    if (OperatingSystem.IsWindows())
    {
        // Windows: %APPDATA%\Microsoft\UserSecrets\{userSecretsId}\secrets.json
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        userSecretsBasePath = Path.Combine(appData, "Microsoft", "UserSecrets");
    }
    else
    {
        // macOS/Linux: ~/.microsoft/usersecrets/{userSecretsId}/secrets.json
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        userSecretsBasePath = Path.Combine(home, ".microsoft", "usersecrets");
    }
    
    return Path.Combine(userSecretsBasePath, userSecretsId, "secrets.json");
}
```

This ensures the self-contained binary can find User Secrets without relying on assembly metadata.

## Benefits

1. **Enables trimmed builds**: Configuration save now works with `PublishTrimmed = true`
2. **Performance**: Source-generated serialization is faster than reflection-based
3. **AOT-ready**: Code is compatible with ahead-of-time compilation
4. **Smaller binaries**: Trimming can remove unused reflection code
5. **Compile-time safety**: Serialization errors caught at compile-time instead of runtime

## Testing

All 860 tests pass (757 succeeded, 103 skipped):

```bash
dotnet test
# Test summary: total: 860, failed: 0, succeeded: 757, skipped: 103
```

Specifically, the 10 `UserSecretsStorageService` tests all pass:

```bash
dotnet test --filter "FullyQualifiedName~UserSecretsStorageService"
# Test summary: total: 10, failed: 0, succeeded: 10, skipped: 0
```

## Manual Verification

To verify the fix works end-to-end:

```bash
# Run the application
dotnet run --project src/TenSecondTom.csproj

# In the shell, run setup
/setup

# Complete the wizard and confirm save with 'y'
# Configuration should save successfully to:
# ~/.microsoft/usersecrets/ten-second-tom-secrets/secrets.json
```

## Related Files

- `src/Infrastructure/Configuration/ConfigurationJsonContext.cs` - NEW: Source generation context
- `src/Infrastructure/Configuration/UserSecretsStorageService.cs` - MODIFIED: Added TypeInfoResolver
- `src/TenSecondTom.csproj` - CONTEXT: Has `PublishTrimmed = true`

## Pattern for Future Use

When adding new types that need JSON serialization in this project:

1. Add `[JsonSerializable(typeof(YourType))]` to `ConfigurationJsonContext`
2. Use `JsonOptions` with `TypeInfoResolver = ConfigurationJsonContext.Default`
3. Or create a separate context for different domains (e.g., `ApiJsonContext`, `StorageJsonContext`)

This pattern ensures compatibility with:

- ✅ Trimmed builds (`PublishTrimmed = true`)
- ✅ Native AOT compilation
- ✅ Self-contained single-file executables
- ✅ Performance-critical scenarios

## References

- [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [.NET app trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
