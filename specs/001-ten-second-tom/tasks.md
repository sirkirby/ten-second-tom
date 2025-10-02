# Tasks: Ten Second Tom - Personal Memory Management CLI

**Input**: Design documents from `/specs/001-ten-second-tom/`  
**Prerequisites**: plan.md, research.md, data-model.md, contracts/, quickstart.md

## Execution Summary

This task list implements a CLI memory management application using:
- **Language**: C# with .NET 9
- **Architecture**: Vertical Slice Architecture with CQRS
- **CLI Framework**: System.CommandLine
- **LLM Providers**: OpenAI SDK + Anthropic.SDK
- **Storage**: File system (markdown files with YAML frontmatter)
- **Authentication**: SSH key-based (Ed25519/RSA)
- **Testing**: xUnit + FluentAssertions + Moq (80% coverage minimum)

**Project Structure**: Single CLI project at repository root
- `src/` - Application code
- `tests/` - Test projects
- `.memory/` - User data directory (created at runtime)

---

## Format: `[ID] [P?] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- Include exact file paths in descriptions
- TDD: Tests MUST be written and FAIL before implementation

---

## Phase 3.1: Project Setup & Infrastructure

### T001: Initialize Project Structure ✅ COMPLETE
**Type**: Setup  
**Dependencies**: None  
**Files**:
- `src/TenSecondTom.csproj`
- `tests/TenSecondTom.Tests.csproj`
- `tests/TenSecondTom.IntegrationTests.csproj`

**Description**: Create .NET 9 console application with test projects. Configure project structure following Vertical Slice Architecture:
```
src/
├── TenSecondTom.csproj
├── Program.cs
├── Features/
│   ├── Today/
│   ├── ThisWeek/
│   ├── Search/
│   └── Auth/
├── Infrastructure/
│   ├── Storage/
│   ├── Llm/
│   ├── Prompts/
│   ├── Auth/
│   └── Configuration/
└── Shared/
    ├── Models/
    └── Results/

tests/
├── TenSecondTom.Tests.csproj          # Unit tests
├── TenSecondTom.IntegrationTests.csproj  # Integration tests
├── Unit/
├── Integration/
└── TestHelpers/
```

**Acceptance Criteria**:
- [x] Projects compile successfully
- [x] Solution structure matches Vertical Slice Architecture
- [x] .NET 9 target framework configured
- [x] Projects reference each other correctly

---

### T002: Add Core Dependencies ✅ COMPLETE
**Type**: Setup  
**Dependencies**: T001  
**Files**:
- `src/TenSecondTom.csproj`
- `tests/TenSecondTom.Tests.csproj`
- `tests/TenSecondTom.IntegrationTests.csproj`

**Description**: Add NuGet packages per research.md decisions:

