# Quickstart: Generate Feature Development

**Feature**: Generate Command for Recording Processing
**Branch**: `009-generate-recordings`
**For**: Developers implementing or extending this feature

## Overview

This guide helps developers quickly understand and work with the Generate feature. It covers architecture, key patterns, testing approach, and common development tasks.

## Architecture at a Glance

```text
┌─────────────────────────────────────────────────────────────────┐
│                         CLI Layer                                │
│  tom generate [--template NAME]                                  │
│  (System.CommandLine + Spectre.Console)                         │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Features/Generate/                            │
│                                                                   │
│  Commands/                                                       │
│  ├── GenerateOutputCommand ──────┐                              │
│                                   │                               │
│  Queries/                         │                               │
│  ├── ListRecordingsQuery ─────┐  │                              │
│  └── GetRecordingTranscriptQuery │                              │
│                                │  │                               │
│  Handlers/                     ▼  ▼                              │
│  ├── GenerateOutputCommandHandler ◄─── Core Orchestration       │
│  ├── ListRecordingsQueryHandler                                 │
│  └── GetRecordingTranscriptQueryHandler                         │
│                                   │                               │
│  Services/                        │                               │
│  ├── IRecordingService ◄──────────┤                             │
│  ├── ITranscriptProcessor ◄───────┤                             │
│  └── IOutputStorageService ◄──────┘                             │
│                                                                   │
└───────────────┬────────────────────────┬────────────────────────┘
                │                        │
                ▼                        ▼
┌──────────────────────┐   ┌──────────────────────────┐
│ Templates Feature    │   │ Infrastructure/Llm       │
│ (Existing)           │   │ (Existing)               │
│                      │   │                          │
│ ListTemplatesQuery   │   │ ILlmProvider             │
│ PromptTemplate model │   │ GenerateCompletionAsync()│
└──────────────────────┘   └──────────────────────────┘
```

## Key Patterns

### 1. Vertical Slice Architecture (VSA)

Each feature is self-contained in its own directory:

```text
src/Features/Generate/
├── Commands/       # Write operations
├── Queries/        # Read operations
├── Handlers/       # Business logic
├── Models/         # Feature-specific models
├── Services/       # Domain services
└── DependencyInjection.cs
```

**Why**: Keeps related code together, makes features easy to understand and modify in isolation.

### 2. CQRS (Command Query Responsibility Segregation)

- **Commands**: Change state (GenerateOutputCommand → saves file)
- **Queries**: Read-only (ListRecordingsQuery → no side effects)

**Why**: Clear separation of concerns, easier to test, better performance optimization opportunities.

### 3. Result Pattern

All operations return `Result<T>` instead of throwing exceptions for expected errors:

```csharp
var result = await handler.Handle(command, cancellationToken);

if (result.IsSuccess)
{
    var output = result.Value;
    // Happy path
}
else
{
    var error = result.Error;
    // Handle error gracefully
}
```

**Why**: Makes error handling explicit, improves testability, avoids exception performance costs.

### 4. Dependency Injection

All dependencies injected via constructor:

```csharp
public sealed class GenerateOutputCommandHandler
{
    private readonly IRecordingService _recordingService;
    private readonly ILlmProvider _llmProvider;
    // etc...

    public GenerateOutputCommandHandler(
        IRecordingService recordingService,
        ILlmProvider llmProvider,
        // etc...
    )
    {
        _recordingService = recordingService;
        _llmProvider = llmProvider;
    }
}
```

**Why**: Testability (easy to mock), loose coupling, clear dependencies.

## Development Workflow

### Step 1: Set Up Your Environment

```bash
# Clone and navigate to repo
cd ten-second-tom
git checkout 009-generate-recordings

# Restore dependencies
dotnet restore

# Run tests to verify setup
dotnet test
```

### Step 2: Understand Existing Patterns

Before writing code, study similar features:

```bash
# Look at Today feature (similar template selection)
src/Features/Today/

# Look at Templates feature (template loading)
src/Features/Templates/

# Look at Audio feature (recording model)
src/Features/Audio/Models/StoredRecording.cs
```

### Step 3: TDD Approach (REQUIRED)

**Red → Green → Refactor**

Example for `RecordingService`:

```csharp
// 1. RED: Write failing test
[Fact]
public async Task ListRecordingsAsync_WithNoRecordings_ReturnsFailure()
{
    // Arrange
    var fileSystem = new MockFileSystem();
    var config = CreateTestConfig();
    var logger = Mock.Of<ILogger<RecordingService>>();
    var service = new RecordingService(fileSystem, config, logger);

    // Act
    var result = await service.ListRecordingsAsync();

    // Assert
    result.IsSuccess.Should().BeFalse();
    result.Error.Should().Contain("No recordings found");
}

// 2. GREEN: Implement minimum code to pass
public async Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(...)
{
    var files = _fileSystem.Directory.GetFiles(_recordingDirectory, "recording-*.txt");

    if (files.Length == 0)
        return Result<IReadOnlyList<RecordingListItem>>.Failure(
            "No recordings found...");

    // ... rest of implementation
}

// 3. REFACTOR: Clean up while keeping tests green
```

