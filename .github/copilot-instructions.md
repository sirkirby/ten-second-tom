# Ten Second Tom - GitHub Copilot Instructions

## Project Overview

Ten Second Tom is a modern CLI application built with C# and .NET 9, designed for simplicity, testability, and cross-platform distribution. This project follows strict architectural principles to ensure code quality, maintainability, and excellent developer experience.

## Core Development Rules

### Language & Framework (NON-NEGOTIABLE)

- Use **C# with .NET 9** exclusively
- Follow modern C# idioms: nullable reference types, pattern matching, records, init-only properties
- Use async/await patterns for I/O-bound operations
- Leverage .NET 9 performance features (frozen collections, source generators, etc.)
- Follow Microsoft C# coding conventions

### Architecture Patterns

When suggesting code, apply these patterns:

- **Vertical Slice Architecture (VSA) with Co-location Pattern**: Organize features as self-contained vertical slices with all related code co-located in a single file per use case
  - Use case files: `CreateUser.cs`, `ListItems.cs`, `GenerateOutput.cs`
  - Static class container: `public static class [UseCase]` with nested Command/Query, Validator, Handler
  - Example: `CreateUser.cs` contains `CreateUser.Command`, `CreateUser.Validator`, `CreateUser.Handler`
- **CQRS**: Separate commands (mutations) from queries (reads)
  - Nested Command: `public sealed record Command(...) : IRequest<Result<T>>`
  - Nested Query: `public sealed record Query(...) : IRequest<Result<T>>`
  - Nested Handler: `public sealed class Handler(...) : IRequestHandler<Command, Result<T>>`
  - Assembly scanning auto-discovers all nested handlers and validators
- **Options Pattern**: All configuration MUST use strongly-typed options classes
  - Options: `StorageOptions`, `LlmOptions`, `AuthOptions` (in `Shared/Options/`)
  - Validators: `StorageOptionsValidator`, `LlmOptionsValidator` (in `Shared/Options/Validation/`)
  - Inject: `IOptions<T>`, `IOptionsSnapshot<T>`, or `IOptionsMonitor<T>` (NOT `IConfiguration`)
- **Factory Pattern**: Use for complex object construction
- **Dependency Injection**: Always use .NET's built-in DI container

### Testing (MANDATORY)

- **Test-First Development**: Generate test code before implementation code
- **xUnit Framework**: Use xUnit exclusively for all tests
- **80% Coverage Minimum**: Ensure comprehensive test coverage
- **Test Structure**:
  - Unit tests: `tests/Unit/Features/[Feature]/[Class]Tests.cs`
  - Integration tests: `tests/Integration/Features/[Feature]/[Scenario]Tests.cs`
  - CLI tests: `tests/Integration/Cli/[Command]Tests.cs`
- **Use FluentAssertions** for readable assertions
- **Use Moq or NSubstitute** for mocking dependencies
- Tests must be fast, isolated, and deterministic

### Code Quality Standards

- **DRY Principle**: Never duplicate logic; extract to reusable methods/classes
- **No Compiler Warnings**: Code must compile without warnings
- **XML Documentation**: Add XML comments to all public APIs
- **Naming Conventions** (Co-location Pattern since v1.7.0):
  - Use case files: `[Verb][Noun].cs` (e.g., `CreateUser.cs`, `ListTemplates.cs`)
  - Nested Command: `public sealed record Command(...) : IRequest<Result<T>>`
  - Nested Query: `public sealed record Query(...) : IRequest<Result<T>>`
  - Nested Validator: `public sealed class Validator : AbstractValidator<Command>`
  - Nested Handler: `public sealed class Handler(...) : IRequestHandler<Command, Result<T>>`
  - Test files: `[UseCase]Tests.cs` (e.g., `CreateUserTests.cs`, `ListTemplatesTests.cs`)
  - Interfaces: `I*`
- **Error Handling**:
  - Use exceptions only for exceptional cases
  - Return `Result<T>` types for expected failures
  - Provide clear, actionable error messages
  - Always log errors with context

## Project Structure

**NOTE**: The canonical project structure is defined in `.specify/memory/constitution.md` (Project Structure Standards v1.7.0). The structure below is a summary for quick reference.

**Co-location Pattern** (since v1.7.0): All code for a single use case co-located in one file.

```text
src/
├── Features/          # Vertical slices (self-contained feature modules)
│   └── [FeatureName]/
│       ├── [UseCase].cs   # Co-located Command/Query, Validator, Handler
│       ├── Migrations/    # Feature bootstrap migrations [if needed]
│       ├── Services/      # Feature-specific domain services [if needed]
│       └── DependencyInjection.cs  # Feature-specific DI registration
├── Infrastructure/    # Cross-cutting concerns (DI, config, logging, behaviors)
│   ├── Behaviors/         # MediatR pipeline behaviors
│   ├── Configuration/
│   ├── Logging/
│   └── DependencyInjection/
├── Shared/            # Shared domain models, abstractions, utilities
│   ├── Models/
│   ├── Options/           # Configuration options classes (*Options.cs)
│   │   └── Validation/    # Options validators (*OptionsValidator.cs)
│   ├── Constants/         # Centralized constants
│   ├── Abstractions/
│   └── Extensions/
└── Program.cs         # Entry point

tests/
├── TenSecondTom.Tests/         # Unit tests (fast, isolated)
│   └── Features/
│       └── [FeatureName]/
│           └── [UseCase]Tests.cs  # Tests mirror use case files
└── TenSecondTom.IntegrationTests/  # Integration tests
    ├── Features/
    │   └── [FeatureName]/
    └── Cli/
```

