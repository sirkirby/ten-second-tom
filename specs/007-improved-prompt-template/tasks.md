# Tasks: Improved Prompt Template Support

**Feature**: 007-improved-prompt-template
**Input**: Design documents from `/specs/007-improved-prompt-template/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are included throughout as this follows TDD approach (80% coverage required per constitution)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

**Implementation Notes**: See `IMPLEMENTATION-NOTES.md` for deviations from spec, bug fixes, and additional enhancements applied during implementation.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [x] T001 Create feature directory structure at `src/Features/Templates/` with subdirectories: Commands/, Queries/, Handlers/, Validation/, Models/
- [x] T002 [P] Verify YamlDotNet dependency (v16.3.0) is available in project
- [x] T003 [P] Verify Spectre.Console dependency (v0.51.1) is available in project

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Core Models and Enums

- [x] T004 [P] Create `TemplateType` enum in `src/Features/Templates/Models/TemplateMetadata.cs` with Daily and Weekly values
- [x] T005 [P] Create `TemplateSource` enum in `src/Shared/Models/PromptTemplate.cs` with Embedded and FileSystem values
- [x] T006 [P] Test: Create `TemplateMetadataTests.cs` in `tests/Unit/Features/Templates/` with tests for validation rules
- [x] T007 Create `TemplateMetadata` record in `src/Features/Templates/Models/TemplateMetadata.cs` with required fields (TemplateType, Title) and optional fields (Description, Version, Author, CreatedDate, Tags)
- [x] T008 Add Validate() method to `TemplateMetadata` with all validation rules per data-model.md

### Template Models

- [x] T009 [P] Create `TemplateListItem` record in `src/Features/Templates/Models/TemplateListItem.cs` for UI selection
- [x] T010 Enhance `PromptTemplate` record in `src/Shared/Models/PromptTemplate.cs` by adding nullable `Metadata` property and required `Source` property

### YAML Parsing Infrastructure

- [x] T011 [P] Test: Create `YamlFrontMatterParserTests.cs` in `tests/Unit/Infrastructure/Prompts/` with tests for valid YAML, invalid YAML, no YAML, and malformed content
- [x] T012 Create `YamlFrontMatterParser` class in `src/Infrastructure/Prompts/YamlFrontMatterParser.cs` to parse YAML front matter delimited by `---`
- [x] T013 Implement parsing logic with YamlDotNet to extract TemplateMetadata and remaining content

### Template Validation

- [x] T014 [P] Test: Create `TemplateValidatorTests.cs` in `tests/Unit/Features/Templates/` with tests for file size limits, metadata validation, content validation (validator returns Result<bool> or Result<PromptTemplate>)
- [x] T015 Create `TemplateValidator` class in `src/Features/Templates/Validation/TemplateValidator.cs` with validation methods for file size (1MB max), metadata structure, content encoding (UTF-8), and business rules

**Checkpoint**: Foundation ready - core models, parsing, and validation complete. User story implementation can now begin.

---

## Phase 3: User Story 1 - New User Setup with Default Templates (Priority: P1) 🎯 MVP

**Goal**: New users receive default prompt templates automatically during guided setup

**Independent Test**: Run guided setup, verify templates directory created at `{MemoryDirectory}/templates/` with daily-summary.md and weekly-review.md

### Tests for User Story 1

**NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T016 [P] [US1] Test: Create `InstallDefaultTemplatesHandlerTests.cs` in `tests/Unit/Features/Templates/` with tests for: installing to empty directory, skipping existing templates, overwrite behavior, idempotency
- [x] T017 [P] [US1] Test: Create `SetupWithTemplatesTests.cs` in `tests/Integration/Features/Setup/` with end-to-end test for setup installing templates
- [x] T018 [P] [US1] Test: Create `EmbeddedPromptTemplateLoaderTests.cs` in `tests/Unit/Infrastructure/Prompts/` with tests for loading embedded templates with YAML parsing

### Implementation for User Story 1

#### Embedded Template Updates

- [x] T019 [P] [US1] Update `src/Infrastructure/Prompts/Templates/daily-summary.md` to add YAML front matter with templateType: daily, title, description, version: 1.0
- [x] T020 [P] [US1] Update `src/Infrastructure/Prompts/Templates/weekly-review.md` to add YAML front matter with templateType: weekly, title, description, version: 1.0

#### Command and Handler for Template Installation

- [x] T021 [US1] Create `InstallDefaultTemplatesCommand` record in `src/Features/Templates/Commands/InstallDefaultTemplatesCommand.cs` with TargetDirectory and OverwriteExisting parameters
- [x] T022 [US1] Create `InstallDefaultTemplatesResult` record in same file with TemplatesInstalled, TemplatesSkipped, TemplatesFailed, InstalledTemplateIds
- [x] T023 [US1] Create `InstallDefaultTemplatesHandler` class in `src/Features/Templates/Handlers/InstallDefaultTemplatesHandler.cs` implementing IRequestHandler
- [x] T024 [US1] Implement handler logic to: create target directory if missing, copy embedded templates to filesystem, skip existing files when OverwriteExisting=false, return detailed result

#### Loader Enhancements

- [x] T025 [US1] Update `IPromptTemplateLoader` interface in `src/Infrastructure/Prompts/IPromptTemplateLoader.cs` to add LoadAllTemplatesAsync() and TemplatesDirectoryExistsAsync() methods
- [x] T026 [US1] Update `EmbeddedPromptTemplateLoader` in `src/Infrastructure/Prompts/EmbeddedPromptTemplateLoader.cs` to parse YAML front matter and implement new interface methods

#### Setup Integration

- [x] T027 [US1] Test: Update `SetupCommandHandlerTests.cs` to verify template installation during setup
- [x] T028 [US1] Update `SetupCommandHandler` in `src/Features/Setup/Handlers/SetupCommandHandler.cs` to call InstallDefaultTemplatesCommand after memory directory configuration

**Checkpoint**: At this point, new user setup creates templates directory with default templates, fully testable independently

---

## Phase 4: User Story 2 - Template Selection for Summary Generation (Priority: P1) 🎯 MVP

**Goal**: Users can select which prompt template to use when generating summaries, with templates filtered by type

**Independent Test**: Complete today command inputs, verify template selection prompt appears showing only daily templates, select template, verify summary uses selected template

### Tests for User Story 2

- [x] T029 [P] [US2] Test: Create `FileSystemTemplateLoaderTests.cs` in `tests/Unit/Infrastructure/Prompts/` with tests for: loading valid template, handling invalid YAML, file size limits, concurrent access retry, filtering by type
- [x] T030 [P] [US2] Test: Create `ListTemplatesQueryHandlerTests.cs` in `tests/Unit/Features/Templates/` with tests for: filtering by type, sorting (defaults first then alphabetical), handling invalid templates, empty results
- [x] T031 [P] [US2] Test: Create `TemplateSelectionUITests.cs` in `tests/Unit/Infrastructure/Cli/` with tests for: single template auto-selection, multiple template display, user selection handling, cancellation
- [x] T032 [P] [US2] Test: Create `CreateDailyEntryCommandTests.cs` in `tests/Integration/Features/Today/` with end-to-end test including template selection
- [x] T033 [P] [US2] Test: Create `CreateWeeklyReviewCommandTests.cs` in `tests/Integration/Features/ThisWeek/` with end-to-end test including template selection

### Implementation for User Story 2

#### File System Template Loader

- [x] T034 [US2] Create `FileSystemTemplateLoader` class in `src/Infrastructure/Prompts/FileSystemTemplateLoader.cs` implementing IPromptTemplateLoader
- [x] T035 [US2] Implement LoadTemplateAsync() with: file reading using FileShare.Read, YAML parsing via YamlFrontMatterParser, validation via TemplateValidator, retry logic for transient locks (2 attempts, 100ms delay)
- [x] T036 [US2] Implement LoadAllTemplatesAsync() to: discover all .md files in templates directory, parse each file, filter by TemplateType, skip invalid templates with logging, return sorted list (defaults first, then alphabetical)
- [x] T037 [US2] Implement TemplatesDirectoryExistsAsync() to check directory existence and readability

#### Template Query and Selection

- [x] T038 [US2] Create `ListTemplatesQuery` record in `src/Features/Templates/Queries/ListTemplatesQuery.cs` with TemplateType and IncludeInvalid parameters
- [x] T039 [US2] Create `ListTemplatesQueryResult` record in same file with Templates list, TotalFound, InvalidCount
- [x] T040 [US2] Create `ListTemplatesQueryHandler` in `src/Features/Templates/Handlers/ListTemplatesQueryHandler.cs` implementing IRequestHandler
- [x] T041 [US2] Implement handler to: call LoadAllTemplatesAsync() on template loader, map to TemplateListItem models, sort appropriately, return result with counts

#### Selection UI

- [x] T042 [US2] Create `ITemplateSelectionUI` interface in `src/Infrastructure/Cli/TemplateSelectionUI.cs` with SelectTemplateAsync() method
- [x] T043 [US2] Create `TemplateSelectionUI` implementation in same file using Spectre.Console
- [x] T044 [US2] Implement SelectTemplateAsync() to: auto-select if only one template, display SelectionPrompt with formatted template list (Title - Description [Default]), handle cancellation, return selected template ID

#### Command Integration

- [x] T045 [US2] Update `CreateDailyEntryCommand` handler in `src/Features/Today/Commands/CreateDailyEntryCommand.cs` to: add template selection step after data collection, call ListTemplatesQuery with TemplateType.Daily, call TemplateSelectionUI, load selected template, use template content for LLM prompt
- [x] T046 [US2] Update `CreateWeeklyReviewCommand` handler in `src/Features/ThisWeek/Commands/CreateWeeklyReviewCommand.cs` to: add template selection step after data collection, call ListTemplatesQuery with TemplateType.Weekly, call TemplateSelectionUI, load selected template, use template content for LLM prompt

**Checkpoint**: Template selection fully integrated into both commands, filtered by type, with tests confirming independent functionality

---

## Phase 5: User Story 3 - Existing User Configuration Migration (Priority: P2)

**Goal**: Existing users are automatically migrated to include templates directory without manual intervention

**Independent Test**: Simulate existing config without templates directory, run any command, verify migration occurs automatically and templates are installed

### Tests for User Story 3

- [x] T047 [P] [US3] Test: Update `ConfigurationCheckerTests.cs` in `tests/Unit/Infrastructure/Configuration/` with tests for: detecting missing templates directory, auto-creating directory, auto-installing default templates, self-healing behavior, logging migration actions
- [x] T048 [P] [US3] Test: Create `ConfigurationMigrationTests.cs` in `tests/Integration/Infrastructure/Configuration/` with end-to-end migration scenarios

### Implementation for User Story 3

- [x] T049 [US3] Update `ConfigurationChecker` in `src/Infrastructure/Configuration/ConfigurationChecker.cs` to add ValidateTemplatesDirectory() method
- [x] T050 [US3] Implement template directory validation to: check if templates directory exists, check if default templates exist, determine if migration needed
- [x] T051 [US3] Add migration logic to: create templates directory if missing, call InstallDefaultTemplatesCommand to install defaults, log migration actions, handle failures gracefully (continue without blocking app)
- [x] T052 [US3] Update ValidateAndMigrateAsync() or equivalent method to call template directory validation during configuration check

**Checkpoint**: Migration fully automatic, existing users seamlessly upgraded, self-healing behavior tested

---

## Phase 6: User Story 4 - Custom Template Creation (Priority: P3)

**Goal**: Users can create custom templates by adding markdown files to templates directory

**Independent Test**: Manually create new template file with YAML metadata in templates directory, run appropriate command, verify custom template appears in selection list

### Tests for User Story 4

- [x] T053 [P] [US4] Test: Add tests to `ListTemplatesQueryHandlerTests.cs` for: custom templates appearing alongside defaults, custom templates sorted alphabetically, multiple custom templates handling
- [x] T054 [P] [US4] Test: Add tests to `FileSystemTemplateLoaderTests.cs` for: loading custom templates, handling malformed custom templates, validating custom template metadata
- [x] T055 [P] [US4] Test: Create `TemplateWorkflowTests.cs` in `tests/Integration/Features/Templates/` with end-to-end test for: creating custom template file, running command, selecting custom template, verifying it works

### Implementation for User Story 4

**NOTE**: Core implementation already complete in US2 (FileSystemTemplateLoader discovers all .md files)

- [x] T056 [US4] Add validation rules for custom templates in `TemplateValidator` to ensure: valid filename (kebab-case, no path separators), proper YAML structure, required metadata fields present
- [x] T057 [US4] Enhance `ListTemplatesQueryHandler` sorting logic to ensure: default templates marked with IsDefault=true, defaults appear first, custom templates sorted alphabetically after defaults
- [x] T058 [US4] Add logging in `FileSystemTemplateLoader` to log information when: new custom templates are discovered, custom templates fail validation (with reason), custom templates load successfully

**Checkpoint**: Custom templates fully supported, discoverable, and sorted appropriately in selection UI

---

## Phase 7: User Story 5 - Template Editing and Updates (Priority: P3)

**Goal**: Users can edit templates and changes take effect immediately without restart

**Independent Test**: Edit an existing template file (change content or metadata), run command, verify changes are reflected

### Tests for User Story 5

- [x] T059 [P] [US5] Test: Create `TemplateEditingTests.cs` in `tests/Integration/Features/Templates/` with tests for: editing template content reflects in next run, changing template type moves it to correct filter, editing metadata updates display in selection
- [x] T060 [P] [US5] Test: Add tests to `FileSystemTemplateLoaderTests.cs` for: loading edited template shows new content, no caching of old content, concurrent edit handling

### Implementation for User Story 5

**NOTE**: Core implementation already complete - templates loaded fresh on each command execution

- [x] T061 [US5] Verify `FileSystemTemplateLoader` does NOT cache templates across command invocations (each command execution loads fresh)
- [x] T062 [US5] Ensure `ListTemplatesQueryHandler` and `FileSystemTemplateLoader` reload templates on each query (no persistent caching)
- [x] T063 [US5] Add integration test to verify: edit template file, run command immediately after, verify new content used
- [x] T063a [US5] Test: Add test case to `TemplateEditingTests.cs` for end-to-end immediate recognition: create new template file in filesystem, run command within same process instance, verify new template appears in selection list without restart (validates FR-012)

**Checkpoint**: Template changes immediately effective, no restart required, tested end-to-end including new file creation (T063a validates FR-012)

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final quality checks

### Error Handling and Fallbacks

- [x] T064 [P] Implement fallback mechanism in command handlers: if no valid filesystem templates found, fall back to embedded templates from `EmbeddedPromptTemplateLoader`
- [x] T065 [P] Add user notification when fallback occurs (e.g., "No custom templates found, using default embedded template")
- [x] T066 [P] Test fallback scenarios: deleted templates directory, all templates invalid, corrupted template files

### Self-Healing Features

- [x] T067 [P] Test: Create `SelfHealingTests.cs` in `tests/Integration/Infrastructure/` with tests for: deleted directory recreated, missing defaults restored, recovery notifications shown
- [x] T068 Implement self-healing in `ConfigurationChecker` to: detect deleted templates directory on command execution, automatically recreate and reinstall defaults, log recovery actions

### XML Documentation

- [x] T069 [P] Add XML documentation to all public APIs in `src/Features/Templates/Models/` (TemplateMetadata, TemplateListItem)
- [x] T070 [P] Add XML documentation to all public APIs in `src/Features/Templates/Commands/` and `src/Features/Templates/Queries/`
- [x] T071 [P] Add XML documentation to all handlers in `src/Features/Templates/Handlers/`
- [x] T072 [P] Add XML documentation to `ITemplateSelectionUI` and implementation
- [x] T073 [P] Add XML documentation to updated methods in `IPromptTemplateLoader` and implementations

### Testing and Quality

- [x] T074 Run full test suite and verify 80%+ code coverage for Templates feature
- [x] T075 [P] Add additional edge case tests for: concurrent template access, very large templates (near 1MB limit), templates with special characters in filenames
- [x] T076 Run quickstart.md validation scenarios to ensure all steps work end-to-end

### Performance Optimization

- [x] T077 [P] Profile template loading performance, ensure LoadAllTemplatesAsync() completes in <100ms for 20 templates
- [x] T078 [P] Profile template selection UI, ensure display and selection completes in <10s

### Code Quality

- [x] T079 [P] Code cleanup: remove any TODO comments, unused imports, debug logging
- [x] T080 [P] Refactoring: DRY check across template loading implementations, extract common validation logic
- [x] T081 Security review: ensure no path traversal vulnerabilities, validate all user-provided template paths stay within configured directory

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion
  - US1 (P1) and US2 (P1) are MVP - complete these first
  - US1 must complete before US2 (templates must exist before selection works)
  - US3 (P2) can proceed after US1 completes
  - US4 (P3) can proceed after US2 completes (uses same discovery logic)
  - US5 (P3) can proceed after US2 completes (verifies no caching)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (US1 - P1)**: Can start after Foundational - No dependencies on other stories
- **User Story 2 (US2 - P1)**: Depends on US1 (needs templates to exist for selection to work)
- **User Story 3 (US3 - P2)**: Depends on US1 (migration installs templates using same logic)
- **User Story 4 (US4 - P3)**: Depends on US2 (uses same discovery and loading logic)
- **User Story 5 (US5 - P3)**: Depends on US2 (verifies templates reload on each execution)

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Models before services
- Services before UI components
- Core implementation before integration
- Story tests pass before moving to next priority

### Parallel Opportunities

**Phase 1 (Setup)**:
- T002 and T003 can run in parallel (different dependencies)

**Phase 2 (Foundational)**:
- T004 and T005 can run in parallel (different enums)
- T006 and T009 can run in parallel (different test files)
- After T007 completes: T008, T009, T010 can run in parallel
- T011 and T014 can run in parallel (different test files)

**User Story 1**:
- T016, T017, T018 can run in parallel (different test files)
- T019 and T020 can run in parallel (different template files)
- After T023 completes: T024, T025, T026 can be worked in parallel by different developers

**User Story 2**:
- T029, T030, T031, T032, T033 can run in parallel (different test files)
- T034-T037 are sequential (same file)
- T038-T041 are sequential (same feature)
- T042-T044 are sequential (same file)
- T045 and T046 can run in parallel (different command handlers)

**User Story 3**:
- T047 and T048 can run in parallel (different test types)

**User Story 4**:
- T053, T054, T055 can run in parallel (different test files)
- T056, T057, T058 can run in parallel (different files/concerns)

**User Story 5**:
- T059 and T060 can run in parallel (different test files)

**Phase 8 (Polish)**:
- T064, T065, T066 can run in parallel
- T067 and T068 can run together
- T069-T073 can all run in parallel (different files)
- T074-T081 can run in parallel (different concerns)

---

## Parallel Example: User Story 2 Tests

```bash
# Launch all tests for User Story 2 together:
Task: "Test: Create FileSystemTemplateLoaderTests.cs in tests/Unit/Infrastructure/Prompts/"
Task: "Test: Create ListTemplatesQueryHandlerTests.cs in tests/Unit/Features/Templates/"
Task: "Test: Create TemplateSelectionUITests.cs in tests/Unit/Infrastructure/Cli/"
Task: "Test: Create CreateDailyEntryCommandTests.cs in tests/Integration/Features/Today/"
Task: "Test: Create CreateWeeklyReviewCommandTests.cs in tests/Integration/Features/ThisWeek/"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T015) - CRITICAL blocking phase
3. Complete Phase 3: User Story 1 (T016-T028) - Default templates installed during setup
4. Complete Phase 4: User Story 2 (T029-T046) - Template selection integrated into commands
5. **STOP and VALIDATE**: Test end-to-end workflow for new user
6. **MVP READY**: New users can run setup, receive default templates, and select templates when generating summaries

