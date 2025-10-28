# Ten Second Tom - Claude Code Instructions

## Project Context

Ten Second Tom is a modern CLI application built with **C# and .NET 9**. This is a cross-platform command-line tool designed for simplicity, testability, and excellent developer experience. The project follows strict architectural principles documented in `.specify/memory/constitution.md`.

## Technology Stack

- **Language**: C# 10+ (modern features required)
- **Framework**: .NET 9
- **Testing**: xUnit, FluentAssertions, Moq/NSubstitute
- **CLI Framework**: System.CommandLine
- **Logging**: Serilog
- **Validation**: FluentValidation
- **Target Platforms**: macOS, Windows (Linux future)

## Development Workflow

### Before Making Changes

1. **Read the constitution** at `.specify/memory/constitution.md` - it contains non-negotiable principles
2. **Check for existing tests** - update tests first (TDD approach)
3. **Understand the feature context** - read related files in the vertical slice
4. **Look for duplication** - refactor rather than duplicate

### When Creating New Features

Follow this order (TDD):

1. **Tests First**: Create xUnit test file showing expected behavior
2. **Make tests fail**: Verify tests fail with clear error messages
3. **Minimal implementation**: Write just enough code to pass tests
4. **Refactor**: Clean up while keeping tests green
5. **Document**: Add XML comments to public APIs

### Code Organization Patterns

**Vertical Slice Architecture**: Each feature is self-contained.

**NOTE**: The canonical project structure is defined in `.specify/memory/constitution.md` (Project Structure Standards). Always consult the constitution for authoritative structural guidance.

```text
src/Features/[FeatureName]/
├── Commands/          # Command classes (mutations)
├── Queries/           # Query classes (reads) [if needed]
├── Handlers/          # Business logic handlers
├── Validation/        # FluentValidation validators [if needed]
└── DependencyInjection.cs  # Feature-specific DI registration

src/Infrastructure/    # Cross-cutting concerns (DI, config, logging)
src/Shared/           # Shared models, abstractions, extensions

tests/TenSecondTom.Tests/Features/[FeatureName]/
tests/TenSecondTom.IntegrationTests/Features/[FeatureName]/
```

**CQRS Pattern**: Separate commands from queries.

```csharp
// Command (mutation)
public sealed record CreateUserCommand(string Username, string Email) 
    : IRequest<Result<Guid>>;

// Query (read)
public sealed record GetUserQuery(Guid UserId) 
    : IRequest<Result<UserDto>>;

// Handler
public sealed class CreateUserCommandHandler 
    : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    // Implementation
}
```

## Configuration Management (REQUIRED)

### .NET Options Pattern

**All configuration MUST use the .NET Options Pattern. Direct `IConfiguration` access with string keys is PROHIBITED.**

```csharp
// ❌ PROHIBITED - Stringly-typed configuration
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

// ✅ REQUIRED - Options Pattern with strongly-typed configuration
// 1. Create Options class in src/Shared/Options/
namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for MyService.
/// Maps to the "TenSecondTom:MyService" configuration section.
/// </summary>
/// <remarks>
/// Configuration example (appsettings.json):
/// <code>
/// {
///   "TenSecondTom": {
///     "MyService": {
///       "ApiKey": "your-key-here",
///       "Timeout": 30
///     }
///   }
/// }
/// </code>
///
/// Environment variables:
/// - TenSecondTom__MyService__ApiKey
/// - TenSecondTom__MyService__Timeout
/// </remarks>
public sealed class MyServiceOptions
{
    public const string SectionName = "TenSecondTom:MyService";

    public required string ApiKey { get; init; }
    public int Timeout { get; init; } = 30;
}

// 2. Create Validator in src/Shared/Options/Validation/
public sealed class MyServiceOptionsValidator : IValidateOptions<MyServiceOptions>
{
    public ValidateOptionsResult Validate(string? name, MyServiceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return ValidateOptionsResult.Fail("ApiKey is required");

        if (options.Timeout <= 0)
            return ValidateOptionsResult.Fail("Timeout must be positive");

        return ValidateOptionsResult.Success;
    }
}

// 3. Register in ServiceCollectionExtensions.cs
services.Configure<MyServiceOptions>(configuration.GetSection(MyServiceOptions.SectionName));
services.AddSingleton<IValidateOptions<MyServiceOptions>, MyServiceOptionsValidator>();

// 4. Inject and use in service
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

### Options Pattern Interfaces

Choose the right interface based on your needs:

- **`IOptions<T>`**: Singleton - configuration doesn't change during application lifetime
  ```csharp
  public MyService(IOptions<MyServiceOptions> options)
  {
      _options = options.Value; // Read once and cache
  }
  ```

- **`IOptionsSnapshot<T>`**: Scoped - configuration reloads per scope (e.g., per CLI command execution)
  ```csharp
  public MyService(IOptionsSnapshot<MyServiceOptions> options)
  {
      _options = options.Value; // Reloads per scope
  }
  ```

- **`IOptionsMonitor<T>`**: Singleton with change notifications - hot-reload scenarios
  ```csharp
  public MyService(IOptionsMonitor<MyServiceOptions> monitor)
  {
      _monitor = monitor;
      _monitor.OnChange(options => {
          // React to configuration changes
      });
  }
  ```

**For most CLI scenarios, use `IOptions<T>` since configuration is typically static during a single command execution.**

### Options Pattern Checklist

When creating configuration for a new feature:

1. ✅ Create `*Options.cs` in `src/Shared/Options/`
2. ✅ Add `public const string SectionName` to options class
3. ✅ Use `required` for mandatory properties or provide sensible defaults
4. ✅ Add comprehensive XML documentation with config examples
5. ✅ Create `*OptionsValidator.cs` in `src/Shared/Options/Validation/`
6. ✅ Register both options and validator in `ServiceCollectionExtensions.cs`
7. ✅ Inject `IOptions<T>` (or `IOptionsSnapshot<T>`) into services
8. ✅ Never inject `IConfiguration` directly for accessing config values

## Code Style Rules

### Modern C# Features (REQUIRED)

```csharp
// ✅ File-scoped namespaces (C# 10)
namespace TenSecondTom.Features.Users;