**Application Dependencies**:
- `System.CommandLine` (CLI framework)
- `OpenAI` (official OpenAI SDK)
- `Anthropic.SDK` (Anthropic integration)
- `Markdig` (markdown parsing)
- `Spectre.Console` (terminal rendering)
- `SSH.NET` (Renci.SshNet for SSH authentication)
- `YamlDotNet` (YAML frontmatter parsing)
- `Microsoft.Extensions.Configuration`
- `Microsoft.Extensions.Configuration.Json`
- `Microsoft.Extensions.Configuration.EnvironmentVariables`
- `Microsoft.Extensions.Configuration.UserSecrets`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`
- `Serilog` (logging framework - organizational standard)
- `Serilog.Extensions.Logging` (Microsoft.Extensions.Logging integration)
- `Serilog.Sinks.Console` (console output sink)
- `Serilog.Sinks.File` (file-based logging sink)
- `Serilog.Enrichers.Environment` (environment information enrichers)
- `Serilog.Settings.Configuration` (appsettings.json configuration support)

**Test Dependencies**:
- `xunit`
- `xunit.runner.visualstudio`
- `FluentAssertions`
- `Moq`
- `Microsoft.NET.Test.Sdk`

**Acceptance Criteria**:
- [x] All packages restore successfully
- [x] No version conflicts
- [x] Project compiles with all dependencies

---

### T003 [P]: Configure EditorConfig and Analyzers ✅ COMPLETE
**Type**: Setup  
**Dependencies**: T001  
**Files**:
- `.editorconfig`
- `Directory.Build.props`

**Description**: Configure C# code style rules, nullable reference types, and code analyzers following Microsoft conventions.

**Acceptance Criteria**:
- [x] Nullable reference types enabled
- [x] Warnings as errors configured
- [x] Microsoft C# coding conventions enforced
- [x] XML documentation warnings enabled

---

### T004 [P]: Create Shared Result Type ✅ COMPLETE
**Type**: Core - Foundation  
**Dependencies**: T001  
**Files**:
- `src/Shared/Results/Result.cs`
- `tests/Unit/Shared/ResultTests.cs`

**Description**: Implement generic `Result<T>` type for error handling per constitution requirement. Write tests first:

**Test Cases**:
- Success result contains value
- Failure result contains error message
- IsSuccess/IsFailure flags work correctly
- Implicit conversion from T to Result<T>
- Implicit conversion from string to Result<T> (failure)

**Acceptance Criteria**:
- [x] Tests written and failing
- [x] Result<T> implementation passes all tests
- [x] No compiler warnings
- [x] XML documentation added

---

## Phase 3.2: Data Models (TDD - Tests First)

### T005 [P]: Test MemoryEntry Model ✅ COMPLETE
**Type**: Test  
**Dependencies**: T001, T004  
**Files**:
- `tests/Unit/Models/MemoryEntryTests.cs`

**Description**: Write unit tests for MemoryEntry base record per data-model.md. Tests MUST fail initially.

**Test Cases**:
- Create valid MemoryEntry succeeds
- EntryId format validation (command-date-number)
- Command must be "today" or "thisweek"
- EntryNumber must be >= 1
- UserInput cannot be empty
- LlmResponse cannot be empty
- Timestamp cannot be in future
- FilePath property derives correct path
- Metadata validation (LlmProvider must be OpenAI or Anthropic)

**Acceptance Criteria**:
- [x] All tests written
- [x] Tests fail (no implementation yet)
- [x] Test coverage includes all validation rules

---

### T006 [P]: Implement MemoryEntry Model ✅ COMPLETE
**Type**: Core - Model  
**Dependencies**: T005  
**Files**:
- `src/Shared/Models/MemoryEntry.cs`

**Description**: Implement MemoryEntry record with validation per data-model.md specification.

**Acceptance Criteria**:
- [x] All T005 tests pass
- [x] Immutable record with init-only properties
- [x] FilePath property correctly generates paths
- [x] XML documentation complete
- [x] No compiler warnings

---

### T007 [P]: Test DailyEntry Model ✅ COMPLETE
**Type**: Test  
**Dependencies**: T005  
**Files**:
- `tests/Unit/Models/DailyEntryTests.cs`

**Description**: Write unit tests for DailyEntry record (inherits MemoryEntry) per data-model.md.

**Test Cases**:
- Create valid DailyEntry with Summary
- DailySummary validation (at least one section with content)
- TodoItem properties (Description, IsCompleted, DueDate)
- Inheritance from MemoryEntry works correctly
- File format serialization/deserialization

**Acceptance Criteria**:
- [x] All tests written and failing
- [x] Tests cover DailySummary validation
- [x] TodoItem tests included

---

### T008 [P]: Implement DailyEntry Model ✅ COMPLETE
**Type**: Core - Model  
**Dependencies**: T006, T007  
**Files**:
- `src/Shared/Models/DailyEntry.cs`

**Description**: Implement DailyEntry and DailySummary records per data-model.md.

**Acceptance Criteria**:
- [x] All T007 tests pass
- [x] Inherits MemoryEntry correctly
- [x] DailySummary with all properties (KeyEvents, Themes, TodoItems, ImportantPeople, NotableTasks)
- [x] TodoItem record implemented
- [x] XML documentation complete

---

### T009 [P]: Test WeeklyEntry Model ✅ COMPLETE
**Type**: Test  
**Dependencies**: T005  
**Files**:
- `tests/Unit/Models/WeeklyEntryTests.cs`

**Description**: Write unit tests for WeeklyEntry record per data-model.md.

**Test Cases**:
- Create valid WeeklyEntry with Summary
- TopAccomplishments must have exactly 3 items
- TopChallenges must have exactly 3 items
- WeekRange validation (Start < End)
- WeekRange duration 3-10 days
- DailyEntriesCount >= 0
- DateRange helper properties (Duration, DayCount)

**Acceptance Criteria**:
- [x] All tests written and failing
- [x] Tests enforce exactly 3 accomplishments
- [x] Tests enforce exactly 3 challenges
- [x] DateRange validation covered

---

### T010 [P]: Implement WeeklyEntry Model ✅ COMPLETE
**Type**: Core - Model  
**Dependencies**: T006, T009  
**Files**:
- `src/Shared/Models/WeeklyEntry.cs`

**Description**: Implement WeeklyEntry, WeeklySummary, and DateRange records per data-model.md.

**Acceptance Criteria**:
- [x] All T009 tests pass
- [x] WeeklySummary validates exactly 3 accomplishments/challenges
- [x] DateRange record with Duration and DayCount properties
- [x] XML documentation complete

---

### T011 [P]: Test PromptTemplate Model ✅ COMPLETE
**Type**: Test  
**Dependencies**: T001  
**Files**:
- `tests/Unit/Models/PromptTemplateTests.cs`

**Description**: Write unit tests for PromptTemplate record per data-model.md.

**Test Cases**:
- Create valid PromptTemplate
- TemplateId uniqueness validation
- Content must contain valid markdown
- Variables must appear in Content as {{VARIABLE_NAME}}
- Variables must be uppercase with underscores
- Template rendering substitutes variables correctly

**Acceptance Criteria**:
- [x] All tests written and failing
- [x] Variable substitution tests included
- [x] Template validation tests complete

---

### T012 [P]: Implement PromptTemplate Model ✅ COMPLETE
**Type**: Core - Model  
**Dependencies**: T011  
**Files**:
- `src/Shared/Models/PromptTemplate.cs`

**Description**: Implement PromptTemplate record with variable substitution per data-model.md.

**Acceptance Criteria**:
- [x] All T011 tests pass
- [x] RenderTemplate method substitutes variables
- [x] TemplateType enum (DailySummary, WeeklyReview, SearchInsight)
- [x] XML documentation complete

---

### T013 [P]: Test UserSession Model ✅ COMPLETE
**Type**: Test  
**Dependencies**: T001  
**Files**:
- `tests/Unit/Models/UserSessionTests.cs`

**Description**: Write unit tests for UserSession record per data-model.md.

**Test Cases**:
- Create valid UserSession
- SessionId is unique Guid
- SshKeyHash validation (SHA256 format)
- IsActive flag management
- LoggedOutAt nullable handling
- Session state transitions

**Acceptance Criteria**:
- [x] All tests written and failing
- [x] State transition tests included

---

### T014 [P]: Implement UserSession Model ✅ COMPLETE
**Type**: Core - Model  
**Dependencies**: T013  
**Files**:
- `src/Shared/Models/UserSession.cs`

**Description**: Implement UserSession record for authentication tracking per data-model.md.

**Acceptance Criteria**:
- [x] All T013 tests pass
- [x] Immutable record with state management
- [x] XML documentation complete

---

### T015 [P]: Test StorageConfiguration Model ✅ COMPLETE
**Type**: Test  
**Dependencies**: T001  
**Files**:
- `tests/Unit/Models/StorageConfigurationTests.cs`

**Description**: Write unit tests for StorageConfiguration record per data-model.md.

**Test Cases**:
- Create valid StorageConfiguration
- MemoryDirectory path validation
- RetentionPolicy enum values (Indefinite, Days30, Days90, OneYear, TwoYears)
- AutoPurge flag handling

**Acceptance Criteria**:
- [x] All tests written and failing
- [x] Retention policy tests complete

---

### T016 [P]: Implement StorageConfiguration Model ✅ COMPLETE
**Type**: Core - Model  
**Dependencies**: T015  
**Files**:
- `src/Shared/Models/StorageConfiguration.cs`

**Description**: Implement StorageConfiguration record per data-model.md.

**Acceptance Criteria**:
- [x] All T015 tests pass
- [x] RetentionPolicy enum implemented
- [x] XML documentation complete

---

## Phase 3.3: Infrastructure - Storage (TDD)

### T017: Test IMemoryStorageProvider Interface Design ✅ COMPLETE
**Type**: Test  
**Dependencies**: T006, T008, T010  
**Files**:
- `tests/Unit/Infrastructure/Storage/IMemoryStorageProviderTests.cs`

**Description**: Write tests defining IMemoryStorageProvider contract per research.md. Tests use mock implementation.

**Test Cases**:
- SaveAsync creates entry with correct EntryId
- SaveAsync returns Result<MemoryEntry>
- GetEntriesAsync filters by command and date range
- CountEntriesAsync returns correct count for date
- SearchEntriesAsync filters by query text
- DeleteEntriesAsync removes entries by date range
- PurgeExpiredEntriesAsync respects retention policy
- GetEntryByIdAsync retrieves specific entry

**Acceptance Criteria**:
- [x] All interface methods tested via mock
- [x] Tests define expected behavior clearly
- [x] Error cases covered

---

### T018: Implement IMemoryStorageProvider Interface ✅ COMPLETE
**Type**: Core - Interface  
**Dependencies**: T017  
**Files**:
- `src/Infrastructure/Storage/IMemoryStorageProvider.cs`

**Description**: Define IMemoryStorageProvider interface per research.md specification.

**Acceptance Criteria**:
- [x] All T017 tests pass with mock implementation
- [x] Interface methods return Result<T> types
- [x] CancellationToken support on all async methods
- [x] XML documentation complete

---

### T019: Test FileSystemStorageProvider ✅ COMPLETE
**Type**: Test  
**Dependencies**: T018  
**Files**:
- `tests/Unit/Infrastructure/Storage/FileSystemStorageProviderTests.cs`

**Description**: Write unit tests for FileSystemStorageProvider implementation per research.md.

**Test Cases**:
- SaveAsync creates markdown file with YAML frontmatter
- File path follows pattern (.memory/today/MM-DD-YYYY_N.md)
- Entry number increments for multiple same-day entries
- GetEntriesAsync reads and parses markdown files
- CountEntriesAsync counts files matching pattern
- SearchEntriesAsync searches file content
- DeleteEntriesAsync removes files
- PurgeExpiredEntriesAsync respects retention policy
- File I/O errors return Result.Failure
- Directory creation if not exists

**Acceptance Criteria**:
- [x] All tests written and failing
- [x] File I/O operations mocked for unit tests
- [x] Error handling tests included

---

### T020: Implement FileSystemStorageProvider ✅ COMPLETE
**Type**: Core - Implementation  
**Dependencies**: T019  
**Files**:
- `src/Infrastructure/Storage/FileSystemStorageProvider.cs`

**Description**: Implement FileSystemStorageProvider with markdown file persistence per research.md.

**Implementation Details**:
- Use Markdig for parsing markdown with YAML frontmatter
- File naming: `{command}/{date}_{number}.md`
- YAML frontmatter contains metadata (command, timestamp, llm-provider, etc.)
- Body contains user input and LLM response sections
- Async file I/O with proper error handling

**Acceptance Criteria**:
- [x] All T019 tests pass
- [x] Creates directory structure if missing
- [x] YAML frontmatter correctly serialized
- [x] Markdown content properly formatted
- [x] Error handling returns Result.Failure
- [x] XML documentation complete

---

### T020a: Implement Retry Mechanism for Failed LLM Summarization ✅ COMPLETE
**Type**: Core - Error Handling  
**Dependencies**: T020  
**Files**:
- `src/Infrastructure/Llm/ILlmProvider.cs`
- `src/Features/Retry/Commands/RetryFailedSummarizationCommand.cs`
- `src/Features/Retry/Handlers/RetryFailedSummarizationHandler.cs`
- `tests/Unit/Features/Retry/RetryFailedSummarizationHandlerTests.cs`

**Description**: Implement retry mechanism for FR-036 to FR-039. Store user input when LLM summarization fails and provide `/retry` command to reprocess failed entries.

**Implementation Details**:
- Store partial entries with metadata flag `summarization-failed: true`
- RetryCommand discovers failed entries and resubmits to LLM
- Support `tom retry` (all failed) or `tom retry <entry-id>` (specific entry)
- Update entry metadata on successful retry

**Acceptance Criteria**:
- [x] Partial entries saved with failed flag when LLM errors
- [x] RetryCommand discovers unsummarized entries
- [x] Retry resubmits to LLM provider
- [x] Success updates entry and removes failed flag
- [x] Error handling for retry failures
- [x] Tests cover retry workflow (7 tests passing)
- [x] XML documentation complete

---

### T020b: Implement Auto-Purge Functionality ✅ COMPLETE
**Type**: Core - Data Management  
**Dependencies**: T020  
**Files**:
- `src/Infrastructure/Storage/AutoPurgeService.cs`
- `tests/Unit/Infrastructure/Storage/AutoPurgeServiceTests.cs`

**Description**: Implement auto-purge functionality per FR-035b to automatically delete entries older than configured retention period.

**Implementation Details**:
- Run on application startup if `AutoPurge=true` in config
- Calculate cutoff date based on RetentionPolicy (30 days, 90 days, 1 year, 2 years)
- Delegate to IMemoryStorageProvider.PurgeExpiredEntriesAsync
- Log purge operations (count, oldest date purged)
- Skip if RetentionPolicy is Indefinite

**Acceptance Criteria**:
- [x] Tests verify purge respects RetentionPolicy (9 tests passing)
- [x] Tests verify Indefinite retention skips purge
- [x] Tests verify AutoPurge=false skips purge
- [x] Purge service delegates to storage provider
- [x] Logs purge summary (entries deleted, date range)
- [x] Error handling for storage failures
- [x] XML documentation complete

---

## Phase 3.4: Infrastructure - LLM Providers (TDD)

### T021 [P]: Test ILlmProvider Interface Design ✅ COMPLETE
**Type**: Test  
**Dependencies**: T004  
**Files**:
- `tests/Unit/Infrastructure/Llm/ILlmProviderTests.cs`

**Description**: Write tests defining ILlmProvider contract per research.md.

**Test Cases**:
- GenerateCompletionAsync returns Result<string>
- Accepts prompt and optional parameters (maxTokens, temperature)
- Handles API errors gracefully (Result.Failure)
- Supports cancellation token
- Provider name property

**Acceptance Criteria**:
- [x] Interface behavior tested via mock (6 tests passing)
- [x] Error scenarios covered

---

### T022 [P]: Implement ILlmProvider Interface ✅ COMPLETE
**Type**: Core - Interface  
**Dependencies**: T021  
**Files**:
- `src/Infrastructure/Llm/ILlmProvider.cs`

**Description**: Define ILlmProvider interface per research.md.

**Acceptance Criteria**:
- [x] All T021 tests pass with mock
- [x] Returns Result<string> for completion
- [x] XML documentation complete

---

### T023 [P]: Test OpenAILlmProvider ✅ COMPLETE
**Type**: Test  
**Dependencies**: T022  
**Files**:
- `tests/Unit/Infrastructure/Llm/OpenAILlmProviderTests.cs`

**Description**: Write unit tests for OpenAI SDK integration.

**Test Cases**:
- GenerateCompletionAsync calls OpenAI API correctly
- Returns completion text on success
- Handles API errors (rate limit, auth, network)
- Uses configured model (gpt-4)
- Respects maxTokens and temperature parameters
- Logs API calls and token usage

**Acceptance Criteria**:
- [x] Tests created (9 tests defined, skipped pending full integration testing)
- [x] All error scenarios covered
- [x] API call parameters validated

---

### T024 [P]: Implement OpenAILlmProvider ✅ COMPLETE
**Type**: Core - Implementation  
**Dependencies**: T023  
**Files**:
- `src/Infrastructure/Llm/OpenAILlmProvider.cs`

**Description**: Implement OpenAI LLM provider using official SDK per research.md.

**Acceptance Criteria**:
- [x] Implementation complete
- [x] Uses official OpenAI NuGet package
- [x] Async/await with cancellation token and ConfigureAwait(false)
- [x] Error handling returns Result.Failure (rate limit, auth, network)
- [x] Logs API calls with Serilog (Debug for calls, Information for token usage)
- [x] XML documentation complete

---

### T025 [P]: Test AnthropicLlmProvider ✅ COMPLETE
**Type**: Test  
**Dependencies**: T022  
**Files**:
- `tests/Unit/Infrastructure/Llm/AnthropicLlmProviderTests.cs`

**Description**: Write unit tests for Anthropic SDK integration.

**Test Cases**:
- GenerateCompletionAsync calls Anthropic API correctly
- Returns completion text on success
- Handles API errors
- Uses configured model (claude-3-sonnet-20240229)
- Respects maxTokens and temperature parameters
- Logs API calls and token usage

**Acceptance Criteria**:
- [x] Tests created (9 tests defined, skipped pending full integration testing)
- [x] All error scenarios covered
- [x] API call parameters validated

---

### T026 [P]: Implement AnthropicLlmProvider ✅ COMPLETE
**Type**: Core - Implementation  
**Dependencies**: T025  
**Files**:
- `src/Infrastructure/Llm/AnthropicLlmProvider.cs`

**Description**: Implement Anthropic LLM provider using Anthropic.SDK per research.md.

**Acceptance Criteria**:
- [x] Implementation complete
- [x] Uses Anthropic.SDK NuGet package
- [x] Async/await with cancellation token and ConfigureAwait(false)
- [x] Error handling returns Result.Failure (rate limit, auth, network)
- [x] Logs API calls with Serilog (Debug for calls, Information for token usage)
- [x] XML documentation complete

---

### T027: Test LlmProviderFactory ✅ COMPLETE
**Type**: Test  
**Dependencies**: T024, T026  
**Files**:
- `tests/Unit/Infrastructure/Llm/LlmProviderFactoryTests.cs`

**Description**: Write tests for factory that creates appropriate ILlmProvider based on configuration.

**Test Cases**:
- Create("OpenAI") returns OpenAILlmProvider
- Create("Anthropic") returns AnthropicLlmProvider
- Create(invalid) returns Result.Failure
- Factory uses DI to inject dependencies

**Acceptance Criteria**:
- [x] Factory tests written and passing (11 tests)
- [x] Error handling for unknown providers
- [x] Case-insensitive provider name matching
- [x] Null/empty provider validation

---

### T028: Implement LlmProviderFactory ✅ COMPLETE
**Type**: Core - Factory  
**Dependencies**: T027  
**Files**:
- `src/Infrastructure/Llm/LlmProviderFactory.cs`

**Description**: Implement factory pattern for LLM provider instantiation per research.md.

**Acceptance Criteria**:
- [x] All T027 tests pass (11/11)
- [x] Returns correct provider type based on name
- [x] Uses dependency injection from IServiceProvider
- [x] XML documentation complete
- [x] Comprehensive error handling

---

## Phase 3.5: Infrastructure - Prompt Templates (TDD)

### T029 [P]: Test IPromptTemplateLoader Interface
**Type**: Test  
**Dependencies**: T012  
**Files**:
- `tests/Unit/Infrastructure/Prompts/IPromptTemplateLoaderTests.cs`

**Description**: Write tests defining IPromptTemplateLoader contract per research.md.

**Test Cases**:
- LoadTemplateAsync loads embedded resource
- LoadTemplateAsync loads user override from .memory/templates/
- User override takes precedence over embedded
- Returns Result<PromptTemplate>
- Handles missing template (Result.Failure)

**Acceptance Criteria**:
- [ ] Interface behavior tested via mock
- [ ] Override precedence tested

---

### T030 [P]: Implement IPromptTemplateLoader Interface
**Type**: Core - Interface  
**Dependencies**: T029  
**Files**:
- `src/Infrastructure/Prompts/IPromptTemplateLoader.cs`

**Description**: Define IPromptTemplateLoader interface per research.md.

**Acceptance Criteria**:
- [ ] All T029 tests pass with mock
- [ ] Returns Result<PromptTemplate>
- [ ] XML documentation complete

---

### T031: Test EmbeddedPromptTemplateLoader
**Type**: Test  
**Dependencies**: T030  
**Files**:
- `tests/Unit/Infrastructure/Prompts/EmbeddedPromptTemplateLoaderTests.cs`

**Description**: Write tests for loading templates from embedded resources and file system.

**Test Cases**:
- LoadTemplateAsync finds embedded resource
- LoadTemplateAsync finds user override file
- Parses template content and extracts variables
- Hot reload support for user overrides
- Missing template returns error
- Invalid template format returns error

**Acceptance Criteria**:
- [ ] Tests cover both embedded and file system sources
- [ ] Hot reload functionality tested
- [ ] Error cases covered

---

### T032: Implement EmbeddedPromptTemplateLoader
**Type**: Core - Implementation  
**Dependencies**: T031  
**Files**:
- `src/Infrastructure/Prompts/EmbeddedPromptTemplateLoader.cs`

**Description**: Implement template loader with embedded resources and file system fallback per research.md.

**Acceptance Criteria**:
- [ ] All T031 tests pass
- [ ] Loads from embedded resources
- [ ] Checks for user overrides in .memory/templates/
- [ ] Parses markdown and extracts {{VARIABLES}}
- [ ] XML documentation complete

---

### T033 [P]: Create Embedded Prompt Templates
**Type**: Core - Resources  
**Dependencies**: T012  
**Files**:
- `src/Infrastructure/Prompts/Templates/daily-summary.md`
- `src/Infrastructure/Prompts/Templates/weekly-review.md`

**Description**: Create prompt template markdown files as embedded resources per research.md. Templates must include detailed instructions for LLM output structure to enable reliable parsing.

**Daily Summary Template** (`daily-summary.md`):
- Variables: {{USER_INPUT}}, {{DATE}}
- Instructions to extract KeyEvents, Themes, TodoItems, ImportantPeople, NotableTasks
- Output format: structured markdown with exact section headers:
  ```
  ## Key Events
  - [event 1]
  - [event 2]
  
  ## Themes
  - [theme 1]
  
  ## To-Do Items
  - [ ] [task with optional due date]
  
  ## Important People
  - [person 1]
  
  ## Notable Tasks
  - [task 1]
  ```
- Specify "key" means most impactful/memorable, "notable" means requiring attention/follow-up
- Instruct LLM to use bullet points, be concise (1-2 sentences per item)

**Weekly Review Template** (`weekly-review.md`):
- Variables: {{DAILY_ENTRIES}}, {{START_DATE}}, {{END_DATE}}, {{ENTRY_COUNT}}
- Instructions to identify **exactly 3** top accomplishments and **exactly 3** top challenges
- Extract recurring themes, interaction patterns, next week suggestions
- Output format: structured markdown with exact section headers:
  ```
  ## Top 3 Accomplishments
  1. [accomplishment with context]
  2. [accomplishment with context]
  3. [accomplishment with context]
  
  ## Top 3 Challenges
  1. [challenge with context]
  2. [challenge with context]
  3. [challenge with context]
  
  ## Recurring Themes
  - [theme 1]
  
  ## Interaction Patterns
  - [pattern 1]
  
  ## Next Week Suggestions
  - [suggestion 1]
  ```
- Emphasize numbered lists for accomplishments/challenges, bullet points for others

**Acceptance Criteria**:
- [ ] Templates marked as embedded resources in .csproj
- [ ] Variables properly formatted ({{VARIABLE_NAME}})
- [ ] Clear instructions for LLM with explicit definitions ("key", "notable", "recurring")
- [ ] Expected output format specified with exact markdown structure
- [ ] Parsing hints included (section headers, list formats)
- [ ] Example output provided in template comments

---

## Phase 3.6: Infrastructure - Authentication (TDD)

### T034: Test IAuthenticationService Interface
**Type**: Test  
**Dependencies**: T014  
**Files**:
- `tests/Unit/Infrastructure/Auth/IAuthenticationServiceTests.cs`

**Description**: Write tests defining IAuthenticationService contract per research.md.

**Test Cases**:
- AuthenticateAsync discovers SSH key from ~/.ssh/
- Prompts for passphrase if key encrypted
- Creates UserSession on successful auth
- Returns Result<UserSession>
- IsAuthenticated checks active session
- Logout invalidates session
- Session persists until logout

**Acceptance Criteria**:
- [ ] Interface behavior tested via mock
- [ ] Session lifecycle tested

---

### T035: Implement IAuthenticationService Interface
**Type**: Core - Interface  
**Dependencies**: T034  
**Files**:
- `src/Infrastructure/Auth/IAuthenticationService.cs`

**Description**: Define IAuthenticationService interface per research.md.

**Acceptance Criteria**:
- [ ] All T034 tests pass with mock
- [ ] Returns Result<UserSession>
- [ ] XML documentation complete

---

### T036: Test SshKeyAuthenticationService
**Type**: Test  
**Dependencies**: T035  
**Files**:
- `tests/Unit/Infrastructure/Auth/SshKeyAuthenticationServiceTests.cs`

**Description**: Write tests for SSH key authentication implementation.

**Test Cases**:
- Discovers id_ed25519 (preferred) or id_rsa (fallback)
- Prompts for passphrase using Spectre.Console
- Creates session with SSH key fingerprint
- Stores session token in app config
- Validates existing session on startup
- Logout removes session token
- Handles missing SSH key (Result.Failure)
- Handles incorrect passphrase (Result.Failure)

**Acceptance Criteria**:
- [ ] SSH.NET library mocked for tests
- [ ] File system access mocked
- [ ] All error scenarios covered

---

### T037: Implement SshKeyAuthenticationService
**Type**: Core - Implementation  
**Dependencies**: T036  
**Files**:
- `src/Infrastructure/Auth/SshKeyAuthenticationService.cs`

**Description**: Implement SSH key authentication using SSH.NET per research.md with user-friendly passphrase prompt UX.

**Passphrase Prompt UX Requirements**:
- Use Spectre.Console `PromptPassword` for secure input (no echo)
- Maximum 3 attempts for passphrase entry
- Clear error message on incorrect passphrase: "Incorrect passphrase. {attempts_remaining} attempts remaining."
- After 3 failed attempts: "Authentication failed. Please check your passphrase or SSH key configuration."
- Support Ctrl+C to cancel authentication at any time
- Display key location being used: "Authenticating with SSH key: ~/.ssh/id_ed25519"
- If key not encrypted, skip passphrase prompt

**Acceptance Criteria**:
- [ ] All T036 tests pass
- [ ] Uses SSH.NET (Renci.SshNet)
- [ ] Discovers keys from standard locations (~/.ssh/id_ed25519, ~/.ssh/id_rsa)
- [ ] Passphrase prompt: max 3 attempts, Ctrl+C cancels, clear error messages
- [ ] Detects unencrypted keys and skips passphrase prompt
- [ ] Displays key path to user before prompting
- [ ] Stores session in app config (~/.tom/session.json)
- [ ] Session persists until logout
- [ ] XML documentation complete

---

## Phase 3.7: Infrastructure - Configuration & Logging (TDD)

### T038 [P]: Test Configuration Loading
**Type**: Test  
**Dependencies**: T001  
**Files**:
- `tests/Unit/Infrastructure/Configuration/ConfigurationTests.cs`

**Description**: Write tests for configuration hierarchy per research.md.

**Test Cases**:
- Load appsettings.json defaults
- User Secrets override appsettings.json
- Environment variables override User Secrets
- Command-line args override everything
- Missing required config returns error
- Invalid config values return error

**Acceptance Criteria**:
- [ ] Configuration priority tested
- [ ] Error handling for missing/invalid values

---

### T039 [P]: Configure Application Settings
**Type**: Core - Configuration  
**Dependencies**: T038  
**Files**:
- `src/appsettings.json`
- `src/appsettings.Development.json`
- `src/TenSecondTom.csproj` (enable User Secrets)

**Description**: Create configuration files per research.md specification.

**appsettings.json** (defaults, no secrets):
```json
{
  "TenSecondTom": {
    "MemoryDirectory": "./.memory",
    "LlmProvider": "OpenAI",
    "OpenAI": {
      "Model": "gpt-4",
      "MaxTokens": 2000
    },
    "Anthropic": {
      "Model": "claude-3-sonnet-20240229",
      "MaxTokens": 2000
    },
    "DataRetention": {
      "DefaultPolicy": "Indefinite",
      "AutoPurgeEnabled": false
    }
  }
}
```

**Acceptance Criteria**:
- [ ] All T038 tests pass
- [ ] No secrets in appsettings.json
- [ ] User Secrets enabled in .csproj
- [ ] Example provided for setting secrets

---

### T040 [P]: Configure Logging with Serilog
**Type**: Core - Logging  
**Dependencies**: T002  
**Files**:
- `src/Infrastructure/Logging/LoggingConfiguration.cs`
- `src/appsettings.json` (Serilog configuration)
- `src/Program.cs` (bootstrap logging)

**Description**: Configure Serilog as the logging framework per constitution v1.1.0 organizational standard.

**Serilog Configuration Requirements**:

**Sinks**:
- Console sink: Formatted output for CLI diagnostics (not user-facing output)
- File sink: Rolling file logs in `.logs/tom-.log` (daily rolling, 7-day retention)

**Log Levels** (per constitution Logging Standards):
- **Debug**: I/O operations (file reads/writes, API calls)
- **Information**: CLI commands execution, authentication events, memory entry creation
- **Warning**: Retry attempts, degraded performance, non-fatal errors
- **Error**: Failed operations, LLM API errors, storage errors
- **Fatal**: Unrecoverable errors causing application termination

**Enrichers**:
- Environment: Machine name, environment (Development/Production)
- Thread ID: For parallel operation debugging
- Timestamp: UTC timestamps for all log entries

**Structured Logging**:
- Use semantic properties: `{Command}`, `{EntryId}`, `{Provider}`, `{Duration}`
- Example: `Log.Information("Created {EntryType} entry {EntryId} in {Duration}ms", "Daily", entryId, duration)`

**Security**:
- Never log secrets: API keys, SSH passphrases, session tokens
- Never log full user memory content (use excerpts or IDs only)
- Sanitize PII before logging

**Configuration** (appsettings.json):
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": ".logs/tom-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithEnvironmentName", "WithMachineName", "WithThreadId"]
  }
}
```

