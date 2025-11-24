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

## test-notifier.sh

**Purpose**: Sends a sample payload through the native macOS notifier sidecar to validate interactive notifications without running the full CLI.

**Usage**:

```bash
.scripts/test-notifier.sh "Optional message override"
```

**Behavior**:

- Builds `bin/TenSecondTom.Extensions.MacOS.app` via `make extensions` if the notifier binary is missing
- Emits the JSON payload for easy debugging
- Blocks until you interact with the notification (or `Ctrl+C` to exit)

**Development tip**: Add the following to your `.env` (or copy `example.env`) so CLI runs use the dev-built helper alongside the Homebrew release:

```bash
TenSecondTom__Notifications__ExtensionDirectory=$PWD/bin/TenSecondTom.Extensions.MacOS.app
```

**Last Updated**: 2025-11-24
