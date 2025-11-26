# Ten Second Tom - AI Agent Instructions

> **⚠️ IMPORTANT**: This is a quick reference guide for AI agents. The **authoritative source of truth** is [`.specify/memory/constitution.md`](./.specify/memory/constitution.md). On any conflict, the constitution wins.

## Quick Links

- **Constitution (READ FIRST)**: [`.specify/memory/constitution.md`](./.specify/memory/constitution.md)
- **Architecture Tests**: [`tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs`](./tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs)

## Project Overview

Ten Second Tom is a modern CLI application built with **C# and .NET 10**, following **Vertical Slice Architecture (VSA)** with the **Co-location Pattern**. All architectural principles are defined in the constitution.

## Core Technology Stack

```text
Language:     C# 14 with .NET 10
CLI:          System.CommandLine 2.0-rc
UI:           Spectre.Console 0.51.1
CQRS:         MediatR 13.1.0
Validation:   FluentValidation 12.0.0
Logging:      Serilog 4.3.0
Testing:      xUnit 2.9+ + FluentAssertions 8.7+
Platforms:    macOS, Windows, (Linux future)
```

## Development Workflow

### Before Making Changes

1. ✅ Read the constitution at `.specify/memory/constitution.md`
2. ✅ Check existing tests and understand current behavior
3. ✅ Locate the feature slice - all related code should be nearby
4. ✅ Look for duplication - refactor rather than duplicate

### TDD Cycle (Red-Green-Refactor)

1. **Write Test** - Create test showing expected behavior
2. **Verify Red** - Confirm test fails with clear error
3. **Minimal Implementation** - Write just enough code to pass
4. **Verify Green** - Confirm test passes
5. **Refactor** - Clean up code while keeping tests green

## Code Organization (Co-location Pattern)

**One use case = One file** with nested Command/Query, Validator, Handler:

```csharp
// src/Features/[FeatureName]/[UseCase].cs
namespace TenSecondTom.Features.[FeatureName];

/// <summary>Brief description of use case</summary>
public static class [UseCase]
{
    public sealed record Command(...) : IRequest<Result<T>>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            // Validation rules (auto-discovered)
        }
    }

    public sealed class Handler(...) : IRequestHandler<Command, Result<T>>
    {
        public async Task<Result<T>> Handle(Command request, CancellationToken ct)
        {
            // Business logic (validation/logging already done by pipeline)
        }
    }
}
```

**File naming**: `[Verb][Noun].cs` (e.g., `CreateUser.cs`, `ListTemplates.cs`, `GenerateOutput.cs`)

## Configuration Management (REQUIRED)

**Never access `IConfiguration` directly. Always use the Options Pattern:**

```csharp
// ❌ PROHIBITED
var apiKey = _configuration["TenSecondTom:ApiKey"];

// ✅ REQUIRED
// 1. Options class in Shared/Options/
public sealed class MyFeatureOptions
{
    public const string SectionName = "TenSecondTom:MyFeature";
    public required string ApiKey { get; init; }
}

// 2. Validator in Shared/Options/Validation/
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
services.Configure<MyFeatureOptions>(configuration.GetSection(MyFeatureOptions.SectionName));
services.AddSingleton<IValidateOptions<MyFeatureOptions>, MyFeatureOptionsValidator>();

// 4. Inject IOptions<T>
public sealed class MyService(IOptions<MyFeatureOptions> options)
{
    private readonly MyFeatureOptions _options = options.Value;
}
```

## Modern C# Features (Required)

```csharp
// ✅ File-scoped namespaces
namespace TenSecondTom.Features.Users;

// ✅ Primary constructors
public sealed class UserService(IUserRepository repo, ILogger<UserService> logger)
{
    // Use dependencies directly
}

// ✅ Records for DTOs
public sealed record UserDto(Guid Id, string Username, string Email);

// ✅ Required properties
public sealed class Config
{
    public required string ApiKey { get; init; }
}

// ✅ Collection expressions
var items = [item1, item2, item3];

// ✅ Constants (NO magic strings!)
var dir = configuration[ConfigurationKeys.RootDirectory];
if (command == CommandNames.Today) { }
```

## Testing Requirements (80% Coverage Minimum)