// ✅ Records for DTOs and commands
public sealed record UserDto(Guid Id, string Username, string Email);

// ✅ Primary constructors (C# 12) for simple classes
public sealed class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    // Use repository and logger directly
}

// ✅ Required properties (C# 11)
public sealed class UserConfig
{
    public required string ConnectionString { get; init; }
    public required int MaxRetries { get; init; }
}

// ✅ Collection expressions (C# 12)
var users = [user1, user2, user3];

// ✅ Pattern matching
var result = user switch
{
    { IsActive: true, Role: "Admin" } => "Active admin",
    { IsActive: true } => "Active user",
    _ => "Inactive"
};

// ✅ Nullable reference types (always enabled)
public string? GetOptionalValue() => _value;
public string GetRequiredValue() => _value ?? throw new InvalidOperationException();

// ✅ Constants instead of magic strings (REQUIRED)
var memoryDir = configuration[ConfigurationKeys.MemoryDirectory]; // Not "TenSecondTom:MemoryDirectory"
if (command == CommandNames.Today) { } // Not "today"

// ✅ ALLOWED - Logging and diagnostics can use literal strings
_logger.LogInformation("Processing request for user {UserId}", userId);
```

### Naming Conventions

- Commands: `CreateUserCommand`, `UpdateSettingsCommand`
- Queries: `GetUserQuery`, `ListUsersQuery`
- Handlers: `CreateUserCommandHandler`, `GetUserQueryHandler`
- Tests: `CreateUserCommandHandlerTests`
- Interfaces: `IUserRepository`, `IEmailService`

### Error Handling Pattern

```csharp
// ✅ Return Result<T> for expected failures
public async Task<Result<User>> CreateUserAsync(string username)
{
    if (string.IsNullOrWhiteSpace(username))
        return Result<User>.Failure("Username is required");
    
    try
    {
        var user = await _repository.CreateAsync(username);
        return Result<User>.Success(user);
    }
    catch (DuplicateUserException ex)
    {
        _logger.LogWarning(ex, "Duplicate user: {Username}", username);
        return Result<User>.Failure($"User {username} already exists");
    }
}

