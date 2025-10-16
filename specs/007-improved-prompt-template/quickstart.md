# Quick Start Guide: Improved Prompt Template Support

**Feature**: 007-improved-prompt-template
**Date**: 2025-10-15
**For**: Developers implementing this feature

## Overview

This guide provides a step-by-step walkthrough for implementing filesystem-based prompt template support with YAML metadata. Follow the TDD approach: write tests first, make them fail, then implement.

## Prerequisites

- .NET 9 SDK installed
- Ten Second Tom repository cloned
- Familiarity with xUnit, FluentAssertions, and Moq
- Understanding of Vertical Slice Architecture and CQRS patterns

## Implementation Order

Follow this sequence to minimize integration issues:

### Phase 1: Core Models and Metadata Parsing (Day 1)

**Goal**: Create data structures and YAML parsing logic

1. **Create TemplateMetadata Model**
   - File: `src/Features/Templates/Models/TemplateMetadata.cs`
   - Test: `tests/Unit/Features/Templates/TemplateMetadataTests.cs`
   - TDD Steps:
     ```csharp
     // Test first
     [Fact]
     public void Validate_WithValidMetadata_ReturnsNoErrors()
     {
         var metadata = new TemplateMetadata
         {
             TemplateType = TemplateType.Daily,
             Title = "Test Template"
         };

         var errors = metadata.Validate();

         errors.Should().BeEmpty();
     }
     ```
   - Implement validation logic
   - Add tests for all validation rules

2. **Create TemplateListItem Model**
   - File: `src/Features/Templates/Models/TemplateListItem.cs`
   - Simple record, minimal testing needed
   - Test sorting logic in query handler tests later

3. **Update PromptTemplate Model**
   - File: `src/Shared/Models/PromptTemplate.cs` (existing)
   - Add `Metadata` and `Source` properties
   - Mark as nullable for backward compatibility
   - No breaking changes to existing code

4. **Create YAML Parser Helper**
   - File: `src/Infrastructure/Prompts/YamlFrontMatterParser.cs`
   - Test: `tests/Unit/Infrastructure/Prompts/YamlFrontMatterParserTests.cs`
   - TDD Steps:
     ```csharp
     [Fact]
     public void Parse_WithValidYaml_ReturnsMetadataAndContent()
     {
         var input = @"---
templateType: daily
title: Test
---
Content here";

         var result = YamlFrontMatterParser.Parse(input);

         result.IsSuccess.Should().BeTrue();
         result.Value.Metadata.Title.Should().Be("Test");
         result.Value.Content.Should().Be("Content here");
     }
     ```
   - Implement using YamlDotNet
   - Test edge cases: no front matter, malformed YAML, empty content

### Phase 2: Template Validation (Day 1-2)

**Goal**: Ensure template files are valid before loading

5. **Create TemplateValidator**
   - File: `src/Features/Templates/Validation/TemplateValidator.cs`
   - Test: `tests/Unit/Features/Templates/TemplateValidatorTests.cs`
   - TDD Steps:
     ```csharp
     [Theory]
     [InlineData(1_048_577)] // Just over 1MB
     [InlineData(2_000_000)]
     public void ValidateFile_WhenTooLarge_ReturnsError(long fileSize)
     {
         var validator = new TemplateValidator();
         var fileInfo = CreateMockFileInfo(fileSize);

         var result = validator.ValidateFile(fileInfo);

         result.IsSuccess.Should().BeFalse();
         result.Error.Should().Contain("size limit");
     }
     ```
   - Implement all validation rules from data-model.md
   - Test file size, metadata, content, encoding

### Phase 3: File System Template Loader (Day 2-3)

**Goal**: Load templates from filesystem with YAML parsing

6. **Create FileSystemTemplateLoader**
   - File: `src/Infrastructure/Prompts/FileSystemTemplateLoader.cs`
   - Test: `tests/Unit/Infrastructure/Prompts/FileSystemTemplateLoaderTests.cs`
   - TDD Steps:
     ```csharp
     [Fact]
     public async Task LoadTemplateAsync_WithValidTemplate_ReturnsTemplate()
     {
         var loader = new FileSystemTemplateLoader(testDirectory);
         CreateTemplateFile("test-template.md", validYaml);

         var result = await loader.LoadTemplateAsync("test-template");

         result.IsSuccess.Should().BeTrue();
         result.Value.TemplateId.Should().Be("test-template");
         result.Value.Source.Should().Be(TemplateSource.FileSystem);
     }
     ```
   - Use temporary directories for tests
   - Mock file system for unit tests, use real files for integration tests
   - Test concurrent access with retry logic