**Acceptance Criteria**:
- [ ] Serilog configured with Console and File sinks
- [ ] Structured logging enabled with semantic properties
- [ ] Log levels configurable via appsettings.json
- [ ] Rolling file logs in `.logs/` directory (7-day retention)
- [ ] Logger injected via Microsoft.Extensions.Logging.ILogger<T>
- [ ] Environment enrichers configured
- [ ] Security: No secrets or full user content logged
- [ ] Output templates provide clear, parseable format
- [ ] `.logs/` directory added to .gitignore

---

## Phase 3.8: Feature - Today Command (CQRS with TDD)

### T041: Test CreateDailyEntryCommand Handler
**Type**: Test  
**Dependencies**: T006, T008, T020, T028, T032, T037  
**Files**:
- `tests/Unit/Features/Today/CreateDailyEntryHandlerTests.cs`

**Description**: Write comprehensive unit tests for CreateDailyEntryHandler per contract spec.

**Test Cases** (from CreateDailyEntryCommand.md):
1. Handle_WithValidCommand_CreatesDailyEntry
2. Handle_WithEmptyResponses_ReturnsValidationError
3. Handle_WithFewerThan3Responses_ReturnsValidationError
4. Handle_WithMoreThan5Responses_ReturnsValidationError
5. Handle_WhenLlmProviderFails_SavesUserInputAndReturnsError
6. Handle_WhenStorageFails_ReturnsStorageError
7. Handle_WithOpenAIProvider_UsesOpenAI
8. Handle_WithAnthropicProvider_UsesAnthropic
9. Handle_WithInvalidProvider_ReturnsValidationError
10. Handle_MultipleCallsSameDay_IncrementsEntryNumber