// ❌ Don't swallow exceptions
public async Task CreateUserAsync(string username)
{
    try
    {
        await _repository.CreateAsync(username);
    }
    catch
    {
        // Silent failure - BAD!
    }
}
```

## Testing Requirements (NON-NEGOTIABLE)

### Test Coverage: 80% Minimum

Every feature needs:

- Unit tests for handlers and business logic
- Integration tests for CLI commands and multi-component interactions
- Test helpers for common setup/teardown

### Test Structure (AAA Pattern)

```csharp
public sealed class CreateUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        // Arrange
        var repository = new Mock<IUserRepository>();
        var logger = Mock.Of<ILogger<CreateUserCommandHandler>>();
        var handler = new CreateUserCommandHandler(repository.Object, logger);
        var command = new CreateUserCommand("john", "john@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        repository.Verify(r => r.AddAsync(
            It.Is<User>(u => u.Username == "john"), 
            It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task Handle_WithInvalidUsername_ReturnsFailure(string username)
    {
        // Arrange
        var handler = CreateHandler();
        var command = new CreateUserCommand(username, "test@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("username");
    }
}
```

### Test Organization

```text
tests/
├── Unit/
│   └── Features/
│       └── Users/
│           ├── CreateUserCommandHandlerTests.cs
│           └── GetUserQueryHandlerTests.cs
├── Integration/
│   ├── Cli/
│   │   └── UserCommandsTests.cs
│   └── Features/
│       └── Users/
│           └── UserWorkflowTests.cs
└── TestHelpers/
    ├── Builders/
    └── Fixtures/
```

## CLI Command Structure

```csharp
// Use System.CommandLine
var rootCommand = new RootCommand("Ten Second Tom CLI");

var userCommand = new Command("user", "User management commands");
var createCommand = new Command("create", "Create a new user");

var usernameOption = new Option<string>(
    "--username",
    "The username for the new user")
{
    IsRequired = true
};

createCommand.AddOption(usernameOption);
createCommand.SetHandler(async (string username) =>
{
    // Handler logic
    var result = await _mediator.Send(new CreateUserCommand(username));
    
    if (result.IsSuccess)
    {
        Console.WriteLine($"User created: {result.Value}");
        return 0; // Success exit code
    }
    
    Console.Error.WriteLine($"Error: {result.Error}");
    return 1; // Failure exit code
}, usernameOption);

userCommand.AddCommand(createCommand);
rootCommand.AddCommand(userCommand);
```

## What to Avoid

### ❌ Don't Do These

```csharp
// ❌ Web or GUI frameworks
using Microsoft.AspNetCore.Mvc;  // NO!
using System.Windows.Forms;      // NO!

// ❌ Using var for public APIs
public var GetUser() => _user;   // NO! Use explicit type

// ❌ Anemic models
public class User
{
    public string Name { get; set; }  // NO! Add behavior
}

// ❌ Duplicate logic
public void ValidateUser(User user) { /* ... */ }
public void CheckUser(User user) { /* same logic */ }  // NO! Extract to one place

// ❌ Outdated patterns
public class UserService
{
    private IUserRepository _repository;
    
    public UserService(IUserRepository repository)  // Use primary constructor instead
    {
        _repository = repository;
    }
}

// ❌ Hardcoded secrets or config
var connectionString = "Server=localhost;...";  // NO! Use configuration

// ❌ Magic strings for config, commands, or shared identifiers
var memoryDir = config["TenSecondTom:MemoryDirectory"];  // NO! Use ConfigurationKeys.MemoryDirectory
if (command == "today") { }  // NO! Use CommandNames.Today
if (user.Role == "admin") { }  // NO! Use constants or enums
```

### ✅ Do This Instead

```csharp
// ✅ Rich domain models with behavior
public sealed class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; }
    
    private User() { } // For EF Core
    
    public static Result<User> Create(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return Result<User>.Failure("Username is required");
            
        return Result<User>.Success(new User { Id = Guid.NewGuid(), Username = username });
    }
    
    public Result ChangeUsername(string newUsername)
    {
        // Validation and business rules
        Username = newUsername;
        return Result.Success();
    }
}

// ✅ Configuration using Options Pattern (REQUIRED)
// Options class in Shared/Options/
namespace TenSecondTom.Shared.Options;

/// <summary>
/// Configuration options for database connection.
/// Maps to the "TenSecondTom:Database" configuration section.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "TenSecondTom:Database";
    public required string ConnectionString { get; init; }
}

// Validator in Shared/Options/Validation/
public sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            return ValidateOptionsResult.Fail("ConnectionString is required");
        return ValidateOptionsResult.Success;
    }
}

// Registration in ServiceCollectionExtensions.cs
services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
services.AddSingleton<IValidateOptions<DatabaseOptions>, DatabaseOptionsValidator>();

// Usage in service (inject IOptions<T>)
public sealed class DatabaseService(IOptions<DatabaseOptions> options)
{
    private readonly DatabaseOptions _options = options.Value;

    public void Connect()
    {
        var connectionString = _options.ConnectionString; // Type-safe!
    }
}

// ✅ Constants for shared identifiers (in Shared/Constants/)
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