7. **Update IPromptTemplateLoader Interface**
   - File: `src/Infrastructure/Prompts/IPromptTemplateLoader.cs` (existing)
   - Add `LoadAllTemplatesAsync()` method
   - Add `TemplatesDirectoryExistsAsync()` method
   - Update existing implementations

8. **Update EmbeddedPromptTemplateLoader**
   - File: `src/Infrastructure/Prompts/EmbeddedPromptTemplateLoader.cs` (existing)
   - Add YAML parsing for embedded templates
   - Implement new interface methods
   - Test fallback behavior

### Phase 4: Template Installation (Day 3-4)

**Goal**: Install default templates during setup/migration

9. **Create InstallDefaultTemplatesCommand**
   - File: `src/Features/Templates/Commands/InstallDefaultTemplatesCommand.cs`
   - Record definition, no logic

10. **Create InstallDefaultTemplatesHandler**
    - File: `src/Features/Templates/Handlers/InstallDefaultTemplatesHandler.cs`
    - Test: `tests/Unit/Features/Templates/InstallDefaultTemplatesHandlerTests.cs`
    - TDD Steps:
      ```csharp
      [Fact]
      public async Task Handle_WithEmptyDirectory_InstallsAllTemplates()
      {
          var handler = CreateHandler();
          var command = new InstallDefaultTemplatesCommand(testDir);

          var result = await handler.Handle(command, default);

          result.IsSuccess.Should().BeTrue();
          result.Value.TemplatesInstalled.Should().Be(2); // daily + weekly
          Directory.GetFiles(testDir, "*.md").Should().HaveCount(2);
      }
      ```
    - Test idempotency (run twice, second run skips existing)
    - Test overwrite behavior

11. **Update Embedded Templates with YAML**
    - Files: `src/Infrastructure/Prompts/Templates/daily-summary.md`
    - Files: `src/Infrastructure/Prompts/Templates/weekly-review.md`
    - Add YAML front matter to existing templates
    - Ensure templates still work with existing code

### Phase 5: Template Query and Selection (Day 4-5)

**Goal**: List and select templates

12. **Create ListTemplatesQuery and Handler**
    - File: `src/Features/Templates/Queries/ListTemplatesQuery.cs`
    - File: `src/Features/Templates/Handlers/ListTemplatesQueryHandler.cs`
    - Test: `tests/Unit/Features/Templates/ListTemplatesQueryHandlerTests.cs`
    - TDD Steps:
      ```csharp
      [Fact]
      public async Task Handle_WithMultipleTemplates_ReturnsSortedList()
      {
          var handler = CreateHandler();
          SetupTemplates("daily-custom", "daily-summary");
          var query = new ListTemplatesQuery(TemplateType.Daily);

          var result = await handler.Handle(query, default);

          result.IsSuccess.Should().BeTrue();
          result.Value.Templates.Should().HaveCount(2);
          result.Value.Templates[0].IsDefault.Should().BeTrue(); // daily-summary first
      }
      ```
    - Test filtering by type
    - Test sorting (defaults first, then alphabetical)
    - Test invalid template handling

13. **Create TemplateSelectionUI**
    - File: `src/Infrastructure/Cli/TemplateSelectionUI.cs`
    - Test: `tests/Unit/Infrastructure/Cli/TemplateSelectionUITests.cs`
    - TDD Steps:
      ```csharp
      [Fact]
      public async Task SelectTemplateAsync_WithSingleTemplate_AutoSelects()
      {
          var ui = new TemplateSelectionUI();
          var templates = new[] { CreateListItem("daily-summary") };

          var selected = await ui.SelectTemplateAsync(templates, "today");

          selected.Should().Be("daily-summary");
      }
      ```
    - Mock Spectre.Console for unit tests
    - Test auto-selection logic
    - Test multi-selection display