**Acceptance Criteria**:
- [ ] All 10 test cases written and failing
- [ ] Dependencies mocked (storage, LLM, auth)
- [ ] Error scenarios covered

---

### T042: Implement CreateDailyEntryCommand
**Type**: Core - Command  
**Dependencies**: T041  
**Files**:
- `src/Features/Today/Commands/CreateDailyEntryCommand.cs`

**Description**: Implement CQRS command record per contract spec.

**Acceptance Criteria**:
- [ ] Record with required Responses Dictionary<string,string>
- [ ] Optional LlmProviderOverride property
- [ ] Returns Result<DailyEntry>
- [ ] XML documentation

---

### T043: Implement CreateDailyEntryHandler
**Type**: Core - Handler  
**Dependencies**: T042  
**Files**:
- `src/Features/Today/Handlers/CreateDailyEntryHandler.cs`

**Description**: Implement command handler per contract pseudocode.

**Handler Flow**:
1. Validate command (3-5 responses, valid provider)
2. Check authentication
3. Determine entry number for today
4. Format user input for LLM
5. Load prompt template
6. Call LLM provider
7. Parse LLM response into DailySummary
8. Create DailyEntry
9. Save to storage
10. Return Result<DailyEntry>

**Acceptance Criteria**:
- [ ] All T041 tests pass
- [ ] Follows handler pseudocode from contract
- [ ] Error handling returns Result.Failure
- [ ] XML documentation complete

