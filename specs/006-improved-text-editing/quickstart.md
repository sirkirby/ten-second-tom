# Quickstart Guide: Interactive Text Editing Implementation

**Feature**: 006-improved-text-editing
**For**: Developers implementing this feature
**Date**: 2025-10-14

## Overview

This guide helps you quickly understand and implement the interactive text editing feature. Follow this guide after reviewing the specification, research findings, and data model.

---

## Prerequisites

Before starting implementation:

1. ✅ Read [spec.md](./spec.md) - Understand requirements and user stories
2. ✅ Read [research.md](./research.md) - Understand Terminal.Gui decision rationale
3. ✅ Read [data-model.md](./data-model.md) - Understand entities and value objects
4. ✅ Read [contracts/IInteractiveTextEditor.md](./contracts/IInteractiveTextEditor.md) - Understand service contract
5. ✅ Ensure .NET 9 SDK installed (`dotnet --version`)
6. ✅ Project builds successfully (`dotnet build`)
7. ✅ Existing tests pass (`dotnet test`)

---

## Implementation Status ✅

**Status**: **COMPLETED** (2025-10-14)

- ✅ Terminal.Gui v1.x successfully integrated (stable release)
- ✅ All 958 tests passing (833 unit + 125 integration)
- ✅ Zero build warnings or errors
- ✅ Unicode/emoji support confirmed working
- ✅ StreamBasedTextEditor fallback for piped input
- ✅ Manual testing confirmed on macOS Terminal.app and Warp

**Key Implementation Notes**:
- Used Terminal.Gui v1 API (v2 alpha was unstable on macOS)
- Simplified UX: Ctrl+D saves directly (no confirmation dialog)
- Fallback editor auto-saves after EOF for piped scenarios
- Manual testing checklist created for interactive TUI testing
- Integration tests cover both editor implementations

**Architecture Decision**: Removed confirmation dialog - Ctrl+D now saves immediately for better UX and to avoid Terminal.Gui v1 nested dialog complexity.

---

## Architecture Quick Reference

### Location Decision

Based on the research, the editor is **shared infrastructure**, so place it in:

```
src/Shared/TextEditing/
```

**Rationale**: The editor is reusable across multiple features (Today, future Search edit), making it cross-cutting infrastructure rather than feature-specific code.

### Folder Structure

```
src/
└── Shared/
    └── TextEditing/
        ├── Models/
        │   ├── TextEditingSession.cs
        │   ├── EditorResult.cs
        │   ├── EditorOutcome.cs
        │   ├── EditorMetadata.cs
        │   ├── EditorConfiguration.cs
        │   └── SanitizedText.cs
        ├── Services/
        │   ├── IInteractiveTextEditor.cs         (interface)
        │   ├── TerminalGuiTextEditor.cs          (primary implementation)
        │   ├── StreamBasedTextEditor.cs          (fallback)
        │   └── InputSanitizer.cs                 (helper service)
        ├── Exceptions/
        │   └── EditorException.cs
        └── Validation/
            └── EditorContentValidator.cs          (FluentValidation)

tests/
├── TenSecondTom.Tests/Unit/Shared/TextEditing/
│   ├── Models/
│   │   ├── TextEditingSessionTests.cs
│   │   ├── EditorResultTests.cs
│   │   └── EditorMetadataTests.cs
│   └── Services/
│       ├── TerminalGuiTextEditorTests.cs
│       ├── StreamBasedTextEditorTests.cs
│       └── InputSanitizerTests.cs
└── TenSecondTom.IntegrationTests/Integration/
    ├── Features/Today/
    │   └── TodayCommandWithEditorTests.cs
    └── Shared/TextEditing/
        └── EditorWorkflowTests.cs
```

---

## Implementation Sequence (TDD)

### Phase 1: Models & Value Objects (Day 1)

#### Step 1.1: Create EditorOutcome Enum
**File**: `src/Shared/TextEditing/Models/EditorOutcome.cs`
**Test**: `tests/.../EditorOutcomeTests.cs` (if needed)

