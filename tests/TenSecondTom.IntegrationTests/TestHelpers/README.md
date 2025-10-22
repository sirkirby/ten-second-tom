# Test Helpers - User Secrets Cleanup

## Problem Statement

Integration tests that use UserSecrets create temporary directories in `~/.microsoft/usersecrets/` with names like `TenSecondTom-Test-{guid}` or `tom-test-{guid}`. When tests fail, timeout, or are interrupted (Ctrl+C), the `IDisposable.Dispose()` or `IAsyncLifetime.DisposeAsync()` methods may not get called, leaving orphaned directories behind.

Over time, hundreds or thousands of these orphaned directories can accumulate, consuming disk space and causing confusion during debugging.

## Solution Architecture

We implement a three-layer cleanup strategy:

### 1. Test Fixture Base Class (`UserSecretsTestFixture`)

**Purpose**: Provides robust, reusable cleanup logic for individual test classes.

**Key Features**:
- Implements `IAsyncLifetime` for xUnit lifecycle management
- Generates unique test UserSecrets IDs with recognizable prefixes
- Pre-test cleanup: Removes orphaned directories from previous failed runs
- Post-test cleanup: Ensures cleanup even when tests fail
- Retry logic: Handles file locks and race conditions (3 retries with 100ms delay)
- Diagnostic logging: Helps troubleshoot cleanup issues

**Usage Example**:
```csharp
[Collection(UserSecretsCollection.Name)]
public sealed class MyIntegrationTests : UserSecretsTestFixture
{
    public MyIntegrationTests()
    {
        // Set up logger for diagnostic output
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        Logger = loggerFactory.CreateLogger<UserSecretsTestFixture>();
    }

    [Fact]
    public async Task MyTest()
    {
        // Use TestUserSecretsId from base fixture
        var storageService = new UserSecretsStorageService(logger, TestUserSecretsId);
        // ... test code
    }
}
```

### 2. Collection Fixture (`UserSecretsCollectionFixture`)

**Purpose**: Provides collection-wide cleanup before and after all tests in the collection run.

**Key Features**:
- Runs once per test collection (not per test class)
- Pre-collection cleanup: Removes all orphaned test directories before any tests start
- Post-collection cleanup: Final cleanup after all tests complete
- Works across multiple test classes in the same collection

**Usage Example**:
```csharp
// Apply collection attribute to all test classes that need cleanup
[Collection(UserSecretsCollection.Name)]
public sealed class MyTests : UserSecretsTestFixture
{
    // Tests here benefit from collection-wide cleanup
}
```

### 3. Manual Cleanup Script (`.scripts/cleanup-test-secrets.sh`)

**Purpose**: Allows developers to manually clean up orphaned directories.

**Key Features**:
- Cross-platform support (macOS, Linux, Windows via Git Bash)
- Safe deletion with retry logic
- Dry-run mode for preview
- Verbose output for debugging
- Finds and removes both naming patterns: `TenSecondTom-Test-*` and `tom-test-*`

**Usage Examples**:
```bash
# Preview what would be deleted
.scripts/cleanup-test-secrets.sh --dry-run

# Delete orphaned directories
.scripts/cleanup-test-secrets.sh

# Verbose output for debugging
.scripts/cleanup-test-secrets.sh --dry-run --verbose
```

## When to Use Each Approach

| Scenario | Solution | Why |
|----------|----------|-----|
| Writing new integration tests | `UserSecretsTestFixture` base class | Automatic cleanup with minimal boilerplate |
| Test collection needs pre/post cleanup | `UserSecretsCollectionFixture` | Ensures clean state for entire test collection |
| Tests were interrupted (Ctrl+C) | Manual cleanup script | Removes accumulated orphaned directories |
| CI/CD cleanup | Manual cleanup script in pipeline | Ensures clean build environment |
| Debugging cleanup issues | Verbose logging + manual script | Diagnose why directories aren't being cleaned |

## Implementation Details

### Naming Convention

All test UserSecrets directories use recognizable prefixes:
- `TenSecondTom-Test-{guid}` - Standard pattern
- `tom-test-{guid}` - Shorter pattern (used in some older tests)

This makes it safe for cleanup scripts to identify and remove test directories without touching production UserSecrets.

### Retry Logic

Cleanup attempts use exponential backoff with retries:
1. **Attempt 1**: Immediate cleanup
2. **Attempt 2**: Wait 100ms, retry
3. **Attempt 3**: Wait 100ms, retry
4. **Failure**: Log error with instructions to run manual cleanup script

### Error Handling

Cleanup is designed to be non-fatal:
- **Individual test cleanup failure**: Logs warning, continues with other tests
- **Collection cleanup failure**: Logs error, provides manual cleanup instructions
- **Script cleanup failure**: Reports failed count, suggests manual intervention

