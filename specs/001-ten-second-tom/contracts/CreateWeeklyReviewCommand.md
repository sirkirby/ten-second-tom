# Command Contract: CreateWeeklyReviewCommand

**Feature**: ThisWeek - Weekly Reflection Review  
**Pattern**: CQRS Command  
**Handler**: `CreateWeeklyReviewHandler`

## Contract Definition

```csharp
public record CreateWeeklyReviewCommand : IRequest<Result<WeeklyEntry>>
{
    public DateRange? CustomDateRange { get; init; }
    public string? LlmProviderOverride { get; init; }
}
```

## Input Validation

| Field | Rules | Error Message |
|-------|-------|---------------|
| `CustomDateRange` | If set, Start < End | "Invalid date range: start must be before end" |
| `CustomDateRange` | If set, End not in future | "Date range cannot extend into the future" |
| `CustomDateRange` | If set, duration 3-10 days | "Date range must be between 3-10 days" |
| `LlmProviderOverride` | If set, must be "OpenAI" or "Anthropic" | "Invalid LLM provider" |

## Success Response

**Type**: `Result<WeeklyEntry>`

**Structure**:
```csharp
{
    IsSuccess = true,
    Value = new WeeklyEntry
    {
        EntryId = "thisweek-2025-40-1",
        Command = "thisweek",
        Timestamp = DateTimeOffset.UtcNow,
        EntryNumber = 1,
        UserInput = "{DailyEntriesCount} daily entries from {StartDate} to {EndDate}",
        LlmResponse = "...", // Structured weekly summary
        Metadata = new() { ... },
        Summary = new WeeklySummary
        {
            TopAccomplishments = [...], // Exactly 3
            TopChallenges = [...],      // Exactly 3
            RecurringThemes = [...],
            InteractionPatterns = [...],
            NextWeekSuggestions = [...]
        },
        WeekRange = new DateRange { Start = ..., End = ... },
        DailyEntriesCount = 5
    },
    Error = null
}
```

## Error Responses

| Error Type | Condition | Response |
|------------|-----------|----------|
| **ValidationError** | Invalid date range | `Result.Failure("Validation error: {details}")` |
| **NoDataError** | No daily entries in range | `Result.Failure("No daily entries found for the specified period")` |
| **LlmProviderError** | API call failed | `Result.Failure("LLM provider error: {reason}")` |
| **StorageError** | File I/O failure | `Result.Failure("Failed to save weekly review: {reason}")` |
| **AuthenticationError** | No active session | `Result.Failure("Authentication required")` |

## Test Specification

### Unit Tests (CreateWeeklyReviewHandlerTests.cs)

```csharp
[Fact]
public async Task Handle_WithValidCommand_CreatesWeeklyReview()
[Fact]
public async Task Handle_WithNoDailyEntries_ReturnsNoDataError()
[Fact]
public async Task Handle_WithCustomDateRange_UsesCustomRange()
[Fact]
public async Task Handle_WithoutCustomDateRange_UsesLast7Days()
[Fact]
public async Task Handle_WhenLlmProviderFails_ReturnsError()
[Fact]
public async Task Handle_WithFewerThan7Days_Succeeds()
[Fact]
public async Task Handle_WithFewerThan3Days_ReturnsValidationError()
[Fact]
public async Task Handle_EnsuresExactly3Accomplishments()
[Fact]
public async Task Handle_EnsuresExactly3Challenges()
[Fact]
public async Task Handle_AggregatesMultipleDailyEntriesPerDay()
```

### Integration Tests (WeeklyReviewWorkflowTests.cs)

```csharp
[Fact]
public async Task CompleteWorkflow_AggregatesDailyEntries()
[Fact]
public async Task CompleteWorkflow_CreatesFileWithCorrectWeekNumber()
[Fact]
public async Task CompleteWorkflow_ParsesLlmResponseCorrectly()
```

## Handler Implementation Pseudocode