**Co-location Pattern Structure** (one file per use case):

```csharp
namespace TenSecondTom.Features.[FeatureName];

/// <summary>
/// [Brief description of what this use case does]
/// </summary>
public static class [UseCase]
{
    public sealed record Command(...) : IRequest<Result<T>>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator() { /* validation rules */ }
    }

    public sealed class Handler(...) : IRequestHandler<Command, Result<T>>
    {
        public async Task<Result<T>> Handle(Command request, CancellationToken ct)
        {
            // Business logic (input is pre-validated, execution is pre-logged)
        }
    }
}
```

**See `.specify/memory/constitution.md` v1.7.0 for detailed rules on:**
- Co-location pattern requirements
- Feature organization rules
- Naming conventions for vertical slices
- Cross-feature dependency restrictions
- Test structure mirroring
- Assembly scanning for auto-discovery

## Configuration Management (REQUIRED)

**All configuration MUST use the .NET Options Pattern. Direct `IConfiguration` access with string keys is PROHIBITED.**

### Options Pattern Requirements

1. **Create Options class** in `src/Shared/Options/`:
   ```csharp
   public sealed class MyServiceOptions
   {
       public const string SectionName = "TenSecondTom:MyService";
       public required string ApiKey { get; init; }
       public int Timeout { get; init; } = 30;
   }
   ```

2. **Create Validator** in `src/Shared/Options/Validation/`:
   ```csharp
   public sealed class MyServiceOptionsValidator : IValidateOptions<MyServiceOptions>
   {
       public ValidateOptionsResult Validate(string? name, MyServiceOptions options)
       {
           if (string.IsNullOrWhiteSpace(options.ApiKey))
               return ValidateOptionsResult.Fail("ApiKey is required");
           return ValidateOptionsResult.Success;
       }
   }
   ```

3. **Register in DI** (ServiceCollectionExtensions.cs):
   ```csharp
   services.Configure<MyServiceOptions>(configuration.GetSection(MyServiceOptions.SectionName));
   services.AddSingleton<IValidateOptions<MyServiceOptions>, MyServiceOptionsValidator>();
   ```

4. **Inject and use**:
   ```csharp
   public sealed class MyService(IOptions<MyServiceOptions> options)
   {
       private readonly MyServiceOptions _options = options.Value;

       public void DoWork()
       {
           var apiKey = _options.ApiKey; // Type-safe!
       }
   }
   ```

### Options Interfaces

- `IOptions<T>`: Singleton - use for static configuration (most CLI scenarios)
- `IOptionsSnapshot<T>`: Scoped - use when config may change per scope
- `IOptionsMonitor<T>`: Singleton with change notifications - use for hot-reload

## CLI Design Guidelines

- Use `System.CommandLine` for CLI implementation
- Commands should be intuitive and follow Unix conventions
- Support both interactive and scripted usage
- Provide helpful error messages and usage examples
- Output formats:
  - Human-readable text by default
  - Optional JSON output with `--json` flag
- Exit codes:
  - `0` for success
  - Non-zero for errors (with meaningful codes)

## Code Generation Guidelines

### When Generating New Features

1. **Start with tests**: Create test file first showing expected behavior
2. **Create vertical slice structure**:
   - Command/Query class with validation
   - Handler class with business logic
   - Required DTOs or models
   - Registration in DI container
3. **Add CLI command** if user-facing
4. **Update documentation** if adding public API

### When Modifying Existing Code

1. **Check for existing tests** and update them first
2. **Maintain DRY principle**: Refactor if creating duplication
3. **Preserve error handling patterns**
4. **Update XML documentation** for public API changes

### Code Style Preferences

