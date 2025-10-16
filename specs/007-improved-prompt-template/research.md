# Research: Improved Prompt Template Support

**Feature**: 007-improved-prompt-template
**Date**: 2025-10-15
**Purpose**: Document technical decisions, alternatives considered, and best practices for implementing filesystem-based prompt template support with YAML metadata.

## Research Questions & Decisions

### 1. YAML Front Matter Parsing

**Question**: What's the best approach for parsing YAML front matter in markdown files in .NET?

**Decision**: Use YamlDotNet library (already in project dependencies)

**Rationale**:
- Already included as project dependency (`YamlDotNet` v16.3.0)
- Industry-standard library for YAML parsing in .NET
- Supports strong typing with C# objects
- Well-maintained and actively developed
- Jekyll/Hugo-compatible format (delimited by `---`)

**Alternatives Considered**:
- **Custom regex parsing**: Rejected - fragile, error-prone, doesn't handle complex YAML structures
- **MarkDig metadata extension**: Rejected - MarkDig is already in dependencies but primarily for rendering, YamlDotNet is purpose-built for YAML
- **JSON front matter**: Rejected - YAML is the industry standard for markdown metadata (Jekyll, Hugo, Docusaurus all use YAML)

**Implementation Pattern**:
```csharp
// Example YAML front matter structure
---
templateType: daily
title: Daily Summary Template
description: Default template for daily summaries
version: 1.0
---

# Template content starts here
```

**Best Practices**:
- Use YamlDotNet's `Deserializer` with schema validation
- Define strongly-typed `TemplateMetadata` record
- Handle missing/malformed YAML gracefully with Result<T>
- Validate required fields (templateType minimum)
- Log warnings for unknown fields (forward compatibility)

### 2. Template Type Enumeration

**Question**: Should template types be extensible or fixed enum?

**Decision**: Fixed enum with "daily" and "weekly" values initially, with room for future extension

**Rationale**:
- Currently only two command types: `today` and `thisweek`
- Fixed enum provides type safety and compile-time checking
- Simpler to filter and validate
- Can add new types in future (MINOR version bump)
- YAML accepts string values that map to enum

**Alternatives Considered**:
- **String-based types**: Rejected - no type safety, prone to typos, harder to validate
- **Plugin-based extensible system**: Rejected - over-engineered for current needs, violates simplicity principle

**Implementation Pattern**:
```csharp
public enum TemplateType
{
    Daily,
    Weekly
}

// In TemplateMetadata
public required TemplateType TemplateType { get; init; }
```

**Best Practices**:
- Use string representation in YAML for readability
- Map case-insensitively ("daily", "Daily", "DAILY" all valid)
- Provide clear error messages for invalid types
- Document valid values in template examples

### 3. Template Storage Location

**Question**: Where should templates be stored relative to the memory directory?

**Decision**: `{MemoryDirectory}/templates/` subdirectory

**Rationale**:
- Keeps all user data in one location (already configured)
- Easy to find and edit (in same directory as memories)
- Simple path construction: `Path.Combine(memoryDir, "templates")`
- Consistent with existing `StorageConfiguration.MemoryDirectory`
- Backup-friendly (backup memory directory = backup everything)

**Alternatives Considered**:
- **Separate config directory**: Rejected - splits user data, harder to backup
- **App directory**: Rejected - permissions issues, not user-editable on all platforms
- **Per-command subdirectories** (`templates/daily/`, `templates/weekly/`): Rejected - unnecessary nesting, metadata handles filtering

**Implementation Pattern**:
```csharp
public static class TemplateDirectoryHelper
{
    public static string GetTemplatesDirectory(string memoryDirectory)
        => Path.Combine(memoryDirectory, "templates");
}
```

**Best Practices**:
- Create directory if missing (self-healing)
- Validate directory is writable during setup
- Handle concurrent access gracefully (multiple reads OK)
- Use absolute paths throughout

### 4. Configuration Migration Strategy

