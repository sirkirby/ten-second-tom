# Data Model: Ten Second Tom

**Date**: October 1, 2025  
**Feature**: Personal Memory Management CLI  
**Phase**: 1 - Data Model Design

## Overview

This document defines the core entities, their relationships, validation rules, and state transitions for the Ten Second Tom application.

---

## Entity Diagram

```
┌─────────────────┐
│   MemoryEntry   │
│                 │
│  - EntryId      │◄──────────┐
│  - Command      │            │
│  - Timestamp    │            │ References
│  - EntryNumber  │            │
│  - UserInput    │            │
│  - LlmResponse  │            │
│  - Metadata     │            │
└─────────────────┘            │
                               │
                               │
                    ┌──────────┴──────────┐
                    │                     │
            ┌───────▼────────┐   ┌───────▼────────┐
            │  DailyEntry    │   │  WeeklyEntry   │
            │                │   │                │
            │  Inherits from │   │  Inherits from │
            │  MemoryEntry   │   │  MemoryEntry   │
            └────────────────┘   └────────────────┘


┌──────────────────┐
│  PromptTemplate  │
│                  │
│  - TemplateId    │
│  - TemplateName  │
│  - TemplateType  │
│  - Content       │
│  - Variables     │
└──────────────────┘


┌──────────────────┐
│  UserSession     │
│                  │
│  - SessionId     │
│  - UserId        │
│  - SshKeyHash    │
│  - CreatedAt     │
│  - IsActive      │
└──────────────────┘


┌──────────────────┐
│ StorageProvider  │
│   Configuration  │
│                  │
│  - MemoryDir     │
│  - RetentionDays │
│  - AutoPurge     │
└──────────────────┘
```

---

## Core Entities

### 1. MemoryEntry (Base)

**Purpose**: Represents a single memory entry with user input and LLM-generated response.

**Properties**:
```csharp
public record MemoryEntry
{
    public required string EntryId { get; init; }           // Format: {command}-{date}-{number}
    public required string Command { get; init; }            // "today" or "thisweek"
    public required DateTimeOffset Timestamp { get; init; }
    public required int EntryNumber { get; init; }           // 1-based daily/weekly counter
    public required string UserInput { get; init; }
    public required string LlmResponse { get; init; }
    public required MemoryEntryMetadata Metadata { get; init; }
    
    // Derived properties
    public string FilePath => Command == "today" 
        ? $".memory/today/{Timestamp:MM-dd-yyyy}_{EntryNumber}.md"
        : $".memory/thisweek/{Timestamp:yyyy-ww}_{EntryNumber}.md";
}

public record MemoryEntryMetadata
{
    public required string LlmProvider { get; init; }        // "OpenAI" or "Anthropic"
    public required string LlmModel { get; init; }           // "gpt-4", "claude-3-sonnet-20240229"
    public int TokensUsed { get; init; }
    public TimeSpan ProcessingDuration { get; init; }
    public Dictionary<string, string> CustomTags { get; init; } = new();
}
```

**Validation Rules**:
- `EntryId` must be unique
- `Command` must be one of: "today", "thisweek"
- `EntryNumber` must be >= 1
- `UserInput` cannot be empty or whitespace
- `LlmResponse` cannot be empty or whitespace
- `Timestamp` must not be in the future
- `LlmProvider` must be one of: "OpenAI", "Anthropic"

**State Transitions**: Immutable (no state changes after creation)

**Relationships**:
- One MemoryEntry maps to one markdown file on disk
- Weekly entries reference multiple daily entries (logically, via date range query)

---

### 2. DailyEntry

**Purpose**: Specialized memory entry for `/today` command reflections.

**Properties**:
```csharp
public record DailyEntry : MemoryEntry
{
    public required DailySummary Summary { get; init; }
}

public record DailySummary
{
    public List<string> KeyEvents { get; init; } = new();
    public List<string> Themes { get; init; } = new();
    public List<TodoItem> TodoItems { get; init; } = new();
    public List<string> ImportantPeople { get; init; } = new();
    public List<string> NotableTasks { get; init; } = new();
}

public record TodoItem
{
    public required string Description { get; init; }
    public bool IsCompleted { get; init; }
    public DateTimeOffset? DueDate { get; init; }
}
```

**Validation Rules**:
- Inherits MemoryEntry validation
- Summary is extracted from LlmResponse (parsed from markdown sections)
- At least one section in Summary should have content

**File Format**:
```markdown
---
command: today
timestamp: 2025-10-01T14:30:00Z
entry-number: 1
llm-provider: OpenAI
llm-model: gpt-4
tokens-used: 1500
processing-duration: 3.2s
---

# User Input

**What happened today?**
> Had a productive morning meeting with the team about the new feature.

**Anything interesting planned for tomorrow?**
> Planning to finalize the design document and start implementation.

# LLM Summary

## Key Events
- Productive team meeting about new feature
- Made progress on project planning

## Themes
- Collaboration
- Feature planning

## To-Do Items
- [ ] Finalize design document
- [ ] Start implementation
- [ ] Review PR from John

## Important People
- Team members from morning meeting

## Notable Tasks
- Design document completion
```

