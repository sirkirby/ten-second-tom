# Ten Second Tom - Claude Code Quick Reference

> **⚠️ IMPORTANT**: This is a quick reference guide. The **authoritative source of truth** is [`.specify/memory/constitution.md`](./.specify/memory/constitution.md). On any conflict, the constitution wins.

## Project Overview

Ten Second Tom is a modern CLI application built with **C# and .NET 9**, following **Vertical Slice Architecture (VSA)** with strict adherence to the principles defined in the constitution.

## Quick Links

- **Constitution (READ FIRST)**: [`.specify/memory/constitution.md`](./.specify/memory/constitution.md)
- **Architecture Tests**: [`tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs`](./tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs)

## Core Technology Stack

```text
Language:     C# 12+ with .NET 9
CLI:          System.CommandLine 2.0-rc
UI:           Spectre.Console 0.51.1
CQRS:         MediatR 13.1.0
Validation:   FluentValidation 12.0.0
Logging:      Serilog 4.3.0
Testing:      xUnit 2.9.2 + FluentAssertions 8.7.1
Platforms:    macOS, Windows, (Linux future)
```

## Before Making Changes

1. ✅ **Read the constitution** at `.specify/memory/constitution.md`
2. ✅ **Check existing tests** - understand current behavior
3. ✅ **Understand the feature** - read related files in the vertical slice
4. ✅ **Look for duplication** - refactor rather than duplicate

## Code Organization (Co-location Pattern)

**One use case = One file** with nested types:

```csharp
// src/Features/Users/CreateUser.cs
namespace TenSecondTom.Features.Users;

/// <summary>
/// Creates a new user with validation and persistence.
/// </summary>
public static class CreateUser
{
    public sealed record Command(string Username, string Email)
        : IRequest<Result<Guid>>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Username).NotEmpty().MinimumLength(3);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public sealed class Handler(
        IUserRepository repository,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<Guid>>
    {
        public async Task<Result<Guid>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            // Business logic here
            // Validation already done by FluentValidation pipeline
            // Logging already done by RequestLoggingPipelineBehavior
        }
    }
}
```

**File naming**: `[Verb][Noun].cs` (e.g., `CreateUser.cs`, `ListTemplates.cs`, `GenerateOutput.cs`)

## Configuration Management

### ✅ REQUIRED: Options Pattern

**Never** access `IConfiguration` directly. Always use strongly-typed options:

```csharp
// 1. Create Options class in src/Shared/Options/
namespace TenSecondTom.Shared.Options;

public sealed class MyFeatureOptions
{
    public const string SectionName = "TenSecondTom:MyFeature";

    public required string ApiKey { get; init; }
    public int Timeout { get; init; } = 30;
}

// 2. Create Validator in src/Shared/Options/Validation/
public sealed class MyFeatureOptionsValidator : IValidateOptions<MyFeatureOptions>
{
    public ValidateOptionsResult Validate(string? name, MyFeatureOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return ValidateOptionsResult.Fail("ApiKey is required");
        return ValidateOptionsResult.Success;
    }
}

// 3. Register in ServiceCollectionExtensions.cs
services.Configure<MyFeatureOptions>(
    configuration.GetSection(MyFeatureOptions.SectionName));
services.AddSingleton<IValidateOptions<MyFeatureOptions>,
    MyFeatureOptionsValidator>();

// 4. Inject IOptions<T> into your service
public sealed class MyService(IOptions<MyFeatureOptions> options)
{
    private readonly MyFeatureOptions _options = options.Value;

    public void DoWork()
    {
        var apiKey = _options.ApiKey; // ✅ Type-safe!
    }
}
```

### Configuration Storage

Use `IConfigurationSectionStore` for reading/writing config:

```csharp
// Read a section
var result = await sectionStore.ReadSectionAsync<AudioOptions>(
    "TenSecondTom:Audio",
    cancellationToken);

// Write a section
var writeResult = await sectionStore.WriteSectionAsync(
    "TenSecondTom:Audio",
    audioOptions,
    cancellationToken);
```

## Modern C# Features (Required)

```csharp
// ✅ File-scoped namespaces
namespace TenSecondTom.Features.Users;

// ✅ Primary constructors
public sealed class UserService(
    IUserRepository repository,
    ILogger<UserService> logger)
{
    // Use repository and logger directly
}

// ✅ Records for DTOs
public sealed record UserDto(Guid Id, string Username, string Email);

// ✅ Required properties
public sealed class UserConfig
{
    public required string ConnectionString { get; init; }
}

// ✅ Collection expressions
var users = [user1, user2, user3];

// ✅ Constants for shared strings (NO magic strings!)
var dir = configuration[ConfigurationKeys.RootDirectory];
if (command == CommandNames.Today) { /* ... */ }
```

## Testing (TDD - Non-Negotiable)

### Red-Green-Refactor Cycle

1. **Write test** showing expected behavior
2. **Verify RED** - test fails with clear message
3. **Minimal code** to make test pass
4. **Verify GREEN** - test passes
5. **Refactor** while keeping tests green

### Test Structure (AAA Pattern)

```csharp
public sealed class CreateUserTests
{
    [Fact]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        // Arrange
        var repository = new Mock<IUserRepository>();
        var logger = Mock.Of<ILogger<CreateUser.Handler>>();
        var handler = new CreateUser.Handler(repository.Object, logger);
        var command = new CreateUser.Command("john", "john@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
```

