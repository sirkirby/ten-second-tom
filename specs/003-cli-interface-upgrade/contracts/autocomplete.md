# Contract: Autocomplete Engine

**Component**: `AutocompleteEngine`  
**Namespace**: `TenSecondTom.Features.Shell.Services`  
**Purpose**: Provides command suggestions based on partial input

## Interface Contract

```csharp
public interface IAutocompleteEngine
{
    /// <summary>
    /// Gets command suggestions for partial input.
    /// </summary>
    /// <param name="partialInput">The partial command text entered by the user.</param>
    /// <returns>List of suggestions ranked by relevance (max 10).</returns>
    IReadOnlyList<AutocompleteSuggestion> GetSuggestions(string partialInput);
}

public record AutocompleteSuggestion(
    string CommandName,
    string HelpText,
    int MatchScore);
```

## Behavior Contract

### Suggestion Generation
- **Input**: Partial command text (e.g., "/tod", "/thi")
- **Preconditions**: Input is not null (empty string is valid)
- **Actions**:
  1. If input doesn't start with '/', return empty list
  2. Normalize input to lowercase for matching
  3. Filter commands where name starts with input
  4. Rank by match score (exact prefix match > fuzzy match)
  5. Limit to top 10 suggestions
- **Output**: Ordered list of AutocompleteSuggestion

### Match Scoring Algorithm
- **Exact prefix match**: Score = 100 - (command length - input length)
  - Example: "/today" matching "/tod" = 100 - (6 - 4) = 98
- **Case-insensitive prefix**: Score = 90 - (command length - input length)
- **Substring match**: Score = 50 - position_index
- **No match**: Not included in results

### Suggestion Display Format
- **Format**: `{CommandName} - {HelpText}`
- **Example**: `/today - Capture today's reflection with 3-5 prompts`
- **Truncation**: HelpText truncated to 60 characters if longer
- **Styling**: Command name in bold, help text in dim gray (via Spectre.Console)

## Command Catalog

### Static Command Metadata
```csharp
private static readonly List<CommandMetadata> Commands = new()
{
    new("/today", "Capture today's reflection with 3-5 prompts"),
    new("/thisweek", "Generate a weekly review from recent daily entries"),
    new("/search", "Search memory entries by text query"),
    new("/login", "Authenticate with SSH key and create a session"),
    new("/logout", "Log out and invalidate the current session"),
    new("/quit", "Exit the shell", Aliases: new[] { "/exit" }),
    new("/help", "Display available commands with descriptions"),
};
```

### Alias Handling
- Aliases are treated as separate commands for autocomplete purposes
- Example: Typing "/ex" suggests both "/exit" and "/exec" (if implemented)
- Primary command name is displayed in suggestion, alias noted in help text

## Error Handling Contract

### Invalid Input
- **Null input**: Throw ArgumentNullException (precondition violation)
- **Empty input**: Return empty list (no suggestions)
- **Non-slash input**: Return empty list (not a command)
- **Input too long (>50 chars)**: Return empty list (invalid command)

### Empty Catalog
- **No commands registered**: Return empty list, log warning
- **All commands filtered out**: Return empty list (normal behavior)

## Performance Contract

- **Suggestion generation**: < 100ms for any input (constitutional requirement)
- **Typical latency**: < 10ms for prefix matching on 10 commands
- **Memory overhead**: < 10KB for command catalog (static data)
- **Scalability**: O(n) where n = number of commands (max 50 expected)

## Testing Contract

### Unit Tests (AutocompleteEngineTests.cs)
1. `GetSuggestions_WithValidPrefix_ReturnsSuggestions`: Input "/tod", expect ["/today"]
2. `GetSuggestions_WithEmptyInput_ReturnsEmptyList`: Input "", expect []
3. `GetSuggestions_WithoutSlashPrefix_ReturnsEmptyList`: Input "today", expect []
4. `GetSuggestions_WithNoMatches_ReturnsEmptyList`: Input "/xyz", expect []
5. `GetSuggestions_WithMultipleMatches_ReturnsRankedList`: Input "/t", expect ["/today", "/thisweek"] (ranked)
6. `GetSuggestions_WithExactMatch_ReturnsSingleSuggestion`: Input "/today", expect ["/today"]
7. `GetSuggestions_LimitsToTenResults`: Input "/", expect max 10 suggestions
8. `GetSuggestions_IncludesAliases`: Input "/ex", expect ["/exit"] (alias of /quit)

### Integration Tests
- Autocomplete is tested as part of ReplLoop integration tests (keyboard navigation)

## Dependencies

- None (self-contained, uses static command catalog)
- Optional: `ILogger<AutocompleteEngine>` for diagnostics

## Example Usage

```csharp
var engine = new AutocompleteEngine();
var suggestions = engine.GetSuggestions("/tod");

foreach (var suggestion in suggestions)
{
    Console.WriteLine($"{suggestion.CommandName} - {suggestion.HelpText}");
}

// Output:
// /today - Capture today's reflection with 3-5 prompts
```

## Integration with Spectre.Console

```csharp
public class CommandAutoCompleteSource : IAutoCompleteSource
{
    private readonly IAutocompleteEngine _engine;
    
    public IEnumerable<string> GetSuggestions(string text, int cursorIndex)
    {
        var suggestions = _engine.GetSuggestions(text);
        return suggestions.Select(s => $"{s.CommandName} - {s.HelpText}");
    }
}
```

## Contract Validation

- [x] Interface defined with XML documentation
- [x] Behavior specified for suggestion generation and ranking
- [x] Match scoring algorithm documented
- [x] Command catalog provided
- [x] Error cases enumerated
- [x] Performance requirements stated
- [x] Test scenarios identified
- [x] Integration pattern with Spectre.Console shown