---

### 3. WeeklyEntry

**Purpose**: Specialized memory entry for `/thisweek` command reviews.

**Properties**:
```csharp
public record WeeklyEntry : MemoryEntry
{
    public required WeeklySummary Summary { get; init; }
    public required DateRange WeekRange { get; init; }
    public int DailyEntriesCount { get; init; }
}

public record WeeklySummary
{
    public List<string> TopAccomplishments { get; init; } = new();  // Exactly 3
    public List<string> TopChallenges { get; init; } = new();       // Exactly 3
    public List<string> RecurringThemes { get; init; } = new();
    public List<string> InteractionPatterns { get; init; } = new();
    public List<string> NextWeekSuggestions { get; init; } = new();
}

public record DateRange
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    
    public TimeSpan Duration => End - Start;
    public int DayCount => (int)Duration.TotalDays + 1;
}
```

**Validation Rules**:
- Inherits MemoryEntry validation
- `TopAccomplishments` must contain exactly 3 items
- `TopChallenges` must contain exactly 3 items
- `WeekRange.Start` must be before `WeekRange.End`
- `WeekRange` should span approximately 7 days (allow 3-10 days)
- `DailyEntriesCount` must be >= 0

**File Format**:
```markdown
---
command: thisweek
timestamp: 2025-10-06T10:00:00Z
entry-number: 1
week-range: 2025-09-30 to 2025-10-06
daily-entries-count: 5
llm-provider: Anthropic
llm-model: claude-3-sonnet-20240229
tokens-used: 2400
processing-duration: 5.8s
---

# Weekly Review: Week 40, 2025

## Top 3 Accomplishments
1. Completed feature specification for Ten Second Tom
2. Resolved 8 critical clarifications improving implementation clarity
3. Designed comprehensive technical architecture

## Top 3 Challenges
1. Balancing feature scope with v1 simplicity
2. Selecting appropriate LLM provider SDKs (no official Anthropic .NET SDK)
3. Designing extensible storage without premature optimization

## Recurring Themes
- Test-driven development emphasis
- Cross-platform compatibility requirements
- Secrets management security

## Interaction Patterns
- Daily clarification sessions refining requirements
- Collaborative decision-making on technical stack
- Iterative spec refinement

## Suggestions for Next Week
- Begin Phase 1 contract design
- Set up initial project structure
- Configure CI/CD pipeline
```

---

### 4. PromptTemplate

**Purpose**: Manages LLM prompt templates for different commands.

**Properties**:
```csharp
public record PromptTemplate
{
    public required string TemplateId { get; init; }         // "daily-summary", "weekly-review"
    public required string TemplateName { get; init; }       // "Daily Summary Template"
    public required TemplateType Type { get; init; }
    public required string Content { get; init; }            // Markdown template with placeholders
    public List<string> Variables { get; init; } = new();    // ["USER_INPUT", "DATE"]
    public DateTimeOffset LastModified { get; init; }
}

public enum TemplateType
{
    DailySummary,
    WeeklyReview,
    SearchInsight
}
```

**Validation Rules**:
- `TemplateId` must be unique
- `Content` must contain valid markdown
- All `Variables` must appear in `Content` as `{{VARIABLE_NAME}}`
- `Variables` must be uppercase with underscores

**Template Storage**:
- Embedded resources: `src/Infrastructure/Prompts/Templates/{template-id}.md`
- User overrides: `.memory/templates/{template-id}.md` (optional)

**Variable Substitution**:
```csharp
public string RenderTemplate(PromptTemplate template, Dictionary<string, string> values)
{
    string rendered = template.Content;
    foreach (var variable in template.Variables)
    {
        if (values.TryGetValue(variable, out var value))
        {
            rendered = rendered.Replace($"{{{{{variable}}}}}", value);
        }
    }
    return rendered;
}
```

---

### 5. UserSession

**Purpose**: Tracks authenticated user sessions with SSH key-based auth.

**Properties**:
```csharp
public record UserSession
{
    public required Guid SessionId { get; init; }
    public required string UserId { get; init; }             // SSH key fingerprint
    public required string SshKeyHash { get; init; }         // SHA256 hash of public key
    public required DateTimeOffset CreatedAt { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset? LoggedOutAt { get; init; }
}
```

**Validation Rules**:
- `SessionId` must be unique
- `UserId` must be valid SSH key fingerprint format
- `SshKeyHash` must be 64-character hex string (SHA256)
- `CreatedAt` must not be in the future
- If `LoggedOutAt` is set, `IsActive` must be false