**Coverage Requirement**: 80% minimum across all features

## VSA Compliance Rules

### ✅ Allowed
- Features use MediatR to call other features: `await _mediator.Send(new OtherFeature.Query())`
- Infrastructure coordinates features (no business logic)
- Shared code in `src/Shared/` (models, abstractions, constants)

### ❌ Prohibited
- Features directly referencing other features
- God Objects (monolithic config/service classes)
- Magic strings (use constants from `Shared/Constants/`)
- Direct `IConfiguration` access (use Options Pattern)
- `[Obsolete]` code in production (delete or refactor it)

## Common Patterns

### Result Pattern (Error Handling)

```csharp
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
```

### CQRS Cross-Feature Communication

```csharp
// ✅ Correct: Use MediatR
var audioConfig = await _mediator.Send(new GetAudioConfiguration.Query());
if (audioConfig.IsSuccess)
{
    var sttProvider = audioConfig.Value.SttProvider;
}

// ❌ Wrong: Direct feature reference
var audioService = new AudioService(); // NO! Cross-feature coupling
```

### Notification System

**Infrastructure**: OS native notifications (macOS via osascript, Windows future)

```csharp
// Send a basic notification
await _mediator.Send(new ShowNotification.Command
{
    Title = "Recording Saved",
    Message = "Your recording has been saved successfully",
    Priority = NotificationPriority.Normal
});

// Send with timeout
await _mediator.Send(new ShowNotification.Command
{
    Title = "Session Expiring",
    Message = "Your 30-minute session has ended",
    Priority = NotificationPriority.High,
    TimeoutSeconds = 30
});
```

**Key Principles**:
- **Graceful degradation** - notifications are enhancements, not requirements
- **Non-blocking** - use fire-and-forget pattern (Task.Run) to avoid blocking primary flows
- **Error handling** - catch and log notification failures, never propagate to calling code
- **macOS limitations** - no interactive buttons (AppleScript limitation)
- **Terminal first** - always preserve terminal output as primary interface

```csharp
// Fire-and-forget pattern for notifications
_ = Task.Run(async () =>
{
    try
    {
        await _mediator.Send(new ShowNotification.Command { /* ... */ });
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Notification failed (non-critical)");
    }
});

// Continue with primary flow immediately
Console.WriteLine("Recording saved successfully");
```

**Configuration**:
- `TenSecondTom:Notifications` - Enable/disable, timeout, priority defaults
- `TenSecondTom:Security` - Notification token secret, max token age

## CLI Command Structure

```csharp
// Use System.CommandLine
var createCommand = new Command("create", "Create a new user");

var usernameOption = new Option<string>("--username")
{
    IsRequired = true
};

createCommand.AddOption(usernameOption);
createCommand.SetHandler(async (string username) =>
{
    var result = await _mediator.Send(new CreateUserCommand(username));

    if (result.IsSuccess)
    {
        Console.WriteLine($"User created: {result.Value}");
        return 0; // Success
    }

    Console.Error.WriteLine($"Error: {result.Error}");
    return 1; // Failure
}, usernameOption);
```

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Use Case Files | `[Verb][Noun].cs` | `CreateUser.cs`, `ListTemplates.cs` |
| Nested Command/Query | `Command` or `Query` | `public sealed record Command(...)` |
| Nested Validator | `Validator` | `public sealed class Validator` |
| Nested Handler | `Handler` | `public sealed class Handler` |
| Options Classes | `[Feature]Options` | `AudioOptions`, `LlmOptions` |
| Options Validators | `[Options]Validator` | `AudioOptionsValidator` |
| DI Methods | `Add[Feature]Feature` | `AddAuthFeature()` |
| Test Files | `[UseCase]Tests.cs` | `CreateUserTests.cs` |

## What to Avoid

```csharp
// ❌ Don't Do
var config = _configuration["TenSecondTom:ApiKey"];  // Magic string
var handler = new OtherFeature.Handler();            // Cross-feature coupling
if (command == "today") { }                          // Magic string

// ✅ Do Instead
var config = _options.Value.ApiKey;                  // Options Pattern
var result = await _mediator.Send(new OtherFeature.Query());  // CQRS
if (command == CommandNames.Today) { }               // Constant
```

## Priority Order

1. **Correctness** - Code must work and handle edge cases
2. **Tests** - TDD with 80% coverage
3. **Maintainability** - DRY, clear, well-organized
4. **Performance** - Optimize when justified
5. **Documentation** - XML comments on public APIs

## When in Doubt

1. Check [`.specify/memory/constitution.md`](./.specify/memory/constitution.md) first
2. Look for similar patterns in the codebase
3. Run architecture tests: `tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs`
4. Ask the user for clarification

---

**Constitution Version**: 1.8.0 | **Last Updated**: 2025-01-19

**Recent Changes**:
- OS Native Notification System added (macOS support, Windows future)
- NotificationService infrastructure with channel-agnostic architecture
- Recording session expiration notifications (graceful degradation)
- ConfigurationSettings God Object removed (aggressive refactor complete)
- IConfigurationSectionStore is now the standard for config storage
- Force parameter pattern for independent configuration commands
- All `[Obsolete]` code removed from production