```csharp
namespace TenSecondTom.Shared.TextEditing.Models;

public enum EditorOutcome
{
    Saved,
    Cancelled,
    TimedOut,
    Error
}
```

#### Step 1.2: Create EditorResult (TDD)
**Test First**: `tests/.../EditorResultTests.cs`

```csharp
public class EditorResultTests
{
    [Fact]
    public void Saved_WithContent_CreatesSuccessResult()
    {
        // Arrange
        var content = "Test content";
        var metadata = EditorMetadata.Empty;

        // Act
        var result = EditorResult.Saved(content, metadata);

        // Assert
        result.IsSaved.Should().BeTrue();
        result.Content.Should().Be(content);
        result.Outcome.Should().Be(EditorOutcome.Saved);
    }

    [Fact]
    public void Cancelled_CreatesResultWithEmptyContent()
    {
        // Act
        var result = EditorResult.Cancelled(EditorMetadata.Empty);

        // Assert
        result.IsCancelled.Should().BeTrue();
        result.Content.Should().BeEmpty();
    }

    // ... more tests
}
```

**Implementation**: `src/Shared/TextEditing/Models/EditorResult.cs`
- Copy from data-model.md
- Run tests → Green

#### Step 1.3: Create Remaining Models
Repeat TDD process for:
- `EditorMetadata.cs`
- `EditorConfiguration.cs`
- `TextEditingSession.cs`
- `SanitizedText.cs`

---

### Terminal.Gui v1 API Reference

**IMPORTANT**: The project uses Terminal.Gui v1.x (stable), NOT v2 alpha. Key v1 API patterns:

#### Keyboard Event Handling (v1)

```csharp
// ✅ v1 API - Use KeyPress with bitwise operations
_textView.KeyPress += (e) =>
{
    // Ctrl+D: Bitwise check for control modifier
    if (e.KeyEvent.Key == (Key.CtrlMask | Key.D))
    {
        _shouldSave = true;
        e.Handled = true;
        return;
    }
    
    // Ctrl+C: Cancel
    if (e.KeyEvent.Key == (Key.CtrlMask | Key.C))
    {
        _shouldCancel = true;
        e.Handled = true;
        return;
    }
};
```

#### Layout API (v1)

```csharp
// ✅ v1 API - No null-forgiving operators needed
_textView = new TextView
{
    X = 0,
    Y = 1,
    Width = Dim.Fill(),
    Height = Dim.Fill() - 2,  // Works directly
    WordWrap = true,
    AllowsTab = true
};

// ColorScheme available in v1
var titleLabel = new Label
{
    Text = "Title",
    ColorScheme = new ColorScheme
    {
        Normal = Application.Driver.MakeAttribute(Color.BrightCyan, Color.Black)
    }
};
```

#### Application Lifecycle (v1)

```csharp
// ✅ v1 API - Application.Init creates Application.Top automatically
Application.Init();
var top = Application.Top;  // Already created

// Add views to top
top.Add(titleLabel);
top.Add(_textView);

// Application.Run() is synchronous/blocking - don't wrap in Task.Run()
Application.Run();

// Clean shutdown
Application.Shutdown();
```

#### Polling Pattern for Exit Conditions

```csharp
// ✅ Use MainLoop.AddTimeout to poll flags and stop editor
Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), (loop) =>
{
    if (_shouldSave || _shouldCancel)
    {
        Application.RequestStop();
        return false; // Remove timeout
    }
    return true; // Keep checking
});
```

#### Dialog Pattern (v1)

```csharp
// ✅ Use Button controls with hotkeys for reliable input
var dialog = new Dialog("Confirm Action", 80, 20);

var saveButton = new Button("_Save (S)")  // Underscore creates hotkey
{
    X = Pos.Center() - 20,
    Y = Pos.Bottom(previewLabel),
    IsDefault = true
};
saveButton.Clicked += () =>
{
    _shouldSave = true;
    Application.RequestStop();
};

dialog.AddButton(saveButton);
Application.Run(dialog);  // Modal dialog
```

