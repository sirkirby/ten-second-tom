# Contract: Command Router

**Component**: `CommandRouter`  
**Namespace**: `TenSecondTom.Features.Shell.Services`  
**Purpose**: Parses slash commands and routes them to appropriate handlers

## Interface Contract

```csharp
public interface ICommandRouter
{
    /// <summary>
    /// Routes a command string to the appropriate handler.
    /// </summary>
    /// <param name="commandText">The command text entered by the user (e.g., "/today").</param>
    /// <param name="cancellationToken">Cancellation token for command interruption.</param>
    /// <returns>Command result indicating success or failure.</returns>
    Task<CommandResult> RouteAsync(string commandText, CancellationToken cancellationToken);
}

public record CommandResult(bool IsSuccess, string? Message = null, Exception? Error = null);
```

## Behavior Contract

### Command Parsing
- **Input**: Raw command string (e.g., "/today --provider OpenAI")
- **Preconditions**: Command text is not null or empty
- **Actions**:
  1. Trim whitespace
  2. Check if command starts with '/' (slash command)
  3. Split into command name and arguments
  4. Normalize command name to lowercase
- **Output**: Parsed command structure (name, args)

### Command Resolution
- **Input**: Parsed command name
- **Actions**:
  1. Look up command in registry (case-insensitive)
  2. Check for aliases (e.g., /exit → /quit)
  3. Resolve handler from service provider
- **Output**: Handler instance or null (unknown command)
- **Error Cases**:
  - Unknown command: Return failure result with "Unknown command" message
  - Handler not registered: Return failure result with "Internal error" message

### Handler Execution
- **Input**: Handler instance, arguments, cancellation token
- **Preconditions**: Handler is not null
- **Actions**:
  1. Build System.CommandLine parseResult from arguments
  2. Invoke handler with parsed arguments and cancellation token
  3. Capture result or exception
- **Output**: CommandResult with success/failure status
- **Error Cases**:
  - OperationCanceledException: Return success with "Command interrupted" message
  - AuthenticationException: Return failure with auth error message
  - Any other exception: Return failure with exception message, log full stack trace

### Supported Commands (Initial Set)
- `/today [--provider <name>]`: Create daily reflection entry
- `/thisweek [--from-date <date>] [--to-date <date>] [--provider <name>]`: Generate weekly review
- `/search <query> [--from-date <date>] [--to-date <date>]`: Search memory entries
- `/login`: Authenticate with SSH key
- `/logout`: Invalidate current session
- `/quit` or `/exit`: Exit the shell
- `/help`: Display available commands with descriptions

## Error Handling Contract

### Parse Errors
- **Missing slash prefix**: Return failure with "Commands must start with '/'. Type /help for available commands."
- **Empty command after slash**: Return failure with "Empty command. Type /help for available commands."
- **Invalid arguments**: Return failure with argument validation error from System.CommandLine

### Execution Errors
- **Authentication required**: Return failure with "Authentication required. Run /login first."
- **Network timeout**: Return failure with "Network timeout. Check your connection and try again."
- **LLM service error**: Return failure with "LLM service unavailable: {details}"
- **Cancellation**: Return success with "(interrupted)" message (not an error)

### Handler Registration Errors
- **Handler not found in DI**: Log critical error, return failure with "Internal configuration error"
- **Multiple handlers for command**: Log warning, use first registered handler

## Performance Contract

- **Routing latency**: < 10ms for command lookup and handler resolution
- **Total command execution time**: Depends on handler (LLM calls may take 1-3 seconds)
- **Memory overhead**: < 1KB per routed command (includes parse result and command result)

## Testing Contract

### Unit Tests (CommandRouterTests.cs)
1. `RouteAsync_WithValidCommand_ReturnsSuccess`: Route `/today`, expect success
2. `RouteAsync_WithUnknownCommand_ReturnsFailure`: Route `/unknown`, expect failure with message
3. `RouteAsync_WithoutSlashPrefix_ReturnsFailure`: Route `today`, expect "must start with /" error
4. `RouteAsync_WithAliasCommand_RoutesToCorrectHandler`: Route `/exit`, verify `/quit` handler called
5. `RouteAsync_WithCancellationToken_PropagatesCorrectly`: Cancel during execution, verify OperationCanceledException handling
6. `RouteAsync_WithArguments_ParsesCorrectly`: Route `/today --provider OpenAI`, verify args passed to handler
7. `RouteAsync_WithAuthenticationError_ReturnsFailureWithHint`: Simulate auth error, verify hint message

### Integration Tests (CommandExecutionTests.cs)
1. `Router_CanRouteAllSupportedCommands`: Execute each command, verify routing works
2. `Router_PropagatesExceptionsAsFailures`: Trigger exception in handler, verify captured in result

## Dependencies

- `IServiceProvider`: Resolves command handlers dynamically
- `System.CommandLine.RootCommand`: Parses command strings with argument validation
- `ILogger<CommandRouter>`: Logs routing events and errors

## Example Usage

```csharp
var router = serviceProvider.GetRequiredService<ICommandRouter>();
var result = await router.RouteAsync("/today --provider OpenAI", cancellationToken);

if (result.IsSuccess)
{
    AnsiConsole.MarkupLine("[green]Success![/]");
}
else
{
    AnsiConsole.MarkupLine($"[red]Error: {result.Message}[/]");
}
```

## Contract Validation

- [x] Interface defined with XML documentation
- [x] Behavior specified for parsing, resolution, execution
- [x] Error cases enumerated
- [x] Performance requirements stated
- [x] Test scenarios identified
- [x] Dependencies documented
- [x] Supported commands list provided
