<!--
Sync Impact Report:
- Version change: (initial) → 1.0.0
- Added principles:
  * I. Modern .NET & Idiomatic C#
  * II. CLI-First Interface
  * III. Test-First (NON-NEGOTIABLE)
  * IV. DRY & Design Patterns
  * V. Semantic Versioning & Automated Releases
  * VI. Cross-Platform Distribution
  * VII. Local Development Excellence
  * VIII. Secrets Management
- Added sections:
  * Architecture & Design Standards
  * Quality & Testing Standards
  * Development & Operations Standards
- Templates requiring updates:
  * ✅ plan-template.md (constitution check section will be auto-generated based on these principles)
  * ✅ spec-template.md (already aligned with testability requirements)
  * ✅ tasks-template.md (already aligned with TDD and test-first approach)
- Follow-up TODOs: None
-->

# Ten Second Tom Constitution

## Core Principles

### I. Modern .NET & Idiomatic C#

**All code MUST be written in modern, idiomatic C# using .NET 9.**

- Follow current C# language features and patterns (nullable reference types, pattern matching, records, etc.)
- Adhere to official Microsoft C# coding conventions
- Code must be elegant, sleek, and readable
- Use modern async/await patterns where appropriate
- Leverage .NET 9 performance improvements and features
- Use Serilog as the logging framework (organizational standard)

**Rationale**: Modern C# provides powerful features that improve code quality, safety, and maintainability. Idiomatic code is easier for the open-source community to understand and contribute to. Serilog provides structured logging with excellent performance and is the organizational standard.

### II. CLI-First Interface

**The application MUST be a command-line interface with no web or GUI dependencies.**

- All user interaction occurs through the terminal
- Commands must follow industry-standard CLI patterns
- Support standard input/output streams
- Provide clear, helpful error messages
- Support both interactive and scripted usage
- Text-based output for debuggability

**Rationale**: CLI applications are lightweight, scriptable, and integrable with existing toolchains. They provide a great developer experience without unnecessary complexity.

### III. Test-First (NON-NEGOTIABLE)

**Test-Driven Development is mandatory. 80% minimum test coverage using xUnit.**

- Tests MUST be written before implementation
- All tests MUST fail initially (Red-Green-Refactor)
- Implementation proceeds only after test approval
- Code coverage MUST meet or exceed 80% threshold
- xUnit is the required testing framework
- Tests must be fast, isolated, and deterministic

**Rationale**: TDD ensures code is testable by design, reduces defects, provides living documentation, and enables confident refactoring. The 80% coverage threshold ensures comprehensive validation.

### IV. DRY & Design Patterns

**Code MUST follow DRY principles and leverage appropriate design patterns.**

- No duplication of logic or data structures
- Use CQRS (Command Query Responsibility Segregation) when appropriate
- Apply Factory pattern for object creation where beneficial
- Implement Vertical Slice Architecture (VSA) for feature organization
- Patterns must serve clarity and maintainability, not complexity
- Extract reusable components into well-defined abstractions

**Rationale**: DRY reduces maintenance burden and defect surface area. Well-chosen patterns improve code organization, testability, and long-term maintainability for open-source contributors.

### V. Semantic Versioning & Automated Releases

**All releases MUST use semantic versioning with automated GitHub releases.**

- Follow semver strictly: MAJOR.MINOR.PATCH
- MAJOR: Breaking changes
- MINOR: New features (backward compatible)
- PATCH: Bug fixes (backward compatible)
- Releases MUST be created automatically when PRs merge to main
- GitHub Actions MUST automate the release process
- Release notes MUST be generated automatically

**Rationale**: Semantic versioning provides clear communication about change impact. Automated releases ensure consistency and reduce human error.

### VI. Cross-Platform Distribution

**Self-contained applications MUST be published for macOS and Windows via package managers.**

- Automated builds for macOS and Windows on every release
- Self-contained applications include all dependencies
- Support installation via Homebrew (macOS), Chocolatey/winget (Windows)
- Automated publishing through GitHub workflows
- Zero manual installation dependencies for end users

**Rationale**: Users expect native package manager support. Self-contained apps eliminate dependency issues and simplify installation.

### VII. Local Development Excellence

**Development and debugging experience MUST be first-class.**

- Project MUST be easily cloneable and runnable
- Clear README with setup instructions
- Fast build and test cycles
- Comprehensive debugging support in modern IDEs
- Local environment setup MUST be straightforward
- Development dependencies clearly documented