**v1 Documentation**: https://gui-cs.github.io/Terminal.Gui/

---

### Phase 2: Service Interface & Validation (Day 1-2)

#### Step 2.1: Create IInteractiveTextEditor Interface
**File**: `src/Shared/TextEditing/Services/IInteractiveTextEditor.cs`

```csharp
namespace TenSecondTom.Shared.TextEditing.Services;

public interface IInteractiveTextEditor
{
    Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default);
}
```

#### Step 2.2: Create EditorException
**File**: `src/Shared/TextEditing/Exceptions/EditorException.cs`

```csharp
namespace TenSecondTom.Shared.TextEditing.Exceptions;

public class EditorException : Exception
{
    public EditorException(string message) : base(message) { }
    public EditorException(string message, Exception inner) : base(message, inner) { }
}
```

#### Step 2.3: Create InputSanitizer (TDD)
**Purpose**: Strip ANSI escape sequences per FR-013

**Test First**: `tests/.../InputSanitizerTests.cs`

```csharp
public class InputSanitizerTests
{
    [Fact]
    public void Sanitize_WithAnsiSequences_RemovesThem()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var input = "Hello \u001b[31mWorld\u001b[0m"; // Red color codes

        // Act
        var result = sanitizer.Sanitize(input);

        // Assert
        result.Content.Should().Be("Hello World");
        result.WasSanitized.Should().BeTrue();
    }

    [Fact]
    public void Sanitize_WithEmojiAndUnicode_PreservesThem()
    {
        // Arrange
        var sanitizer = new InputSanitizer();
        var input = "Hello 👋 café";

        // Act
        var result = sanitizer.Sanitize(input);

        // Assert
        result.Content.Should().Be("Hello 👋 café");
        result.WasSanitized.Should().BeFalse();
    }
}
```

**Implementation**: Use regex to strip ANSI codes:

```csharp
public class InputSanitizer
{
    private static readonly Regex AnsiRegex = new(@"\u001b\[[0-9;]*m", RegexOptions.Compiled);

    public SanitizedText Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return new SanitizedText { Content = input ?? string.Empty };

        var originalLength = input.Length;
        var sanitized = AnsiRegex.Replace(input, string.Empty);

        return new SanitizedText
        {
            Content = sanitized,
            WasSanitized = sanitized.Length != originalLength,
            OriginalLength = originalLength
        };
    }
}
```

---

### Phase 3: StreamBasedTextEditor (Fallback) (Day 2)

**Why Start Here?** Simpler than Terminal.Gui, validates interface contract.

#### Step 3.1: Write Tests
**File**: `tests/.../StreamBasedTextEditorTests.cs`

```csharp
public class StreamBasedTextEditorTests
{
    [Fact]
    public async Task EditAsync_WithInitialContent_ReturnsItIfNoInput()
    {
        // This test is tricky - requires mocking Console.ReadLine
        // Consider integration test instead
    }

    // Focus on unit-testable logic:
    // - Content parsing
    // - EditorResult creation
    // - Metadata calculation
}
```

#### Step 3.2: Implement StreamBasedTextEditor
**File**: `src/Shared/TextEditing/Services/StreamBasedTextEditor.cs`

