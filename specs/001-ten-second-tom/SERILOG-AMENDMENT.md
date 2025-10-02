# Serilog Logging Framework Amendment

**Date**: October 2, 2025  
**Amendment Type**: Constitutional + Specification Update  
**Affected Documents**: constitution.md, spec.md, plan.md, tasks.md  
**Rationale**: Organizational standard for logging framework

---

## Overview

This amendment codifies **Serilog** as the required logging framework for Ten Second Tom, reflecting organizational standards. The amendment updates the constitution (MINOR version bump to v1.1.0) and cascades the requirement through all specification documents.

---

## Constitutional Amendment

### Constitution Version Change
- **Previous**: v1.0.0
- **New**: v1.1.0
- **Change Type**: MINOR (new organizational standard requirement)

### Changes to Core Principles

#### Section I: Modern .NET & Idiomatic C#

**Added**:
- Use Serilog as the logging framework (organizational standard)

**Updated Rationale**:
> Modern C# provides powerful features that improve code quality, safety, and maintainability. Idiomatic code is easier for the open-source community to understand and contribute to. **Serilog provides structured logging with excellent performance and is the organizational standard.**

### New Section: Logging Standards

Added to **Development & Operations Standards**:

```markdown
### Logging Standards

- **Serilog** is the required logging framework (organizational standard)
- Use structured logging with semantic context
- Configure appropriate sinks (Console for CLI, File for diagnostics)
- Log levels: Debug (I/O operations), Information (commands), Warning (retries), Error (failures), Fatal (unrecoverable)
- Include correlation IDs for tracing related operations
- Never log secrets or sensitive user data
```

### Constitution Changelog

Added comprehensive changelog section tracking all amendments:

**Version 1.1.0 (2025-10-02)**:
- MINOR: Added Serilog logging framework mandate (organizational standard)
- Added Logging Standards section to Development & Operations Standards
- Specified log levels and structured logging requirements
- Added security requirement: never log secrets or sensitive data

---

## Specification Updates

### spec.md Changes

**Added Section**: "Technical Requirements" (before Functional Requirements)

```markdown
## Technical Requirements *(informational - for planning reference)*

### Logging Framework
- **MUST use Serilog** as the logging framework (organizational standard per constitution v1.1.0)
- Configure with Console sink for CLI output diagnostics
- Configure with File sink for persistent logs (`.logs/` directory)
- Use structured logging with semantic properties
- Log levels: Debug (I/O), Information (commands), Warning (retries), Error (failures)
- Never log secrets or sensitive user data (API keys, SSH passphrases, user memory content excerpts)
```

**Rationale**: Provides clear technical mandate before diving into functional requirements, ensuring implementers understand the logging framework requirement upfront.

---

### plan.md Changes

#### Technical Context - Dependencies

**Added Serilog packages**:
- Serilog (logging framework - organizational standard)
- Serilog.Sinks.Console (console output)
- Serilog.Sinks.File (file-based logs)
- Serilog.Extensions.Logging (Microsoft.Extensions.Logging integration)
- Serilog.Enrichers.Environment (environment enrichers)
- Serilog.Settings.Configuration (appsettings.json configuration)

#### Constitution Check

**Updated Section I**:
- [x] Using Serilog as logging framework (organizational standard per constitution v1.1.0)

---

### tasks.md Changes

#### T002: Add Core Dependencies

**Added Serilog packages** to Application Dependencies:
```
- `Serilog` (logging framework - organizational standard)
- `Serilog.Extensions.Logging` (Microsoft.Extensions.Logging integration)
- `Serilog.Sinks.Console` (console output sink)
- `Serilog.Sinks.File` (file-based logging sink)
- `Serilog.Enrichers.Environment` (environment information enrichers)
- `Serilog.Settings.Configuration` (appsettings.json configuration support)
- `YamlDotNet` (YAML frontmatter parsing)
```

**Note**: Also added YamlDotNet to address analysis finding I1 (YAML library not explicitly specified).

#### T040: Configure Logging with Serilog

**Completely rewritten** with comprehensive Serilog configuration guidance:

**New Content Includes**:

1. **Sinks Configuration**:
   - Console sink: Formatted output for CLI diagnostics
   - File sink: Rolling file logs in `.logs/tom-.log` (daily rolling, 7-day retention)

2. **Log Levels** (per constitution):
   - Debug: I/O operations (file reads/writes, API calls)
   - Information: CLI commands, authentication, memory entry creation
   - Warning: Retry attempts, degraded performance, non-fatal errors
   - Error: Failed operations, LLM API errors, storage errors
   - Fatal: Unrecoverable errors causing application termination

3. **Enrichers**:
   - Environment: Machine name, environment (Development/Production)
   - Thread ID: For parallel operation debugging
   - Timestamp: UTC timestamps for all log entries

4. **Structured Logging Examples**:
   ```csharp
   Log.Information("Created {EntryType} entry {EntryId} in {Duration}ms", 
                   "Daily", entryId, duration)
   ```

5. **Security Requirements**:
   - Never log secrets: API keys, SSH passphrases, session tokens
   - Never log full user memory content (use excerpts or IDs only)
   - Sanitize PII before logging

6. **Complete appsettings.json Configuration**:
   - Console sink with formatted output template
   - File sink with daily rolling, 7-day retention
   - Minimum level configuration with overrides for Microsoft/System namespaces
   - Multiple enrichers configured

7. **Enhanced Acceptance Criteria**:
   - Security validation (no secrets logged)
   - .gitignore update for `.logs/` directory
   - Microsoft.Extensions.Logging.ILogger<T> integration

---

## Rationale for Serilog

### Why Serilog?

