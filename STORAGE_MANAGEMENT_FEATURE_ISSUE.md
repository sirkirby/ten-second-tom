# Storage Management Feature - Issue Draft

**Title**: Add Storage Management feature slice for provider switching, migration, and routing

## Summary

Now that issue #44 has implemented the pluggable storage provider infrastructure, we need user-facing commands to manage storage providers. This feature will allow users to switch providers, migrate data between providers, validate configurations, and potentially route different note types to different providers.

## Context

**Completed in #44**:
- ✅ Core infrastructure: `IStorageProvider`, `IStorageProviderFactory`, assembly scanning
- ✅ Two provider implementations: `DefaultStorageProvider`, `ObsidianStorageProvider`
- ✅ Setup wizard integration for initial provider selection
- ✅ Options pattern configuration (`StorageOptions`)
- ✅ Backward compatibility with legacy `MemoryDirectory`

**What's Missing**: User-facing commands to manage storage post-setup

**Architectural Clarity**:
- Storage provider **infrastructure** lives in `src/Infrastructure/Storage/` (cross-cutting)
- Storage management **feature** should live in `src/Features/StorageManagement/` (user commands)
- This properly applies VSA: features consume infrastructure, infrastructure doesn't execute user commands

## Proposed Commands

### 1. `tom storage list`

**Purpose**: List available storage providers with metadata

**Output**:
```
Available Storage Providers:

  * default (active)
    Default File System
    TST-native hierarchical structure optimized for organization.
    Current location: /Users/john/ten-second-tom

  obsidian
    Obsidian Vault Integration
    Store entries in an Obsidian vault for seamless integration.
```

**Implementation Notes**:
- Use `IStorageProviderFactory.GetAvailableProviders()` (may need to add this method)
- Show current active provider from `IOptions<StorageOptions>`
- Display provider metadata: `DisplayName`, `Description`

---

### 2. `tom storage validate`

**Purpose**: Validate current storage configuration and provider health

**Output** (success):
```
✓ Storage Provider: Obsidian Vault Integration
✓ Vault Path: /Users/john/Documents/MyVault
✓ Subdirectory: ten-second-tom
✓ Write Permissions: OK
✓ Vault Structure: Valid (.obsidian directory found)
✓ Storage Health: OK (237 entries, 1.2 MB)
```

**Output** (failure):
```
✗ Storage Provider: Obsidian Vault Integration
✗ Vault Path: /Users/john/Documents/MyVault
  Error: .obsidian directory not found
  Suggestion: Ensure the vault has been opened in Obsidian at least once
```

**Implementation Notes**:
- Call `IStorageProvider.ValidateConfigurationAsync()`
- Check directory existence, permissions, structure
- Report storage statistics (entry count, disk usage)
- Provide actionable error messages and suggestions

---

### 3. `tom storage switch`

**Purpose**: Switch to a different storage provider

**Usage**:
```bash
# Interactive mode (prompts for provider and configuration)
tom storage switch

# Non-interactive mode
tom storage switch --provider obsidian --root /Users/john/Documents/MyVault --subdirectory ten-second-tom

# With migration option
tom storage switch --provider obsidian --migrate
```

**Workflow**:
1. Validate current storage provider
2. Prompt user for new provider selection (similar to setup wizard)
3. Prompt for provider-specific configuration
4. Validate new configuration before committing
5. Update configuration file (`~/.tom/config/tom-config.json`)
6. Optionally trigger migration (see `tom storage migrate`)
7. Confirm switch successful

**Options**:
- `--provider <id>`: Target provider ID (e.g., `default`, `obsidian`)
- `--root <path>`: RootDirectory for new provider
- `--subdirectory <name>`: Optional subdirectory within root
- `--migrate`: Automatically migrate existing entries to new provider
- `--no-migrate`: Switch provider without migrating data (user handles manually)
- `--dry-run`: Preview configuration changes without applying

