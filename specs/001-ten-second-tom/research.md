# Research: Ten Second Tom Implementation

**Date**: October 1, 2025  
**Feature**: Personal Memory Management CLI  
**Phase**: 0 - Technical Research & Decisions

## Overview

This document captures research findings and technical decisions for implementing Ten Second Tom, a CLI application for personal memory management using LLM-powered summarization.

---

## 1. LLM Provider SDKs

### Decision: Official SDKs with Fallback Strategy

**OpenAI Integration**:
- **Chosen**: Official `OpenAI` NuGet package (maintained by OpenAI)
- **Version**: Latest stable (7.x+)
- **Rationale**: Official SDK provides first-class .NET support with async/await, strongly-typed models, and automatic retry logic
- **Usage**: For GPT-4, GPT-3.5-turbo models for summarization tasks

**Anthropic Integration**:
- **Chosen**: `Anthropic.SDK` NuGet package (community-maintained, most popular)
- **Alternative**: Direct HTTP client implementation if official SDK releases
- **Rationale**: No official .NET SDK from Anthropic as of 2025; Anthropic.SDK is well-maintained OSS with strong community support
- **Usage**: For Claude 3 models (Opus, Sonnet, Haiku) for summarization tasks

**Alternatives Considered**:
- Semantic Kernel: Rejected - adds unnecessary abstraction layer for our simple use case
- LangChain.NET: Rejected - overkill for basic prompt completion
- Raw HTTP calls: Rejected - reinventing SDK features (auth, retries, types)

---

## 2. CLI Framework

### Decision: System.CommandLine

**Package**: `System.CommandLine` (Microsoft official)

**Rationale**:
- Official Microsoft CLI framework for .NET
- Strong typing for command options and arguments
- Built-in help generation and validation
- Middleware support for cross-cutting concerns (auth, logging)
- Excellent async/await support
- Industry-standard patterns (similar to Click for Python, Cobra for Go)

**Command Structure**:
```csharp
// Example: /today command
RootCommand root = new("Ten Second Tom - Personal Memory Assistant");

Command todayCmd = new("today", "Capture daily reflection");
root.AddCommand(todayCmd);

Command thisWeekCmd = new("thisweek", "Generate weekly review");
root.AddCommand(thisWeekCmd);

Command searchCmd = new("search", "Search memory entries");
searchCmd.AddOption(new Option<string>("--query", "Search query"));
root.AddCommand(searchCmd);
```

**Alternatives Considered**:
- CommandLineParser: Rejected - less idiomatic, attribute-based approach
- Spectre.Console: Considered for UI but kept separate for rendering concerns
- Custom parsing: Rejected - reinventing standard functionality

---

## 3. Markdown Processing

### Decision: Markdig for Parsing, Spectre.Console for Rendering

**Parsing**: `Markdig` NuGet package
- Fast, extensible Markdown parser
- CommonMark compliant
- Supports extensions (tables, task lists, etc.)
- Can convert markdown to HTML or plain text

**Terminal Rendering**: `Spectre.Console`
- Rich terminal UI library for .NET
- Markdown rendering support with syntax highlighting
- Tables, panels, colors for formatted output
- Cross-platform (Windows Terminal, macOS Terminal, Linux)

**Rationale**: Separate concerns - Markdig for parsing stored markdown, Spectre.Console for beautiful terminal rendering

**Storage Format**: Plain markdown files
- Human-readable without tools
- Version control friendly
- Easy to backup and migrate
- Can be opened in any text editor

**Alternatives Considered**:
- HTML output: Rejected - not suitable for terminal display
- Plain text: Rejected - loses formatting information
- JSON with markdown: Rejected - unnecessary complexity

---

## 4. SSH Key Authentication

### Decision: SSH.NET Library

**Package**: `SSH.NET` (Renci.SshNet)