```csharp
public sealed class StreamBasedTextEditor : IInteractiveTextEditor
{
    private readonly ILogger<StreamBasedTextEditor> _logger;
    private readonly InputSanitizer _sanitizer;

    public StreamBasedTextEditor(
        ILogger<StreamBasedTextEditor> logger,
        InputSanitizer sanitizer)
    {
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var config = configuration ?? EditorConfiguration.Default;
        var session = new TextEditingSession(initialContent);

        _logger.LogInformation(
            "Starting stream-based editing session {SessionId}",
            session.SessionId
        );

        try
        {
            // Display hint
            Console.WriteLine("Enter text (Ctrl+D on blank line to finish):");
            if (!string.IsNullOrEmpty(initialContent))
            {
                Console.WriteLine(initialContent);
            }

            var lines = new List<string>();
            string? line;

            while ((line = Console.ReadLine()) != null)
            {
                lines.Add(line);
            }

            var content = string.Join('\n', lines);

            // Sanitize if configured
            if (config.SanitizeInput)
            {
                var sanitized = _sanitizer.Sanitize(content);
                content = sanitized.Content;
            }

            session.UpdateContent(content);

            // Simple confirmation
            Console.WriteLine($"\nSave this entry? (y/n)");
            var response = Console.ReadKey(intercept: true);

            if (response.Key == ConsoleKey.Y)
            {
                session.Complete(EditorOutcome.Saved);
                var metadata = EditorMetadata.FromSession(session);
                return EditorResult.Saved(content, metadata);
            }
            else
            {
                session.Complete(EditorOutcome.Cancelled);
                return EditorResult.Cancelled(EditorMetadata.FromSession(session));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in stream-based editor");
            session.Complete(EditorOutcome.Error);
            return EditorResult.Error(ex.Message, EditorMetadata.FromSession(session));
        }
    }
}
```

---

### Phase 4: Terminal.Gui Integration (Day 3-4)

#### Step 4.1: Add NuGet Package

```bash
cd src
dotnet add package Terminal.Gui --version 2.0.0-alpha.*
```

#### Step 4.2: Create TerminalGuiTextEditor (TDD)
**Test First**: `tests/.../TerminalGuiTextEditorTests.cs`

**Challenge**: Terminal.Gui requires `Application.Init()` which is hard to unit test.

**Solution**: Focus on integration tests for Terminal.Gui implementation.

#### Step 4.3: Implement TerminalGuiTextEditor
**File**: `src/Shared/TextEditing/Services/TerminalGuiTextEditor.cs`

```csharp
using Terminal.Gui;

public sealed class TerminalGuiTextEditor : IInteractiveTextEditor
{
    private readonly ILogger<TerminalGuiTextEditor> _logger;
    private readonly InputSanitizer _sanitizer;

    public TerminalGuiTextEditor(
        ILogger<TerminalGuiTextEditor> logger,
        InputSanitizer sanitizer)
    {
        _logger = logger;
        _sanitizer = sanitizer;
    }

    public async Task<EditorResult> EditAsync(
        string? initialContent = null,
        EditorConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        var config = configuration ?? EditorConfiguration.Default;
        var session = new TextEditingSession(initialContent);

        _logger.LogInformation(
            "Starting Terminal.Gui editing session {SessionId}",
            session.SessionId
        );

        try
        {
            // Initialize Terminal.Gui
            Application.Init();

            var top = new Toplevel();

            // Create TextView
            var textView = new TextView
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill() - 2,
                Text = initialContent ?? string.Empty
            };

            // Hint label
            var hintLabel = new Label
            {
                X = 0,
                Y = 0,
                Text = "Ctrl+D to finish | Ctrl+C to cancel | Use arrows to navigate"
            };

            // Status bar
            var statusBar = new Label
            {
                X = 0,
                Y = Pos.Bottom(top) - 1,
                Width = Dim.Fill(),
                Text = "Editing..."
            };

            top.Add(hintLabel, textView, statusBar);

            var outcome = EditorOutcome.Cancelled;
            var finalContent = string.Empty;

            // Handle Ctrl+D (finish)
            top.KeyPress += (e) =>
            {
                if (e.KeyEvent.Key == Key.CtrlMask | Key.D)
                {
                    // Show preview and confirmation
                    var content = textView.Text.ToString();
                    var confirmed = ShowConfirmation(content, config);

                    if (confirmed == ConfirmationResult.Save)
                    {
                        finalContent = content;
                        outcome = EditorOutcome.Saved;
                        Application.RequestStop();
                    }
                    else if (confirmed == ConfirmationResult.Cancel)
                    {
                        outcome = EditorOutcome.Cancelled;
                        Application.RequestStop();
                    }
                    // EditMore: continue editing

                    e.Handled = true;
                }
                else if (e.KeyEvent.Key == Key.CtrlMask | Key.C)
                {
                    outcome = EditorOutcome.Cancelled;
                    Application.RequestStop();
                    e.Handled = true;
                }
            };

            Application.Run(top);
            Application.Shutdown();

            // Process result
            if (config.SanitizeInput && !string.IsNullOrEmpty(finalContent))
            {
                var sanitized = _sanitizer.Sanitize(finalContent);
                finalContent = sanitized.Content;
            }

            session.UpdateContent(finalContent);
            session.Complete(outcome);

            var metadata = EditorMetadata.FromSession(session);

            return outcome == EditorOutcome.Saved
                ? EditorResult.Saved(finalContent, metadata)
                : EditorResult.Cancelled(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Terminal.Gui editor");
            session.Complete(EditorOutcome.Error);

            // Ensure cleanup
            try { Application.Shutdown(); } catch { }

            return EditorResult.Error(ex.Message, EditorMetadata.FromSession(session));
        }
    }

    private ConfirmationResult ShowConfirmation(string content, EditorConfiguration config)
    {
        // Show preview dialog with Save/Edit/Cancel options
        // Implementation details based on spec requirements
        // (first 10 lines if >10, otherwise full content)

        // Placeholder:
        var dialog = new Dialog("Save Entry?");
        // ... add preview, buttons, etc.
        Application.Run(dialog);

        // Return user choice
        return ConfirmationResult.Save; // Replace with actual logic
    }

    private enum ConfirmationResult { Save, EditMore, Cancel }
}
```