**Implementation Notes**:
- Reuse `ISetupWizardUI` methods from Setup feature for consistency
- Update configuration using Options pattern
- Validate new provider configuration before committing
- Provide clear warnings about data migration

---

### 4. `tom storage migrate`

**Purpose**: Migrate entries from current provider to another provider

**Usage**:
```bash
# Interactive migration wizard
tom storage migrate

# Non-interactive migration
tom storage migrate --from default --to obsidian \
  --source /Users/john/ten-second-tom \
  --target /Users/john/Documents/MyVault/ten-second-tom

# Dry run to preview migration
tom storage migrate --from default --to obsidian --dry-run
```

**Workflow**:
1. Validate source provider and location
2. Validate target provider and location
3. Scan source entries (daily, weekly, templates)
4. Transform file paths and names to target provider conventions
5. Show migration plan with entry count and affected files
6. Prompt for confirmation (unless `--force`)
7. Copy entries with progress indicator
8. Validate migrated entries
9. Optionally update configuration to use new provider
10. Optionally backup or delete source entries

**Options**:
- `--from <provider>`: Source provider ID
- `--to <provider>`: Target provider ID
- `--source <path>`: Source root directory (if different from config)
- `--target <path>`: Target root directory
- `--dry-run`: Show migration plan without executing
- `--backup`: Create backup of source before migration (default: true)
- `--delete-source`: Delete source entries after successful migration (default: false)
- `--force`: Skip confirmation prompt
- `--filter <pattern>`: Migrate only entries matching pattern (e.g., `2025-10-*`)

**Output**:
```
Migration Plan:
  From: Default File System (/Users/john/ten-second-tom)
  To:   Obsidian Vault (/Users/john/Documents/MyVault/ten-second-tom)

Entries to migrate:
  Daily entries:   127 files
  Weekly entries:  18 files
  Templates:       3 files
  Total size:      2.4 MB

Transformations:
  today/2025/10/today-10-28-2025-1.md
    → Daily Notes/2025-10-28 Entry 1.md

? Proceed with migration? (y/N)

Migrating entries... ████████████████████ 100% (148/148)

✓ Migration complete!
  Migrated: 148 entries
  Failed:   0 entries
  Duration: 2.3s

? Update storage configuration to use Obsidian provider? (Y/n)
```

**Implementation Notes**:
- Use `IStorageProvider` implementations for reading/writing
- Implement file path transformation logic (Default ↔ Obsidian naming conventions)
- Preserve YAML frontmatter during migration
- Validate each migrated entry
- Implement progress reporting (Spectre.Console progress bar)
- Create backup directory before migration (`~/.tom/backups/migration-{timestamp}/`)
- Log all migration operations for troubleshooting

---

### 5. `tom storage info`

**Purpose**: Show detailed information about current storage configuration

**Output**:
```
Storage Configuration:

Provider:       Obsidian Vault Integration (obsidian)
Root Directory: /Users/john/Documents/MyVault
Subdirectory:   ten-second-tom
Full Path:      /Users/john/Documents/MyVault/ten-second-tom

Directory Structure:
  Daily Notes/      127 entries, 1.8 MB
  Weekly Reviews/    18 entries, 412 KB
  Templates/          3 files, 24 KB

Configuration:
  Retention Policy:  OneYear
  Auto Purge:        Enabled
  Create If Missing: Yes

Statistics:
  Total entries:     145
  Total size:        2.2 MB
  Oldest entry:      2024-03-15
  Newest entry:      2025-10-28
  Daily avg:         0.4 entries/day

Health:
  ✓ All directories accessible
  ✓ Write permissions OK
  ✓ No orphaned files
  ✓ All entries have valid metadata
```

**Implementation Notes**:
- Read from `IOptions<StorageOptions>`
- Use `IStorageProvider` to scan directories
- Calculate storage statistics
- Perform health checks
- Use Spectre.Console tables for formatted output

---

### 6. `tom storage repair` (Future Enhancement)

