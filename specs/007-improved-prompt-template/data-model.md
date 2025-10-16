# Data Model: Improved Prompt Template Support

**Feature**: 007-improved-prompt-template
**Date**: 2025-10-15
**Purpose**: Define entities, relationships, validation rules, and state transitions for the prompt template system.

## Entity Definitions

### 1. TemplateMetadata

**Purpose**: Represents YAML front matter metadata from template files

**Type**: `sealed record`

**Properties**:

| Property | Type | Required | Default | Validation | Description |
|----------|------|----------|---------|------------|-------------|
| `TemplateType` | `TemplateType` enum | ✅ Yes | None | Must be valid enum value | Indicates if template is for daily or weekly summaries |
| `Title` | `string` | ✅ Yes | None | Not null or whitespace, max 200 chars | Display name for template selection UI |
| `Description` | `string?` | ❌ No | `null` | Max 500 chars if provided | Optional description shown in selection UI |
| `Version` | `string?` | ❌ No | `"1.0"` | Semantic version format if provided | Template version for tracking updates |
| `Author` | `string?` | ❌ No | `null` | Max 100 chars if provided | Optional author name for custom templates |
| `CreatedDate` | `DateTime?` | ❌ No | `null` | Valid DateTime if provided | When template was created (for custom templates) |
| `Tags` | `string[]?` | ❌ No | `null` | Max 20 tags, each max 50 chars | Future: for categorization (not used in v1) |

**Relationships**:
- Embedded in `PromptTemplate` (1:1)
- Parsed from YAML front matter in `.md` files

**Example YAML**:
```yaml
---
templateType: daily
title: Daily Summary
description: Default template for daily journal entries
version: 1.0
---
```

**Validation Rules**:
- `TemplateType` MUST be `daily` or `weekly` (case-insensitive in YAML)
- `Title` MUST be present and non-empty
- `Version` SHOULD follow semantic versioning (advisory warning if not)

---

### 2. PromptTemplate (Enhanced)

**Purpose**: Represents a complete prompt template with metadata and content

**Type**: `sealed record` (existing model, enhanced)

**Properties**:

| Property | Type | Required | Default | Validation | Description |
|----------|------|----------|---------|------------|-------------|
| `TemplateId` | `string` | ✅ Yes | None | Kebab-case, max 100 chars, no path separators | Unique identifier (filename without extension) |
| `Content` | `string` | ✅ Yes | None | Not null, max 1MB | Template body with variable placeholders |
| `TemplateType` | `TemplateType` enum | ✅ Yes | None | Valid enum value | Type of template |
| `Description` | `string?` | ❌ No | `null` | Max 500 chars | Description for display |
| `Metadata` | `TemplateMetadata?` | ❌ No | `null` | Valid metadata if present | Parsed YAML front matter (new field) |
| `Source` | `TemplateSource` enum | ✅ Yes | None | Valid enum value | Where template was loaded from (new field) |

**New Enum: TemplateSource**:
```csharp
public enum TemplateSource
{
    Embedded,    // Loaded from compiled resources
    FileSystem   // Loaded from user's templates directory
}
```

**Relationships**:
- Contains `TemplateMetadata` (1:0..1)
- Loaded by `IPromptTemplateLoader` implementations
- Selected by user via `TemplateSelectionUI`
- Used by `CreateDailyEntryCommand` and `CreateWeeklyReviewCommand`

**Validation Rules**:
- `TemplateId` MUST be valid filename (no path separators, no invalid chars)
- `Content` MUST NOT be empty after removing YAML front matter
- `TemplateType` from `Metadata` MUST match `TemplateType` property if both present
- If `Source` is `FileSystem`, `Metadata` SHOULD be present (warning if missing)

---

### 3. TemplateListItem

**Purpose**: Lightweight model for template selection UI

**Type**: `sealed record`

**Properties**:

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `TemplateId` | `string` | ✅ Yes | None | Unique identifier for selection |
| `Title` | `string` | ✅ Yes | None | Display name in selection list |
| `Description` | `string` | ✅ Yes | None | Description shown in selection list (empty string if none) |
| `Source` | `TemplateSource` | ✅ Yes | None | Where template came from |
| `IsDefault` | `bool` | ✅ Yes | None | True if this is a default template |

**Relationships**:
- Created from `PromptTemplate` for UI display
- Displayed in `Spectre.Console` `SelectionPrompt`

**Sorting Rules**:
- Default templates first (`IsDefault = true`)
- Then by `Title` alphabetically (case-insensitive)

**Display Format**:
```
{Title} - {Description} [Default]
```

---

### 4. Validation Pattern (No Custom Result Type)

**Purpose**: Validate template files using standard Result<T> pattern

**Implementation**: `TemplateValidator` returns `Result<PromptTemplate>` or `Result<bool>` for validation operations, using the standard Result pattern already established in the codebase.

**Rationale**: Maintains consistency with existing error handling patterns. Validation errors are communicated via Result.Failure(errorMessage) rather than custom result objects.

