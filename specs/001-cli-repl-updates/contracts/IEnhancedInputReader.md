# Contract: Enhanced Input Reader

**Component**: `IEnhancedInputReader`  
**Namespace**: `TenSecondTom.Features.Shell.Services`  
**Purpose**: Provides enhanced REPL input handling with Tab completion, history navigation, and escape key support

## Interface Contract

```csharp
/// <summary>
/// Abstracts console key reading for testability.
/// </summary>
public interface IConsoleKeyReader
{
    /// <summary>
    /// Gets a value indicating whether a key press is available in the input stream.
    /// </summary>
    bool KeyAvailable { get; }

    /// <summary>
    /// Obtains the next key pressed by the user.
    /// </summary>
    /// <param name="intercept">True to not display the pressed key.</param>
    /// <returns>Information about the key pressed.</returns>
    ConsoleKeyInfo ReadKey(bool intercept);

    /// <summary>
    /// Gets a value indicating whether input has been redirected (non-interactive).
    /// </summary>
    bool IsInputRedirected { get; }
}

/// <summary>
/// Default implementation using System.Console.
/// </summary>
public sealed class SystemConsoleKeyReader : IConsoleKeyReader
{
    public bool KeyAvailable => Console.KeyAvailable;
    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
    public bool IsInputRedirected => Console.IsInputRedirected;
}

/// <summary>
/// Provides enhanced input reading for REPL with Tab completion, history navigation, and escape support.
/// Dependencies injected via constructor following project patterns.
/// </summary>
public interface IEnhancedInputReader
{
    /// <summary>
    /// Checks if enhanced input reader is available (interactive terminal).
    /// </summary>
    /// <returns>True if terminal supports interactive input, false otherwise.</returns>
    bool IsAvailable();

    /// <summary>
    /// Reads user input with Tab completion, history navigation, and escape key support.
    /// Uses IAutocompleteEngine and ISessionManager injected via constructor.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>Command string if submitted, null if cancelled (Escape key).</returns>
    Task<string?> ReadInputAsync(CancellationToken cancellationToken = default);
}
```

### Implementation Constructor

```csharp
/// <summary>
/// Enhanced input reader with constructor-injected dependencies.
/// </summary>
public sealed class EnhancedInputReader(
    IConsoleKeyReader consoleKeyReader,
    IAutocompleteEngine autocompleteEngine,
    ISessionManager sessionManager,
    ILogger<EnhancedInputReader> logger) : IEnhancedInputReader
{
    // Dependencies available throughout class via primary constructor
}
```

## Behavior Contract

### Availability Check

**Preconditions**:
- Terminal must support interactive input (`!Console.IsInputRedirected`)
- Terminal must support key reading (`Console.KeyAvailable` check)

**Postconditions**:
- Returns `true` if enhanced input available, `false` for fallback to `TextPrompt`

**Usage**:
```csharp
if (_inputReader.IsAvailable())
{
    return await _inputReader.ReadInputAsync(cancellationToken);
}
else
{
    // Fallback to TextPrompt
    return AnsiConsole.Prompt(new TextPrompt<string>("[cyan]>[/]"));
}
```

### Input Reading

**Input**: User keyboard input via `IConsoleKeyReader.ReadKey()` (abstracted for testability)

**Key Handling**:
- **Character Keys**: Append to input buffer, update cursor position
- **Tab**: Cycle through autocomplete suggestions from `IAutocompleteEngine`
- **Arrow Up**: Navigate backward through history (newest → oldest)
- **Arrow Down**: Navigate forward through history (oldest → newest)
- **Escape**: Cancel input, return `null`
- **Enter**: Submit command, return buffer content
- **Backspace**: Delete character before cursor
- **Delete**: Delete character at cursor
- **Left/Right Arrow**: Move cursor within buffer
- **Home/End**: Move cursor to start/end of buffer
- **Ctrl+C**: Propagate cancellation token (existing behavior)

**Output**: 
- `string`: Command to execute (trimmed, non-empty)
- `null`: Input cancelled (Escape key pressed)

**Edge Cases**:
- Empty input + Enter: Returns empty string (handled by ReplLoop)
- Escape at empty prompt: Returns `null` (no-op in ReplLoop)
- Tab with no matches: No completion, visual feedback
- Arrow Up with empty history: No-op, remains at prompt
- Arrow Down at newest command: Returns to empty prompt

### Tab Completion Behavior

**Trigger**: User presses Tab key

**Process**:
1. Get current buffer content
2. Call `autocompleteEngine.GetSuggestions(buffer)`
3. If suggestions exist:
   - Cycle through suggestions (Tab = next, Shift+Tab = previous)
   - Display current suggestion in prompt
   - Update buffer with selected suggestion
4. If no suggestions:
   - Visual feedback (e.g., bell sound or message)
   - No buffer change

