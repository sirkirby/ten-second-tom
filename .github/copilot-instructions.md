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

- **Vertical Slice Architecture (VSA)**: Organize features as self-contained vertical slices
- **CQRS**: Separate commands (mutations) from queries (reads)
  - Commands: `CreateUserCommand`, `UpdateSettingsCommand`
  - Queries: `GetUserQuery`, `ListItemsQuery`
  - Handlers: `CreateUserCommandHandler`, `GetUserQueryHandler`
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
- **Naming Conventions**:
  - Commands: `*Command`
  - Queries: `*Query`
  - Handlers: `*Handler`
  - Tests: `*Tests`
  - Interfaces: `I*`
- **Error Handling**:
  - Use exceptions only for exceptional cases
  - Return `Result<T>` types for expected failures
  - Provide clear, actionable error messages
  - Always log errors with context

## Project Structure

**NOTE**: The canonical project structure is defined in `.specify/memory/constitution.md` (Project Structure Standards section). The structure below is a summary for quick reference.

```text
src/
├── Features/          # Vertical slices (self-contained feature modules)
│   └── [FeatureName]/
│       ├── Commands/      # Command classes (mutations, writes)
│       ├── Queries/       # Query classes (reads) [if needed]
│       ├── Handlers/      # Command/Query handlers (business logic)
│       ├── Validation/    # FluentValidation validators [if needed]
│       └── DependencyInjection.cs  # Feature-specific DI registration
├── Infrastructure/    # Cross-cutting concerns (DI, config, logging)
│   ├── Configuration/
│   ├── Logging/
│   └── DependencyInjection.cs
├── Shared/            # Shared domain models, abstractions, utilities
│   ├── Models/
│   ├── Abstractions/
│   └── Extensions/
└── Program.cs         # Entry point

tests/
├── TenSecondTom.Tests/         # Unit tests (fast, isolated)
│   └── Features/
│       └── [FeatureName]/
│           ├── Commands/
│           ├── Queries/
│           └── Handlers/
└── TenSecondTom.IntegrationTests/  # Integration tests
    ├── Features/
    │   └── [FeatureName]/
    └── Cli/
```

**See `.specify/memory/constitution.md` for detailed rules on:**
- Feature organization requirements
- Naming conventions for vertical slices
- Cross-feature dependency restrictions
- Test structure mirroring

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
❌ Don't swallow exceptions without logging
❌ Don't use outdated C# patterns (pre-C# 9)
❌ Don't create anemic domain models (models without behavior)
❌ Don't violate DRY, SOLID, or KISS principles

## Documentation

- Keep README.md up to date with setup instructions
- Use XML documentation comments for public APIs
- Create ADRs (Architecture Decision Records) for significant architectural choices
- Include inline comments only for non-obvious "why" explanations, not "what"

## Examples

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

**Constitution Version**: 1.2.0 | **Last Updated**: 2025-10-16

For questions about architectural decisions or edge cases, consult `.specify/memory/constitution.md`.
