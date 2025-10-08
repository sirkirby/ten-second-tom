# Contract: REPL Loop

**Component**: `ReplLoop`  
**Namespace**: `TenSecondTom.Features.Shell.Services`  
**Purpose**: Manages the Read-Eval-Print loop for the persistent CLI session

## Interface Contract

```csharp
public interface IReplLoop
{
    /// <summary>
    /// Runs the persistent REPL session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown.</param>
    /// <returns>Exit code (0 for success, non-zero for errors).</returns>
    Task<int> RunAsync(CancellationToken cancellationToken);
}
```

## Behavior Contract

### Initialization
- **Input**: Service provider, configuration, command registry
- **Preconditions**: 
  - Service provider contains required services (ICommandRouter, ISessionManager, IAutocompeteEngine)
  - Terminal supports color output (check Console.IsOutputRedirected)
- **Actions**:
  1. Display shell banner with logo
  2. Initialize SessionManager
  3. Display welcome message with available commands hint
- **Postconditions**:
  - Session is active
  - Prompt is ready for input

### Read Phase
- **Input**: User keyboard input
- **Actions**:
  1. Display prompt: `tom> ` with Spectre.Console styling
  2. Read user input with autocomplete support
  3. Handle special keys (Tab, Arrow Up/Down, Ctrl+C during input)
- **Output**: Command string or null (if empty/whitespace)

### Eval Phase
- **Input**: Command string
- **Preconditions**: Command is not null or whitespace
- **Actions**:
  1. Trim and normalize command string
  2. Delegate to ICommandRouter.RouteAsync(command, cancellationToken)
  3. Capture result or exception
- **Output**: Command result (success/failure)

### Print Phase
- **Input**: Command result or exception
- **Actions**:
  1. Format result based on JSON output flag
  2. Display using appropriate formatter (text or JSON)
  3. Add result to session history
- **Output**: Formatted output to stdout/stderr

### Loop Control
- **Exit Conditions**:
  - User enters `/quit` or `/exit` command
  - Ctrl+C pressed twice in succession (force exit)
  - Unrecoverable error (e.g., terminal I/O failure)
  - Cancellation token is cancelled externally
- **Actions on Exit**:
  1. Display farewell message
  2. Clean up session resources
  3. Return exit code (0 for clean exit, non-zero for errors)

## Error Handling Contract

### Input Errors
- **Empty input**: Ignore, re-display prompt (no error message)
- **Whitespace only**: Ignore, re-display prompt
- **Invalid command**: Display "Unknown command: {input}. Type /help for available commands."

### Command Execution Errors
- **Authentication error**: Display error panel with `/login` hint, return to prompt
- **Network timeout**: Display error panel with retry suggestion, return to prompt
- **LLM service unavailable**: Display error panel with provider info, return to prompt
- **Command cancelled (Ctrl+C)**: Display "(interrupted)" message, return to prompt

### Terminal Errors
- **Console.ReadLine() returns null**: Treat as EOF, exit gracefully
- **Terminal resize during output**: Gracefully reflow content if possible
- **Output redirect detected**: Disable rich formatting, use plain text

## Performance Contract

- **Startup time**: < 500ms from RunAsync() invocation to first prompt display
- **Prompt responsiveness**: < 50ms from Enter key to command execution start
- **Autocomplete latency**: < 100ms from Tab key to suggestion display
- **Memory usage**: < 50MB for session state and history (100 commands max)

## Testing Contract

### Unit Tests (ReplLoopTests.cs)
1. `RunAsync_WithNoInput_ExitsCleanly`: Simulate EOF, expect exit code 0
2. `RunAsync_WithQuitCommand_ExitsWithZero`: Enter `/quit`, expect clean exit
3. `RunAsync_WithValidCommand_InvokesRouter`: Enter `/today`, verify router called
4. `RunAsync_WithInvalidCommand_DisplaysError`: Enter `/unknown`, verify error display
5. `RunAsync_WithEmptyInput_RedisplaysPrompt`: Press Enter on empty, verify prompt re-appears
6. `RunAsync_WithCancellationToken_ExitsGracefully`: Cancel token, expect clean shutdown

### Integration Tests (PersistentShellSessionTests.cs)
1. `Session_CanExecuteMultipleCommandsSequentially`: Run `/today`, `/thisweek`, `/quit`
2. `Session_MaintainsHistoryAcrossCommands`: Execute 3 commands, verify history contains all
3. `Session_HandlesCancelKeyPressGracefully`: Start command, press Ctrl+C, verify recovery

## Dependencies

- `ICommandRouter`: Routes commands to handlers
- `ISessionManager`: Manages session state and history
- `IAutocompleteEngine`: Provides command suggestions
- `IServiceProvider`: Resolves command handlers
- `ILogger<ReplLoop>`: Logs session events (errors only, per constitution)

## Example Usage

```csharp
// In Program.cs (shell mode)
var replLoop = serviceProvider.GetRequiredService<IReplLoop>();
int exitCode = await replLoop.RunAsync(cancellationToken);
return exitCode;
```

## Contract Validation

- [x] Interface defined with XML documentation
- [x] Behavior specified for all phases (Read, Eval, Print, Loop)
- [x] Error cases enumerated and handled
- [x] Performance requirements stated
- [x] Test scenarios identified
- [x] Dependencies documented
