# Quick Start: CLI REPL Enhancements

**Feature**: 001-cli-repl-updates  
**Date**: 2025-01-19

## Overview

This feature adds three critical usability enhancements to the CLI REPL:

1. **Escape Mechanism**: Press Escape to cancel any interactive command and return to the main prompt
2. **Tab Completion**: Press Tab to cycle through command suggestions as you type
3. **History Navigation**: Press Arrow Up/Down to navigate through previously executed commands

## User Guide

### Escape Key

**Usage**: Press `Escape` (or `Ctrl+[`) at any time to cancel the current operation.

**When it works**:
- During command input at the main prompt
- During interactive command prompts (e.g., `/config set` asking for a value)
- While viewing paginated output (e.g., `/search` results)

**When it doesn't work**:
- At the main prompt with no input (no-op, stays at prompt)
- During long-running commands (use `Ctrl+C` instead)

**Example**:
```bash
tom> /config set
Enter configuration key: [user types "api", then presses Escape]
tom>  # Returns to prompt, no configuration changes
```

### Tab Completion

**Usage**: Type a partial command starting with `/`, then press `Tab` to complete it.

**Features**:
- Cycles through matching commands with each Tab press
- Shows best match first, then cycles through alternatives
- Works with command prefixes (e.g., `/rec` → `/record`)
- Works with substrings (e.g., `/co` matches `/config`, `/record`)

**Example**:
```bash
tom> /rec[Tab]        # Completes to /record
tom> /rec[Tab][Tab]   # Cycles to next match (if multiple)
tom> /co[Tab]         # Completes to /config (best match)
```

**No matches**: If no commands match your prefix, Tab does nothing (visual feedback may be shown).

### History Navigation

**Usage**: Press `Arrow Up` to go backward through history, `Arrow Down` to go forward.