- **Prefer explicit types** over `var` for public APIs
- **Use file-scoped namespaces** (C# 10+)
- **Use primary constructors** (C# 12) for simple classes
- **Use collection expressions** `[item1, item2]` (C# 12)
- **Prefer `required` properties** over constructor parameters where appropriate
- **Use `readonly` liberally** for immutability
- **Avoid `null`**: Use nullable reference types and guard clauses
- **No magic strings**: Use constants from `Shared/Constants/` for config keys, command names, paths, and shared identifiers (logging/diagnostic strings are exceptions)

## Security & Secrets

- **Never hardcode secrets** in source files
- Use environment variables or .NET User Secrets for development
- Reference Azure Key Vault or similar for production
- Always validate and sanitize user input
- Log security events (auth, access, errors)

## Performance Considerations

- Use `Span<T>` and `Memory<T>` for performance-critical code
- Prefer `ValueTask<T>` over `Task<T>` for frequently synchronous paths
- Use frozen collections for static data
- Cache expensive operations appropriately
- Profile before optimizing

## Dependencies Management

- **Minimize dependencies**: Only add packages with clear justification
- **Prefer Microsoft packages** for core functionality
- **Pin versions** in `.csproj` for reproducibility
- **Common packages**:
  - `System.CommandLine` for CLI
  - `FluentValidation` for validation
  - `Serilog` for logging
  - `FluentAssertions` for tests
  - `Moq` or `NSubstitute` for mocking

## Version Control & Releases

- **Semantic Versioning**: MAJOR.MINOR.PATCH
  - Breaking changes → MAJOR
  - New features → MINOR
  - Bug fixes → PATCH
- **Conventional Commits**: Use semantic commit messages
  - `feat:` for new features
  - `fix:` for bug fixes
  - `docs:` for documentation
  - `test:` for tests
  - `refactor:` for refactoring
- **Automated releases** via GitHub Actions on merge to `main`

## What NOT to Do

❌ Don't suggest web or GUI frameworks (ASP.NET Core, Blazor, WPF, etc.)
❌ Don't use test frameworks other than xUnit
❌ Don't ignore test coverage requirements
❌ Don't duplicate code instead of extracting reusable components
❌ Don't hardcode configuration or secrets
❌ Don't use direct `IConfiguration` access with string keys (use Options Pattern)
❌ Don't swallow exceptions without logging
❌ Don't use outdated C# patterns (pre-C# 9)
❌ Don't create anemic domain models (models without behavior)
❌ Don't violate DRY, SOLID, or KISS principles
❌ Don't use magic strings for config keys, command names, paths, or shared identifiers

## Documentation

- Keep README.md up to date with setup instructions
- Use XML documentation comments for public APIs
- Create ADRs (Architecture Decision Records) for significant architectural choices
- Include inline comments only for non-obvious "why" explanations, not "what"

## Examples

### Options Pattern vs Direct Configuration

```csharp
// ❌ BAD - Direct IConfiguration access with string keys
public class MyService
{
    private readonly IConfiguration _configuration;

    public MyService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void DoWork()
    {
        var apiKey = _configuration["MyApp:ApiKey"]; // NO! Magic string, no type safety
        var timeout = int.Parse(_configuration["MyApp:Timeout"]); // NO! Runtime errors
    }
}

// ✅ GOOD - Options Pattern
// Options class in Shared/Options/
public sealed class MyServiceOptions
{
    public const string SectionName = "TenSecondTom:MyService";
    public required string ApiKey { get; init; }
    public int Timeout { get; init; } = 30;
}

// Service using options
public sealed class MyService(IOptions<MyServiceOptions> options)
{
    private readonly MyServiceOptions _options = options.Value;

    public void DoWork()
    {
        var apiKey = _options.ApiKey; // ✅ Type-safe, IntelliSense support
        var timeout = _options.Timeout; // ✅ No parsing, validated on startup
    }
}
```

### Constants vs Magic Strings

```csharp
// ❌ BAD - Magic strings
var memoryDir = configuration["TenSecondTom:MemoryDirectory"];
if (commandName == "today") { /* ... */ }
var logsPath = Path.Combine(baseDir, "logs");

// ✅ GOOD - Use constants from Shared/Constants/
var memoryDir = configuration[ConfigurationKeys.MemoryDirectory];
if (commandName == CommandNames.Today) { /* ... */ }
var logsPath = Path.Combine(baseDir, DirectoryNames.Logs);

// ✅ ALLOWED - Logging and diagnostics
_logger.LogInformation("Processing voice entry for user {UserId}", userId);
Console.WriteLine("Setup complete! Run 'tom today' to get started.");
```

### Good Command Structure

```csharp
public sealed record CreateUserCommand(
    string Username,
    string Email) : IRequest<Result<Guid>>;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IUserRepository _repository;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository repository,
        ILogger<CreateUserCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

### Good Test Structure

```csharp
public sealed class CreateUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        // Arrange
        var repository = new Mock<IUserRepository>();
        var handler = new CreateUserCommandHandler(
            repository.Object,
            Mock.Of<ILogger<CreateUserCommandHandler>>());
        
        var command = new CreateUserCommand("john", "john@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        repository.Verify(r => r.AddAsync(
            It.IsAny<User>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

## Priority Order for Suggestions

1. **Correctness**: Code must work correctly and handle edge cases
2. **Tests**: Always include or update tests
3. **Maintainability**: Clear, readable, DRY code
4. **Performance**: Optimize when necessary, but readability first
5. **Documentation**: Update docs for public API changes

---

**Constitution Version**: 1.7.0 | **Last Updated**: 2025-10-28

For questions about architectural decisions or edge cases, consult `.specify/memory/constitution.md`.
