# Interactive Text Editor - Usage Examples

**Location**: `src/Shared/TextEditing/`  
**Purpose**: Reusable interactive multi-line text editor for console applications  
**Platforms**: macOS, Windows (Linux future)

---

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Creating New Content](#creating-new-content)
- [Editing Existing Content](#editing-existing-content)
- [Configuration Options](#configuration-options)
- [Error Handling](#error-handling)
- [Future Integration Examples](#future-integration-examples)

---

## Overview

The Interactive Text Editor provides a full-featured text editing experience in the terminal with:

- ✨ Multi-line editing with cursor navigation (arrows, Home, End)
- 📋 Clipboard paste support (Ctrl+V)
- 🎨 Unicode and emoji support
- 🔒 ANSI escape sequence sanitization
- 💾 Save/cancel workflows with preview
- 📊 Session metadata (duration, modification tracking)

**Two implementations**:
1. **TerminalGuiTextEditor**: Full-featured interactive editor using Terminal.Gui (primary)
2. **StreamBasedTextEditor**: Line-by-line fallback for non-interactive terminals
3. **FallbackTextEditor**: Automatically chooses the best editor for the environment

---

## Quick Start

### Basic Setup (Dependency Injection)

```csharp
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Shared.TextEditing.Services;

// In your service registration (Program.cs or similar)
services.AddSingleton<InputSanitizer>();
services.AddSingleton<IInteractiveTextEditor, FallbackTextEditor>();
```

The `FallbackTextEditor` automatically selects the best implementation:
- Uses `TerminalGuiTextEditor` for interactive terminals
- Falls back to `StreamBasedTextEditor` for piped/redirected input

### Basic Usage

```csharp
using TenSecondTom.Shared.TextEditing.Services;
using TenSecondTom.Shared.TextEditing.Models;

public class MyFeatureHandler
{
    private readonly IInteractiveTextEditor _editor;

    public MyFeatureHandler(IInteractiveTextEditor editor)
    {
        _editor = editor;
    }

    public async Task<Result> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await _editor.EditAsync(
            initialContent: null,
            configuration: null,
            cancellationToken: cancellationToken
        );

        if (result.IsSaved)
        {
            // Process the saved content
            await SaveToDatabase(result.Content);
            return Result.Success();
        }

        if (result.IsCancelled)
        {
            Console.WriteLine("Editing cancelled by user");
            return Result.Failure("User cancelled");
        }

        if (result.IsError)
        {
            Console.WriteLine($"Error: {result.ErrorMessage}");
            return Result.Failure(result.ErrorMessage);
        }

        return Result.Failure("Unknown outcome");
    }
}
```

---

## Creating New Content

**Use case**: User creates a brand new entry (e.g., `/today` command)

```csharp
public async Task<Result<DailyEntry>> CreateDailyEntryAsync(CancellationToken cancellationToken)
{
    // Configure editor for new entry
    var config = new EditorConfiguration
    {
        Title = "What did you accomplish today?",
        ShowHints = true,
        SanitizeInput = true,
        MaxContentLength = 10_000,
        PreviewLineLimit = 10
    };

    // Launch editor with no initial content
    var result = await _editor.EditAsync(
        initialContent: null,  // 👈 No initial content = new entry
        configuration: config,
        cancellationToken: cancellationToken
    );

    if (!result.IsSaved)
    {
        return Result<DailyEntry>.Failure("Entry not saved");
    }

    // Create entry with saved content
    var entry = new DailyEntry
    {
        Id = Guid.NewGuid(),
        Date = DateOnly.FromDateTime(DateTime.UtcNow),
        Content = result.Content,
        CreatedAt = DateTime.UtcNow,
        ModifiedAt = DateTime.UtcNow
    };

    await _repository.SaveAsync(entry);

    // Log metadata for diagnostics
    _logger.LogInformation(
        "Created daily entry {EntryId} in {Duration}ms with {CharCount} characters",
        entry.Id,
        result.Metadata.Duration.TotalMilliseconds,
        result.Metadata.CharacterCount
    );

    return Result<DailyEntry>.Success(entry);
}
```

---

## Editing Existing Content

**Use case**: User edits a previous entry (e.g., from `/search` results)

```csharp
public async Task<Result<DailyEntry>> EditExistingEntryAsync(
    Guid entryId,
    CancellationToken cancellationToken)
{
    // 1. Load existing entry
    var entry = await _repository.GetByIdAsync(entryId);
    if (entry == null)
    {
        return Result<DailyEntry>.Failure("Entry not found");
    }

    // 2. Configure editor for editing mode
    var config = new EditorConfiguration
    {
        Title = $"Edit entry from {entry.Date:yyyy-MM-dd}",
        ShowHints = true,
        SanitizeInput = true
    };

    // 3. Launch editor with existing content pre-filled
    var result = await _editor.EditAsync(
        initialContent: entry.Content,  // 👈 Pre-fill with existing content
        configuration: config,
        cancellationToken: cancellationToken
    );

    if (result.IsCancelled)
    {
        Console.WriteLine("Edit cancelled - no changes made");
        return Result<DailyEntry>.Success(entry); // Return original
    }

    if (result.IsError)
    {
        return Result<DailyEntry>.Failure(result.ErrorMessage);
    }

    // 4. Check if content was actually modified
    if (!result.Metadata.WasModified)  // 👈 Detect if user made changes
    {
        Console.WriteLine("No changes made");
        return Result<DailyEntry>.Success(entry); // Return original
    }

    // 5. Update entry with modified content
    entry.Content = result.Content;
    entry.ModifiedAt = DateTime.UtcNow;
    await _repository.UpdateAsync(entry);

    _logger.LogInformation(
        "Updated entry {EntryId}, modified in {Duration}ms",
        entry.Id,
        result.Metadata.Duration.TotalMilliseconds
    );

    return Result<DailyEntry>.Success(entry);
}
```

---

## Configuration Options

### Default Configuration

```csharp
// Uses sensible defaults
var result = await _editor.EditAsync();
```

**Default values**:
- `MaxContentLength`: 50,000 characters
- `MaxLineCount`: 1,000 lines
- `ShowHints`: true
- `PreviewLineLimit`: 10 lines
- `SanitizeInput`: true
- `Title`: null (no custom title)

### Custom Configuration

```csharp
var config = new EditorConfiguration
{
    // Customize title/prompt shown to user
    Title = "Enter your journal entry for today:",

    // Show keyboard shortcuts at bottom of editor
    ShowHints = true,

    // Strip ANSI escape sequences from input (recommended)
    SanitizeInput = true,

    // Limit content size
    MaxContentLength = 5000,  // 5K characters max
    MaxLineCount = 100,       // 100 lines max

    // Preview settings (Ctrl+D confirmation)
    PreviewLineLimit = 5      // Show first 5 lines in preview (0 = all)
};

var result = await _editor.EditAsync(
    initialContent: null,
    configuration: config,
    cancellationToken: cancellationToken
);
```

### Configuration for Different Scenarios

```csharp
// Quick notes (smaller limits)
var quickNoteConfig = new EditorConfiguration
{
    Title = "Quick note:",
    MaxContentLength = 500,
    MaxLineCount = 10,
    PreviewLineLimit = 5
};

// Long-form content (larger limits)
var articleConfig = new EditorConfiguration
{
    Title = "Write your article:",
    MaxContentLength = 50_000,
    MaxLineCount = 1_000,
    PreviewLineLimit = 20
};

// Code snippets (no sanitization)
var codeConfig = new EditorConfiguration
{
    Title = "Paste code snippet:",
    SanitizeInput = false,  // Preserve code formatting
    ShowHints = true
};
```

---

## Error Handling

### Comprehensive Error Handling Pattern

```csharp
public async Task<Result> HandleEditingWorkflowAsync()
{
    try
    {
        var result = await _editor.EditAsync(cancellationToken: cancellationToken);

        return result.Outcome switch
        {
            EditorOutcome.Saved => await ProcessSavedContent(result.Content),
            EditorOutcome.Cancelled => Result.Failure("User cancelled editing"),
            EditorOutcome.TimedOut => Result.Failure("Editing session timed out"),
            EditorOutcome.Error => Result.Failure($"Editor error: {result.ErrorMessage}"),
            _ => Result.Failure("Unknown editor outcome")
        };
    }
    catch (OperationCanceledException)
    {
        _logger.LogInformation("Editing cancelled via cancellation token");
        return Result.Failure("Operation cancelled");
    }
    catch (EditorException ex)
    {
        _logger.LogError(ex, "Editor initialization failed");
        return Result.Failure("Could not start editor");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during editing");
        return Result.Failure("Unexpected error");
    }
}
```

### Metadata for Diagnostics

```csharp
var result = await _editor.EditAsync();

if (result.IsSaved)
{
    var metadata = result.Metadata;

    _logger.LogInformation(
        "Edit session completed: " +
        "SessionId={SessionId}, " +
        "Duration={Duration}ms, " +
        "WasModified={WasModified}, " +
        "Lines={Lines}, " +
        "Chars={Chars}",
        metadata.SessionId,
        metadata.Duration.TotalMilliseconds,
        metadata.WasModified,
        metadata.LineCount,
        metadata.CharacterCount
    );

    // Use metadata for business logic
    if (metadata.WasModified && metadata.CharacterCount > 0)
    {
        await IndexForSearch(result.Content);
    }
}
```

---

## Future Integration Examples

### Example: `/search` Command Integration

```csharp
public class SearchCommand : ICommandHandler
{
    private readonly ISearchService _searchService;
    private readonly IInteractiveTextEditor _editor;
    private readonly IEntryRepository _repository;

    public async Task<Result> ExecuteAsync(string query)
    {
        // 1. Search for entries
        var results = await _searchService.SearchAsync(query);
        
        if (!results.Any())
        {
            Console.WriteLine("No entries found");
            return Result.Failure("No results");
        }

        // 2. Display results and let user select
        DisplaySearchResults(results);
        Console.Write("Select entry to edit (1-N, or 'q' to quit): ");
        var selection = Console.ReadLine();

        if (selection == "q") return Result.Success();

        if (!int.TryParse(selection, out var index) || 
            index < 1 || 
            index > results.Count)
        {
            return Result.Failure("Invalid selection");
        }

        var selectedEntry = results[index - 1];

        // 3. Load and edit the selected entry
        var config = new EditorConfiguration
        {
            Title = $"Editing entry from {selectedEntry.Date:yyyy-MM-dd}",
            ShowHints = true
        };

        var editResult = await _editor.EditAsync(
            initialContent: selectedEntry.Content,  // 👈 Pre-fill with existing
            configuration: config,
            cancellationToken: CancellationToken.None
        );

        // 4. Handle result
        if (editResult.IsCancelled)
        {
            Console.WriteLine("Edit cancelled");
            return Result.Success();
        }

        if (!editResult.Metadata.WasModified)
        {
            Console.WriteLine("No changes made");
            return Result.Success();
        }

        // 5. Save changes
        selectedEntry.Content = editResult.Content;
        selectedEntry.ModifiedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(selectedEntry);

        Console.WriteLine($"✓ Entry updated ({editResult.Metadata.CharacterCount} characters)");
        return Result.Success();
    }
}
```

### Example: Batch Edit Workflow

```csharp
public async Task<Result> BatchEditEntriesAsync(IEnumerable<Guid> entryIds)
{
    var updatedCount = 0;

    foreach (var entryId in entryIds)
    {
        var entry = await _repository.GetByIdAsync(entryId);
        if (entry == null) continue;

        Console.WriteLine($"\n--- Editing entry {updatedCount + 1} ---");
        Console.WriteLine($"Date: {entry.Date:yyyy-MM-dd}");
        Console.WriteLine($"Preview: {entry.Content.Substring(0, Math.Min(50, entry.Content.Length))}...");
        
        var result = await _editor.EditAsync(
            initialContent: entry.Content,
            configuration: new EditorConfiguration { Title = $"Edit entry {updatedCount + 1}" },
            cancellationToken: CancellationToken.None
        );

        if (result.IsCancelled)
        {
            Console.WriteLine("Batch edit stopped by user");
            break;
        }

        if (result.IsSaved && result.Metadata.WasModified)
        {
            entry.Content = result.Content;
            await _repository.UpdateAsync(entry);
            updatedCount++;
        }
    }

    Console.WriteLine($"\n✓ Updated {updatedCount} entries");
    return Result.Success();
}
```

---

## Best Practices

### ✅ DO

- **Use FallbackTextEditor** for automatic environment detection
- **Configure appropriate limits** for your use case
- **Check `WasModified`** to avoid unnecessary database writes
- **Log metadata** for performance monitoring and diagnostics
- **Handle all three outcomes**: Saved, Cancelled, Error
- **Use cancellation tokens** for long-running operations
- **Sanitize input** unless you specifically need to preserve formatting

### ❌ DON'T

- Don't assume the editor will always return `Saved`
- Don't ignore `IsCancelled` - users expect cancel to work
- Don't set unrealistic limits (too small = frustrating)
- Don't forget to handle `OperationCanceledException`
- Don't skip validation even though editor sanitizes input
- Don't use Terminal.Gui in CI/CD environments (use FallbackTextEditor)

---

## Keyboard Shortcuts

### Terminal.Gui Editor (Primary)

- **Ctrl+D**: Save and continue
- **Ctrl+C**: Cancel editing (discard changes)
- **Arrows**: Navigate cursor
- **Home**: Jump to start of line
- **End**: Jump to end of line
- **Ctrl+V**: Paste from clipboard
- **Enter**: New line
- **Backspace/Delete**: Edit text

### Stream-Based Editor (Fallback)

- **Ctrl+D** (EOF): Finish editing and show preview
- **Enter**: New line
- At save prompt: `Y` (save), `N` (cancel)

---

## Architecture Notes

### Reusability Design

The editor is designed for maximum reusability:

1. **Interface-based**: Depend on `IInteractiveTextEditor`, not concrete types
2. **Shared location**: Located in `src/Shared/TextEditing/` for cross-feature use
3. **No feature coupling**: No dependencies on specific features (Today, Search, etc.)
4. **Configurable**: Customize behavior per use case via `EditorConfiguration`
5. **Testable**: Can mock `IInteractiveTextEditor` for unit tests

### Testing Your Integration

```csharp
public class MyFeatureHandlerTests
{
    [Fact]
    public async Task Execute_WhenUserSaves_ProcessesContent()
    {
        // Arrange: Mock editor to return saved content
        var mockEditor = new Mock<IInteractiveTextEditor>();
        var expectedContent = "User entered this content";
        
        mockEditor
            .Setup(e => e.EditAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(EditorResult.Saved(
                expectedContent,
                EditorMetadata.Empty
            ));

        var handler = new MyFeatureHandler(mockEditor.Object);

        // Act
        var result = await handler.ExecuteAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        mockEditor.Verify(e => e.EditAsync(
            null,
            null,
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }
}
```

---

## See Also

- **Interface**: `src/Shared/TextEditing/Services/IInteractiveTextEditor.cs`
- **Models**: `src/Shared/TextEditing/Models/` (EditorResult, EditorConfiguration, etc.)
- **Manual Tests**: `tests/.../Integration/Shared/TextEditing/MANUAL_TESTS.md`
- **Integration Example**: `src/Features/Today/Handlers/CreateDailyEntryHandler.cs`

---

**Version**: 1.0.0  
**Last Updated**: 2025-10-14  
**Status**: Production Ready ✅