### Phase 6: Configuration Migration (Day 5-6)

**Goal**: Auto-migrate existing configurations

14. **Update ConfigurationChecker**
    - File: `src/Infrastructure/Configuration/ConfigurationChecker.cs` (existing)
    - Test: `tests/Unit/Infrastructure/Configuration/ConfigurationCheckerTests.cs`
    - TDD Steps:
      ```csharp
      [Fact]
      public async Task Validate_WhenTemplatesDirMissing_CreatesAndInstalls()
      {
          var checker = CreateChecker();
          SetupConfigWithoutTemplates();

          var result = await checker.ValidateAndMigrateAsync();

          result.IsSuccess.Should().BeTrue();
          Directory.Exists(templatesDir).Should().BeTrue();
          Directory.GetFiles(templatesDir, "*.md").Should().NotBeEmpty();
      }
      ```
    - Test self-healing behavior
    - Test logging of migration actions

### Phase 7: Command Integration (Day 6-7)

**Goal**: Integrate template selection into existing commands

15. **Update CreateDailyEntryCommand Handler**
    - File: `src/Features/Today/Commands/CreateDailyEntryCommand.cs` (existing)
    - Test: `tests/Integration/Features/Today/CreateDailyEntryCommandTests.cs`
    - Add template selection step after data collection, before LLM call
    - Test with mocked template selection

16. **Update CreateWeeklyReviewCommand Handler**
    - File: `src/Features/ThisWeek/Commands/CreateWeeklyReviewCommand.cs` (existing)
    - Test: `tests/Integration/Features/ThisWeek/CreateWeeklyReviewCommandTests.cs`
    - Add template selection step
    - Test filtering shows only weekly templates

### Phase 8: Setup Integration (Day 7)

**Goal**: Install templates during guided setup

17. **Update SetupCommandHandler**
    - File: `src/Features/Setup/Handlers/SetupCommandHandler.cs` (existing)
    - Test: `tests/Integration/Features/Setup/SetupCommandHandlerTests.cs`
    - Add template installation step
    - Test templates are created in configured memory directory

### Phase 9: Integration Testing (Day 8)

**Goal**: End-to-end validation

18. **Create End-to-End Tests**
    - File: `tests/Integration/Features/Templates/TemplateWorkflowTests.cs`
    - Test complete workflows:
      - New user setup → templates installed → command uses template
      - Existing user → migration → template selection works
      - User edits template → changes reflected in next run
      - User deletes template → falls back to embedded

### Phase 10: Documentation and Polish (Day 9)

**Goal**: Complete the feature

19. **Add XML Documentation**
    - All public APIs need `<summary>` and `<param>` tags
    - Add `<remarks>` for complex behavior

20. **Update User Documentation**
    - Add template documentation to user guide
    - Document template format and metadata
    - Provide examples of custom templates

21. **Test Coverage Check**
    - Run coverage report
    - Ensure 80%+ coverage
    - Add missing tests for edge cases

## Quick Commands

### Run Tests
```bash
# All tests
dotnet test

# Specific feature tests
dotnet test --filter "FullyQualifiedName~Templates"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov
```

### Build
```bash
dotnet build
dotnet build --configuration Release
```

### Run CLI
```bash
dotnet run --project src
```

### Debug Test
```bash
# Set breakpoint in code, then:
dotnet test --filter "FullyQualifiedName~SpecificTest" --logger "console;verbosity=detailed"
```

## Key Files Reference

### New Files to Create

**Models**:
- `src/Features/Templates/Models/TemplateMetadata.cs`
- `src/Features/Templates/Models/TemplateListItem.cs`

**Commands**:
- `src/Features/Templates/Commands/InstallDefaultTemplatesCommand.cs`

**Queries**:
- `src/Features/Templates/Queries/ListTemplatesQuery.cs`

**Handlers**:
- `src/Features/Templates/Handlers/InstallDefaultTemplatesHandler.cs`
- `src/Features/Templates/Handlers/ListTemplatesQueryHandler.cs`