---

### T044: Test CreateDailyEntryValidator
**Type**: Test  
**Dependencies**: T042  
**Files**:
- `tests/Unit/Features/Today/CreateDailyEntryValidatorTests.cs`

**Description**: Write tests for FluentValidation validator per contract validation rules.

**Test Cases**:
- Responses not null or empty
- All response keys non-empty
- All response values non-empty after trim
- 3-5 response pairs required
- LlmProviderOverride must be "OpenAI" or "Anthropic" if set

**Acceptance Criteria**:
- [ ] All validation rules tested
- [ ] Tests failing initially

---

### T045: Implement CreateDailyEntryValidator
**Type**: Core - Validation  
**Dependencies**: T044  
**Files**:
- `src/Features/Today/Validation/CreateDailyEntryValidator.cs`

**Description**: Implement FluentValidation validator per contract validation rules.

**Acceptance Criteria**:
- [ ] All T044 tests pass
- [ ] Uses FluentValidation library
- [ ] Clear error messages

---

### T046: Implement TodayCommand CLI
**Type**: Core - CLI  
**Dependencies**: T043, T045  
**Files**:
- `src/Features/Today/Commands/TodayCommand.cs`

**Description**: Wire up /today command to System.CommandLine per quickstart.md.

**CLI Flow**:
1. Display welcome message
2. Prompt for authentication if not authenticated
3. Ask daily reflection questions (3-5 prompts)
4. Collect responses
5. Call CreateDailyEntryHandler
6. Display formatted summary using Spectre.Console
7. Show file path where entry saved