**Logging**: Warnings and validation failures are logged via Serilog within the validator and loader implementations.

**Relationships**:
- Used by `FileSystemTemplateLoader` to validate templates before loading
- Returns standard Result<T> for consistency with codebase patterns

---

### 5. TemplateDirectory (Conceptual Entity)

**Purpose**: Represents the templates directory and its contents

**Type**: Not a C# class - conceptual entity representing filesystem state

**Location**: `{StorageConfiguration.MemoryDirectory}/templates/`

**Contents**:
- `daily-summary.md` (default daily template)
- `weekly-review.md` (default weekly template)
- Additional `.md` files (custom user templates)

**Operations**:
- Create directory if missing (during setup/migration)
- List all `.md` files (for template discovery)
- Validate directory is writable (during setup)
- Recreate with defaults if deleted (self-healing)

**State Transitions**:
```
[Not Exists] --[Setup/Migration]--> [Exists with Defaults]
[Exists with Defaults] --[User Adds Custom]--> [Exists with Defaults + Custom]
[Exists] --[Deleted by User]--> [Not Exists] --[Next Command]--> [Exists with Defaults]
```

---

## Entity Relationships Diagram

```
┌─────────────────────────┐
│  ConfigurationSettings  │
│  (.Storage.             │
│   MemoryDirectory)      │
└───────────┬─────────────┘
            │
            │ contains
            ▼
┌─────────────────────────┐
│  TemplateDirectory      │
│  (filesystem)           │
│  {MemoryDirectory}/     │
│   templates/            │
└───────────┬─────────────┘
            │
            │ contains multiple
            ▼
┌─────────────────────────┐         ┌─────────────────────────┐
│  Template File (.md)    │────────▶│  TemplateMetadata       │
│  - Filename = ID        │ has 1   │  (YAML front matter)    │
│  - Content              │         │  - TemplateType         │
│                         │         │  - Title                │
└───────────┬─────────────┘         │  - Description          │
            │                       └─────────────────────────┘
            │ loaded as
            ▼
┌─────────────────────────┐
│  PromptTemplate         │
│  - TemplateId           │
│  - Content              │
│  - TemplateType         │
│  - Metadata             │◀───────┐
│  - Source               │        │
└───────────┬─────────────┘        │
            │                      │
            │ mapped to            │
            ▼                      │
┌─────────────────────────┐        │
│  TemplateListItem       │        │
│  (for selection UI)     │        │
│  - TemplateId           │        │
│  - Title                │        │
│  - Description          │        │
└───────────┬─────────────┘        │
            │                      │
            │ selected by          │
            ▼                      │
┌─────────────────────────┐        │
│  User Selection         │        │
│  (via Spectre.Console)  │        │
└───────────┬─────────────┘        │
            │                      │
            │ returns              │
            └──────────────────────┘
```

---

## State Transitions

### Template Lifecycle

```
[Embedded Resource]
    │
    ├──[First Setup]──▶ [Copied to FileSystem] ──┐
    │                                             │
    │                                             ▼
    │                   [Edited by User] ◀─── [In FileSystem]
    │                         │                   │
    │                         ▼                   │
    │                   [Modified Version]        │
    │                         │                   │
    │                         └──────────────────┘
    │
    └──[FileSystem Missing]──▶ [Used as Fallback]
```

### Template Selection Flow

```
[Command Started] (today or thisweek)
    │
    ▼
[Load Templates for Type]
    │
    ├──[Found Multiple]──▶ [Show Selection UI] ──▶ [User Selects] ──┐
    │                                                                 │
    ├──[Found One]──▶ [Auto-Select] ────────────────────────────────┤
    │                                                                 │
    ├──[Found None]──▶ [Fall Back to Embedded] ─────────────────────┤
    │                                                                 │
    └──[Load Failed]──▶ [Fall Back to Embedded] ─────────────────────┤
                                                                      │
                                                                      ▼
                                                            [Use Selected Template]
                                                                      │
                                                                      ▼
                                                            [Generate Summary]
```

### Configuration Migration Flow

```
[App Startup]
    │
    ▼
[Check Configuration]
    │
    ├──[Templates Dir Exists]──▶ [Validate Default Templates Present]
    │                                   │
    │                                   ├──[Present]──▶ [Continue]
    │                                   │
    │                                   └──[Missing]──▶ [Install Missing Defaults]
    │
    └──[Templates Dir Missing]──▶ [Create Directory] ──▶ [Install Defaults]
                                                               │
                                                               ▼
                                                          [Log Migration]
                                                               │
                                                               ▼
                                                          [Continue]
```

---

## Validation Rules Summary

### TemplateMetadata Validation

**Critical (Must Pass)**:
- `templateType` must be "daily" or "weekly" (case-insensitive)
- `title` must be present and non-empty
- `title` must be ≤200 characters
- YAML must be valid syntax

**Warnings (Non-Blocking)**:
- `version` should follow semantic versioning format
- `description` recommended for custom templates
- Unknown fields in YAML (for forward compatibility)