### Step 4: Run Tests Frequently

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~RecordingServiceTests"

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

### Step 5: Manual Testing

```bash
# Build project
dotnet build

# Run command
dotnet run -- generate

# With template argument
dotnet run -- generate --template "business-meeting"
```

## Common Development Tasks

### Adding a New Service

1. **Define interface** in `Services/IYourService.cs`
2. **Implement** in `Services/YourService.cs`
3. **Register** in `DependencyInjection.cs`:
```csharp
services.AddTransient<IYourService, YourService>();
```
4. **Write tests** in `tests/TenSecondTom.Tests/Features/Generate/Services/YourServiceTests.cs`

### Adding a New Command/Query

1. **Define** in `Commands/YourCommand.cs` or `Queries/YourQuery.cs`:
```csharp
public sealed record YourCommand : IRequest<Result<YourResult>>
{
    public required string SomeProperty { get; init; }
}
```

2. **Create handler** in `Handlers/YourCommandHandler.cs`:
```csharp
public sealed class YourCommandHandler
    : IRequestHandler<YourCommand, Result<YourResult>>
{
    public async Task<Result<YourResult>> Handle(
        YourCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

3. **Register** in `DependencyInjection.cs`:
```csharp
services.AddTransient<YourCommandHandler>();
services.AddTransient<IRequestHandler<YourCommand, Result<YourResult>>>(
    sp => sp.GetRequiredService<YourCommandHandler>());
```

4. **Write tests** for handler

### Adding a New Constant

1. **Choose correct file** in `src/Shared/Constants/`:
   - `CommandNames.cs` - CLI command names
   - `TemplateConstants.cs` - Template IDs and limits
   - `LlmConstants.cs` - LLM-related values
   - `DirectoryNames.cs` - Directory names

2. **Add constant with XML docs**:
```csharp
/// <summary>
/// Command name for generate feature.
/// </summary>
public const string Generate = "generate";
```

3. **Use constant** instead of magic string:
```csharp
// ❌ Bad
if (commandName == "generate") { }

// ✅ Good
if (commandName == CommandNames.Generate) { }
```

### Adding Configuration Value

1. **Add to `ConfigurationKeys.cs`**:
```csharp
/// <summary>
/// Configuration key for your setting.
/// Environment variable: TenSecondTom__YourSection__YourKey
/// </summary>
public const string YourSetting = "TenSecondTom:YourSection:YourKey";
```

2. **Add to `appsettings.json`**:
```json
{
  "TenSecondTom": {
    "YourSection": {
      "YourKey": "default-value"
    }
  }
}
```

3. **Access in code**:
```csharp
var value = _configuration[ConfigurationKeys.YourSetting];
```

## Testing Strategies

### Unit Tests

**What to test**:
- Handler logic with mocked dependencies
- Service methods with mocked filesystem (MockFileSystem)
- Token estimation and truncation logic
- Validation rules
- Edge cases (empty, null, max values)

**Example**:
```csharp
public sealed class TranscriptProcessorTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("one two three", 3)]
    [InlineData("one  two   three", 3)] // Multiple spaces
    public void CountWords_WithVariousInputs_ReturnsCorrectCount(
        string input,
        int expected)
    {
        // Arrange
        var processor = CreateProcessor();

        // Act
        var actual = processor.CountWords(input);

        // Assert
        actual.Should().Be(expected);
    }
}
```

### Integration Tests

**What to test**:
- End-to-end command execution
- File system operations with real filesystem
- Multiple services working together
- CLI interface

**Example**:
```csharp
public sealed class GenerateCommandIntegrationTests : IDisposable
{
    private readonly TestFileSystem _testFs;
    private readonly ServiceProvider _serviceProvider;

    public GenerateCommandIntegrationTests()
    {
        _testFs = new TestFileSystem();
        _serviceProvider = BuildServiceProvider();
    }

    [Fact]
    public async Task Generate_WithValidRecordingAndTemplate_CreatesOutputFile()
    {
        // Arrange: Create test recording
        _testFs.CreateRecording("recording-20251024-143022.txt", "Test transcript...");

        var command = new GenerateOutputCommand
        {
            TranscriptFilePath = _testFs.GetPath("recording-20251024-143022.txt"),
            RecordingBaseName = "recording-20251024-143022",
            TemplateId = "business-meeting",
            MaxInputTokens = 8000
        };

        var handler = _serviceProvider.GetRequiredService<GenerateOutputCommandHandler>();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _testFs.FileExists("recording-20251024-143022_business-meeting.md").Should().BeTrue();
    }