**Question**: How to detect and migrate existing configurations to include template support?

**Decision**: Enhance `ConfigurationChecker` to detect missing templates directory and auto-install

**Rationale**:
- `ConfigurationChecker` already runs on startup and validates config
- Can detect missing templates directory
- Simple migration: just create directory and copy default templates
- No schema version change needed (additive only)
- User sees seamless upgrade experience

**Alternatives Considered**:
- **Schema version bumping**: Rejected - overkill for additive change
- **Manual migration command**: Rejected - requires user action, not seamless
- **Lazy migration on first use**: Rejected - could surprise user mid-workflow

**Implementation Pattern**:
```csharp
// In ConfigurationChecker
public async Task<ValidationResult> ValidateAndMigrateAsync()
{
    // Existing validation...

    // Check templates directory
    var templatesDir = GetTemplatesDirectory(config.Storage.MemoryDirectory);
    if (!Directory.Exists(templatesDir))
    {
        _logger.LogInformation("Templates directory missing, auto-installing");
        await InstallDefaultTemplatesAsync(templatesDir);
    }

    // Validate at least default templates exist
    // ...
}
```

**Best Practices**:
- Run migration automatically, silently
- Log migration actions for debugging
- Handle failure gracefully (fall back to embedded)
- Don't block app startup on migration failure
- Notify user of migration in setup completion message

### 5. Template Selection UI

**Question**: How to present template choices to users in the CLI?

**Decision**: Use Spectre.Console's `SelectionPrompt<T>` with template names and descriptions

**Rationale**:
- Spectre.Console already in dependencies
- `SelectionPrompt` provides arrow-key navigation
- Can show template name + description in list
- Consistent with existing CLI interaction patterns
- Supports auto-selection when only one option

**Alternatives Considered**:
- **Numbered list with text input**: Rejected - less intuitive, more error-prone
- **Terminal.Gui full UI**: Rejected - overkill for simple selection, not needed for this feature
- **Default selection without prompt**: Rejected - removes user choice, doesn't meet requirements

**Implementation Pattern**:
```csharp
var selection = AnsiConsole.Prompt(
    new SelectionPrompt<TemplateListItem>()
        .Title("Select a template:")
        .AddChoices(availableTemplates)
        .UseConverter(t => $"{t.Title} - {t.Description}"));
```

**Best Practices**:
- Show template title and description in selection
- Sort templates: defaults first, then custom alphabetically
- Auto-select if only one template available (skip prompt)
- Handle cancellation gracefully (Ctrl+C)
- Cache template list for duration of command

### 6. File Size Limits

**Question**: How to enforce the 1MB template file size limit?

**Decision**: Check `FileInfo.Length` before reading file contents

**Rationale**:
- Prevents memory exhaustion from malicious/accidental large files
- 1MB is generous for text templates (~500 printed pages)
- Check before reading = no wasted I/O
- Simple to implement and test

**Alternatives Considered**:
- **Stream reading with limit**: Rejected - more complex, not needed for text files
- **No limit**: Rejected - security/stability risk
- **Smaller limit (100KB)**: Rejected - might be restrictive for elaborate templates

**Implementation Pattern**:
```csharp
var fileInfo = new FileInfo(templatePath);
if (fileInfo.Length > 1_048_576) // 1MB in bytes
{
    _logger.LogWarning("Template {Path} exceeds 1MB limit, skipping", templatePath);
    return Result<PromptTemplate>.Failure($"Template exceeds 1MB limit");
}
```

**Best Practices**:
- Use constant for limit (`MaxTemplateSizeBytes`)
- Log warning with file path when limit exceeded
- Include limit in error message to user
- Skip oversized templates, don't crash app
- Document limit in template creation guidance

### 7. Concurrent File Access

**Question**: How to handle concurrent reads of template files (e.g., user editing while app reads)?

**Decision**: Use `FileShare.Read` when opening files, catch `IOException` and retry once