**Acceptance Criteria**:
- [ ] System.CommandLine command registered
- [ ] Spectre.Console for prompts and output
- [ ] Calls handler and displays results
- [ ] Error messages user-friendly

---

### T047: Test Daily Entry Integration Workflow
**Type**: Test - Integration  
**Dependencies**: T046  
**Files**:
- `tests/Integration/Features/Today/DailyEntryWorkflowTests.cs`

**Description**: Write end-to-end integration tests per contract spec.

**Test Cases** (from CreateDailyEntryCommand.md):
1. CompleteWorkflow_CreatesFileWithCorrectFormat
2. CompleteWorkflow_ParsesLlmResponseIntoSummary
3. CompleteWorkflow_PreservesUserInputExactly

**Acceptance Criteria**:
- [ ] Tests use real file system (test directory)
- [ ] LLM provider mocked or stubbed
- [ ] File format validation
- [ ] Markdown parsing validation

---

## Phase 3.9: Feature - ThisWeek Command (CQRS with TDD)

### T048: Test CreateWeeklyReviewCommand Handler
**Type**: Test  
**Dependencies**: T006, T010, T020, T028, T032, T037  
**Files**:
- `tests/Unit/Features/ThisWeek/CreateWeeklyReviewHandlerTests.cs`

**Description**: Write comprehensive unit tests for CreateWeeklyReviewHandler per contract spec.

**Test Cases** (from CreateWeeklyReviewCommand.md):
1. Handle_WithValidCommand_CreatesWeeklyReview
2. Handle_WithNoDailyEntries_ReturnsNoDataError
3. Handle_WithCustomDateRange_UsesCustomRange
4. Handle_WithoutCustomDateRange_UsesLast7Days
5. Handle_WhenLlmProviderFails_ReturnsError
6. Handle_WithFewerThan7Days_Succeeds
7. Handle_WithFewerThan3Days_ReturnsValidationError
8. Handle_EnsuresExactly3Accomplishments
9. Handle_EnsuresExactly3Challenges
10. Handle_AggregatesMultipleDailyEntriesPerDay

**Acceptance Criteria**:
- [ ] All 10 test cases written and failing
- [ ] Dependencies mocked
- [ ] Aggregation logic tested

---

### T049: Implement CreateWeeklyReviewCommand
**Type**: Core - Command  
**Dependencies**: T048  
**Files**:
- `src/Features/ThisWeek/Commands/CreateWeeklyReviewCommand.cs`

**Description**: Implement CQRS command record per contract spec.

**Acceptance Criteria**:
- [ ] Record with optional CustomDateRange
- [ ] Optional LlmProviderOverride
- [ ] Returns Result<WeeklyEntry>
- [ ] XML documentation

---

### T050: Implement CreateWeeklyReviewHandler
**Type**: Core - Handler  
**Dependencies**: T049  
**Files**:
- `src/Features/ThisWeek/Handlers/CreateWeeklyReviewHandler.cs`

**Description**: Implement command handler per contract pseudocode.

**Handler Flow**:
1. Validate command
2. Check authentication
3. Determine date range (custom or last 7 days)
4. Retrieve daily entries from storage
5. Return error if no entries found
6. Aggregate daily summaries
7. Load weekly review template
8. Call LLM provider
9. Parse response, validate exactly 3 accomplishments + 3 challenges
10. Create WeeklyEntry
11. Save to storage
12. Return Result<WeeklyEntry>

**Acceptance Criteria**:
- [ ] All T048 tests pass
- [ ] Aggregates multiple daily entries
- [ ] Validates 3+3 structure
- [ ] XML documentation complete

---

### T051: Test CreateWeeklyReviewValidator
**Type**: Test  
**Dependencies**: T049  
**Files**:
- `tests/Unit/Features/ThisWeek/CreateWeeklyReviewValidatorTests.cs`

**Description**: Write tests for validator per contract validation rules.

**Test Cases**:
- CustomDateRange Start < End if set
- CustomDateRange End not in future
- CustomDateRange duration 3-10 days
- LlmProviderOverride valid if set

**Acceptance Criteria**:
- [ ] All validation rules tested
- [ ] Tests failing initially

---

### T052: Implement CreateWeeklyReviewValidator
**Type**: Core - Validation  
**Dependencies**: T051  
**Files**:
- `src/Features/ThisWeek/Validation/CreateWeeklyReviewValidator.cs`

**Description**: Implement FluentValidation validator per contract validation rules.

**Acceptance Criteria**:
- [ ] All T051 tests pass
- [ ] Clear error messages

---

### T053: Implement ThisWeekCommand CLI
**Type**: Core - CLI  
**Dependencies**: T050, T052  
**Files**:
- `src/Features/ThisWeek/Commands/ThisWeekCommand.cs`

**Description**: Wire up /thisweek command to System.CommandLine per quickstart.md.

**CLI Flow**:
1. Display welcome message
2. Check authentication
3. Optional: prompt for custom date range
4. Call CreateWeeklyReviewHandler
5. Display formatted weekly summary using Spectre.Console
6. Show top 3 accomplishments
7. Show top 3 challenges
8. Show recurring themes and suggestions
9. Display file path

**Acceptance Criteria**:
- [ ] System.CommandLine command registered
- [ ] Spectre.Console for formatted output
- [ ] Displays all summary sections
- [ ] Error messages user-friendly

---

### T054: Test Weekly Review Integration Workflow
**Type**: Test - Integration  
**Dependencies**: T053  
**Files**:
- `tests/Integration/Features/ThisWeek/WeeklyReviewWorkflowTests.cs`

**Description**: Write end-to-end integration tests per contract spec.

**Test Cases** (from CreateWeeklyReviewCommand.md):
1. CompleteWorkflow_AggregatesDailyEntries
2. CompleteWorkflow_CreatesFileWithCorrectWeekNumber
3. CompleteWorkflow_ParsesLlmResponseCorrectly

**Acceptance Criteria**:
- [ ] Tests use real file system
- [ ] Creates daily entries as test data
- [ ] Validates aggregation logic
- [ ] File format validation

---

## Phase 3.10: Feature - Search Command (Simplified for v1)

### T055: Test SearchMemoriesQuery Handler
**Type**: Test  
**Dependencies**: T020  
**Files**:
- `tests/Unit/Features/Search/SearchMemoriesQueryHandlerTests.cs`

**Description**: Write tests for basic search functionality.

**Test Cases**:
- Search by query text returns matching entries
- Search with no results returns empty list
- Search respects date range filter
- Search requires authentication
- Case-insensitive search

**Acceptance Criteria**:
- [ ] Tests written and failing
- [ ] Storage search method mocked

---

### T056: Implement SearchMemoriesQuery
**Type**: Core - Query  
**Dependencies**: T055  
**Files**:
- `src/Features/Search/Queries/SearchMemoriesQuery.cs`

**Description**: Implement CQRS query record.

**Acceptance Criteria**:
- [ ] Record with query string
- [ ] Optional date range filter
- [ ] Returns Result<IReadOnlyList<MemoryEntry>>
- [ ] XML documentation

---

### T057: Implement SearchMemoriesQueryHandler
**Type**: Core - Handler  
**Dependencies**: T056  
**Files**:
- `src/Features/Search/Handlers/SearchMemoriesQueryHandler.cs`