**Suggestion Display**:
- Inline completion: Show completed command in prompt
- Visual indicator: Highlight completed portion
- Cycling: Tab cycles forward, Shift+Tab cycles backward

**Input During Cycling**:
When user is cycling through suggestions and types a character:
1. Accept the currently displayed suggestion into the buffer
2. Append the new character to the buffer
3. Reset autocomplete cycling state (index = -1)
4. Trigger new autocomplete lookup for the updated buffer

### History Navigation Behavior

**Trigger**: User presses Arrow Up or Arrow Down

**Process**:
1. Get history from `sessionManager.GetHistory()`
2. Navigate through history entries:
   - Arrow Up: Move backward (increase index)
   - Arrow Down: Move forward (decrease index)
3. Update buffer with historical command
4. Reset autocomplete state (not cycling suggestions)

**Boundary Behavior**:
- At oldest command: Arrow Up does nothing (no wrap-around)
- At newest command: Arrow Down returns to empty prompt
- Empty history: Arrow Up does nothing

**Editing**:
- User can edit historical command before submitting
- Edited command added as new history entry (doesn't replace original)

### Escape Key Behavior

**Trigger**: User presses Escape key (ASCII 27) or Ctrl+[

**Process**:
1. Cancel current input
2. Clear input buffer
3. Reset history navigation index
4. Reset autocomplete index
5. Return `null` to signal cancellation

**Context Handling**:
- **At main prompt**: Returns `null`, ReplLoop continues (no-op)
- **During command input**: Cancels input, returns to prompt
- **During interactive prompts**: Cancels command, returns to main prompt
- **During paginated output**: Exits pagination, returns to prompt

## Performance Contract

**Response Times**:
- Escape key: <100ms (immediate cancellation)
- Tab completion: <50ms (suggestion lookup + display)
- History navigation: <200ms (history lookup + prompt update)
- Character input: <10ms (buffer update + cursor render)

**Optimization**:
- Cache autocomplete results for current prefix
- Debounce Tab key presses (don't recalculate on rapid Tab presses)
- Lazy load history (only fetch when Arrow keys pressed)
- Minimize screen updates (only redraw changed portions)

## Error Handling

**Exceptions**:
- `OperationCanceledException`: Propagated from cancellation token
- `InvalidOperationException`: If `IsAvailable()` returns false but `ReadInputAsync()` called
- Terminal errors: Handled gracefully, fallback to TextPrompt if possible

**Recovery**:
- If terminal becomes non-interactive: Return `null`, ReplLoop falls back to TextPrompt
- If key reading fails: Log error, return `null`, allow fallback

## Testing Contract

**Unit Tests**:
- Mock `Console.ReadKey()` to simulate key presses
- Verify Tab completion cycles through suggestions
- Verify Arrow keys navigate history correctly
- Verify Escape key returns `null`

**Integration Tests**:
- Use `Console.SetIn()` with pre-recorded key sequences
- Test full REPL loop with enhanced input reader
- Verify fallback to TextPrompt for non-interactive terminals

**Manual Tests**:
- Test Tab completion with various command prefixes
- Test history navigation with 100+ commands
- Test Escape key in various contexts
- Test cross-platform behavior (macOS, Windows)

## Dependencies

**Required Services** (injected via constructor):
- `IConsoleKeyReader`: Abstraction for console key reading (enables unit testing)
- `IAutocompleteEngine`: For command suggestion generation
- `ISessionManager`: For command history access
- `ILogger<EnhancedInputReader>`: For diagnostic logging

**Platform Requirements**:
- Interactive terminal (`!IConsoleKeyReader.IsInputRedirected`)
- Key reading support (`IConsoleKeyReader.ReadKey()`)
- ANSI escape sequences for cursor control (optional, for better UX)

## Implementation Notes

**Custom Implementation**:
- Uses `IConsoleKeyReader.ReadKey(intercept: true)` for key capture (abstracted for testing)
- Manually renders prompt and buffer using Spectre.Console markup
- Cross-platform key code handling via .NET runtime

**Unicode Handling (MVP Limitation)**:
- Uses simple codepoint-based cursor movement
- Each `char` in buffer = one cursor position
- Multi-codepoint characters (e.g., emoji 👨‍👩‍👧, combining marks) may display incorrectly
- Grapheme cluster support deferred to future enhancement

**Integration Points**:
- Called from `ReplLoop.ReadInput()` method
- Falls back to `TextPrompt` if `IsAvailable()` returns false
- No changes to existing `IReplLoop` interface

**Service Registration**:
```csharp
// In Shell feature DependencyInjection.cs
services.AddSingleton<IConsoleKeyReader, SystemConsoleKeyReader>();
services.AddSingleton<IEnhancedInputReader, EnhancedInputReader>();
```

