# Scripts

This directory contains utility scripts for the Ten Second Tom project.

## cleanup-test-secrets.sh

**Purpose**: Cleans up orphaned UserSecrets directories left behind by failed or interrupted tests.

**Problem**: When integration tests fail, timeout, or are interrupted (Ctrl+C), they may leave behind temporary UserSecrets directories in `~/.microsoft/usersecrets/` with names like `TenSecondTom-Test-{guid}`. Over time, hundreds of these directories can accumulate.

**Usage**:
```bash
# Preview what would be deleted
.scripts/cleanup-test-secrets.sh --dry-run

# Delete orphaned test directories
.scripts/cleanup-test-secrets.sh

# Show detailed output
.scripts/cleanup-test-secrets.sh --dry-run --verbose

# Get help
.scripts/cleanup-test-secrets.sh --help
```

**Features**:
- Cross-platform support (macOS, Linux, Windows via Git Bash)
- Safe deletion with retry logic (3 attempts with 100ms delays)
- Dry-run mode for preview
- Verbose output for debugging
- Colored output for better readability
- Finds both naming patterns: `TenSecondTom-Test-*` and `tom-test-*`

**When to Use**:
- After interrupting tests with Ctrl+C
- Before running tests to ensure clean state
- Periodically to free up disk space
- In CI/CD pipelines for cleanup

**Exit Codes**:
- `0`: Success (all directories deleted or none found)
- `1`: Partial failure (some directories couldn't be deleted)

**Related Documentation**:
- Test cleanup patterns: `/tests/TenSecondTom.IntegrationTests/TestHelpers/README.md`
- UserSecrets fixture: `/tests/TenSecondTom.IntegrationTests/TestHelpers/UserSecretsTestFixture.cs`

---

**Last Updated**: 2025-10-21