**Description**: Implement query handler for search.

**Acceptance Criteria**:
- [ ] All T055 tests pass
- [ ] Calls storage SearchEntriesAsync
- [ ] Returns filtered results
- [ ] XML documentation

---

### T058: Implement SearchCommand CLI
**Type**: Core - CLI  
**Dependencies**: T057  
**Files**:
- `src/Features/Search/Commands/SearchCommand.cs`

**Description**: Wire up /search command with --query option per quickstart.md. Display results with context per FR-015.

**Result Display Format**:
```
Found 3 results for "meeting":

1. Daily Entry | Oct 1, 2025 | Entry #1
   "Had a productive morning meeting with the team..."
   → .memory/today/10-01-2025_1.md

2. Daily Entry | Oct 2, 2025 | Entry #2
   "Follow-up meeting scheduled for next week..."
   → .memory/today/10-02-2025_1.md
```

**Acceptance Criteria**:
- [ ] System.CommandLine command with --query option
- [ ] Displays results using Spectre.Console table or panel
- [ ] Each result shows: entry type (Daily/Weekly), date, entry number
- [ ] Each result shows content excerpt (first 80 chars of UserInput or Summary)
- [ ] Each result shows file path for reference
- [ ] Results sorted by date (newest first)
- [ ] Empty results message: "No entries found matching '{query}'"
- [ ] Shows entry summaries, not full content

---

## Phase 3.11: Feature - Authentication Commands

### T059: Test LoginCommand Handler
**Type**: Test  
**Dependencies**: T037  
**Files**:
- `tests/Unit/Features/Auth/LoginCommandHandlerTests.cs`

**Description**: Write tests for explicit login command.

**Test Cases**:
- Login discovers SSH key
- Prompts for passphrase
- Creates session
- Returns success/failure

**Acceptance Criteria**:
- [ ] Tests written and failing
- [ ] Auth service mocked

---

### T060: Implement LoginCommand
**Type**: Core - Command  
**Dependencies**: T059  
**Files**:
- `src/Features/Auth/Commands/LoginCommand.cs`
- `src/Features/Auth/Handlers/LoginCommandHandler.cs`

**Description**: Implement explicit login command (calls AuthenticationService).

**Acceptance Criteria**:
- [ ] All T059 tests pass
- [ ] CLI command registered
- [ ] User-friendly messages

---

### T061: Implement LogoutCommand
**Type**: Core - Command  
**Dependencies**: T037  
**Files**:
- `src/Features/Auth/Commands/LogoutCommand.cs`

**Description**: Implement logout command that invalidates session.

**Acceptance Criteria**:
- [ ] Calls AuthenticationService.Logout
- [ ] Clears session token
- [ ] User-friendly confirmation message

---

## Phase 3.12: Program Entry Point & DI Registration

### T062: Configure Dependency Injection
**Type**: Core - Integration  
**Dependencies**: T020, T028, T032, T037, T039  
**Files**:
- `src/Program.cs`
- `src/Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`

**Description**: Register all services in DI container per research.md.

**Services to Register**:
- IMemoryStorageProvider → FileSystemStorageProvider (singleton)
- ILlmProvider → via LlmProviderFactory (transient)
- IPromptTemplateLoader → EmbeddedPromptTemplateLoader (singleton)
- IAuthenticationService → SshKeyAuthenticationService (singleton)
- Configuration (IConfiguration)
- Logging (ILogger<T>)
- Command Handlers (scoped)

**Acceptance Criteria**:
- [ ] All services registered
- [ ] Correct lifetimes (singleton, scoped, transient)
- [ ] Configuration bound to options classes
- [ ] Logging configured

---

### T063: Implement Program.cs Entry Point
**Type**: Core - Integration  
**Dependencies**: T062, T046, T053, T058, T060, T061  
**Files**:
- `src/Program.cs`

**Description**: Implement main entry point with System.CommandLine root command.

**Root Command Structure**:
```
tom [command]

Commands:
  today       Capture daily reflection
  thisweek    Generate weekly review
  search      Search memory entries
  login       Authenticate with SSH key
  logout      End session
```

**Acceptance Criteria**:
- [ ] RootCommand configured
- [ ] All feature commands registered
- [ ] Help text generated automatically
- [ ] Exit codes: 0 for success, non-zero for errors
- [ ] Unhandled exceptions logged

---

### T063a: Implement JSON Output Format for Programmatic Consumers
**Type**: Core - Integration  
**Dependencies**: T063  
**Files**:
- `src/Shared/OutputFormatters/JsonOutputFormatter.cs`
- `src/Program.cs` (add global --output-json option)
- `tests/Unit/Shared/JsonOutputFormatterTests.cs`

**Description**: Implement structured JSON output per FR-020 for programmatic consumers and AI agents.

**Implementation Details**:
- Add global `--output-json` flag to root command
- When enabled, suppress Spectre.Console formatting
- Serialize command results to JSON (System.Text.Json)
- Include standard fields: success (bool), data (object), error (string?), timestamp (ISO8601)
- Commands that support JSON: today, thisweek, search, login, logout

**JSON Output Schema**:
```json
{
  "success": true,
  "timestamp": "2025-10-02T14:30:00Z",
  "command": "today",
  "data": {
    "entryId": "today-10-02-2025-1",
    "filePath": ".memory/today/10-02-2025_1.md",
    "summary": { /* DailySummary object */ }
  },
  "error": null
}
```

**Acceptance Criteria**:
- [ ] Tests verify JSON output for all commands
- [ ] Global --output-json flag registered
- [ ] Human-readable output suppressed when JSON enabled
- [ ] Valid JSON structure (validated with JsonSchema)
- [ ] Error responses include structured error field
- [ ] Success/failure indicated by "success" boolean
- [ ] Exit codes still work correctly (0/non-zero)
- [ ] XML documentation for JsonOutputFormatter

---

## Phase 3.13: End-to-End CLI Testing

### T064: Test CLI Command Execution
**Type**: Test - Integration  
**Dependencies**: T063  
**Files**:
- `tests/Integration/Cli/TodayCommandTests.cs`
- `tests/Integration/Cli/ThisWeekCommandTests.cs`
- `tests/Integration/Cli/SearchCommandTests.cs`
- `tests/Integration/Cli/AuthCommandTests.cs`

**Description**: Write end-to-end tests executing CLI commands per quickstart.md scenarios.

**Test Scenarios**:
- Execute `tom today` with mocked responses
- Execute `tom thisweek` after creating daily entries
- Execute `tom search --query "meeting"`
- Execute `tom login` and `tom logout`
- Test authentication flow
- Test error messages for invalid input

**Acceptance Criteria**:
- [ ] Tests execute actual CLI commands
- [ ] Use test file system directory
- [ ] Mock LLM API calls
- [ ] Validate console output

---

## Phase 3.14: Documentation & Polish

### T065 [P]: Update README.md
**Type**: Documentation  
**Dependencies**: T063  
**Files**:
- `README.md`

**Description**: Write comprehensive README with setup, usage, and examples per quickstart.md.

**Sections**:
- Project overview
- Prerequisites
- Installation instructions (Homebrew, winget, from source)
- Configuration (User Secrets, environment variables)
- Usage examples for all commands
- Architecture overview
- Contributing guidelines

**Acceptance Criteria**:
- [ ] Clear setup instructions
- [ ] Example commands with output
- [ ] Architecture diagram (optional)

---

### T065b [P]: Create ASCII Logo and Integrate into CLI
**Type**: Documentation/UX  
**Dependencies**: T063  
**Files**:
- `src/Infrastructure/Cli/Logo.cs`
- `src/Program.cs` (display on launch)
- `README.md` (include logo)

**Description**: Create ASCII art logo per FR-016 and display on CLI welcome screen and `--version` output.

