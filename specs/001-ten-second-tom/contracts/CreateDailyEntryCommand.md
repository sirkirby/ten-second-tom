# Command Contract: CreateDailyEntryCommand

**Feature**: Today - Daily Reflection Capture  
**Pattern**: CQRS Command  
**Handler**: `CreateDailyEntryHandler`

## Contract Definition

```csharp
public record CreateDailyEntryCommand : IRequest<Result<DailyEntry>>
{
    public required Dictionary<string, string> Responses { get; init; }
    public string? LlmProviderOverride { get; init; }
}
```

## Input Validation

| Field | Rules | Error Message |
|-------|-------|---------------|
| `Responses` | Not null, not empty | "Daily responses cannot be empty" |
| `Responses` | All keys non-empty | "Question text cannot be empty" |
| `Responses` | All values non-empty after trim | "Answer cannot be empty or whitespace" |
| `Responses` | 3-5 key-value pairs | "Daily reflection requires 3-5 responses" |
| `LlmProviderOverride` | If set, must be "OpenAI" or "Anthropic" | "Invalid LLM provider. Use 'OpenAI' or 'Anthropic'" |

## Success Response

**Type**: `Result<DailyEntry>`

**Structure**:
```csharp
{
    IsSuccess = true,
    Value = new DailyEntry
    {
        EntryId = "today-10-01-2025-1",
        Command = "today",
        Timestamp = DateTimeOffset.UtcNow,
        EntryNumber = 1,
        UserInput = "...", // Combined Q&A
        LlmResponse = "...", // Structured markdown summary
        Metadata = new() { ... },
        Summary = new DailySummary { ... }
    },
    Error = null
}
```

## Error Responses

| Error Type | Condition | Response |
|------------|-----------|----------|
| **ValidationError** | Invalid input | `Result.Failure("Validation error: {details}")` |
| **LlmProviderError** | API call failed | `Result.Failure("LLM provider error: {reason}. User input saved for retry.")` |
| **StorageError** | File I/O failure | `Result.Failure("Failed to save entry: {reason}")` |
| **AuthenticationError** | No active session | `Result.Failure("Authentication required. Please login first.")` |

## Test Specification

### Unit Tests (CreateDailyEntryHandlerTests.cs)

```csharp
[Fact]
public async Task Handle_WithValidCommand_CreatesDailyEntry()
[Fact]
public async Task Handle_WithEmptyResponses_ReturnsValidationError()
[Fact]
public async Task Handle_WithFewerThan3Responses_ReturnsValidationError()
[Fact]
public async Task Handle_WithMoreThan5Responses_ReturnsValidationError()
[Fact]
public async Task Handle_WhenLlmProviderFails_SavesUserInputAndReturnsError()
[Fact]
public async Task Handle_WhenStorageFails_ReturnsStorageError()
[Fact]
public async Task Handle_WithOpenAIProvider_UsesOpenAI()
[Fact]
public async Task Handle_WithAnthropicProvider_UsesAnthropic()
[Fact]
public async Task Handle_WithInvalidProvider_ReturnsValidationError()
[Fact]
public async Task Handle_MultipleCallsSameDay_IncrementsEntryNumber()
```

### Integration Tests (DailyEntryWorkflowTests.cs)

```csharp
[Fact]
public async Task CompleteWorkflow_CreatesFileWithCorrectFormat()
[Fact]
public async Task CompleteWorkflow_ParsesLlmResponseIntoSummary()
[Fact]
public async Task CompleteWorkflow_PreservesUserInputExactly()
```

## Handler Implementation Pseudocode

```csharp
public class CreateDailyEntryHandler : IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>
{
    public async Task<Result<DailyEntry>> Handle(CreateDailyEntryCommand request, CancellationToken ct)
    {
        // 1. Validate command
        var validation = ValidateCommand(request);
        if (!validation.IsSuccess) return Result<DailyEntry>.Failure(validation.Error);
        
        // 2. Check authentication
        if (!_authService.IsAuthenticated()) 
            return Result<DailyEntry>.Failure("Authentication required");
        
        // 3. Determine entry number for today
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existingCount = await _storage.CountEntriesAsync("today", today, ct);
        var entryNumber = existingCount + 1;
        
        // 4. Format user input for LLM
        var userInput = FormatUserInput(request.Responses);
        
        // 5. Load prompt template
        var template = await _promptLoader.LoadAsync("daily-summary", ct);
        var prompt = template.Render(new { USER_INPUT = userInput });
        
        // 6. Call LLM provider
        var provider = request.LlmProviderOverride ?? _config.DefaultProvider;
        var llmResult = await _llmFactory.GetProvider(provider).CompletionAsync(prompt, ct);
        if (!llmResult.IsSuccess)
        {
            // Save user input for retry
            await SavePartialEntry(userInput, ct);
            return Result<DailyEntry>.Failure($"LLM error: {llmResult.Error}. Input saved.");
        }
        
        // 7. Parse LLM response into DailySummary
        var summary = ParseDailySummary(llmResult.Value);
        
        // 8. Create DailyEntry
        var entry = new DailyEntry
        {
            EntryId = $"today-{today:MM-dd-yyyy}-{entryNumber}",
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = entryNumber,
            UserInput = userInput,
            LlmResponse = llmResult.Value,
            Metadata = CreateMetadata(provider, llmResult),
            Summary = summary
        };
        
        // 9. Save to storage
        var saveResult = await _storage.SaveAsync(entry, ct);
        if (!saveResult.IsSuccess)
            return Result<DailyEntry>.Failure($"Storage error: {saveResult.Error}");
        
        return Result<DailyEntry>.Success(entry);
    }
}
```

## Example Usage

```csharp
var command = new CreateDailyEntryCommand
{
    Responses = new Dictionary<string, string>
    {
        ["What happened today?"] = "Had a productive meeting about the new feature.",
        ["Anything interesting planned for tomorrow?"] = "Will finalize the design doc.",
        ["Unfinished tasks?"] = "Need to review John's PR.",
        ["How are you feeling?"] = "Energized and focused."
    },
    LlmProviderOverride = "OpenAI"
};

var result = await mediator.Send(command);

if (result.IsSuccess)
{
    Console.WriteLine($"Daily entry created: {result.Value.EntryId}");
    Console.WriteLine($"Saved to: {result.Value.FilePath}");
}
else
{
    Console.Error.WriteLine($"Error: {result.Error}");
}
```

## Contract Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-10-01 | Initial contract definition |
