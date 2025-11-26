# Research: CLI REPL Enhancements

**Feature**: 001-cli-repl-updates  
**Date**: 2025-01-19  
**Status**: Complete

## 1. Tab Completion Implementation

### Decision: Custom implementation using Console.ReadKey (JKToolKit.Spectre.AutoCompletion incompatible)

**Rationale**:
- **Repository Evidence**: Previous feature (003-cli-interface-upgrade) definitively proved `TextPrompt<T>` in Spectre.Console 0.51.1 does NOT support Tab completion
- **Documented Limitation**: `AUTOCOMPLETE-FIXES-SUMMARY.md` explicitly states: "Real-Time Tab Completion: Status: Not possible with Spectre.Console 0.51.1. Why: `TextPrompt<T>` doesn't expose autocomplete API or real-time key handlers"
- **JKToolKit Incompatibility**: JKToolKit.Spectre.AutoCompletion is designed for `CommandApp` (Spectre.Console's command framework), not `TextPrompt<T>` (interactive prompt component). These are two separate components:
  - `CommandApp`: Command-line framework for building CLI apps with commands/options (like System.CommandLine)
  - `TextPrompt<T>`: Interactive single-line prompt component for REPL-style input
- **Current Implementation**: Codebase shows `CommandAutoCompleteSource` exists but only provides post-input suggestions (after Enter), not real-time Tab completion
- Custom `Console.ReadKey` implementation provides full control over Tab key handling
- Can integrate with existing `IAutocompleteEngine` and `CommandMetadata` catalog

**Repository Evidence**:
```csharp
// From specs/003-cli-interface-upgrade/AUTOCOMPLETE-FIXES-SUMMARY.md:
// "This doesn't work with Spectre.Console 0.51.1:
var prompt = new TextPrompt<string>("[cyan]>[/]")
    .AddChoice("/today")  // Not supported
    .AutoComplete(source); // Not available
```

**Current State**:
- `TextPrompt` used in `ReplLoop.ReadInput()` but Tab doesn't work
- Suggestions shown AFTER Enter is pressed (post-input)
- `CommandAutoCompleteSource` is helper class, not integrated with TextPrompt autocomplete

**Architecture**:
```csharp
// Abstraction for testability
public interface IConsoleKeyReader
{
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey(bool intercept);
    bool IsInputRedirected { get; }
}

// Custom input reader with constructor-injected dependencies
public sealed class EnhancedInputReader(
    IConsoleKeyReader consoleKeyReader,
    IAutocompleteEngine autocompleteEngine,
    ISessionManager sessionManager,
    ILogger<EnhancedInputReader> logger) : IEnhancedInputReader
{
    public async Task<string?> ReadInputAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new StringBuilder();
        var history = sessionManager.GetHistory();
        int historyIndex = -1; // -1 = not navigating history

        while (!cancellationToken.IsCancellationRequested)
        {
            var keyInfo = consoleKeyReader.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Escape)
            {
                return null; // Cancel/escape
            }
            else if (keyInfo.Key == ConsoleKey.Tab)
            {
                // Handle Tab completion
                var suggestions = autocompleteEngine.GetSuggestions(buffer.ToString());
                if (suggestions.Count > 0)
                {
                    // Cycle through suggestions or complete best match
                }
            }
            else if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                // Navigate history backward
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                // Navigate history forward
            }
            // ... handle other keys
        }
    }
}
```

**Alternatives Considered**:
- **JKToolKit.Spectre.AutoCompletion**: ❌ **REJECTED** - Designed for `CommandApp` (command framework), not `TextPrompt<T>` (interactive prompt). Repository evidence (003-cli-interface-upgrade) proves `TextPrompt` doesn't support Tab completion in 0.51.1.
- **ReadLine library (tonerdo/readline)**: ⚠️ Considered - provides history and completion, but would require replacing TextPrompt entirely, losing Spectre.Console styling
- **Upgrade Spectre.Console**: ⚠️ Considered - newer versions may support Tab, but 0.51.1 is current stable version and upgrade risk unknown
- **Custom Console.ReadKey implementation**: ✅ **SELECTED** - provides full control, integrates with existing infrastructure, maintains Spectre.Console styling for output

**Key Technical Challenges**:
1. **Unicode Support**: Must handle multi-byte characters correctly (emoji, non-Latin scripts)
2. **Cross-Platform**: Windows/macOS/Linux key code differences
3. **Visual Feedback**: Display suggestions inline without disrupting prompt
4. **History Integration**: Seamless integration with existing `ISessionManager.GetHistory()`

**Implementation Notes**:
- Use `IConsoleKeyReader.ReadKey(intercept: true)` to capture keys (abstracted for testability)
- Manually render prompt and buffer using Spectre.Console markup
- Handle Escape key (ASCII 27) to cancel input
- Tab cycles through `IAutocompleteEngine.GetSuggestions()` results
- Arrow Up/Down navigates `ISessionManager.GetHistory()` list
- Constructor injection for all dependencies (follows project patterns)

---

## 2. Escape Key Handling

### Decision: Escape key (ASCII 27) cancels current input and returns to main prompt

**Rationale**:
- Standard CLI pattern - Escape key universally recognized as "cancel/back"
- Ctrl+[ sends same ASCII code (27) as Escape key
- Works consistently across platforms
- Distinct from Ctrl+C (which cancels running commands)

**Behavior**:
- **At main prompt**: Escape does nothing (no-op) - user remains at prompt
- **During command input**: Escape cancels input, clears buffer, returns to prompt
- **During interactive command prompts**: Escape cancels command, returns to main prompt
- **During paginated output**: Escape exits pagination, returns to main prompt

**Implementation**:
```csharp
if (keyInfo.Key == ConsoleKey.Escape || 
    (keyInfo.Key == ConsoleKey.Oem4 && keyInfo.Modifiers == ConsoleModifiers.Control)) // Ctrl+[
{
    return null; // Signal cancellation
}
```

**Edge Cases**:
- Escape during long-running command: Ctrl+C handles this (existing behavior)
- Escape during file selection: Cancel prompt, return to main prompt
- Escape in nested command flows: Exit entire nested flow, not just current step

---

## 3. History Navigation Implementation

### Decision: Arrow Up/Down keys navigate through `ISessionManager.GetHistory()` list

**Rationale**:
- Standard REPL pattern - Arrow keys universally used for history navigation
- Existing `ISessionManager.GetHistory()` returns `IReadOnlyList<CommandHistoryEntry>`
- No changes needed to history storage - uses existing circular buffer (100 commands max)
- Seamless integration with existing infrastructure

**Behavior**:
- **Arrow Up**: Navigate backward through history (newest → oldest)
- **Arrow Down**: Navigate forward through history (oldest → newest)
- **At oldest command**: Arrow Up does nothing (no wrap-around)
- **At newest command or empty prompt**: Arrow Down returns to empty prompt
- **During history navigation**: User can edit command before executing
- **After editing**: Edited command added as new history entry (doesn't replace original)

**Implementation**:
```csharp
private int _historyIndex = -1; // -1 = not navigating history
private List<CommandHistoryEntry> _history = new();

// Arrow Up
if (keyInfo.Key == ConsoleKey.UpArrow)
{
    if (_historyIndex < _history.Count - 1)
    {
        _historyIndex++;
        buffer.Clear();
        buffer.Append(_history[_history.Count - 1 - _historyIndex].Command);
        RenderPrompt(buffer.ToString());
    }
}

// Arrow Down
if (keyInfo.Key == ConsoleKey.DownArrow)
{
    if (_historyIndex > 0)
    {
        _historyIndex--;
        buffer.Clear();
        buffer.Append(_history[_history.Count - 1 - _historyIndex].Command);
        RenderPrompt(buffer.ToString());
    }
    else if (_historyIndex == 0)
    {
        _historyIndex = -1;
        buffer.Clear();
        RenderPrompt(string.Empty);
    }
}
```

**Edge Cases**:
- Empty history: Arrow Up does nothing
- Partial input at prompt: Arrow Up replaces input with historical command
- History limit (100 commands): Only most recent 100 accessible

---

## 4. Integration with Existing REPL Infrastructure

### Decision: Enhance `ReplLoop.ReadInput()` method with custom input handling

**Rationale**:
- Minimal changes to existing code - enhance rather than replace
- Maintains existing `TextPrompt` usage for simple cases (fallback)
- Preserves existing autocomplete suggestion display (post-input hints)
- No breaking changes to `IReplLoop` interface

**Architecture**:
```csharp
public sealed class ReplLoop : IReplLoop
{
    private readonly IEnhancedInputReader _inputReader; // New service
    
    private string? ReadInput()
    {
        // Try enhanced input reader first (supports Tab, Arrow keys, Escape)
        if (_inputReader.IsAvailable())
        {
            return _inputReader.ReadInputAsync(
                _autocompleteEngine,
                _sessionManager,
                cancellationToken).Result;
        }
        
        // Fallback to TextPrompt for non-interactive terminals
        return AnsiConsole.Prompt(new TextPrompt<string>("[cyan]>[/]")
            .AllowEmpty());
    }
}
```

**Service Registration**:
```csharp
// In Shell feature DependencyInjection.cs
services.AddSingleton<IEnhancedInputReader, EnhancedInputReader>();
```

**Testing Strategy**:
- Unit tests: Mock `IConsoleKeyReader` to simulate key presses (lean, critical paths only)
- No integration tests needed - `IConsoleKeyReader` abstraction enables comprehensive unit testing
- Manual testing: Required for final validation on macOS

---

## 5. Performance Considerations

### Decision: Optimize for <100ms escape response, <200ms history navigation

**Rationale**:
- Success criteria require 100ms escape response, 200ms history navigation
- In-memory operations (history lookup, autocomplete) are fast
- Main bottleneck is terminal rendering (cursor movement, text redraw)
- Use buffered rendering to minimize screen updates

**Optimization Strategies**:
1. **Debounce Tab completion**: Don't recalculate suggestions on every keystroke
2. **Cache autocomplete results**: Store suggestions for current prefix
3. **Lazy history loading**: Only load history when Arrow keys pressed
4. **Minimal screen updates**: Only redraw changed portions of prompt

**Performance Targets**:
- Escape key response: <100ms (immediate cancellation)
- Tab completion: <50ms (suggestion lookup + display)
- History navigation: <200ms (history lookup + prompt update)

---

## 6. Cross-Platform Compatibility

### Decision: Use `Console.ReadKey()` with platform-specific handling

**Rationale**:
- `Console.ReadKey()` is cross-platform (.NET Standard 2.0+)
- Key codes are consistent across platforms for standard keys (Tab, Arrow, Escape)
- Platform-specific differences handled by .NET runtime

**Platform-Specific Notes**:
- **macOS**: Escape key works as expected, Arrow keys work
- **Windows**: Escape key works, Arrow keys work (may need ANSI mode for colors)
- **Linux**: Escape key works, Arrow keys work (terminal-dependent)

**Testing Requirements**:
- Test on macOS (primary platform)
- Test on Windows (supported platform)
- Linux testing deferred (future platform)

---

## Summary of Decisions

| Feature | Decision | Rationale |
|---------|----------|-----------|
| Tab Completion | Custom implementation with `IConsoleKeyReader` abstraction | JKToolKit designed for `CommandApp`, not `TextPrompt<T>`. Custom implementation required. Abstraction enables unit testing. |
| Escape Key | Escape (ASCII 27) or Ctrl+[ | Standard CLI pattern, cross-platform |
| History Navigation | Arrow Up/Down with `ISessionManager.GetHistory()` | Standard REPL pattern, uses existing infrastructure |
| Integration | Enhance `ReplLoop.ReadInput()` | Minimal changes, maintains existing behavior |
| Performance | Optimize rendering, cache results | Meet <100ms escape, <200ms history targets |
| Testability | `IConsoleKeyReader` abstraction | Enables mocking `Console.ReadKey()` for lean unit tests |
| Constructor Injection | All dependencies via primary constructor | Follows project patterns, improves testability |

---

## 7. Escape Support in Spectre.Console Interactive Prompts

### Problem Statement

The escape mechanism implemented in `EnhancedInputReader` only works at the main REPL prompt. However, the application uses Spectre.Console's `SelectionPrompt<T>`, `TextPrompt<T>`, `ConfirmationPrompt`, and `MultiSelectionPrompt<T>` throughout commands and wizards. Users expect Escape to work consistently across ALL prompts, not just the REPL input.

**Affected Scenarios**:
- `/audio config` - Multi-step wizard with SelectionPrompt
- `/llm config` - Multi-step wizard with SelectionPrompt
- `/config set <key>` - TextPrompt for value input
- Most interactive commands use some form of Spectre.Console prompt

### Decision: Static `CancellablePrompt` Helper with IAnsiConsole Wrapper

**Rationale**:
- Spectre.Console 0.51.1 has **NO native Escape key support** in prompts
- Prompts block on input and do not expose key event handlers
- The only interception point is the `IAnsiConsole` input layer

### Research Findings

**Spectre.Console Architecture**:
```
User Input → Console.ReadKey() → IAnsiConsole → Prompt.Show() → Result
                                      ↑
                              Interception point
```

**Why Native Escape Doesn't Work**:
1. `SelectionPrompt` uses internal key handling for Up/Down navigation
2. Escape key is not mapped to any action in prompt handlers
3. No public API to register custom key handlers
4. Prompts run synchronously until user confirms selection

**Solution Architecture**:
```
CancellablePrompt.Selection<T>() → Creates cancellable console
                                 → Wraps prompt execution
                                 → Catches PromptCancelledException
                                 → Returns null on escape
```

### Implementation Design

```csharp
// src/Infrastructure/Cli/CancellablePrompt.cs

/// <summary>
/// Provides escape-key cancellable prompts using Spectre.Console.
/// Wraps standard prompts to detect Escape key and return null on cancel.
/// </summary>
public static class CancellablePrompt
{
    /// <summary>
    /// Shows a selection prompt that can be cancelled with Escape key.
    /// </summary>
    public static T? Selection<T>(Action<SelectionPrompt<T>> configure) where T : class
    {
        var prompt = new SelectionPrompt<T>();
        configure(prompt);

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a text prompt that can be cancelled with Escape key.
    /// </summary>
    public static string? Text(Action<TextPrompt<string>> configure)
    {
        var prompt = new TextPrompt<string>(string.Empty);
        configure(prompt);

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a confirmation prompt that can be cancelled with Escape key.
    /// </summary>
    public static bool? Confirm(string message, bool defaultValue = true)
    {
        var prompt = new ConfirmationPrompt(message) { DefaultValue = defaultValue };
        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Shows a multi-selection prompt that can be cancelled with Escape key.
    /// </summary>
    public static List<T>? MultiSelection<T>(Action<MultiSelectionPrompt<T>> configure)
        where T : notnull
    {
        var prompt = new MultiSelectionPrompt<T>();
        configure(prompt);

        var console = CreateCancellableConsole();

        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    private static IAnsiConsole CreateCancellableConsole()
    {
        return AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(Console.Out),
        });
    }
}

/// <summary>
/// Exception thrown when user presses Escape to cancel a prompt.
/// </summary>
public sealed class PromptCancelledException : OperationCanceledException
{
    public PromptCancelledException() : base("Prompt cancelled by user (Escape key)") { }
}
```

### Critical Discovery: IAnsiConsoleInput Interception Required

The above static helper **will not work as-is** because `AnsiConsole.Create()` does not intercept Escape keys automatically. We need to provide a custom `IAnsiConsoleInput` implementation that:

1. Wraps the standard console input
2. Intercepts `ConsoleKey.Escape` before passing to Spectre
3. Throws `PromptCancelledException` when Escape is detected

**Updated Architecture**:
```csharp
/// <summary>
/// Console input wrapper that throws PromptCancelledException on Escape key.
/// </summary>
public sealed class EscapeCancellableInput : IAnsiConsoleInput
{
    public bool IsKeyAvailable() => Console.KeyAvailable;

    public ConsoleKeyInfo? ReadKey(bool intercept)
    {
        var key = Console.ReadKey(intercept);

        if (key.Key == ConsoleKey.Escape)
        {
            throw new PromptCancelledException();
        }

        return key;
    }

    public Task<ConsoleKeyInfo?> ReadKeyAsync(bool intercept, CancellationToken cancellationToken)
    {
        // Synchronous fallback - Spectre.Console prompts are synchronous
        return Task.FromResult(ReadKey(intercept));
    }
}

private static IAnsiConsole CreateCancellableConsole()
{
    return AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.Detect,
        ColorSystem = ColorSystemSupport.Detect,
        Interactive = InteractionSupport.Yes,
        Out = new AnsiConsoleOutput(Console.Out),
        Input = new EscapeCancellableInput(), // Custom input handler
    });
}
```

### Alternative Approaches Considered

| Approach | Pros | Cons | Decision |
|----------|------|------|----------|
| IAnsiConsoleInput wrapper | Works with all prompt types, minimal code changes | Requires understanding Spectre internals | ✅ **SELECTED** |
| Fork Spectre.Console | Full control over key handling | Maintenance burden, diverges from upstream | ❌ Rejected |
| Replace prompts with custom implementation | Full control | Massive code rewrite, lose Spectre styling | ❌ Rejected |
| Wait for Spectre.Console update | No work required | No ETA, may never happen | ❌ Rejected |

### Integration Pattern

**Before (current code)**:
```csharp
var provider = AnsiConsole.Prompt(new SelectionPrompt<string>()
    .Title("Select STT provider:")
    .AddChoices(["OpenAI", "Whisper", "Azure"]));
```

**After (with escape support)**:
```csharp
var provider = CancellablePrompt.Selection<string>(p => p
    .Title("Select STT provider:")
    .AddChoices(["OpenAI", "Whisper", "Azure"]));

if (provider is null)
{
    // User pressed Escape - handle cancellation
    return; // or navigate back in wizard
}
```

### Multi-Step Wizard Pattern

For wizards with multiple steps, each step checks for null and handles appropriately:

```csharp
public async Task<int> RunWizardAsync()
{
    // Step 1
    var step1Result = CancellablePrompt.Selection<string>(p => p
        .Title("Step 1: Select option")
        .AddChoices(["A", "B", "C"]));

    if (step1Result is null)
    {
        AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
        return 1; // Exit wizard
    }

    // Step 2
    var step2Result = CancellablePrompt.Text(p => p
        .Prompt("Step 2: Enter value"));

    if (step2Result is null)
    {
        // Could go back to step 1, or exit
        AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
        return 1;
    }

    // ... continue with results
}
```

### Testing Strategy

1. **Unit Tests**: Test `EscapeCancellableInput` directly with mocked console input
2. **Integration Tests**: Test full `CancellablePrompt` methods with simulated key sequences
3. **Manual Tests**: Required for visual validation of prompt behavior

### Files to Create/Modify

| File | Action | Description |
|------|--------|-------------|
| `src/Infrastructure/Cli/CancellablePrompt.cs` | Create | Static helper class with cancellable prompt methods |
| `src/Infrastructure/Cli/EscapeCancellableInput.cs` | Create | IAnsiConsoleInput implementation for escape detection |
| Commands using prompts | Modify | Update to use `CancellablePrompt.*` instead of `AnsiConsole.Prompt()` |

### Success Criteria

- [ ] Escape key cancels `SelectionPrompt` and returns null
- [ ] Escape key cancels `TextPrompt` and returns null
- [ ] Escape key cancels `ConfirmationPrompt` and returns null
- [ ] Escape key cancels `MultiSelectionPrompt` and returns null
- [ ] Multi-step wizards can handle escape at any step
- [ ] No visual glitches when escape is pressed mid-prompt

---

**Next Steps**:
1. Create `IConsoleKeyReader` abstraction and `SystemConsoleKeyReader` implementation
2. Create `IEnhancedInputReader` interface with constructor injection pattern
3. Implement `EnhancedInputReader` service with custom key handling
4. Enhance `ReplLoop.ReadInput()` to use `IEnhancedInputReader` with TextPrompt fallback
5. Register services in DI
6. Add lean unit tests for key handling logic (mock `IConsoleKeyReader`)
7. **Create `CancellablePrompt` static helper with `EscapeCancellableInput`**
8. **Update commands/wizards to use `CancellablePrompt.*` methods**
9. Manual testing on macOS

