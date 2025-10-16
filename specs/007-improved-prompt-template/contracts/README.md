# API Contracts: Improved Prompt Template Support

This directory contains the interface and command contracts for the improved prompt template feature.

## Overview

These contracts define the public API surface for:
- Loading templates from filesystem and embedded resources
- Installing default templates during setup/migration
- Querying available templates by type
- Selecting templates via interactive UI
- Parsing and validating YAML front matter metadata

## Files

### Core Interfaces

- **IPromptTemplateLoader.cs** - Enhanced interface for loading templates
  - Loads single templates by ID
  - Loads all templates for a type (new method)
  - Checks if templates directory exists (new method)
  - Supports filesystem and embedded sources
  - Handles YAML front matter parsing

- **ITemplateSelectionUI.cs** - Interface for template selection UI
  - Displays templates using Spectre.Console
  - Handles auto-selection for single template
  - Provides cancellation support

### Commands (CQRS)

- **InstallDefaultTemplatesCommand.cs** - Command to install default templates
  - Used during setup and migration
  - Idempotent (safe to run multiple times)
  - Returns installation statistics
  - Optionally preserves existing templates

- **ListTemplatesQuery.cs** - Query to retrieve available templates
  - Filters by template type
  - Returns sorted list for display
  - Includes validation statistics

### Models

- **TemplateMetadata.cs** - YAML front matter model
  - Required fields: templateType, title
  - Optional fields: description, version, author, createdDate, tags
  - Built-in validation logic
  - TemplateType enum definition

## Usage Examples

### Loading a Specific Template

```csharp
var result = await templateLoader.LoadTemplateAsync(
    "daily-summary",
    cancellationToken);

if (result.IsSuccess)
{
    var template = result.Value;
    // Use template.Content for prompt generation
}
```

### Loading All Templates for Selection

```csharp
var result = await templateLoader.LoadAllTemplatesAsync(
    TemplateType.Daily,
    cancellationToken);

if (result.IsSuccess && result.Value.Count > 0)
{
    var templates = result.Value
        .Select(t => new TemplateListItem { ... })
        .ToList();

    var selectedId = await selectionUI.SelectTemplateAsync(
        templates,
        "today",
        cancellationToken);
}
```

### Installing Default Templates

```csharp
var command = new InstallDefaultTemplatesCommand(
    TargetDirectory: Path.Combine(memoryDir, "templates"),
    OverwriteExisting: false);

var result = await mediator.Send(command, cancellationToken);

if (result.IsSuccess)
{
    Console.WriteLine($"Installed {result.Value.TemplatesInstalled} templates");
}
```

### Querying Available Templates

```csharp
var query = new ListTemplatesQuery(
    TemplateType.Weekly,
    IncludeInvalid: false);

var result = await mediator.Send(query, cancellationToken);

if (result.IsSuccess)
{
    Console.WriteLine($"Found {result.Value.Templates.Count} valid templates");
    Console.WriteLine($"Skipped {result.Value.InvalidCount} invalid templates");
}
```

## Design Principles

### CQRS Pattern

- **Commands** modify state: InstallDefaultTemplatesCommand creates/copies files
- **Queries** read state: ListTemplatesQuery retrieves available templates
- Clear separation of concerns

### Result<T> Pattern

All operations return `Result<T>` for consistent error handling:
- Success: `IsSuccess = true`, value in `Value` property
- Failure: `IsSuccess = false`, error message in `Error` property
- No exceptions for expected failures (e.g., template not found)

### Async All the Way

All I/O operations are async:
- File reading/writing
- Template loading
- Directory operations
- Follows .NET async best practices

### Immutability

All models are `sealed record` types:
- Immutable after construction
- Value equality semantics
- Thread-safe by default

### Nullable Reference Types

Contracts use nullable reference types for clarity:
- `string?` for optional fields
- `string` for required fields
- Compile-time null safety

## Testing Considerations

### Mockability

All interfaces can be easily mocked for testing:
```csharp
var mockLoader = new Mock<IPromptTemplateLoader>();
mockLoader
    .Setup(l => l.LoadTemplateAsync("daily-summary", default))
    .ReturnsAsync(Result<PromptTemplate>.Success(template));
```

### Contract Tests

Each contract should have:
- Positive test cases (happy path)
- Negative test cases (error conditions)
- Edge cases (empty lists, null values)
- Integration tests (file system operations)

## Backward Compatibility

These contracts are designed to be backward compatible:
- New methods added to existing interfaces (not breaking)
- Optional parameters with defaults
- New models don't break existing code
- Existing embedded templates continue to work

## Future Extensibility

Reserved fields for future features:
- `TemplateMetadata.Tags` - for categorization
- `TemplateMetadata.CreatedDate` - for sorting/filtering
- Additional TemplateType enum values can be added

---

**Note**: These contracts are design documents. Actual implementation may vary slightly based on technical constraints discovered during implementation. Any deviations should be documented in the implementation plan.