```csharp
// Test structure: Arrange-Act-Assert
public sealed class CreateUserTests
{
    [Fact]
    public async Task Handle_WithValidCommand_CreatesUser()
    {
        // Arrange
        var repository = new Mock<IUserRepository>();
        var handler = new CreateUser.Handler(
            repository.Object,
            Mock.Of<ILogger<CreateUser.Handler>>());
        var command = new CreateUser.Command("john", "john@example.com");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("", "test@example.com")]
    [InlineData("john", "")]
    public async Task Validator_WithInvalidInput_Fails(string username, string email)
    {
        // Arrange
        var validator = new CreateUser.Validator();
        var command = new CreateUser.Command(username, email);

        // Act
        var result = await validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
    }
}
```

## VSA Compliance

### ✅ Allowed
```csharp
// Cross-feature communication via MediatR
var audioConfig = await _mediator.Send(new GetAudioConfiguration.Query());

// Shared code in Shared/ directory
var logsDir = Path.Combine(baseDir, DirectoryNames.Logs);
```

### ❌ Prohibited
```csharp
// Direct feature reference
var audioService = new AudioService(); // NO! Cross-feature coupling

// Magic strings
var key = config["TenSecondTom:ApiKey"]; // NO! Use Options Pattern
if (command == "today") { } // NO! Use CommandNames.Today

// God Objects
public class ConfigurationSettings { /* all app config */ } // NO! Violates VSA

// Obsolete code
[Obsolete] public void OldMethod() { } // NO! Delete it instead
```

## Result Pattern (Error Handling)

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

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Use Case Files | `[Verb][Noun].cs` | `CreateUser.cs`, `GenerateOutput.cs` |
| Nested Types | `Command`, `Query`, `Validator`, `Handler` | No prefixes |
| Options Classes | `[Feature]Options` | `AudioOptions`, `LlmOptions` |
| Options Validators | `[Options]Validator` | `AudioOptionsValidator` |
| Constants | `[Domain]Keys/Names/Constants` | `ConfigurationKeys`, `CommandNames` |
| DI Methods | `Add[Feature]Feature` | `AddAuthFeature()`, `AddTemplatesFeature()` |
| Test Files | `[UseCase]Tests.cs` | `CreateUserTests.cs` |

## CLI Command Structure

```csharp
// Use System.CommandLine
var createCommand = new Command("create", "Create a new user");

var usernameOption = new Option<string>("--username") { IsRequired = true };
createCommand.AddOption(usernameOption);

createCommand.SetHandler(async (string username) =>
{
    var result = await _mediator.Send(new CreateUser.Command(username, email));

    if (result.IsSuccess)
    {
        Console.WriteLine($"User created: {result.Value}");
        return 0; // Success
    }

    Console.Error.WriteLine($"Error: {result.Error}");
    return 1; // Failure
}, usernameOption);
```

## Priority Order for Suggestions

1. **Correctness** - Code must work correctly and handle edge cases
2. **Tests** - TDD with 80% coverage (non-negotiable)
3. **Maintainability** - DRY, clear, well-organized code
4. **Performance** - Optimize when justified
5. **Documentation** - XML comments on public APIs

## What NOT to Do

❌ Web frameworks (ASP.NET Core, Blazor)
❌ GUI frameworks (WPF, WinForms)
❌ Direct `IConfiguration` access with strings
❌ Magic strings for config keys, commands, paths
❌ Cross-feature coupling (direct references)
❌ God Objects (monolithic classes)
❌ `[Obsolete]` attributes (delete old code)
❌ Code without tests
❌ Test frameworks other than xUnit
❌ Ignoring 80% coverage requirement

## When in Doubt

1. Check [`.specify/memory/constitution.md`](./.specify/memory/constitution.md) first
2. Look for similar patterns in the codebase
3. Run architecture tests to verify VSA compliance: `tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs`
4. Ask the user for clarification

---

**Constitution Version**: 1.8.0 | **Last Updated**: 2025-01-19

**Recent Changes**:
- ConfigurationSettings God Object removed (aggressive refactor complete)
- IConfigurationSectionStore is now the standard for config storage
- Force parameter pattern for independent configuration commands
- All `[Obsolete]` code removed from production

For detailed architectural principles, design patterns, and governance, consult [`.specify/memory/constitution.md`](./.specify/memory/constitution.md).