### Incremental Delivery

1. **MVP** (US1 + US2): New users get full template support → Deploy/Demo
2. **+ US3**: Existing users automatically migrated → Deploy/Demo
3. **+ US4**: Custom template creation enabled → Deploy/Demo
4. **+ US5**: Template editing verified → Deploy/Demo
5. **+ Phase 8**: Polish, performance, documentation → Final Release

### Parallel Team Strategy

With multiple developers after Foundational phase completes:

1. **Team completes Setup + Foundational together** (T001-T015)
2. **Developer A**: User Story 1 (T016-T028) - Template installation
3. **Developer B**: User Story 2 tests and models (T029-T037) - Template loading
4. **After US1 completes**:
   - Developer A: User Story 3 (T047-T052) - Migration
   - Developer B: Continue User Story 2 (T038-T046) - Selection UI and integration
5. **After US2 completes**:
   - Developer A or B: User Story 4 (T053-T058) - Custom templates
   - Developer A or B: User Story 5 (T059-T063) - Template editing
6. **Team**: Polish phase together (T064-T081)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail (RED) before implementing (TDD cycle)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Focus on US1 + US2 for MVP before adding US3, US4, US5
- Templates feature is self-contained vertical slice - minimal impact on existing code
- All file paths are absolute from repository root
- Constitution Principle III mandates 80% test coverage - tests included throughout