**Note**: The above is a skeleton - full implementation requires:
- Preview rendering logic
- Button handlers for S/E/C keys
- Error handling for terminal not supporting TUI

---

### Phase 5: Integration with Today Feature (Day 4-5)

#### Step 5.1: Update CreateDailyEntryHandler (TDD)
**Test First**: `tests/.../CreateDailyEntryHandlerTests.cs`

```csharp
public class CreateDailyEntryHandlerTests
{
    [Fact]
    public async Task Handle_WithEditorSaved_CreatesEntry()
    {
        // Arrange
        var editor = new Mock<IInteractiveTextEditor>();
        editor.Setup(e => e.EditAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(EditorResult.Saved("Test content", EditorMetadata.Empty));

        var handler = new CreateDailyEntryHandler(
            editor.Object,
            /* other dependencies */
        );

        // Act
        var result = await handler.Handle(new CreateDailyEntryCommand(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Test content");
    }

    [Fact]
    public async Task Handle_WithEditorCancelled_ReturnsFailure()
    {
        // Arrange
        var editor = new Mock<IInteractiveTextEditor>();
        editor.Setup(e => e.EditAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(EditorResult.Cancelled(EditorMetadata.Empty));

        var handler = new CreateDailyEntryHandler(editor.Object, /* ... */);

        // Act
        var result = await handler.Handle(new CreateDailyEntryCommand(), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }
}
```

#### Step 5.2: Modify CreateDailyEntryHandler
**File**: `src/Features/Today/Handlers/CreateDailyEntryHandler.cs`

```csharp
public sealed class CreateDailyEntryHandler
{
    private readonly IInteractiveTextEditor _editor;
    // ... other dependencies

    public CreateDailyEntryHandler(
        IInteractiveTextEditor editor,
        IStorageProvider storage,
        ILogger<CreateDailyEntryHandler> logger)
    {
        _editor = editor;
        // ...
    }

    public async Task<Result<DailyEntry>> Handle(
        CreateDailyEntryCommand command,
        CancellationToken cancellationToken)
    {
        // Launch editor
        var editorResult = await _editor.EditAsync(
            initialContent: "What did you accomplish today?\n\n",
            configuration: EditorConfiguration.Default,
            cancellationToken
        );

        if (editorResult.IsCancelled)
        {
            return Result<DailyEntry>.Failure("Entry creation cancelled by user");
        }

        if (editorResult.IsError)
        {
            return Result<DailyEntry>.Failure($"Editor error: {editorResult.ErrorMessage}");
        }

        // Create entry with edited content
        var entry = new DailyEntry
        {
            Date = DateTime.UtcNow.Date,
            Content = editorResult.Content
        };

        // Save to storage
        await _storage.SaveEntryAsync(entry, cancellationToken);

        return Result<DailyEntry>.Success(entry);
    }
}
```