## Best Practices

### For Test Authors

1. **Always inherit from `UserSecretsTestFixture`** for tests using UserSecrets
2. **Add `[Collection(UserSecretsCollection.Name)]`** attribute to test classes
3. **Use `TestUserSecretsId`** property instead of generating your own IDs
4. **Implement `DisposeAsync`** if you have additional cleanup beyond UserSecrets
5. **Call `base.DisposeAsync()`** at the end of your cleanup

Example:
```csharp
[Collection(UserSecretsCollection.Name)]
public sealed class MyTests : UserSecretsTestFixture
{
    private readonly TemporaryTestDirectory _tempDir;

    public MyTests()
    {
        _tempDir = new TemporaryTestDirectory();

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        Logger = loggerFactory.CreateLogger<UserSecretsTestFixture>();
    }

    public override async Task DisposeAsync()
    {
        // Clean up your resources first
        _tempDir.Dispose();

        // Then call base cleanup for UserSecrets
        await base.DisposeAsync();
    }
}
```

### For CI/CD

Add cleanup to your CI pipeline:
```yaml
# Before tests
- name: Clean up orphaned test secrets
  run: .scripts/cleanup-test-secrets.sh

# Run tests
- name: Run integration tests
  run: dotnet test

# After tests (even if tests fail)
- name: Final cleanup
  if: always()
  run: .scripts/cleanup-test-secrets.sh
```

### For Developers

Run cleanup periodically:
```bash
# Check how many orphaned directories exist
ls ~/.microsoft/usersecrets/ | grep -c "TenSecondTom-Test"

# Clean them up
.scripts/cleanup-test-secrets.sh
```

## Troubleshooting

### Problem: Tests still leaving orphaned directories

**Diagnosis**:
1. Check if tests are inheriting from `UserSecretsTestFixture`
2. Verify tests have `[Collection(UserSecretsCollection.Name)]` attribute
3. Enable verbose logging to see cleanup attempts

**Solution**:
- Update tests to use the fixture pattern
- Run manual cleanup script to remove existing orphans

### Problem: "Permission denied" errors during cleanup

**Diagnosis**:
- Directory may be locked by another process
- File permissions may be restrictive

**Solution**:
1. Close any running test processes
2. Wait a few seconds for locks to release
3. Re-run cleanup script (retry logic will handle transient locks)
4. If persistent, manually delete with elevated permissions

### Problem: Cleanup script not finding directories

**Diagnosis**:
- UserSecrets location may differ by OS
- Directory naming pattern may have changed

**Solution**:
1. Verify UserSecrets location: `~/.microsoft/usersecrets/` (macOS/Linux) or `%APPDATA%\Microsoft\UserSecrets\` (Windows)
2. Check for directories matching patterns: `TenSecondTom-Test-*` or `tom-test-*`
3. Update script if naming convention changed

## Migration Guide

### Migrating Existing Tests

If you have existing tests using the old cleanup pattern:

**Before**:
```csharp
public sealed class MyTests : IDisposable
{
    private readonly string _testSecretsId;

    public MyTests()
    {
        _testSecretsId = $"TenSecondTom-Test-{Guid.NewGuid()}";
    }

    public void Dispose()
    {
        var path = Path.Combine(..., _testSecretsId);
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Cleanup fails silently - directories left behind!
        }
    }
}
```

**After**:
```csharp
[Collection(UserSecretsCollection.Name)]
public sealed class MyTests : UserSecretsTestFixture
{
    public MyTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        Logger = loggerFactory.CreateLogger<UserSecretsTestFixture>();
    }

    // No need to implement DisposeAsync if only cleaning up UserSecrets
    // Base class handles it automatically with retry logic
}
```

**Benefits**:
- Automatic retry on cleanup failures
- Pre-test cleanup of orphaned directories
- Collection-wide cleanup
- Diagnostic logging
- Less boilerplate code

## References

- **xUnit IAsyncLifetime**: https://xunit.net/docs/shared-context#async-lifetime
- **xUnit Collection Fixtures**: https://xunit.net/docs/shared-context#collection-fixture
- **UserSecrets Documentation**: https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets

---

**Last Updated**: 2025-10-21
**Related Files**:
- `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.IntegrationTests/TestHelpers/UserSecretsTestFixture.cs`
- `/Users/chris/Repos/ten-second-tom/tests/TenSecondTom.IntegrationTests/TestHelpers/UserSecretsCollectionFixture.cs`
- `/Users/chris/Repos/ten-second-tom/.scripts/cleanup-test-secrets.sh`