**Rationale**: Great developer experience attracts contributors and reduces onboarding friction. Fast feedback loops improve productivity.

### VIII. Secrets Management

**Secrets MUST NEVER be stored in source control.**

- Use environment variables for secrets
- Support .env files (gitignored) for local development
- Leverage .NET Secret Manager for development secrets
- Document required secrets clearly
- Provide example configuration files (without real secrets)
- Use Azure Key Vault or similar for production secrets

**Rationale**: Secrets in source control create security vulnerabilities and compliance issues. Proper secrets management is a baseline security requirement.

## Architecture & Design Standards

### Code Organization

- **Vertical Slice Architecture**: Organize features as vertical slices containing all layers (command/query, handler, validation, tests)
- **CQRS**: Separate read and write operations for clarity and scalability
- **Factory Pattern**: Use for complex object construction and dependency resolution
- **Dependency Injection**: Leverage built-in .NET DI container
- **Single Responsibility**: Each class/method has one clear purpose

### Naming Conventions

- Follow Microsoft naming guidelines
- Use descriptive names that reveal intent
- Commands end with "Command", Queries end with "Query"
- Handlers end with "Handler"
- Test classes end with "Tests"

### Error Handling

- Use exceptions for exceptional cases only
- Return Result types for expected failures
- Provide clear, actionable error messages
- Log errors with appropriate context
- Never swallow exceptions silently

## Quality & Testing Standards

### Test Coverage Requirements

- Minimum 80% code coverage across the solution
- Unit tests for business logic (fast, isolated)
- Integration tests for component interactions, when practical
- CLI command tests for user-facing functionality

### Test Organization

- Tests mirror source structure
- Use xUnit test framework
- Use FluentAssertions for readable assertions
- Use Moq or NSubstitute for mocking
- Tests must be independently runnable

### Code Quality

- No compiler warnings permitted
- Run static analysis (Roslyn analyzers, SonarAnalyzer)
- Format code with .editorconfig rules
- Review and resolve all code analysis warnings
- Maintain XML documentation comments for public APIs

## Development & Operations Standards

### Version Control

- Git required
- Feature branches for all work
- Pull requests required for merging to main
- Semantic commit messages
- Squash commits when merging

### CI/CD Pipeline

- GitHub Actions for all automation
- Automated build on every push
- Automated tests on every PR
- Automated release on merge to main
- Automated package publishing

### Release Process

- Tag releases with version numbers
- Generate release notes from PR titles
- Build self-contained executables
- Publish to package managers automatically
- Update documentation automatically

### Documentation

- README with quick start guide
- Architecture decision records (ADRs) for significant choices
- API documentation for public interfaces
- Contributing guidelines for open source
- Changelog maintained automatically

### Logging Standards

- **Serilog** is the required logging framework (organizational standard)
- Use structured logging with semantic context
- Configure appropriate sinks (Console for CLI, File for diagnostics)
- Log levels: Debug (I/O operations), Information (commands), Warning (retries), Error (failures), Fatal (unrecoverable)
- Include correlation IDs for tracing related operations
- Never log secrets or sensitive user data

## Governance

### Amendment Process

- Constitution amendments require documented justification
- Amendments must maintain backward compatibility where possible
- MAJOR version bump if core principles change significantly
- MINOR version bump if new principles or sections added
- PATCH version bump for clarifications or non-semantic changes

### Compliance Review

- All PRs MUST verify compliance with constitutional principles
- Constitution Check section required in implementation plans
- Complexity must be justified against simplicity principle
- Violations require explicit documentation and mitigation plan

### Development Guidance

- Agent-specific guidance files may extend but not override constitution
- When principles conflict, constitution takes precedence
- Consult constitution before major architectural decisions
- Educate new contributors on constitutional requirements

**Version**: 1.1.0 | **Ratified**: 2025-10-01 | **Last Amended**: 2025-10-02

---

## Changelog

### Version 1.1.0 (2025-10-02)
- **MINOR**: Added Serilog logging framework mandate (organizational standard)
- Added Logging Standards section to Development & Operations Standards
- Specified log levels and structured logging requirements
- Added security requirement: never log secrets or sensitive data

### Version 1.0.0 (2025-10-01)
- **MAJOR**: Initial constitution ratified
- Established 8 core principles
- Defined architecture, quality, and operations standards