```csharp
public class CreateWeeklyReviewHandler : IRequestHandler<CreateWeeklyReviewCommand, Result<WeeklyEntry>>
{
    public async Task<Result<WeeklyEntry>> Handle(CreateWeeklyReviewCommand request, CancellationToken ct)
    {
        // 1. Validate command
        var validation = ValidateCommand(request);
        if (!validation.IsSuccess) return Result<WeeklyEntry>.Failure(validation.Error);
        
        // 2. Check authentication
        if (!_authService.IsAuthenticated()) 
            return Result<WeeklyEntry>.Failure("Authentication required");
        
        // 3. Determine date range (custom or default last 7 days)
        var dateRange = request.CustomDateRange ?? GetLast7Days();
        
        // 4. Retrieve daily entries for the week
        var dailyEntries = await _storage.GetEntriesAsync("today", dateRange, ct);
        if (dailyEntries.Count == 0)
            return Result<WeeklyEntry>.Failure("No daily entries found for the specified period");
        
        // 5. Determine entry number for this week
        var weekNumber = GetIsoWeekNumber(dateRange.End);
        var year = dateRange.End.Year;
        var existingCount = await _storage.CountWeeklyEntriesAsync(year, weekNumber, ct);
        var entryNumber = existingCount + 1;
        
        // 6. Aggregate daily summaries for LLM input
        var aggregatedInput = AggregateDailySummaries(dailyEntries);
        
        // 7. Load prompt template
        var template = await _promptLoader.LoadAsync("weekly-review", ct);
        var prompt = template.Render(new 
        { 
            DAILY_SUMMARIES = aggregatedInput,
            START_DATE = dateRange.Start.ToString("yyyy-MM-dd"),
            END_DATE = dateRange.End.ToString("yyyy-MM-dd"),
            ENTRY_COUNT = dailyEntries.Count
        });
        
        // 8. Call LLM provider
        var provider = request.LlmProviderOverride ?? _config.DefaultProvider;
        var llmResult = await _llmFactory.GetProvider(provider).CompletionAsync(prompt, ct);
        if (!llmResult.IsSuccess)
            return Result<WeeklyEntry>.Failure($"LLM error: {llmResult.Error}");
        
        // 9. Parse LLM response into WeeklySummary
        var summary = ParseWeeklySummary(llmResult.Value);
        if (summary.TopAccomplishments.Count != 3 || summary.TopChallenges.Count != 3)
            return Result<WeeklyEntry>.Failure("Invalid LLM response: must contain exactly 3 accomplishments and 3 challenges");
        
        // 10. Create WeeklyEntry
        var entry = new WeeklyEntry
        {
            EntryId = $"thisweek-{year}-{weekNumber:D2}-{entryNumber}",
            Command = "thisweek",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = entryNumber,
            UserInput = $"{dailyEntries.Count} daily entries from {dateRange.Start:yyyy-MM-dd} to {dateRange.End:yyyy-MM-dd}",
            LlmResponse = llmResult.Value,
            Metadata = CreateMetadata(provider, llmResult),
            Summary = summary,
            WeekRange = dateRange,
            DailyEntriesCount = dailyEntries.Count
        };
        
        // 11. Save to storage
        var saveResult = await _storage.SaveAsync(entry, ct);
        if (!saveResult.IsSuccess)
            return Result<WeeklyEntry>.Failure($"Storage error: {saveResult.Error}");
        
        return Result<WeeklyEntry>.Success(entry);
    }
}
```

## Example Usage

```csharp
// Default: last 7 days
var command1 = new CreateWeeklyReviewCommand
{
    LlmProviderOverride = "Anthropic"
};

// Custom date range
var command2 = new CreateWeeklyReviewCommand
{
    CustomDateRange = new DateRange
    {
        Start = new DateTimeOffset(2025, 9, 30, 0, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2025, 10, 6, 23, 59, 59, TimeSpan.Zero)
    }
};

var result = await mediator.Send(command1);

if (result.IsSuccess)
{
    Console.WriteLine($"Weekly review created: {result.Value.EntryId}");
    Console.WriteLine($"Analyzed {result.Value.DailyEntriesCount} daily entries");
    Console.WriteLine($"\nTop 3 Accomplishments:");
    foreach (var acc in result.Value.Summary.TopAccomplishments)
        Console.WriteLine($"  - {acc}");
}
```

## Contract Changelog

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2025-10-01 | Initial contract definition |