**Rationale**:
- `FileShare.Read` allows multiple readers
- Most text editors release lock immediately after save
- Single retry handles transient locks (save operation)
- Reading is the common case (editing is rare)
- Graceful degradation: fall back to embedded on persistent failure

**Alternatives Considered**:
- **File locking/coordination**: Rejected - over-engineered, not needed for read-mostly scenario
- **No retry**: Rejected - poor user experience if save happens during read
- **Multiple retries with backoff**: Rejected - templates are fast to read, one retry sufficient

**Implementation Pattern**:
```csharp
for (int attempt = 0; attempt < 2; attempt++)
{
    try
    {
        using var stream = new FileStream(
            templatePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        // Read template...
        break;
    }
    catch (IOException) when (attempt == 0)
    {
        await Task.Delay(100); // Brief pause
    }
}
```

**Best Practices**:
- Open files with `FileShare.Read`
- Use `using` statements for proper disposal
- Single retry with 100ms delay
- Log retry attempts at Debug level
- Fall back to embedded template on failure

### 8. Template Validation

**Question**: What validation should be performed on template files?

**Decision**: Multi-level validation: metadata structure, required fields, file size, content encoding

**Rationale**:
- Metadata validation ensures template is usable
- Required fields check prevents incomplete templates
- File size validation prevents resource exhaustion
- Encoding validation prevents display issues
- Early validation = better error messages

**Alternatives Considered**:
- **Minimal validation (just parse)**: Rejected - silent failures, confusing to user
- **Strict validation (schema)**: Rejected - too rigid, limits user flexibility
- **Runtime validation only**: Rejected - fails during use instead of load

**Validation Levels**:
1. **File-level**: Size, existence, readability
2. **Metadata-level**: Valid YAML, required fields present
3. **Content-level**: UTF-8 encoding, reasonable length
4. **Business-level**: TemplateType valid, no duplicate IDs

**Implementation Pattern**:
```csharp
public sealed class TemplateValidator
{
    public Result ValidateTemplate(string filePath, TemplateMetadata metadata, string content)
    {
        // File validation
        if (new FileInfo(filePath).Length > MaxTemplateSizeBytes)
            return Result.Failure("Template exceeds size limit");

        // Metadata validation
        if (!Enum.IsDefined(typeof(TemplateType), metadata.TemplateType))
            return Result.Failure("Invalid template type");

        // Content validation
        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure("Template content is empty");

        return Result.Success();
    }
}
```

**Best Practices**:
- Validate early (during load, not use)
- Return specific error messages
- Log validation failures at Warning level
- Skip invalid templates, continue with valid ones
- Document validation rules in template guide

### 9. Embedded Template Updates

**Question**: How to update embedded default templates when already installed to filesystem?

**Decision**: Never overwrite user's filesystem templates; add version field to detect outdated templates

**Rationale**:
- User may have customized default templates (intended behavior)
- Overwriting loses user customizations (bad UX)
- Version field allows detecting outdated templates
- User can manually delete and re-run setup if they want latest defaults

**Alternatives Considered**:
- **Always overwrite on version change**: Rejected - loses user customizations
- **Merge/patch templates**: Rejected - complex, error-prone, unclear semantics
- **Side-by-side versioning** (`daily-v1.md`, `daily-v2.md`): Rejected - clutters directory

**Implementation Pattern**:
```yaml
---
templateType: daily
version: 1.0
---
```

```csharp
// Check version during validation (informational only)
if (metadata.Version < LatestVersion)
{
    _logger.LogInformation(
        "Template {Id} version {Version} is older than latest {Latest}",
        templateId, metadata.Version, LatestVersion);
}
```

