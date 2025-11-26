# Ten Second Tom - GitHub Copilot Instructions

> **⚠️ IMPORTANT**: This is a quick reference guide for GitHub Copilot. The **authoritative source of truth** is [`.specify/memory/constitution.md`](../.specify/memory/constitution.md). On any conflict, the constitution wins.

## Quick Links

- **Constitution (READ FIRST)**: [`.specify/memory/constitution.md`](../.specify/memory/constitution.md)
- **Architecture Tests**: [`tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs`](../tests/TenSecondTom.Tests/Architecture/VsaComplianceTests.cs)

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

## Code Generation Rules

### When Generating New Features

1. **Start with tests** (TDD - non-negotiable)
2. **Co-location pattern**: One use case = one file with nested Command/Query, Validator, Handler
3. **Options Pattern**: Never use direct `IConfiguration` access
4. **Constants**: Never use magic strings for config keys, commands, paths
5. **VSA Compliance**: Features must not directly reference other features

### Use Case File Structure

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
            // Validation rules (auto-discovered by FluentValidation)
        }
    }

    public sealed class Handler(...) : IRequestHandler<Command, Result<T>>
    {
        public async Task<Result<T>> Handle(Command request, CancellationToken ct)
        {
            // Business logic (validation already done, logging already done)
        }
    }
}
```

**File naming**: `[Verb][Noun].cs` (e.g., `CreateUser.cs`, `ListTemplates.cs`)

## Modern C# Features (Required)

```csharp
// ✅ File-scoped namespaces
namespace TenSecondTom.Features.Users;

// ✅ Primary constructors
public sealed class UserService(IUserRepository repository, ILogger<UserService> logger)
{
    // Use dependencies directly
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

// ✅ Constants (NO magic strings!)
var dir = configuration[ConfigurationKeys.RootDirectory];
if (command == CommandNames.Today) { }
```

## Configuration Pattern (REQUIRED)

```csharp
// ❌ NEVER do this
var apiKey = _configuration["TenSecondTom:ApiKey"];

// ✅ ALWAYS use Options Pattern
// 1. Create options class in Shared/Options/
public sealed class MyFeatureOptions
{
    public const string SectionName = "TenSecondTom:MyFeature";
    public required string ApiKey { get; init; }
}

// 2. Create validator in Shared/Options/Validation/
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

## Test Generation (TDD)

```csharp
// Generate tests BEFORE implementation code
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
        repository.Verify(r => r.AddAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

**Coverage Requirement**: 80% minimum

## VSA Compliance

### ✅ Allowed
```csharp
// Cross-feature communication via MediatR
var config = await _mediator.Send(new GetAudioConfiguration.Query());

// Shared code in Shared/ directory
var dir = Path.Combine(baseDir, DirectoryNames.Logs);
```

### ❌ Prohibited
```csharp
// Direct feature reference
var audioService = new AudioService(); // NO!

// Magic strings
var key = config["TenSecondTom:ApiKey"]; // NO!
if (command == "today") { } // NO!

// God Objects
public class ConfigurationSettings { /* all app config */ } // NO!

// Obsolete code
[Obsolete] public void OldMethod() { } // NO! Delete it
```

## Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Use Case Files | `[Verb][Noun].cs` | `CreateUser.cs`, `GenerateOutput.cs` |
| Nested Types | `Command`, `Query`, `Validator`, `Handler` | No prefixes |
| Options | `[Feature]Options` | `AudioOptions`, `LlmOptions` |
| Validators | `[Options]Validator` | `AudioOptionsValidator` |
| Constants | `[Domain]Keys/Names/Constants` | `ConfigurationKeys`, `CommandNames` |
| Tests | `[UseCase]Tests.cs` | `CreateUserTests.cs` |

## Priority Order

1. **Correctness** - Code must work and handle edge cases
2. **Tests** - TDD with 80% coverage (non-negotiable)
3. **Maintainability** - DRY, clear, well-organized
4. **Performance** - Optimize when justified
5. **Documentation** - XML comments on public APIs

## What NOT to Generate

❌ Web frameworks (ASP.NET, Blazor)
❌ GUI frameworks (WPF, WinForms)
❌ Direct `IConfiguration` access with strings
❌ Magic strings for config/commands/paths
❌ Cross-feature coupling
❌ God Objects
❌ `[Obsolete]` attributes (delete old code instead)
❌ Code without tests

## When in Doubt

1. Check [`.specify/memory/constitution.md`](../.specify/memory/constitution.md)
2. Look for similar patterns in the codebase
3. Run architecture tests to verify VSA compliance

---

**Constitution Version**: 1.8.0 | **Last Updated**: 2025-01-19

**Recent Changes**:
- ConfigurationSettings God Object removed (aggressive refactor complete)
- IConfigurationSectionStore is now the standard for config storage
- Force parameter pattern for independent configuration commands
- All `[Obsolete]` code removed from production

For detailed architectural principles, consult [`.specify/memory/constitution.md`](../.specify/memory/constitution.md).