**State Transitions**:
```
[Created] --login--> [Active]
[Active] --logout--> [Inactive]
```

**Storage**: JSON file in app config directory
- Location: `~/.config/ten-second-tom/session.json` (Linux/macOS)
- Location: `%APPDATA%\TenSecondTom\session.json` (Windows)

---

### 6. StorageConfiguration

**Purpose**: Configuration for memory storage behavior.

**Properties**:
```csharp
public record StorageConfiguration
{
    public required string MemoryDirectory { get; init; }    // Default: "./.memory"
    public RetentionPolicy RetentionPolicy { get; init; } = RetentionPolicy.Indefinite;
    public int? RetentionDays { get; init; }                 // Required if policy != Indefinite
    public bool AutoPurgeEnabled { get; init; } = true;
    public TimeSpan PurgeCheckInterval { get; init; } = TimeSpan.FromDays(1);
}

public enum RetentionPolicy
{
    Indefinite,
    Days30,
    Days90,
    OneYear,
    TwoYears
}
```

**Validation Rules**:
- `MemoryDirectory` must be a valid directory path
- If `RetentionPolicy` != Indefinite, `RetentionDays` must be set
- `RetentionDays` must be > 0
- `PurgeCheckInterval` must be > 0

**Mapping**:
```csharp
RetentionPolicy.Days30 => RetentionDays = 30
RetentionPolicy.Days90 => RetentionDays = 90
RetentionPolicy.OneYear => RetentionDays = 365
RetentionPolicy.TwoYears => RetentionDays = 730
RetentionPolicy.Indefinite => RetentionDays = null
```

---

## Query Models

### SearchQuery

**Purpose**: Represents user search request against memory store.

**Properties**:
```csharp
public record SearchQuery
{
    public required string QueryText { get; init; }
    public DateRange? DateRange { get; init; }
    public List<string> Commands { get; init; } = new();     // Filter by "today" or "thisweek"
    public List<string> Tags { get; init; } = new();
    public int MaxResults { get; init; } = 50;
}
```

**Validation Rules**:
- `QueryText` cannot be empty or whitespace
- `MaxResults` must be between 1 and 1000

---

## Command Models (CQRS)

### CreateDailyEntryCommand

```csharp
public record CreateDailyEntryCommand : IRequest<Result<DailyEntry>>
{
    public required Dictionary<string, string> Responses { get; init; }  // Question -> Answer
    public string? LlmProviderOverride { get; init; }                    // Optional provider selection
}
```

### CreateWeeklyReviewCommand

```csharp
public record CreateWeeklyReviewCommand : IRequest<Result<WeeklyEntry>>
{
    public DateRange? CustomDateRange { get; init; }                     // Default: past 7 days
    public string? LlmProviderOverride { get; init; }
}
```

### SearchMemoriesQuery

```csharp
public record SearchMemoriesQuery : IRequest<Result<List<MemoryEntry>>>
{
    public required SearchQuery Query { get; init; }
}
```

### LogoutCommand

```csharp
public record LogoutCommand : IRequest<Result<bool>>
{
    public Guid SessionId { get; init; }
}
```

---

## Data Validation Summary

| Entity | Key Validations |
|--------|-----------------|
| MemoryEntry | Non-empty input/response, valid command, positive entry number |
| DailyEntry | Valid summary with at least one section populated |
| WeeklyEntry | Exactly 3 accomplishments + 3 challenges, valid date range |
| PromptTemplate | Valid markdown, all variables present in content |
| UserSession | Valid SSH key formats, consistent active state |
| StorageConfiguration | Valid paths, consistent retention settings |

---

## Indexes & Search

**File System Indexes**:
- Directory structure provides date-based indexing
- File naming convention enables chronological sorting
- Frontmatter metadata enables filtering

**Search Strategy**:
1. Read all markdown files in memory directory
2. Parse frontmatter for metadata filtering
3. Full-text search in markdown content
4. Rank by relevance (keyword frequency + recency)
5. Return top N results

**Future Optimization**:
- SQLite FTS5 for full-text search
- Cached index file for faster searches
- Incremental index updates

---

## Persistence Format

All entities are persisted as markdown files with YAML frontmatter:

**Frontmatter** (metadata):
```yaml
command: today
timestamp: 2025-10-01T14:30:00Z
entry-number: 1
llm-provider: OpenAI
llm-model: gpt-4
tokens-used: 1500
processing-duration: 3.2s
```

**Body** (content):
- User Input section (original questions and answers)
- LLM Summary section (structured response)

This format enables:
- Human readability without tools
- Easy version control (text diffs)
- Simple migration to database (parse frontmatter + body)
- Direct editing by users if needed

---

## Next Steps

✅ Data model complete
✅ Ready to proceed to contracts design
✅ Entity validation rules defined
✅ Persistence format specified

**Next**: Create command/query contracts in `/contracts/` directory