---

### Phase 6: Dependency Injection (Day 5)

#### Update Program.cs or ServiceCollectionExtensions.cs

```csharp
// Program.cs
builder.Services.AddTransient<InputSanitizer>();

builder.Services.AddTransient<IInteractiveTextEditor>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<TerminalGuiTextEditor>>();
    var sanitizer = sp.GetRequiredService<InputSanitizer>();

    // Detect if interactive terminal available
    if (Console.IsInputRedirected)
    {
        var streamLogger = sp.GetRequiredService<ILogger<StreamBasedTextEditor>>();
        return new StreamBasedTextEditor(streamLogger, sanitizer);
    }

    return new TerminalGuiTextEditor(logger, sanitizer);
});
```

---

### Phase 7: Integration & Manual Testing (Day 6)

1. **Run the app**: `dotnet run --project src/TenSecondTom.csproj`
2. **Test /today command**: `tom /today`
3. **Verify**:
   - Editor launches
   - Can type and navigate with arrows
   - Ctrl+D shows preview
   - Save/Edit/Cancel work correctly
   - Content persists correctly

4. **Test on Windows Terminal** (if on macOS, coordinate with team member)

5. **Test emoji and Unicode**:
   - Type emoji: 👋 ✅ 🎉
   - Type accented characters: café, naïve
   - Verify preserved in saved entry

---

## Common Pitfalls & Solutions

### Pitfall 1: Terminal.Gui Crashes on Init
**Symptom**: `Application.Init()` throws exception
**Solution**: Ensure terminal supports TUI. Add try/catch and fall back to StreamBasedTextEditor.

### Pitfall 2: Tests Hang When Running Terminal.Gui
**Symptom**: Integration tests hang indefinitely
**Solution**: Skip Terminal.Gui tests in CI, mark as `[Fact(Skip = "Manual test only")]` or use `[Trait("Category", "Manual")]`.

### Pitfall 3: Unicode/Emoji Corrupted
**Symptom**: Emoji display as `?` or broken characters
**Solution**: Ensure terminal supports UTF-8 (macOS Terminal.app and Windows Terminal do by default).

### Pitfall 4: Cannot Test Keyboard Input
**Symptom**: Unit tests for Console.ReadKey not possible
**Solution**: Use integration tests or extract keyboard input handling to separate testable class.

---

## Quick Commands

```bash
# Build
dotnet build

# Run unit tests
dotnet test --filter "Category!=Manual"

# Run specific test class
dotnet test --filter "FullyQualifiedName~EditorResultTests"

# Run with code coverage
dotnet test /p:CollectCoverage=true /p:CoverageReporter=Html

# Run integration tests
dotnet test --filter "Category=Integration"

# Run app locally
dotnet run --project src/TenSecondTom.csproj
```

---

## Definition of Done

Before marking implementation complete:

- [ ] All models have unit tests with ≥80% coverage
- [ ] `StreamBasedTextEditor` implemented and tested
- [ ] `TerminalGuiTextEditor` implemented with integration tests
- [ ] `InputSanitizer` tested with ANSI sequences and Unicode
- [ ] `/today` command uses `IInteractiveTextEditor`
- [ ] Manual testing on macOS Terminal.app completed
- [ ] Manual testing on Windows Terminal completed
- [ ] Emoji and Unicode input/output verified
- [ ] All acceptance scenarios from spec.md pass
- [ ] Code coverage ≥80% for new code
- [ ] No compiler warnings
- [ ] PR passes CI/CD pipeline

---

## Next Steps After Implementation

1. Run `/speckit.tasks` to break this plan into granular tasks
2. Implement tasks in order following TDD
3. Create PR with comprehensive test coverage
4. Get code review
5. Merge to main → Automated release

---

**Quickstart Version**: 1.0
**Last Updated**: 2025-10-14
**Ready**: Yes - Begin implementation!