**Purpose**: Repair common storage issues

**Potential Use Cases**:
- Regenerate missing YAML frontmatter
- Fix incorrect file naming conventions
- Rebuild directory structure
- Remove duplicate entries
- Update metadata fields

**Note**: This can be added in a future iteration after core management commands are stable.

---

## Architectural Design

### Vertical Slice Structure

```
src/Features/StorageManagement/
├── ListProviders.cs
│   ├── Query
│   ├── Handler
│   └── ProviderInfo (record)
├── ValidateStorage.cs
│   ├── Query
│   ├── Handler
│   └── ValidationResult (record)
├── SwitchProvider.cs
│   ├── Command
│   ├── Validator
│   ├── Handler
│   └── SwitchResult (record)
├── MigrateStorage.cs
│   ├── Command
│   ├── Validator
│   ├── Handler
│   └── MigrationResult (record)
├── GetStorageInfo.cs
│   ├── Query
│   ├── Handler
│   └── StorageInfo (record)
├── Services/
│   ├── IStorageMigrationService.cs
│   ├── StorageMigrationService.cs       # Handles migration logic
│   └── IPathTransformationService.cs    # Transforms paths between providers
└── DependencyInjection.cs
```

### CLI Command Structure

```
src/Cli/Commands/StorageCommands/
├── StorageCommand.cs                    # Root command: tom storage
├── ListProvidersCommand.cs              # Subcommand: tom storage list
├── ValidateStorageCommand.cs            # Subcommand: tom storage validate
├── SwitchProviderCommand.cs             # Subcommand: tom storage switch
├── MigrateStorageCommand.cs             # Subcommand: tom storage migrate
└── StorageInfoCommand.cs                # Subcommand: tom storage info
```

### Dependencies

**Feature consumes Infrastructure**:
- `IStorageProviderFactory` - List and create providers
- `IStorageProvider` - Validate, read, write entries
- `IOptions<StorageOptions>` - Current configuration
- `IConfiguration` - Update configuration file

**Feature-specific services**:
- `IStorageMigrationService` - Migration orchestration
- `IPathTransformationService` - Path/name transformations between providers

### Key Interfaces

```csharp
namespace TenSecondTom.Features.StorageManagement.Services;

/// <summary>
/// Service for migrating storage entries between providers.
/// </summary>
public interface IStorageMigrationService
{
    /// <summary>
    /// Plans a migration from one provider to another.
    /// </summary>
    Task<MigrationPlan> PlanMigrationAsync(
        IStorageProvider sourceProvider,
        IStorageProvider targetProvider,
        MigrationOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a migration plan.
    /// </summary>
    Task<Result<MigrationResult>> ExecuteMigrationAsync(
        MigrationPlan plan,
        IProgress<MigrationProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a backup of source entries before migration.
    /// </summary>
    Task<Result<string>> CreateBackupAsync(
        IStorageProvider sourceProvider,
        CancellationToken cancellationToken);
}

/// <summary>
/// Service for transforming file paths and names between provider conventions.
/// </summary>
public interface IPathTransformationService
{
    /// <summary>
    /// Transforms a file path from source provider format to target provider format.
    /// </summary>
    string TransformPath(
        string sourcePath,
        string sourceProviderId,
        string targetProviderId);

    /// <summary>
    /// Transforms entry metadata between provider conventions.
    /// </summary>
    EntryMetadata TransformMetadata(
        EntryMetadata sourceMetadata,
        string targetProviderId);
}
```

---

## Implementation Phases

### Phase 1: Core Commands (MVP)
- `tom storage list` - Discover available providers
- `tom storage validate` - Health check current storage
- `tom storage info` - Show current configuration

**Effort**: 2-3 days
**Value**: Immediate visibility and diagnostics

### Phase 2: Provider Switching
- `tom storage switch` - Change provider interactively
- Configuration file updates
- Provider-specific prompts (reuse Setup wizard UI)