1. **Organizational Standard**: Widely used across the organization, ensuring consistency
2. **Structured Logging**: First-class support for semantic properties (not just string interpolation)
3. **Performance**: Highly optimized for minimal overhead
4. **Flexibility**: Rich sink ecosystem (Console, File, Seq, Elasticsearch, etc.)
5. **Configuration**: Supports JSON configuration via appsettings.json
6. **Microsoft Integration**: Seamless integration with Microsoft.Extensions.Logging

### Key Advantages for Ten Second Tom

1. **Diagnostics**: File-based logs help troubleshoot user issues
2. **Performance Monitoring**: Structured properties enable duration tracking
3. **Security Auditing**: Authentication events and security-relevant operations logged
4. **User Support**: Users can share logs without exposing sensitive data (with proper sanitization)
5. **Development**: Rich console output aids local debugging

---

## Implementation Guidance

### For Task Execution

When implementing T040 (Configure Logging with Serilog):

1. **Install NuGet Packages** (from T002):
   ```bash
   dotnet add package Serilog
   dotnet add package Serilog.Extensions.Logging
   dotnet add package Serilog.Sinks.Console
   dotnet add package Serilog.Sinks.File
   dotnet add package Serilog.Enrichers.Environment
   dotnet add package Serilog.Settings.Configuration
   ```

2. **Configure in Program.cs**:
   ```csharp
   Log.Logger = new LoggerConfiguration()
       .ReadFrom.Configuration(configuration)
       .CreateLogger();
   
   builder.Services.AddLogging(loggingBuilder =>
       loggingBuilder.AddSerilog(dispose: true));
   ```

3. **Update appsettings.json** (use template from T040 description)

4. **Update .gitignore**:
   ```
   .logs/
   ```

5. **Use ILogger<T> in Code**:
   ```csharp
   public class CreateDailyEntryHandler
   {
       private readonly ILogger<CreateDailyEntryHandler> _logger;
       
       public CreateDailyEntryHandler(ILogger<CreateDailyEntryHandler> logger)
       {
           _logger = logger;
       }
       
       public async Task<Result<DailyEntry>> Handle(...)
       {
           _logger.LogInformation("Creating daily entry for command {Command}", "today");
           // ... implementation
       }
   }
   ```

### Security Guidelines

**Always Sanitize Before Logging**:

```csharp
// ❌ NEVER do this:
_logger.LogDebug("API Key: {ApiKey}", apiKey);
_logger.LogInformation("User input: {Input}", fullUserMemoryContent);

// ✅ ALWAYS do this:
_logger.LogInformation("Created entry {EntryId}", entryId);
_logger.LogDebug("User input length: {Length} chars", userInput.Length);
_logger.LogError("LLM provider {Provider} failed with error: {Error}", 
                 providerName, errorMessage);
```

---

## Validation Checklist

### Constitution Compliance

- [x] Serilog mandate added to Core Principles (Section I)
- [x] Logging Standards section added to Development & Operations Standards
- [x] Constitution version bumped to v1.1.0 (MINOR)
- [x] Changelog added to constitution
- [x] Last Amended date updated to 2025-10-02

### Specification Updates

- [x] spec.md: Added Technical Requirements > Logging Framework section
- [x] plan.md: Added all Serilog packages to dependencies
- [x] plan.md: Updated Constitution Check Section I
- [x] tasks.md: Added all Serilog packages to T002
- [x] tasks.md: Completely rewrote T040 with comprehensive guidance

### Implementation Readiness

- [x] All required Serilog packages identified
- [x] Configuration template provided (appsettings.json)
- [x] Log levels defined per constitutional standards
- [x] Security requirements specified (no secrets/PII)
- [x] Structured logging patterns provided
- [x] Integration with Microsoft.Extensions.Logging documented

---

## Impact Analysis

### Breaking Changes
**None** - This is a new requirement for a greenfield project.

### New Requirements
- Serilog NuGet packages (6 packages)
- appsettings.json configuration section
- .gitignore entry for `.logs/` directory
- ILogger<T> injection in all handlers/services

### Estimated Effort Impact
- **T002** (Add Dependencies): +5 minutes (6 additional packages)
- **T040** (Configure Logging): +15 minutes (comprehensive configuration vs. basic setup)
- **Total**: +20 minutes to overall project timeline

### Benefits
- ✅ Organizational consistency
- ✅ Better diagnostics and troubleshooting
- ✅ Structured logging for monitoring
- ✅ Security audit trail
- ✅ User support capabilities (shareable logs)

---

## Related Analysis Findings

This amendment also addresses analysis finding **A5**:

> **A5** (Ambiguity - LOW): "Log levels configurable" but no specification of which levels to use for which events

**Resolution**: T040 now explicitly specifies:
- Debug: I/O operations
- Information: CLI commands, authentication
- Warning: Retry attempts, degraded performance
- Error: Failed operations
- Fatal: Unrecoverable errors

---

## Next Steps

1. ✅ Constitutional amendment complete (v1.1.0)
2. ✅ All specification documents updated
3. ⏭️ Proceed with implementation following updated T040 guidance
4. ⏭️ Validate logging during integration testing (T064)
5. ⏭️ Review log output in quickstart execution (T068)

---

## Approval Status

**Constitutional Amendment**: Approved (October 2, 2025)  
**Specification Updates**: Complete  
**Ready for Implementation**: ✅ YES

**Amendment Author**: AI Specification Enhancement  
**Requested By**: User (organizational requirement)  
**Review Status**: Ready for stakeholder approval

---

## References

- Constitution v1.1.0: `.specify/memory/constitution.md`
- Feature Specification: `specs/001-ten-second-tom/spec.md`
- Implementation Plan: `specs/001-ten-second-tom/plan.md`
- Task List: `specs/001-ten-second-tom/tasks.md`
- Serilog Documentation: https://serilog.net/