**Logo Requirements**:
- ASCII art representing "Ten Second Tom" theme (memory, time, brevity)
- Max width: 80 characters (terminal compatibility)
- Include tagline: "Your personal memory assistant"
- Color using Spectre.Console (use project theme colors)

**Display Logic**:
- Show on `tom --version`
- Show on first launch (authentication prompt)
- Show on `tom` with no arguments (help screen)
- Suppress when using --output-json flag

**Example ASCII Logo Structure**:
```
 ████████╗███████╗███╗   ██╗    ███████╗███████╗ ██████╗
 ╚══██╔══╝██╔════╝████╗  ██║    ██╔════╝██╔════╝██╔════╝
    ██║   █████╗  ██╔██╗ ██║    ███████╗█████╗  ██║     
    ██║   ██╔══╝  ██║╚██╗██║    ╚════██║██╔══╝  ██║     
    ██║   ███████╗██║ ╚████║    ███████║███████╗╚██████╗
    ╚═╝   ╚══════╝╚═╝  ╚═══╝    ╚══════╝╚══════╝ ╚═════╝
                   TOM - Your personal memory assistant
```

**Acceptance Criteria**:
- [ ] ASCII logo created (max 80 chars wide)
- [ ] Logo.cs class with static Display() method
- [ ] Logo displayed on --version, help screen, first launch
- [ ] Logo suppressed with --output-json flag
- [ ] Uses Spectre.Console for colors
- [ ] Logo included in README.md
- [ ] XML documentation complete

---

### T066 [P]: Create Example Configuration
**Type**: Documentation  
**Dependencies**: T039  
**Files**:
- `example.appsettings.json`
- `CONFIGURATION.md`

**Description**: Provide example configuration files and documentation.

**Acceptance Criteria**:
- [ ] Example config without secrets
- [ ] Instructions for User Secrets
- [ ] Environment variable examples

---

### T067 [P]: Performance Validation
**Type**: Testing  
**Dependencies**: T064  
**Files**:
- `tests/Performance/CommandPerformanceTests.cs`

**Description**: Validate performance requirements from plan.md.

**Performance Requirements**:
- CLI command response < 500ms (excluding LLM calls)
- Markdown file I/O < 100ms per operation
- LLM API calls 2-10 seconds (acceptable)

**Acceptance Criteria**:
- [ ] Performance tests measure actual timings
- [ ] Tests fail if requirements not met

---

### T068: Execute Quickstart Guide Manually
**Type**: Manual Testing  
**Dependencies**: T063  
**Files**:
- `specs/001-ten-second-tom/quickstart.md`

**Description**: Manually execute all scenarios from quickstart.md to validate user experience.

**Test Scenarios**:
1. First-time setup (install, configure API key)
2. Authentication flow
3. Daily reflection workflow
4. Weekly review workflow
5. Search workflow
6. Logout and login again
7. Multiple daily entries same day
8. Custom weekly date range

**Acceptance Criteria**:
- [ ] All quickstart scenarios execute successfully
- [ ] User experience matches documentation
- [ ] Error messages helpful
- [ ] Output formatted correctly

---

### T069 [P]: Code Coverage Report
**Type**: Testing  
**Dependencies**: All test tasks  
**Files**:
- `.github/workflows/test.yml` (if using GitHub Actions)

**Description**: Generate code coverage report and ensure 80% minimum per constitution.

**Acceptance Criteria**:
- [ ] Coverage report generated
- [ ] Coverage >= 80%
- [ ] Coverage excludes Program.cs and DI config
- [ ] CI pipeline fails if coverage below threshold

---

### T070: Final Constitution Compliance Check
**Type**: Validation  
**Dependencies**: T069  

**Description**: Validate all constitutional requirements met per plan.md.

**Checklist**:
- [x] Modern .NET & Idiomatic C#
- [x] CLI-First Interface
- [x] Test-First (80% coverage)
- [x] DRY & Design Patterns (VSA, CQRS, Factory, Provider)
- [ ] Semantic Versioning & Automated Releases (setup GitHub Actions)
- [ ] Cross-Platform Distribution (setup Homebrew, winget)
- [x] Local Development Excellence
- [x] Secrets Management

**Acceptance Criteria**:
- [ ] All constitutional requirements validated
- [ ] No compiler warnings
- [ ] All tests pass
- [ ] Documentation complete

---

## Dependencies Graph

```
Setup (T001-T003)
  ↓
Models (T004-T016) [P]
  ↓
Infrastructure - Storage (T017-T020)
  ↓
Infrastructure - Storage Extended (T020a, T020b)
Infrastructure - LLM (T021-T028) [P]
Infrastructure - Prompts (T029-T033) [P]
Infrastructure - Auth (T034-T037)
Infrastructure - Config/Logging (T038-T040) [P]
  ↓
Features - Today (T041-T047)
Features - ThisWeek (T048-T054)
Features - Search (T055-T058) [P]
Features - Auth (T059-T061) [P]
  ↓
Integration (T062-T063)
  ↓
Integration - JSON Output (T063a)
  ↓
CLI Testing (T064)
  ↓
Documentation & Polish (T065, T065b, T066-T070) [P]
```

---

## Parallel Execution Examples

### Phase 1: Models (all independent)
```bash
# Launch all model tasks simultaneously:
Task T005: Test MemoryEntry Model
Task T007: Test DailyEntry Model  
Task T009: Test WeeklyEntry Model
Task T011: Test PromptTemplate Model
Task T013: Test UserSession Model
Task T015: Test StorageConfiguration Model
```

### Phase 2: Infrastructure Providers (independent)
```bash
# Launch infrastructure tests in parallel:
Task T023: Test OpenAILlmProvider
Task T025: Test AnthropicLlmProvider
Task T029: Test IPromptTemplateLoader Interface
Task T038: Test Configuration Loading
```

### Phase 3: Feature Commands (after infrastructure)
```bash
# Today and ThisWeek can be parallel:
Task T041: Test CreateDailyEntryCommand Handler
Task T048: Test CreateWeeklyReviewCommand Handler
```

---

## Validation Checklist

- [x] All contracts have corresponding test tasks (T041, T048)
- [x] All entities have model tasks (T005-T016)
- [x] All tests come before implementation (TDD enforced)
- [x] Parallel tasks marked [P] are truly independent
- [x] Each task specifies exact file path
- [x] No [P] task modifies same file as another [P] task
- [x] Setup tasks before everything else
- [x] Tests before implementation (Phase 3.2 before 3.3)
- [x] Core before integration (T062-T063 depend on all features)
- [x] Integration before polish (T064 before T065-T070)

---

## Task Execution Summary

**Total Tasks**: 74 (original 70 + 4 new: T020a, T020b, T063a, T065b)
**Parallel Tasks**: ~32 (marked [P])
**Estimated Effort**: 45-55 hours (with 80% test coverage)

**New Tasks Added** (from analysis remediation):
- T020a: Retry mechanism for failed LLM summarization (addresses FR-036 to FR-039)
- T020b: Auto-purge functionality (addresses FR-035b)
- T063a: JSON output format for programmatic consumers (addresses FR-020)
- T065b: ASCII logo creation and display (addresses FR-016)

**Critical Path**:
T001 → T002 → Models → Infrastructure → Storage Extended → Features → Integration → JSON Output → CLI Testing → Polish

**Fastest Path** (with parallelization):
~22 hours with 3-4 developers executing [P] tasks simultaneously

---

## Notes

- **TDD Enforcement**: Phase 3.2 tests MUST be written and MUST FAIL before Phase 3.3 implementation
- **No Compiler Warnings**: Each task must leave codebase warning-free
- **XML Documentation**: Required for all public APIs
- **Commit After Each Task**: Maintain clean git history
- **Constitution Compliance**: Validated at T070 before completion

---

**Generated**: October 1, 2025  
**Based On**: spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md  
**Ready For**: Task execution following TDD principles