**Best Practices**:
- Include version field in metadata
- Log outdated version info (don't block)
- Document update process in user guide
- Provide CLI command to restore defaults (future enhancement)
- Never auto-delete or auto-overwrite user files

### 10. Error Recovery and Fallbacks

**Question**: What should happen when template loading fails?

**Decision**: Graceful degradation: fall back to embedded templates, log warnings, notify user

**Rationale**:
- App must remain functional even with template issues
- Embedded templates are always available (compiled in)
- User sees notification, can fix and retry
- Logging helps debugging
- Self-healing behavior (recreate directory if missing)

**Alternatives Considered**:
- **Fail fast**: Rejected - breaks app for non-critical feature
- **Silent fallback**: Rejected - user doesn't know there's an issue
- **Retry indefinitely**: Rejected - could hang app

**Fallback Hierarchy**:
1. User's filesystem template (preferred)
2. Embedded default template (fallback)
3. Error message to user (last resort)

**Implementation Pattern**:
```csharp
public async Task<Result<PromptTemplate>> LoadTemplateAsync(
    string templateId,
    TemplateType type)
{
    // Try filesystem first
    var result = await TryLoadFromFileSystem(templateId, type);
    if (result.IsSuccess)
        return result;

    _logger.LogWarning(
        "Failed to load template from filesystem: {Error}, falling back to embedded",
        result.Error);

    // Fall back to embedded
    return await LoadEmbeddedTemplate(templateId, type);
}
```

**Best Practices**:
- Always have a fallback path
- Log the fallback reason
- Show user-friendly notification
- Recreate missing directories automatically
- Provide clear steps to fix in error messages

## Technology Stack Summary

### Core Dependencies (Already in Project)
- **YamlDotNet** (16.3.0): YAML front matter parsing
- **Spectre.Console** (0.51.1): Template selection UI
- **Serilog** (4.3.0): Structured logging
- **System.CommandLine** (2.0.0-rc): CLI framework

### .NET Features Leveraged
- File I/O with `FileStream` and `FileShare.Read`
- `Path.Combine` for cross-platform path handling
- Records for immutable models (`TemplateMetadata`)
- Nullable reference types for safety
- Async/await for file operations
- Result<T> pattern for error handling

**Validation Approach**: Custom `TemplateValidator` class is used rather than FluentValidation. For this feature's straightforward validation rules (file size, required fields, string lengths), a custom validator is simpler and has zero learning curve. FluentValidation would be valuable for more complex conditional validation scenarios with cross-field rules, but adds unnecessary complexity here.

### Design Patterns Applied
- **Vertical Slice Architecture**: Templates feature is self-contained
- **CQRS**: Queries to load, Commands to create/migrate
- **Factory Pattern**: Template loader creation
- **Strategy Pattern**: Different loaders (embedded vs filesystem)
- **Fallback Pattern**: Graceful degradation on errors

## Open Questions / Future Enhancements

1. **Template variables**: Current implementation uses `{{VARIABLE}}` syntax - should we validate variable names?
   - Decision: Not in this iteration - keep templates flexible, validation can come later

2. **Template categories/tags**: Should templates support additional metadata for organization?
   - Decision: Not needed yet - only 2 types initially, can add in future if needed

3. **Template preview**: Should CLI show template preview before selection?
   - Decision: Not in initial version - description field should be sufficient

4. **Template export/import**: Should there be commands to export/import template collections?
   - Decision: Future enhancement - users can manually copy directories for now

5. **Template versioning/history**: Should system track template edit history?
   - Decision: Out of scope - users can use git or backup tools

## References

- [YamlDotNet Documentation](https://github.com/aaubry/YamlDotNet/wiki)
- [Spectre.Console Selection Prompts](https://spectreconsole.net/prompts/selection)
- [Jekyll Front Matter](https://jekyllrb.com/docs/front-matter/)
- [.NET File I/O Best Practices](https://learn.microsoft.com/en-us/dotnet/standard/io/)
- Ten Second Tom Constitution v1.1.0

## Conclusion

All technical decisions have been documented with clear rationale. The design leverages existing project dependencies and patterns, requires no new external libraries, and aligns with constitutional principles. The implementation will be straightforward, testable, and maintainable. Ready to proceed to Phase 1 (design artifacts).