// ✅ Use constants from Shared/Constants/
var memoryDir = configuration[ConfigurationKeys.MemoryDirectory];
if (command == CommandNames.Today) { /* ... */ }
var logsDir = Path.Combine(baseDir, DirectoryNames.Logs);

// ✅ Logging and diagnostics can use literal strings
_logger.LogInformation("User {Username} logged in successfully", username);
```

## Performance Guidelines

### When to Optimize

- **Profile first**: Use BenchmarkDotNet or profiler before optimizing
- **Hot paths only**: Focus on frequently-called code
- **Measure impact**: Verify optimizations actually help

### Common Optimizations

```csharp
// ✅ Use Span<T> for memory-efficient operations
public void ProcessData(ReadOnlySpan<byte> data)
{
    // Zero-copy operations
}

// ✅ Use ValueTask<T> for hot paths that may complete synchronously
public ValueTask<User?> GetCachedUserAsync(Guid id)
{
    if (_cache.TryGetValue(id, out var user))
        return ValueTask.FromResult<User?>(user);
    
    return new ValueTask<User?>(LoadUserAsync(id));
}

// ✅ Use frozen collections for static data
private static readonly FrozenDictionary<string, int> _statusCodes = 
    new Dictionary<string, int>
    {
        ["success"] = 0,
        ["error"] = 1
    }.ToFrozenDictionary();
```

## Dependencies

### Allowed Packages

- `System.CommandLine` - CLI framework
- `FluentValidation` - Validation
- `Serilog` - Logging
- `FluentAssertions` - Test assertions
- `Moq` or `NSubstitute` - Mocking
- Microsoft.* packages - Framework extensions

### Before Adding New Dependencies

1. Is there a .NET built-in alternative?
2. Is the package actively maintained?
3. Does it have a compatible license?
4. Does it align with CLI-only focus?

## Working with the Constitution

The project constitution at `.specify/memory/constitution.md` defines 8 core principles:

1. **Modern .NET & Idiomatic C#** - Use .NET 9 and modern C# patterns
2. **CLI-First Interface** - Command-line only, no web/GUI
3. **Test-First (NON-NEGOTIABLE)** - TDD with 80% coverage using xUnit
4. **DRY & Design Patterns** - CQRS, Factory, VSA patterns
5. **Semantic Versioning** - Automated releases on PR merge
6. **Cross-Platform Distribution** - Self-contained apps for Mac/Windows
7. **Local Development Excellence** - Great dev experience
8. **Secrets Management** - Never commit secrets

**Always check the constitution before making architectural decisions.**

## Documentation Standards

### XML Documentation (Required for Public APIs)

```csharp
/// <summary>
/// Creates a new user with the specified username and email.
/// </summary>
/// <param name="username">The unique username for the user.</param>
/// <param name="email">The user's email address.</param>
/// <param name="cancellationToken">Cancellation token for async operation.</param>
/// <returns>A result containing the created user's ID or an error message.</returns>
/// <exception cref="ArgumentException">Thrown when username or email is invalid.</exception>
public async Task<Result<Guid>> CreateUserAsync(
    string username, 
    string email,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### Code Comments (Minimal)

```csharp
// ✅ Good - explains WHY
// Using ConcurrentDictionary to allow lock-free reads during cache warming
private readonly ConcurrentDictionary<Guid, User> _cache = new();

// ❌ Bad - explains WHAT (obvious from code)
// Create a new user
var user = new User();
```

## Priority Order

When making suggestions, prioritize in this order:

1. **Correctness** - Code must work and handle edge cases
2. **Tests** - Include or update tests (TDD)
3. **Maintainability** - DRY, clear, well-organized code
4. **Performance** - Optimize when justified
5. **Documentation** - Update XML docs for public APIs

---

**Constitution**: `.specify/memory/constitution.md` v1.4.0
**Last Updated**: 2025-10-28

When in doubt, consult the constitution or ask the user for clarification.

## Active Technologies
- C# 12+ with .NET 9 + System.CommandLine 2.0-rc, Spectre.Console 0.51.1, FluentValidation 12.0.0, Serilog 4.3.0 (009-generate-recordings)
- Local filesystem (recording directory for transcripts, template directory for prompt templates, memory directory for outputs) (009-generate-recordings)

## Recent Changes
- 009-generate-recordings: Added C# 12+ with .NET 9 + System.CommandLine 2.0-rc, Spectre.Console 0.51.1, FluentValidation 12.0.0, Serilog 4.3.0