**Features**:
- Navigates through last 100 commands in current session
- Shows historical command in prompt, ready to edit
- Edited commands added as new history entry (doesn't replace original)
- Empty history: Arrow Up does nothing

**Example**:
```bash
tom> /help
tom> /config
tom> /search meeting
tom> [Arrow Up]      # Shows /search meeting
tom> [Arrow Up]      # Shows /config
tom> [Arrow Up]      # Shows /help
tom> [Arrow Down]    # Back to /config
tom> [Arrow Down]   # Back to /search meeting
tom> [Arrow Down]   # Back to empty prompt
```

**Editing History**:
```bash
tom> [Arrow Up]           # Shows /search meeting
tom> [Edit to] /search test[Enter]  # Executes /search test
tom> # New entry added: /search test (original /search meeting preserved)
```

## Developer Guide

### Architecture

**New Components**:

1. **`IConsoleKeyReader`** - Abstraction for console key reading (enables unit testing)
   - Location: `src/Features/Shell/Services/ConsoleKeyReader.cs`
   - Purpose: Abstracts `Console.ReadKey()` for testability
   - Default: `SystemConsoleKeyReader` wraps `System.Console`

2. **`IEnhancedInputReader`** - Enhanced input handler
   - Location: `src/Features/Shell/Services/EnhancedInputReader.cs`
   - Purpose: Handles Tab, Arrow keys, and Escape key
   - Integration: Called from `ReplLoop.ReadInput()` method
   - Dependencies: Injected via constructor (follows project patterns)

**No Changes To**:
- `ISessionManager`: Uses existing `GetHistory()` method
- `IAutocompleteEngine`: Uses existing `GetSuggestions()` method
- `CommandMetadata`: Uses existing catalog
- `CommandHistoryEntry`: Uses existing model

### Implementation Steps

1. **Create `IConsoleKeyReader` abstraction**
   ```csharp
   public interface IConsoleKeyReader
   {
       bool KeyAvailable { get; }
       ConsoleKeyInfo ReadKey(bool intercept);
       bool IsInputRedirected { get; }
   }

   public sealed class SystemConsoleKeyReader : IConsoleKeyReader
   {
       public bool KeyAvailable => Console.KeyAvailable;
       public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
       public bool IsInputRedirected => Console.IsInputRedirected;
   }
   ```

2. **Create `IEnhancedInputReader` interface**
   ```csharp
   public interface IEnhancedInputReader
   {
       bool IsAvailable();
       Task<string?> ReadInputAsync(CancellationToken cancellationToken = default);
   }
   ```

3. **Implement `EnhancedInputReader` with constructor injection**
   ```csharp
   public sealed class EnhancedInputReader(
       IConsoleKeyReader consoleKeyReader,
       IAutocompleteEngine autocompleteEngine,
       ISessionManager sessionManager,
       ILogger<EnhancedInputReader> logger) : IEnhancedInputReader
   {
       // Handle Tab, Arrow Up/Down, Escape keys
   }
   ```

4. **Enhance `ReplLoop.ReadInput()`**
   ```csharp
   private string? ReadInput()
   {
       if (_inputReader.IsAvailable())
       {
           return await _inputReader.ReadInputAsync(cancellationToken);
       }
       // Fallback to TextPrompt
       return AnsiConsole.Prompt(...);
   }
   ```

5. **Register Services**
   ```csharp
   // In Shell feature DependencyInjection.cs
   services.AddSingleton<IConsoleKeyReader, SystemConsoleKeyReader>();
   services.AddSingleton<IEnhancedInputReader, EnhancedInputReader>();
   ```

### Testing

**Test Strategy**: Lean unit tests with mocked `IConsoleKeyReader` - critical paths only.

**Unit Tests**:
```csharp
[Fact]
public async Task ReadInputAsync_EscapeKey_ReturnsNull()
{
    // Arrange
    var mockKeyReader = new Mock<IConsoleKeyReader>();
    mockKeyReader.SetupSequence(k => k.ReadKey(true))
        .Returns(new ConsoleKeyInfo('\x1b', ConsoleKey.Escape, false, false, false));

    var reader = new EnhancedInputReader(
        mockKeyReader.Object,
        Mock.Of<IAutocompleteEngine>(),
        Mock.Of<ISessionManager>(),
        Mock.Of<ILogger<EnhancedInputReader>>());

    // Act
    var result = await reader.ReadInputAsync(CancellationToken.None);

    // Assert
    result.Should().BeNull();
}
```

**No integration tests** - the `IConsoleKeyReader` abstraction enables comprehensive unit testing without needing real console interaction.

### Key Bindings Reference

| Key | Action |
|-----|--------|
| `Tab` | Cycle through autocomplete suggestions |
| `Shift+Tab` | Cycle backward through suggestions |
| `Arrow Up` | Navigate backward through history |
| `Arrow Down` | Navigate forward through history |
| `Escape` | Cancel input, return to prompt |
| `Ctrl+[` | Same as Escape (ASCII 27) |
| `Ctrl+C` | Cancel running command (existing) |
| `Enter` | Submit command |
| `Backspace` | Delete character before cursor |
| `Delete` | Delete character at cursor |
| `Left/Right Arrow` | Move cursor within buffer |
| `Home/End` | Move cursor to start/end of buffer |

## Troubleshooting

### Tab Completion Not Working

**Symptoms**: Pressing Tab does nothing

**Possible Causes**:
1. Non-interactive terminal (`Console.IsInputRedirected = true`)
2. Terminal doesn't support key reading
3. No matching commands for current prefix

**Solutions**:
- Check terminal supports interactive input
- Verify command prefix starts with `/`
- Try typing more characters to narrow matches

### History Navigation Not Working

**Symptoms**: Arrow Up/Down don't navigate history

**Possible Causes**:
1. No commands executed yet (empty history)
2. Terminal doesn't support Arrow keys
3. History limit reached (100 commands)

**Solutions**:
- Execute at least one command first
- Check terminal supports Arrow keys
- History limited to most recent 100 commands

### Escape Key Not Working

**Symptoms**: Pressing Escape doesn't cancel input

**Possible Causes**:
1. Terminal doesn't send Escape key correctly
2. During long-running command (use Ctrl+C instead)
3. Terminal in special mode (e.g., raw mode)

**Solutions**:
- Try `Ctrl+[` instead (same as Escape)
- Use `Ctrl+C` for running commands
- Check terminal configuration

## Performance

**Target Response Times**:
- Escape key: <100ms
- Tab completion: <50ms
- History navigation: <200ms

**Optimization**:
- Autocomplete results cached for current prefix
- History loaded lazily (only when Arrow keys pressed)
- Minimal screen updates (only redraw changed portions)

## Cross-Platform Notes

**macOS**: All features work as expected

**Windows**: All features work (may require ANSI mode for colors)

**Linux**: All features work (terminal-dependent)

**Non-Interactive Terminals**: Falls back to `TextPrompt` (no Tab/Arrow/Escape support)

## Related Documentation

- [Specification](./spec.md): Full feature requirements
- [Research](./research.md): Implementation decisions and alternatives
- [Data Model](./data-model.md): Entity relationships and validation
- [Contracts](./contracts/): API contracts and interfaces