### Template File Validation

**Critical (Must Pass)**:
- File must be ≤1MB
- File must be readable
- File must be valid UTF-8
- Filename must be valid (no path separators, no special chars except `-`, `_`)
- Content after YAML front matter must not be empty

**Warnings (Non-Blocking)**:
- File has no YAML front matter (will use defaults based on filename)
- File has very long lines (>500 chars - may indicate formatting issue)

### Template Directory Validation

**Critical (Must Pass)**:
- Directory must be within configured memory directory
- Directory must be writable (checked during setup)
- At least one default template must be loadable (daily or weekly)

**Warnings (Non-Blocking)**:
- Directory contains non-.md files (ignored, but may indicate mistake)
- Multiple templates with similar names (not duplicate, but potentially confusing)

---

## Indexing and Performance

### Template Discovery

**Strategy**: Eager load all templates at start of command, cache for command duration

**Rationale**:
- Small number of templates (~5-20 expected)
- Fast to read (<100ms total for 20 templates)
- Avoids repeated file I/O during selection
- Simpler than lazy loading

**Implementation**:
```csharp
// Load once at command start
var templates = await _templateLoader.LoadAllTemplatesAsync(templateType);
var listItems = templates.Select(t => new TemplateListItem { ... });

// Cache for duration of command
_cachedTemplates = listItems;
```

**No Persistent Index**: Templates are re-discovered on each command run (fast enough)

### File Watching

**Decision**: No file system watching in v1

**Rationale**:
- Template edits are rare during command execution
- File watching adds complexity
- User can re-run command to see changes
- Can add in future if needed

---

## Data Persistence

### Filesystem Layout

```
{MemoryDirectory}/
├── templates/
│   ├── daily-summary.md          # Default daily template
│   ├── weekly-review.md          # Default weekly template
│   ├── daily-standup.md          # User's custom daily template
│   ├── weekly-retro.md           # User's custom weekly template
│   └── ...                       # Additional custom templates
├── daily/                        # Existing: daily memory entries
├── weekly/                       # Existing: weekly memory entries
└── search-index/                 # Existing: search functionality
```

### Template File Format

```markdown
---
templateType: daily
title: My Custom Daily Template
description: A focused template for daily standups
version: 1.0
author: John Doe
---

# Daily Standup - {{DATE}}

## What I did today
{{TODAY_ENTRIES}}

## What I'll do tomorrow
[User fills in]

## Blockers
[User fills in]
```

### Variable Substitution

**Variables Supported** (existing functionality):
- `{{DATE}}` - Current date
- `{{ENTRIES}}` - User's entries
- `{{TODAY_ENTRIES}}` - Daily entries
- `{{WEEK_ENTRIES}}` - Weekly entries
- (Additional variables determined by command context)

**Validation**: Variables are NOT validated in templates (flexible design)

---

## Backward Compatibility

### Existing Code Impact

**Minimal Changes Required**:
1. `PromptTemplate` record enhanced (add `Metadata` and `Source` fields)
2. `IPromptTemplateLoader` interface enhanced (add `LoadAllTemplatesAsync()` method)
3. `EmbeddedPromptTemplateLoader` updated (add YAML parsing fallback)
4. Command handlers updated (add template selection step)

**No Breaking Changes**:
- Existing embedded templates continue to work
- Existing API signatures remain compatible
- New fields are optional (nullable)

### Migration Path

**New Users**: Templates installed automatically during setup
**Existing Users**: Templates added automatically on next command run (via `ConfigurationChecker`)
**No User Action Required**: Fully automatic migration

---

## Testing Considerations

### Test Data

**Sample Valid Templates**:
- `test-daily-valid.md` - Complete valid daily template
- `test-weekly-valid.md` - Complete valid weekly template
- `test-minimal.md` - Minimal valid template (only required fields)

**Sample Invalid Templates**:
- `test-invalid-yaml.md` - Malformed YAML
- `test-missing-type.md` - Missing required `templateType`
- `test-oversized.md` - File >1MB
- `test-empty-content.md` - Empty after front matter
- `test-invalid-type.md` - Invalid `templateType` value

### Mocking Strategy

**File System Mocking**:
- Use `IFileSystem` abstraction (or mock `File`/`Directory` via wrapper)
- Test with in-memory file systems where possible
- Integration tests use temporary directories

**Template Loader Mocking**:
- Mock `IPromptTemplateLoader` for command handler tests
- Test concrete `FileSystemTemplateLoader` in unit tests
- Test `EmbeddedPromptTemplateLoader` for fallback scenarios

---

## Conclusion

The data model is straightforward, leveraging existing patterns and entities. Key additions are:
1. **TemplateMetadata** for YAML front matter
2. **TemplateListItem** for UI display
3. Enhanced **PromptTemplate** with metadata and source tracking
4. Clear validation rules and state transitions

All entities follow project conventions (sealed records, nullable reference types, validation with Result<T>). Ready to proceed with contract generation.