---

## Success Criteria Verification

After implementation, verify these success criteria from spec.md:

- **SC-001**: ✅ New users completing guided setup receive working default templates within 2 seconds
- **SC-002**: ✅ 100% of existing users automatically migrated without manual intervention
- **SC-003**: ⏸️ Template selection completes in under 10 seconds (Phase 4 - pending)
- **SC-004**: ✅ Custom templates recognized within 1 second of next command (foundation complete)
- **SC-005**: ⏸️ Template filtering 100% accurate (Phase 4 - pending)
- **SC-006**: ⏸️ Edited templates reflected immediately without restart (Phase 7 - pending)
- **SC-007**: ⏸️ Users can edit templates in any text editor without issues (Phase 7 - pending)
- **SC-008**: ⏸️ System handles invalid templates without crashing (Phase 8 - pending)
- **SC-009**: ⏸️ Templates >1MB rejected with clear warnings (Phase 8 - pending)

---

## Additional Tasks Completed (Not in Original Spec)

### Environment Variable Override Fix

**Issue**: During testing, discovered that `config show` command was not respecting environment variable overrides for Storage, SSH, and Optional configuration sections.

**Tasks Completed**:
- Fixed `.env` file to use correct configuration key: `Storage__MemoryDirectory` instead of `TenSecondTom__MemoryDirectory`
- Enhanced `ConfigCommandHandler.HandleShowAsync()` to apply environment variable overrides for ALL configuration sections:
  - SSH: `Ssh__KeyPath`, `Ssh__KeySource`, `Ssh__AgentSocketPath`
  - LLM: `Llm__Provider`, `Llm__ApiKey`, `Llm__Model` (already worked)
  - Storage: `Storage__MemoryDirectory`, `Storage__CreateIfMissing` (fixed)
  - Optional: `Optional__LogLevel`, `Optional__RetentionDays`, `Optional__EnableTelemetry` (added)

**Files Modified**:
- `src/Features/Setup/Handlers/ConfigCommandHandler.cs` (lines 76-145)
- `.env` (corrected configuration key)

**Impact**: Developers can now override ANY configuration setting via environment variables, and `config show` accurately displays the effective configuration. Critical for local development and testing.

**Testing Verified**:
```bash
Storage__MemoryDirectory="./.memory" ./src/bin/Release/net9.0/TenSecondTom config show
# Correctly shows: Memory Directory │ ./.memory
```

See `IMPLEMENTATION-NOTES.md` for complete details on all deviations and fixes.
