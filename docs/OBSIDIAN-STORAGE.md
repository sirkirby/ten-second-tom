# Obsidian Storage Integration Guide

This guide covers how to use Ten Second Tom with Obsidian vault storage, including setup, migration, and bidirectional sync workflows.

## Table of Contents

- [Overview](#overview)
- [Why Use Obsidian Storage?](#why-use-obsidian-storage)
- [Setup for New Users](#setup-for-new-users)
- [Migrating from Default Storage](#migrating-from-default-storage)
- [File Structure Comparison](#file-structure-comparison)
- [Subdirectory Isolation](#subdirectory-isolation)
- [Bidirectional Sync](#bidirectional-sync)
- [Configuration Reference](#configuration-reference)
- [Troubleshooting](#troubleshooting)

## Overview

Ten Second Tom's **Obsidian Storage Provider** integrates seamlessly with your Obsidian vault, allowing you to:

- Store TST entries as standard Markdown files in your vault
- Access and edit entries from both TST CLI and Obsidian app
- Leverage Obsidian's powerful linking, tagging, and graph view features
- Keep all your notes in one place with automatic bidirectional sync
- Use Obsidian's daily notes alongside TST's structured memory entries

The Obsidian provider maintains full compatibility with TST's command structure while adapting file paths and formats to Obsidian conventions.

## Why Use Obsidian Storage?

### Benefits

**Unified Knowledge Base**
- All your notes, memories, and thoughts in one searchable vault
- Leverage Obsidian's graph view to visualize connections between TST entries and other notes
- Use Obsidian's powerful search across all your content

**Rich Editing Experience**
- Edit TST entries in Obsidian's feature-rich editor
- Add backlinks, tags, and embeds to TST entries
- Use Obsidian plugins (templates, calendar, dataview) with TST content

**Bidirectional Sync**
- Changes made in TST appear immediately in Obsidian
- Edits made in Obsidian are accessible via TST commands
- No manual export/import needed

**Obsidian-Friendly Conventions**
- Entries use human-readable filenames: `2025-10-28 Entry 1.md`
- Organized in familiar Obsidian folders: `Daily Notes/`, `Weekly Reviews/`
- Compatible with Obsidian's YAML frontmatter format

### When to Use Default Storage

The Default storage provider may be better if you:
- Don't use Obsidian or prefer a lightweight setup
- Want TST to manage its own isolated directory structure
- Prefer the traditional TST folder hierarchy (`today/`, `thisweek/`)
- Don't need integration with other note-taking tools

## Setup for New Users

If you're setting up Ten Second Tom for the first time and want to use Obsidian storage:

### 1. Prerequisites

- **Obsidian vault**: Ensure you have an existing Obsidian vault
  - The vault must contain a `.obsidian/` directory (automatically created by Obsidian)
  - If you don't have a vault yet, create one in Obsidian first

### 2. Run Setup Wizard

```bash
tom setup
```

### 3. Storage Provider Selection

When prompted for storage provider, select **Obsidian Vault**:

```
? Select storage provider:
  > Obsidian Vault
    Default File System

  Obsidian Vault: Store entries in an Obsidian vault for seamless
  integration with your notes. Supports bidirectional sync and
  Obsidian's daily notes format.
```

### 4. Configure Vault Path

Enter the absolute path to your Obsidian vault:

```
? Enter the path to your Obsidian vault: /Users/yourname/Documents/MyVault
```

The wizard will validate that:
- The directory exists
- It contains a `.obsidian/` directory (valid Obsidian vault)
- TST has write permissions

### 5. Optional: Configure Subdirectory

Choose whether to isolate TST entries in a subdirectory:

```
? Do you want to store TST entries in a subdirectory? (optional)
  Leave empty to store in vault root, or specify a subdirectory name: ten-second-tom
```

**Recommendation**: Use a subdirectory (e.g., `ten-second-tom/`) to keep TST entries organized separately from other notes.

### 6. Complete Setup

Finish the remaining setup steps (LLM configuration, SSH keys, etc.) as normal.

### 7. Verify Installation

After setup completes, verify the Obsidian integration:

```bash
# Check storage configuration
tom config show

# Create a test entry
echo "Test entry" | tom today

# Verify in Obsidian
# Open your vault and navigate to:
# - [vault]/ten-second-tom/Daily Notes/2025-10-28 Entry 1.md (if using subdirectory)
# - [vault]/Daily Notes/2025-10-28 Entry 1.md (if storing in vault root)
```

## Migrating from Default Storage

If you're already using Ten Second Tom with Default storage and want to migrate to Obsidian:

### Migration Steps

#### 1. Backup Existing Data

**IMPORTANT**: Always backup before migration!

```bash
# Backup your existing TST directory
tar -czf tst-backup-$(date +%Y%m%d).tar.gz ~/ten-second-tom

# Verify backup
tar -tzf tst-backup-*.tar.gz | head -20
```

#### 2. Run Setup with Obsidian Provider

```bash
# Re-run setup wizard
tom setup

# Select Obsidian Vault when prompted
# Configure your vault path and subdirectory
```

This updates your configuration but **does not move existing files**.

#### 3. Copy Existing Entries to Vault

**Manual Migration** (recommended for small datasets):

```bash
# Assuming:
# - Old location: ~/ten-second-tom/
# - Vault: ~/Documents/MyVault/
# - Subdirectory: ten-second-tom/

# Copy daily entries
cp -r ~/ten-second-tom/today/* ~/Documents/MyVault/ten-second-tom/Daily\ Notes/

# Copy weekly entries
cp -r ~/ten-second-tom/thisweek/* ~/Documents/MyVault/ten-second-tom/Weekly\ Reviews/

# Copy templates
cp -r ~/ten-second-tom/templates/* ~/Documents/MyVault/ten-second-tom/Templates/
```

**Automated Migration Script** (for large datasets):

Create a migration script to transform file paths:

```bash
#!/bin/bash
# migrate-to-obsidian.sh

OLD_DIR=~/ten-second-tom
VAULT=~/Documents/MyVault
TST_SUBDIR=ten-second-tom

# Migrate daily entries with Obsidian naming
find "$OLD_DIR/today" -name "*.md" | while read file; do
    # Extract date and entry number from filename
    # today-10-28-2025-1.md -> 2025-10-28 Entry 1.md
    basename=$(basename "$file")
    # ... transformation logic ...

    cp "$file" "$VAULT/$TST_SUBDIR/Daily Notes/$new_name"
done

# Similar logic for weekly entries
```

#### 4. Verify Migration

```bash
# List entries using TST
tom today -l

# Verify in Obsidian
# Open vault and check Daily Notes/ folder
```

#### 5. Test Bidirectional Sync

```bash
# Create new entry via TST
echo "Test after migration" | tom today

# Verify in Obsidian (should appear immediately)

# Edit entry in Obsidian
# Add content: "Edited in Obsidian"

# Read entry via TST
tom today -l
tom today -t  # Should show "Edited in Obsidian"
```

#### 6. Cleanup Old Storage (Optional)

Once you've verified the migration:

```bash
# Remove old storage directory
rm -rf ~/ten-second-tom

# Keep your backup for 30+ days
# Delete after you're confident migration succeeded
```

### Migration Gotchas

**File Path Differences**
- Default: `today/2025/10/today-10-28-2025-1.md`
- Obsidian: `Daily Notes/2025-10-28 Entry 1.md`

The Obsidian provider automatically transforms paths for new entries, but existing files need manual/scripted migration.

**Metadata Compatibility**
- TST uses YAML frontmatter compatible with Obsidian
- Existing metadata should transfer without issues
- Custom Obsidian frontmatter is preserved

**Template Differences**
- Both providers support templates
- Templates remain in `templates/` (Default) or `Templates/` (Obsidian)
- Template content and syntax are identical

## File Structure Comparison

### Default Storage Structure

```
~/ten-second-tom/
├── today/
│   └── 2025/
│       └── 10/
│           ├── today-10-28-2025-1.md
│           └── today-10-28-2025-2.md
├── thisweek/
│   └── 2025/
│       └── week-43/
│           └── thisweek-week-43-2025-1.md
├── templates/
│   ├── daily.md
│   └── weekly.md
└── config/
    └── tom-config.json
```

### Obsidian Storage Structure

```
~/Documents/MyVault/
├── .obsidian/                          # Obsidian metadata
├── ten-second-tom/                     # TST subdirectory (optional)
│   ├── Daily Notes/
│   │   ├── 2025-10-28 Entry 1.md      # Human-readable names
│   │   └── 2025-10-28 Entry 2.md
│   ├── Weekly Reviews/
│   │   └── 2025 Week 43 Entry 1.md
│   └── Templates/
│       ├── daily.md
│       └── weekly.md
└── [other Obsidian notes]
```

### File Naming Conventions

**Daily Entries**
- Default: `today-10-28-2025-1.md`
- Obsidian: `2025-10-28 Entry 1.md`

**Weekly Entries**
- Default: `thisweek-week-43-2025-1.md`
- Obsidian: `2025 Week 43 Entry 1.md`

The Obsidian naming convention is more human-readable and integrates better with Obsidian's file explorer and search.

## Subdirectory Isolation

### Why Use a Subdirectory?

**Organization Benefits**
- Keeps TST entries separate from other vault notes
- Easier to find TST-specific content
- Cleaner vault root directory
- Can apply Obsidian folder-specific settings

**Vault Root vs. Subdirectory**

**Vault Root** (e.g., `~/Documents/MyVault/`):
```
MyVault/
├── Daily Notes/           # TST daily entries here
├── Weekly Reviews/        # TST weekly entries here
├── Templates/             # TST templates here
└── [other notes]
```

**Subdirectory** (e.g., `~/Documents/MyVault/ten-second-tom/`):
```
MyVault/
├── ten-second-tom/
│   ├── Daily Notes/       # TST daily entries isolated
│   ├── Weekly Reviews/
│   └── Templates/
└── [other notes]
```

### Configuring Subdirectory

**During Setup**:
```
? Do you want to store TST entries in a subdirectory? ten-second-tom
```

**Manual Configuration** (`~/.tom/config/tom-config.json`):
```json
{
  "TenSecondTom": {
    "Storage": {
      "ProviderId": "obsidian",
      "RootDirectory": "/Users/yourname/Documents/MyVault",
      "MemorySubdirectory": "ten-second-tom"
    }
  }
}
```

**Environment Variable**:
```bash
export TenSecondTom__Storage__MemorySubdirectory="ten-second-tom"
```

### Changing Subdirectory

To move TST entries to a different subdirectory:

1. Update configuration (re-run `tom setup` or edit config file)
2. Move existing files manually or with script
3. Verify entries are accessible via TST commands

## Bidirectional Sync

### How It Works

The Obsidian provider uses the **file system as the source of truth**:

- TST writes entries as standard Markdown files
- Obsidian reads files from the vault
- Changes in either app are immediately reflected (no polling/sync delay)
- File watchers ensure both apps see updates instantly

### Supported Workflows

**TST → Obsidian**
```bash
# Create entry via TST
echo "Meeting notes" | tom today

# Immediately visible in Obsidian
# Navigate to: Daily Notes/2025-10-28 Entry 1.md
```

**Obsidian → TST**
```
1. Open entry in Obsidian
2. Edit content: Add "## Action Items\n- Follow up with team"
3. Save file (Cmd+S / Ctrl+S)
4. Read via TST: tom today -t
   # Shows updated content including action items
```

**Concurrent Edits**
- If you edit in both apps simultaneously, the **last save wins**
- Obsidian typically auto-saves more frequently than manual TST updates
- For long editing sessions, prefer Obsidian's editor

### File Format Compatibility

**YAML Frontmatter**

TST entries use YAML frontmatter that Obsidian understands:

```markdown
---
id: 550e8400-e29b-41d4-a716-446655440000
timestamp: 2025-10-28T10:30:00Z
command: today
entry_number: 1
tags:
  - tst
  - daily-entry
---

# Daily Entry

Content here...
```

You can add additional Obsidian-specific frontmatter:

```markdown
---
id: 550e8400-e29b-41d4-a716-446655440000
timestamp: 2025-10-28T10:30:00Z
command: today
entry_number: 1
tags:
  - tst
  - daily-entry
  - project/alpha      # Custom Obsidian tag
cssclass: tst-daily    # Custom CSS class for styling
---
```

**Markdown Body**

- TST and Obsidian share standard Markdown syntax
- Obsidian wikilinks (`[[Note]]`) are preserved but not interpreted by TST
- TST output is plain text/Markdown, fully compatible with Obsidian rendering

### Linking Between Notes

**From TST Entries to Obsidian Notes**

Edit TST entries in Obsidian to add wikilinks:

```markdown
# Meeting Notes

Discussed project timeline with [[John Doe]].

Related to: [[Project Alpha Planning]]
```

**From Obsidian Notes to TST Entries**

Link to TST entries using their Obsidian-friendly names:

```markdown
# Project Plan

See daily standup notes: [[2025-10-28 Entry 1]]
```

Or use relative paths if in subdirectory:

```markdown
[[ten-second-tom/Daily Notes/2025-10-28 Entry 1|Standup Notes]]
```

## Configuration Reference

### Environment Variables

```bash
# Storage provider selection
export TenSecondTom__Storage__ProviderId="obsidian"

# Obsidian vault path (absolute path required)
export TenSecondTom__Storage__RootDirectory="/Users/yourname/Documents/MyVault"

# Optional subdirectory within vault
export TenSecondTom__Storage__MemorySubdirectory="ten-second-tom"

# Retention policy (applies to all providers)
export TenSecondTom__Storage__RetentionPolicy="OneYear"

# Auto-purge expired entries
export TenSecondTom__Storage__AutoPurge="true"
```

### Configuration File

Location: `~/.tom/config/tom-config.json`

```json
{
  "TenSecondTom": {
    "Storage": {
      "ProviderId": "obsidian",
      "RootDirectory": "/Users/yourname/Documents/MyVault",
      "MemorySubdirectory": "ten-second-tom",
      "RetentionPolicy": "OneYear",
      "AutoPurge": true
    }
  }
}
```

### Validation

Check your configuration:

```bash
# Show current storage configuration
tom config show

# Validate storage setup
tom config validate

# Expected output for Obsidian provider:
# ✓ Storage: Obsidian vault: /Users/yourname/Documents/MyVault (subdirectory: ten-second-tom)
```

## Troubleshooting

### Vault Not Recognized

**Problem**: `.obsidian directory not found at {path}`

**Solutions**:
1. Verify you've opened the vault in Obsidian at least once (creates `.obsidian/`)
2. Check path is absolute, not relative: `/Users/name/Vault` not `~/Vault`
3. Ensure no typos in vault path
4. On Windows, use forward slashes: `C:/Users/name/Vault`

### Permission Denied

**Problem**: `Vault is not writable: Access to the path is denied`

**Solutions**:
1. Check file system permissions: `ls -la /path/to/vault`
2. Ensure vault is not on read-only volume (external drive, network share)
3. On macOS, grant Terminal/app Full Disk Access (System Preferences → Security → Privacy → Full Disk Access)
4. Run TST with appropriate user permissions (avoid root unless necessary)

### Entries Not Appearing in Obsidian

**Problem**: Created entry via TST but not visible in Obsidian

**Solutions**:
1. Refresh Obsidian file explorer (right-click → Refresh)
2. Check subdirectory configuration matches between TST and expectations
3. Verify entry was actually created: `tom today -l`
4. Check Obsidian's file exclusion settings (Settings → Files & Links → Excluded files)
5. Ensure vault path in TST config matches actual vault location

### Edits in Obsidian Not Showing in TST

**Problem**: Edited file in Obsidian but TST shows old content

**Solutions**:
1. Ensure file was saved in Obsidian (check for unsaved indicator)
2. Verify file path matches expected location
3. Check file permissions (TST must have read access)
4. Try reading specific entry: `tom today -t` (shows today's entries)

### File Name Conflicts

**Problem**: Entry numbering skipped (e.g., Entry 1, Entry 3, missing Entry 2)

**Explanation**: This happens if you manually created a file with the same name or deleted an entry between saves.

**Solution**: TST automatically increments entry numbers to avoid conflicts. This is expected behavior.

### Retroactive Migration Issues

**Problem**: Manually copied files but TST doesn't recognize them

**Cause**: File names don't match Obsidian provider's expected format.

**Solution**: Ensure migrated files use Obsidian naming:
- `2025-10-28 Entry 1.md` (not `today-10-28-2025-1.md`)
- Maintain YAML frontmatter with `id`, `timestamp`, `command`, `entry_number` fields

### Subdirectory Configuration Mismatch

**Problem**: TST creating entries in vault root but expected in subdirectory

**Verification**:
```bash
# Check current configuration
tom config show | grep MemorySubdirectory

# Should show:
# MemorySubdirectory: ten-second-tom
```

**Solution**: Update configuration and re-run `tom setup` if incorrect.

### Cloud Sync Conflicts (Obsidian Sync, iCloud, Dropbox)

**Problem**: Duplicate entries or sync conflicts when vault is synced across devices

**Recommendations**:
1. Use TST on only one device at a time per vault
2. Let cloud sync complete before running TST on another device
3. Avoid creating entries simultaneously on multiple devices
4. If conflicts occur, manually merge duplicates in Obsidian

### Getting Help

If you encounter issues not covered here:

1. Check logs: TST logs to `~/.tom/logs/`
2. Run with verbose logging: `tom today -v` (if supported)
3. Validate configuration: `tom config validate`
4. Open an issue: [GitHub Issues](https://github.com/sirkirby/ten-second-tom/issues)
5. Include:
   - TST version: `tom --version`
   - Storage provider: `tom config show | grep ProviderId`
   - Vault path structure (anonymized)
   - Error messages or unexpected behavior

---

**Next Steps**:
- [Configuration Guide](CONFIGURATION.md) - Advanced configuration options
- [README](../README.md) - General TST documentation
- [CI/CD Guide](CICD.md) - Automated workflows and releases