**Effort**: 2-3 days
**Value**: Enable post-setup provider changes

### Phase 3: Data Migration
- `tom storage migrate` - Full migration workflow
- `IStorageMigrationService` implementation
- Path transformation logic
- Dry-run and validation
- Progress reporting

**Effort**: 4-5 days
**Value**: Seamless data portability

### Phase 4: Advanced Features (Future)
- `tom storage repair` - Fix common issues
- Multi-provider routing (route different note types to different providers)
- Scheduled migrations or sync

**Effort**: TBD
**Value**: Power user features

---

## Testing Requirements

Following critical path testing approach from #44:

### Unit Tests (per use case)

**ListProviders.cs Tests**:
- Handler returns all discovered providers
- Active provider is marked correctly
- Empty provider list handled gracefully

**ValidateStorage.cs Tests**:
- Validation succeeds for healthy storage
- Validation fails for missing directories
- Validation fails for permission issues
- Clear error messages provided

**SwitchProvider.cs Tests**:
- Switch updates configuration correctly
- Invalid provider ID rejected
- Configuration validated before committing
- Rollback on validation failure

**MigrateStorage.cs Tests**:
- Migration plan calculates correct entry counts
- Path transformation works for all providers
- Backup created before migration
- Failed migration rolls back gracefully
- Progress reporting updates correctly

**GetStorageInfo.cs Tests**:
- Info displays all configuration fields
- Storage statistics calculated correctly
- Health checks detect issues

### Integration Tests

**StorageManagementWorkflowTests.cs**:
- Complete switch workflow (Default → Obsidian)
- Complete migration workflow with validation
- Switch without migrate (manual migration)
- Dry-run migration (no side effects)
- Backup and restore workflow

### CLI Tests

**StorageCommandsTests.cs**:
- All subcommands registered correctly
- Options parsed and validated
- Exit codes correct (0 success, 1 failure)
- Error messages displayed to stderr

---

## Configuration Impact

**No changes to `StorageOptions`** - this feature only reads configuration and updates via standard mechanisms.

**Configuration Update Strategy**:
- Use `IConfiguration` to update `~/.tom/config/tom-config.json`
- Preserve all non-storage configuration
- Validate JSON before writing
- Create backup of config before updates

---

## User Experience Considerations

### Interactive vs Non-Interactive

**Interactive Mode** (default):
- Prompts for all required values
- Provides suggestions and validation feedback
- Asks for confirmation on destructive operations
- Shows progress indicators for long operations

**Non-Interactive Mode** (CI/CD, scripting):
- All values provided via flags
- `--force` skips confirmation prompts
- `--dry-run` available for preview
- Exit codes indicate success/failure

### Error Handling

**Clear error messages**:
```
✗ Error: Cannot migrate to Obsidian provider
  Reason: Target vault is not writable
  Path: /Users/john/Documents/MyVault

  Suggestions:
  • Check file system permissions: ls -la /Users/john/Documents
  • Ensure vault is not on a read-only volume
  • Grant Full Disk Access on macOS (System Preferences → Security)

  For more help: tom storage validate
```

**Graceful degradation**:
- If provider discovery fails, show last known providers
- If validation fails, show cached status
- Always provide `--help` for self-service

### Progress Indicators

Use Spectre.Console for rich CLI experience:
- Progress bars for migration operations
- Status indicators (✓, ✗, ⚠)
- Tables for structured output (storage info, provider list)
- Confirmation prompts with sensible defaults

---

## Documentation Updates

### README.md

Add new section after "Setup":

```markdown
## Storage Management

After initial setup, you can manage storage providers using the `tom storage` command:

### List Available Providers
\`\`\`bash
tom storage list
\`\`\`

### Switch Storage Provider
\`\`\`bash
# Interactive mode
tom storage switch

# Non-interactive with migration
tom storage switch --provider obsidian --migrate
\`\`\`

### Migrate Data Between Providers
\`\`\`bash
tom storage migrate --from default --to obsidian
\`\`\`

### View Storage Information
\`\`\`bash
tom storage info
\`\`\`

### Validate Storage Health
\`\`\`bash
tom storage validate
\`\`\`

📚 See [Storage Management Guide](docs/STORAGE-MANAGEMENT.md) for detailed documentation.
```