**Infrastructure**:
- `src/Infrastructure/Prompts/FileSystemTemplateLoader.cs`
- `src/Infrastructure/Prompts/YamlFrontMatterParser.cs`
- `src/Infrastructure/Cli/TemplateSelectionUI.cs`

**Validation**:
- `src/Features/Templates/Validation/TemplateValidator.cs`

### Files to Modify

**Existing Models**:
- `src/Shared/Models/PromptTemplate.cs` - Add Metadata and Source fields

**Existing Interfaces**:
- `src/Infrastructure/Prompts/IPromptTemplateLoader.cs` - Add new methods

**Existing Loaders**:
- `src/Infrastructure/Prompts/EmbeddedPromptTemplateLoader.cs` - Add YAML support

**Existing Commands**:
- `src/Features/Today/Commands/CreateDailyEntryCommand.cs` - Add template selection
- `src/Features/ThisWeek/Commands/CreateWeeklyReviewCommand.cs` - Add template selection
- `src/Features/Setup/Handlers/SetupCommandHandler.cs` - Add template installation

**Existing Infrastructure**:
- `src/Infrastructure/Configuration/ConfigurationChecker.cs` - Add migration logic

**Existing Templates**:
- `src/Infrastructure/Prompts/Templates/daily-summary.md` - Add YAML front matter
- `src/Infrastructure/Prompts/Templates/weekly-review.md` - Add YAML front matter

## Common Patterns

### Test Structure (AAA Pattern)

```csharp
[Fact]
public async Task MethodName_WhenCondition_ExpectedBehavior()
{
    // Arrange - Set up dependencies and inputs
    var service = CreateService();
    var input = CreateInput();

    // Act - Execute the method under test
    var result = await service.MethodAsync(input);

    // Assert - Verify the expected outcome
    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeNull();
}
```

### Result<T> Pattern

```csharp
// Success
return Result<PromptTemplate>.Success(template);

// Failure
return Result<PromptTemplate>.Failure("Template not found");

// Usage
var result = await LoadTemplateAsync(id);
if (result.IsSuccess)
{
    UseTemplate(result.Value);
}
else
{
    LogError(result.Error);
}
```

### File System Testing

```csharp
// Use temporary directories
private string _testDir;

public void Setup()
{
    _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Directory.CreateDirectory(_testDir);
}

public void Teardown()
{
    if (Directory.Exists(_testDir))
        Directory.Delete(_testDir, recursive: true);
}

// Or use IDisposable with using
using var tempDir = new TempDirectory();
```

### Mocking with Moq

```csharp
var mockLoader = new Mock<IPromptTemplateLoader>();
mockLoader
    .Setup(l => l.LoadTemplateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<PromptTemplate>.Success(template));

var handler = new MyHandler(mockLoader.Object);
```

## Troubleshooting

### Tests Failing on File Access

**Problem**: Tests fail with "file in use" errors
**Solution**: Ensure all file streams are properly disposed with `using` statements

### YAML Parsing Issues

**Problem**: YamlDotNet throws exceptions
**Solution**: Wrap in try-catch, return Result.Failure with clear error message

### Template Not Found

**Problem**: Template exists but loader can't find it
**Solution**: Check file naming (lowercase, kebab-case, .md extension)

### Embedded Resources Not Found

**Problem**: Embedded template returns null
**Solution**: Verify .csproj has `<EmbeddedResource Include="...">` and path matches

## Next Steps

After completing implementation:

1. Run full test suite: `dotnet test`
2. Check coverage: Ensure 80%+ coverage
3. Manual testing: Run through user scenarios
4. Code review: Self-review against constitution
5. Documentation: Update any user-facing docs
6. Create PR: Follow PR template

## Questions?

- Check `research.md` for technical decisions
- Check `data-model.md` for entity definitions
- Check `contracts/` for interface specifications
- Check constitution for architectural guidance

## Success Criteria

✅ All tests pass (80%+ coverage)
✅ Embedded templates work as fallback
✅ File system templates load with YAML metadata
✅ Template selection UI works in both commands
✅ Setup installs templates automatically
✅ Migration works for existing users
✅ Invalid templates are gracefully skipped
✅ No breaking changes to existing code

---

**Good luck with the implementation! Remember: Test first, implement second, refactor third.**