**Rationale**:
- Mature, well-tested library for SSH operations in .NET
- Supports Ed25519 and RSA key formats
- Can read keys from standard locations (~/.ssh/)
- Works cross-platform (Windows, macOS, Linux)
- No external native dependencies

**Authentication Flow**:
1. Check for existing session token in app config
2. If no session, prompt for SSH key passphrase (if encrypted)
3. Verify SSH key ownership (challenge-response pattern)
4. Generate session token, store in app config
5. Session persists until explicit logout

**Key Discovery**:
```
~/.ssh/id_ed25519       (preferred)
~/.ssh/id_rsa           (fallback)
~/.ssh/id_ecdsa         (fallback)
```

**Alternatives Considered**:
- BouncyCastle: Rejected - heavier library, SSH.NET sufficient
- Native SSH command: Rejected - platform-specific behavior
- Custom key handling: Rejected - security risk, reinventing crypto

---

## 5. Configuration & Secrets Management

### Decision: Microsoft.Extensions.Configuration with User Secrets

**Configuration Stack**:
- `Microsoft.Extensions.Configuration` (core)
- `Microsoft.Extensions.Configuration.Json` (appsettings.json)
- `Microsoft.Extensions.Configuration.EnvironmentVariables` (production)
- `Microsoft.Extensions.Configuration.UserSecrets` (development)

**Configuration Hierarchy** (last wins):
1. appsettings.json (defaults)
2. appsettings.{Environment}.json (environment-specific)
3. User Secrets (development only)
4. Environment Variables (production)
5. Command-line arguments (overrides)

**Secret Storage**:
- **Development**: .NET User Secrets (`dotnet user-secrets set`)
- **Production**: Environment variables or Azure Key Vault
- **Never**: appsettings.json committed to git

**Configuration Structure**:
```json
{
  "TenSecondTom": {
    "MemoryDirectory": "./.memory",
    "LlmProvider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-...",
      "Model": "gpt-4",
      "MaxTokens": 2000
    },
    "Anthropic": {
      "ApiKey": "sk-ant-...",
      "Model": "claude-3-sonnet-20240229",
      "MaxTokens": 2000
    },
    "DataRetention": {
      "DefaultPolicy": "Indefinite",
      "AutoPurgeEnabled": true
    }
  }
}
```

**Alternatives Considered**:
- Direct environment variable access: Rejected - less structured
- Custom config files: Rejected - reinventing standard patterns
- Hardcoded defaults: Rejected - inflexible

---

## 6. File System Storage Design

### Decision: Provider Pattern with FileSystemStorageProvider

**Interface**: `IMemoryStorageProvider`
```csharp
public interface IMemoryStorageProvider
{
    Task<MemoryEntry> SaveAsync(string command, string userInput, string llmResponse, CancellationToken ct);
    Task<IReadOnlyList<MemoryEntry>> GetEntriesAsync(string command, DateRange dateRange, CancellationToken ct);
    Task<IReadOnlyList<MemoryEntry>> SearchAsync(string query, CancellationToken ct);
    Task DeleteAsync(string entryId, CancellationToken ct);
}
```

**Directory Structure**:
```
.memory/
├── today/
│   ├── 10-01-2025_1.md
│   ├── 10-01-2025_2.md
│   └── 10-02-2025_1.md
└── thisweek/
    ├── 2025-40_1.md    # Week 40 of 2025
    └── 2025-41_1.md
```

**File Naming Convention**:
- Daily entries: `MM-DD-YYYY_N.md` (zero-padded month/day, 4-digit year, 1-based counter)
- Weekly entries: `YYYY-WW_N.md` (4-digit year, ISO week number, 1-based counter)
- Counter increments for multiple same-day/week entries

**File Content Structure**:
```markdown
---
command: today
timestamp: 2025-10-01T14:30:00Z
entry-number: 1
llm-provider: OpenAI
llm-model: gpt-4
---

# User Input

What happened today?
> [User's response]

Anything interesting planned for tomorrow?
> [User's response]

# LLM Summary

## Key Events
- [Event 1]
- [Event 2]

## Themes
- [Theme 1]

## To-Do Items
- [ ] [Task 1]

## Important People
- [Person 1]
```