### docs/STORAGE-MANAGEMENT.md (New)

Create comprehensive guide covering:
- All command usage with examples
- Migration workflows and best practices
- Troubleshooting common migration issues
- Backup and recovery procedures
- Advanced usage (dry-run, filters, scripting)

### docs/OBSIDIAN-STORAGE.md

Update "Migrating from Default Storage" section:

```markdown
## Migrating from Default Storage

### Automated Migration (Recommended)

Use the built-in migration command:

\`\`\`bash
# Interactive migration wizard
tom storage migrate

# Or non-interactive
tom storage migrate --from default --to obsidian \
  --target /Users/yourname/Documents/MyVault \
  --subdirectory ten-second-tom
\`\`\`

This will:
- Automatically transform file paths and names
- Preserve all metadata
- Create a backup before migration
- Validate all migrated entries

### Manual Migration

See [Manual Migration Steps](#manual-migration-steps) if you prefer manual control.
```

---

## Success Criteria

✅ **Commands Implemented**:
- `tom storage list` - Shows available providers
- `tom storage validate` - Validates current storage
- `tom storage info` - Shows storage details
- `tom storage switch` - Changes provider
- `tom storage migrate` - Migrates data

✅ **Testing**:
- Unit tests for all use case handlers (80%+ coverage)
- Integration tests for complete workflows
- CLI tests for command registration and options

✅ **Documentation**:
- README.md updated with storage management section
- docs/STORAGE-MANAGEMENT.md created
- docs/OBSIDIAN-STORAGE.md updated with automated migration

✅ **User Experience**:
- Interactive and non-interactive modes work
- Clear error messages with actionable suggestions
- Progress indicators for long operations
- Dry-run mode for safe preview

✅ **Backward Compatibility**:
- Existing storage configurations continue to work
- No breaking changes to storage infrastructure
- Manual migration path still available

---

## Dependencies and Prerequisites

**Requires**:
- ✅ Issue #44 completed (storage provider infrastructure)
- ✅ Assembly scanning infrastructure (MediatR, FluentValidation)
- ✅ Options pattern configuration
- ✅ Spectre.Console for rich CLI output

**Blocks**:
- Future multi-provider routing features
- Advanced storage orchestration
- Automated backup/sync features

---

## Questions and Open Issues

1. **Provider Discovery Enhancement**: Should `IStorageProviderFactory` expose `GetAvailableProviders()` or should we add `IProviderMetadata`?

2. **Configuration Updates**: Should we use `IConfiguration` directly or create a `IConfigurationWriter` abstraction?

3. **Backup Location**: Should backups live in `~/.tom/backups/` or within the storage root?

4. **Migration Atomicity**: Should we implement rollback on partial migration failure?

5. **Concurrent Access**: Should we implement file locking during migration to prevent concurrent writes?

6. **Multi-Provider Routing**: Phase 4 feature - allow different note types to different providers (e.g., daily → Obsidian, weekly → Default). This would require routing configuration and multi-provider resolution.

---

## Related Issues

- #44 - Expand storage system with pluggable provider architecture (completed)
- (Future) Multi-provider routing and orchestration
- (Future) Cloud storage providers (Google Drive, Dropbox, iCloud)

---

## Notes

This issue properly applies **Vertical Slice Architecture**:
- **Infrastructure** (#44): `IStorageProvider`, `IStorageProviderFactory`, provider implementations
- **Feature** (this issue): User-facing commands that consume infrastructure

The feature is self-contained in `src/Features/StorageManagement/` and has no cross-dependencies on other features. It only depends on shared infrastructure (`IStorageProvider`, `IOptions<StorageOptions>`).