    public void Dispose()
    {
        _testFs.Dispose();
        _serviceProvider.Dispose();
    }
}
```

## Debugging Tips

### 1. Enable Verbose Logging

```bash
export DOTNET_ENVIRONMENT=Development
export Serilog__MinimumLevel__Default=Debug
dotnet run -- generate
```

### 2. Inspect File Operations

Use `IFileSystem` abstraction for easy debugging:

```csharp
// In test, inject MockFileSystem
var mockFs = new MockFileSystem();
mockFs.AddFile("/path/recording.txt", new MockFileData("transcript"));

// Can inspect all file operations
var allFiles = mockFs.AllFiles;
```

### 3. Test LLM Integration Separately

Mock `ILlmProvider` to test without API calls:

```csharp
var mockLlm = new Mock<ILlmProvider>();
mockLlm.Setup(x => x.GenerateCompletionAsync(
        It.IsAny<string>(),
        It.IsAny<CancellationToken>(),
        It.IsAny<int?>(),
        It.IsAny<double?>()))
    .ReturnsAsync(Result<LlmResponse>.Success(new LlmResponse
    {
        Content = "Mock response",
        InputTokens = 100,
        OutputTokens = 50
    }));
```

### 4. Use Breakpoint Logging

```csharp
_logger.LogDebug("DEBUG: Transcript length: {Length}", transcript.Length);
_logger.LogDebug("DEBUG: Estimated tokens: {Tokens}", estimatedTokens);
```

## Common Pitfalls

### ❌ Don't: Use magic strings

```csharp
if (command == "generate") { }  // BAD
```

### ✅ Do: Use constants

```csharp
if (command == CommandNames.Generate) { }  // GOOD
```

---

### ❌ Don't: Swallow exceptions

```csharp
try
{
    await SaveFileAsync(path);
}
catch
{
    // Silent failure - BAD!
}
```

### ✅ Do: Return Result<T>

```csharp
try
{
    await SaveFileAsync(path);
    return Result<string>.Success(path);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to save file");
    return Result<string>.Failure($"Failed to save: {ex.Message}");
}
```

---

### ❌ Don't: Test implementation details

```csharp
// Testing private method behavior - BAD
[Fact]
public void PrivateHelper_DoesInternalThing()
{
    var result = InvokePrivateMethod();
    // ...
}
```

### ✅ Do: Test public contracts

```csharp
// Testing observable behavior - GOOD
[Fact]
public async Task Handle_WithValidInput_ReturnsSuccess()
{
    var result = await handler.Handle(command, CancellationToken.None);
    result.IsSuccess.Should().BeTrue();
}
```

---

### ❌ Don't: Couple to filesystem

```csharp
var files = Directory.GetFiles("/hardcoded/path");  // BAD
```

### ✅ Do: Use IFileSystem abstraction

```csharp
var files = _fileSystem.Directory.GetFiles(_recordingDirectory);  // GOOD
```

## Performance Considerations

### File Operations

- Use async I/O for all file operations
- Don't load all transcript content into memory at once for large files
- Cache template list (templates change infrequently)

```csharp
// Async file reading
var content = await _fileSystem.File.ReadAllTextAsync(path, cancellationToken);
```

### LLM Calls

- Set reasonable timeouts (120s default)
- Handle rate limits gracefully
- Log token usage for cost tracking

```csharp
var result = await _llmProvider.GenerateCompletionAsync(
    prompt,
    cancellationToken,
    maxTokens: 4000);  // Limit output tokens
```

## Code Quality Checklist

Before submitting PR:

- [ ] All tests pass (`dotnet test`)
- [ ] Code coverage ≥ 80% (`dotnet test /p:CollectCoverage=true`)
- [ ] No compiler warnings
- [ ] XML docs on public APIs
- [ ] Constants used instead of magic strings
- [ ] Result<T> pattern for expected errors
- [ ] Async/await used correctly
- [ ] Logging at appropriate levels
- [ ] Manual testing completed

## Resources

**Codebase References**:
- Constitution: `.specify/memory/constitution.md`
- Feature Spec: `specs/009-generate-recordings/spec.md`
- Research: `specs/009-generate-recordings/research.md`
- Data Model: `specs/009-generate-recordings/data-model.md`
- Contracts: `specs/009-generate-recordings/contracts/`

**Similar Features**:
- Today feature: `src/Features/Today/`
- Templates feature: `src/Features/Templates/`
- Audio feature: `src/Features/Audio/`

**External Docs**:
- [System.CommandLine docs](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [Spectre.Console docs](https://spectreconsole.net/)
- [xUnit docs](https://xunit.net/)
- [FluentAssertions docs](https://fluentassertions.com/)

## Getting Help

1. **Check existing patterns**: Look at similar features in the codebase
2. **Review constitution**: `.specify/memory/constitution.md` has architectural guidance
3. **Ask questions**: Create GitHub issue or discussion
4. **Pair program**: Reach out to team members familiar with the codebase

---

**Ready to start?** Begin with Step 1 (Set Up Environment) and follow TDD workflow!