**Rationale**:
- Human-readable markdown format
- YAML frontmatter for metadata
- Preserves both user input and LLM response
- Easy to migrate to database later (just change provider implementation)
- Version control friendly
- No database setup required

**Future Extension Strategy**:
- PostgreSQLStorageProvider (structured data, full-text search)
- AzureBlobStorageProvider (cloud backup, mobile sync)
- Interface remains the same, swap at DI registration

**Alternatives Considered**:
- SQLite: Rejected for v1 - binary format, less user-friendly
- JSON files: Rejected - less human-readable than markdown
- Database first: Rejected - over-engineering for v1, harder local dev

---

## 7. Prompt Template Management

### Decision: Embedded Resources with Hot Reload Support

**Storage**: Markdown files in `src/Infrastructure/Prompts/Templates/`

**Template Structure** (daily-summary.md example):
```markdown
You are a personal memory assistant helping someone track their daily experiences.

The user has provided reflections on their day. Your task is to analyze their input and create a structured summary.

# User Input

{{USER_INPUT}}

# Instructions

Create a summary with the following sections:

1. **Key Events**: List 2-5 significant events from the day
2. **Themes**: Identify 1-3 recurring themes or patterns
3. **To-Do Items**: Extract any mentioned tasks or reminders (use checkbox format)
4. **Important People**: Note significant people mentioned
5. **Notable Tasks**: Highlight important work or personal tasks discussed

Keep the tone professional and neutral. Be concise but capture important details.
Format output as markdown with proper headings and lists.
```

**Template Loading**:
- Embedded resources in compiled assembly (production)
- File system watch for development (hot reload without rebuild)
- Template variables replaced using simple string interpolation
- Templates validated at startup

**Rationale**:
- Markdown is human-readable and easy to edit
- Version controlled with code
- Can be customized per user in future (user-specific template override directory)
- Separates prompt engineering from code

**Alternatives Considered**:
- Hardcoded strings: Rejected - hard to maintain and iterate
- External API for prompts: Rejected - unnecessary complexity
- Database storage: Rejected - overkill, needs deployment with code anyway

---

## 8. Dependency Injection Setup

### Decision: Microsoft.Extensions.DependencyInjection

**Registration Pattern**:
```csharp
// Program.cs
var services = new ServiceCollection();

// Configuration
services.AddSingleton<IConfiguration>(config);
services.AddOptions<TenSecondTomOptions>()
    .Bind(config.GetSection("TenSecondTom"))
    .ValidateDataAnnotations();

// Infrastructure
services.AddSingleton<IMemoryStorageProvider, FileSystemStorageProvider>();
services.AddSingleton<IPromptTemplateLoader, PromptTemplateLoader>();
services.AddSingleton<IMarkdownRenderer, MarkdownRenderer>();

// LLM Providers
services.AddSingleton<ILlmProvider, OpenAiProvider>();
services.AddSingleton<ILlmProvider, AnthropicProvider>();
services.AddSingleton<LlmProviderFactory>();

// Feature Handlers (Scoped for per-request isolation)
services.AddScoped<CreateDailyEntryHandler>();
services.AddScoped<CreateWeeklyReviewHandler>();
services.AddScoped<SearchMemoriesHandler>();

// Auth
services.AddSingleton<ISshAuthenticationService, SshAuthenticationService>();

var serviceProvider = services.BuildServiceProvider();
```

**Rationale**:
- Standard .NET DI container
- Built-in lifetime management (Singleton, Scoped, Transient)
- Integrates with configuration system
- Testable - easy to mock dependencies

---

## 9. Testing Strategy

### Decision: xUnit + FluentAssertions + Moq

**Test Categories**:

1. **Unit Tests** (80%+ of tests)
   - Test individual handlers, providers, services in isolation
   - Mock all dependencies
   - Fast execution (< 1ms per test)
   - Focus on business logic

2. **Integration Tests**
   - Test feature workflows end-to-end
   - Use real file system (temp directories)
   - Mock LLM providers (use test fixtures)
   - Test data flow through multiple components

3. **CLI Tests**
   - Test command parsing and execution
   - Verify output formatting
   - Test error handling and help text
   - Use TestConsole for output capture

**Example Test Structure**:
```csharp
public class CreateDailyEntryHandlerTests
{
    [Fact]
    public async Task Handle_WithValidInput_CreatesDailyEntry()
    {
        // Arrange
        var mockStorage = new Mock<IMemoryStorageProvider>();
        var mockLlm = new Mock<ILlmProvider>();
        var handler = new CreateDailyEntryHandler(mockStorage.Object, mockLlm.Object);
        
        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        mockStorage.Verify(s => s.SaveAsync(
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**Coverage Target**: 80% minimum across all projects

---

## 10. Error Handling & Logging

### Decision: Result Pattern + Serilog

**Result Type** (avoid exceptions for expected failures):
```csharp
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    
    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(string error) => new() { IsSuccess = false, Error = error };
}
```

**Logging**: Serilog with console sink
- Structured logging
- Configurable log levels
- Request/response logging for LLM calls
- Performance timing for operations
- Error context capture

**Error Categories**:
- User errors (invalid input) → Friendly message, exit code 1
- System errors (file I/O) → Error message + log, exit code 2
- LLM errors (API failure) → Save user input, suggest retry, exit code 3
- Auth errors (invalid key) → Clear instructions, exit code 4

---

## Technology Stack Summary

| Component | Technology | Rationale |
|-----------|-----------|-----------|
| **Framework** | .NET 9 | Modern, cross-platform, high performance |
| **CLI** | System.CommandLine | Official Microsoft CLI framework |
| **LLM - OpenAI** | OpenAI SDK | Official SDK, full-featured |
| **LLM - Anthropic** | Anthropic.SDK | Best community SDK available |
| **Markdown** | Markdig | Fast, extensible, CommonMark compliant |
| **Terminal UI** | Spectre.Console | Rich formatting, cross-platform |
| **SSH Auth** | SSH.NET | Mature, supports Ed25519/RSA |
| **Configuration** | MS.Extensions.Configuration | Standard .NET config stack |
| **DI** | MS.Extensions.DependencyInjection | Built-in, sufficient for CLI |
| **Testing** | xUnit + FluentAssertions + Moq | Industry standard, expressive |
| **Logging** | Serilog | Structured logging, flexible sinks |
| **Storage** | File system (markdown) | Simple, human-readable, migrable |

---

## Development Environment Setup

### Prerequisites
- .NET 9 SDK
- Git
- Code editor (VS Code, Visual Studio, Rider)

### Initial Setup
```bash
git clone https://github.com/sirkirby/ten-second-tom.git
cd ten-second-tom
dotnet restore
dotnet user-secrets init --project src
dotnet user-secrets set "TenSecondTom:OpenAI:ApiKey" "sk-..." --project src
dotnet user-secrets set "TenSecondTom:Anthropic:ApiKey" "sk-ant-..." --project src
dotnet build
dotnet test
```

### Running Locally
```bash
dotnet run --project src -- today
dotnet run --project src -- thisweek
dotnet run --project src -- search --query "meeting"
```

---

## Next Steps

✅ All technical decisions made
✅ No NEEDS CLARIFICATION remaining
✅ Ready to proceed to Phase 1 (Design & Contracts)

**Phase 1 Deliverables**:
- data-model.md (entities and relationships)
- contracts/ (command/query contracts)
- quickstart.md (first user experience)
- Test specifications for each contract
