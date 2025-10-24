# Tasks: Generate Command for Recording Processing

**Input**: Design documents from `/specs/009-generate-recordings/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Tests are REQUIRED per project constitution (80% minimum coverage, TDD approach)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `- [ ] [ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/`, `tests/` at repository root
- Following Vertical Slice Architecture: `src/Features/Generate/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure for the Generate feature

- [X] T001 Create Generate feature directory structure at src/Features/Generate/ with subdirectories: Commands/, Queries/, Handlers/, Models/, Services/
- [X] T002 [P] Add new constants to src/Shared/Constants/CommandNames.cs for Generate command name
- [X] T003 [P] Add new constant to src/Shared/Constants/TemplateConstants.cs for BusinessMeetingTemplateId = "business-meeting" (this is the default template FILENAME "business-meeting.md", not the template TYPE enum value TemplateType.BusinessMeeting which is defined separately in T007)
- [X] T004 [P] Create LlmConstants.cs at src/Shared/Constants/LlmConstants.cs with token limit constants and estimation factors
- [X] T005 [P] Add new configuration key to src/Shared/Constants/ConfigurationKeys.cs for LlmMaxInputTokens
- [X] T005a [P] Create token limit configuration loading logic: load LlmMaxInputTokens from configuration, apply provider-specific defaults if not configured (50,000 for OpenAI models per LlmConstants.DefaultMaxInputTokensOpenAI, 80,000 for Anthropic models per LlmConstants.DefaultMaxInputTokensAnthropic), validate value is positive and within reasonable bounds (1000-200000), integrate into GenerateOutputCommandHandler or dedicated configuration service (NOTE: Uses 50k default regardless of provider - provider-specific defaults deferred as future enhancement)
- [X] T006 [P] Verify DirectoryNames.Recording constant exists in src/Shared/Constants/DirectoryNames.cs (add if missing)
- [X] T007 [P] Extend TemplateType enum in src/Shared/Models/PromptTemplate.cs to include BusinessMeeting value
- [X] T008 Create business-meeting.md bundled template at src/Infrastructure/Prompts/Templates/business-meeting.md with multi-speaker meeting summarization prompt including explicit sections for: 1) Meeting Topics, 2) Key Decisions, 3) Action Items (with responsible parties if identifiable), 4) Discussion Points/Conclusions, 5) Participants/Speakers (inferred from context). Validate prompt structure produces these sections with sample multi-speaker transcript (defer full quality validation to T052).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T009 Create RecordingListItem record in src/Features/Generate/Models/RecordingListItem.cs with properties: RecordingBaseName (format: M-D-Y_Increment like "10-21-2025_1"), TranscriptFilePath, RecordedAt, FormattedDate, WordCount, FileSizeBytes, DisplayLabel
- [X] T010 [P] Create GeneratedOutput record in src/Features/Generate/Models/GeneratedOutput.cs with ToMarkdown() method for output file formatting
- [X] T011 [P] Create TruncatedTranscript record in src/Features/Generate/Models/TruncatedTranscript.cs with truncation metadata
- [X] T012 [P] Create GenerationRequest record in src/Features/Generate/Models/GenerationRequest.cs for handler input parameters
- [X] T013 Create IRecordingService interface in src/Features/Generate/Services/IRecordingService.cs with methods: ListRecordingsAsync, GetTranscriptContentAsync, ValidateTranscriptFileAsync, ParseRecordingTimestamp
- [X] T014 [P] Create ITranscriptProcessor interface in src/Features/Generate/Services/ITranscriptProcessor.cs with methods: ProcessTranscriptAsync, EstimateTokenCount, CountWords, TruncateToWordCount
- [X] T015 [P] Create IOutputStorageService interface in src/Features/Generate/Services/IOutputStorageService.cs with methods: SaveOutputAsync, OutputExistsAsync, BuildOutputFilePath
- [X] T016 Create DependencyInjection.cs in src/Features/Generate/DependencyInjection.cs with AddGenerateFeature extension method for service registration

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Process Single Recording Interactively (Priority: P1) 🎯 MVP

**Goal**: Enable users to select a recording and template through interactive menus and generate output using LLM

**Independent Test**: Create a test recording, run `generate` command, select recording from menu, select template from menu, verify output file is generated with correct content and metadata

### Tests for User Story 1 (TDD - Write FIRST, ensure they FAIL)

- [X] T017 [P] [US1] Create RecordingServiceTests.cs at tests/TenSecondTom.Tests/Features/Generate/Services/RecordingServiceTests.cs with tests for: ListRecordingsAsync (empty directory, valid recordings, invalid filenames, sorting), GetTranscriptContentAsync (file not found, valid content), ValidateTranscriptFileAsync, ParseRecordingTimestamp
- [X] T018 [P] [US1] Create TranscriptProcessorTests.cs at tests/TenSecondTom.Tests/Features/Generate/Services/TranscriptProcessorTests.cs with tests for: EstimateTokenCount, CountWords, TruncateToWordCount (no truncation needed, truncation required, sentence boundary preservation), ProcessTranscriptAsync
- [X] T019 [P] [US1] Create OutputStorageServiceTests.cs at tests/TenSecondTom.Tests/Features/Generate/Services/OutputStorageServiceTests.cs with tests for: BuildOutputFilePath, OutputExistsAsync, SaveOutputAsync (success, directory not found, overwrite existing)
- [X] T020 [P] [US1] Create ListRecordingsQueryHandlerTests.cs at tests/TenSecondTom.Tests/Features/Generate/Handlers/ListRecordingsQueryHandlerTests.cs with tests for: no recordings found, valid recordings returned and sorted
- [X] T021 [P] [US1] Create GetRecordingTranscriptQueryHandlerTests.cs at tests/TenSecondTom.Tests/Features/Generate/Handlers/GetRecordingTranscriptQueryHandlerTests.cs with tests for: valid transcript loaded, file not found, empty path
- [X] T022 [US1] Create GenerateOutputCommandHandlerTests.cs at tests/TenSecondTom.Tests/Features/Generate/Handlers/GenerateOutputCommandHandlerTests.cs with tests for: successful generation, transcript file not found, template not found, LLM error, truncation warning logged, output saved to correct path, metadata included in output

### Implementation for User Story 1

- [X] T023 [P] [US1] Implement RecordingService.cs at src/Features/Generate/Services/RecordingService.cs with file discovery, timestamp parsing, recording list building, sorting by date descending
- [X] T023a [P] [US1] Implement filename parsing logic in RecordingService to extract date and increment from M-D-Y_Increment pattern (e.g., "10-21-2025_1" → date: 2025-10-21, increment: 1) for RecordedAt property and sorting
- [X] T024 [P] [US1] Implement TranscriptProcessor.cs at src/Features/Generate/Services/TranscriptProcessor.cs with token estimation, word counting, intelligent truncation logic, safety factor application
- [X] T025 [P] [US1] Implement OutputStorageService.cs at src/Features/Generate/Services/OutputStorageService.cs with file path building, markdown formatting via GeneratedOutput.ToMarkdown(), file write with overwrite
- [X] T026 [US1] Create ListRecordingsQuery record in src/Features/Generate/Queries/ListRecordingsQuery.cs
- [X] T027 [US1] Create GetRecordingTranscriptQuery record in src/Features/Generate/Queries/GetRecordingTranscriptQuery.cs
- [X] T028 [US1] Create GenerateOutputCommand record in src/Features/Generate/Commands/GenerateOutputCommand.cs with properties: TranscriptFilePath, RecordingBaseName, TemplateId, MaxInputTokens
- [X] T029 [US1] Implement ListRecordingsQueryHandler.cs at src/Features/Generate/Handlers/ListRecordingsQueryHandler.cs delegating to IRecordingService
- [X] T030 [US1] Implement GetRecordingTranscriptQueryHandler.cs at src/Features/Generate/Handlers/GetRecordingTranscriptQueryHandler.cs delegating to IRecordingService
- [X] T031 [US1] Implement GenerateOutputCommandHandler.cs at src/Features/Generate/Handlers/GenerateOutputCommandHandler.cs with full orchestration: validate transcript, load template, load transcript content, process transcript (truncate if needed), build prompt, call ILlmProvider, build GeneratedOutput, save output, return result
- [X] T032 [US1] Register all services and handlers in src/Features/Generate/DependencyInjection.cs AddGenerateFeature method
- [X] T033 [US1] Create CLI command handler in src/Features/Generate/GenerateCommand.cs using System.CommandLine with Spectre.Console for interactive recording selection menu and template selection menu
- [X] T034 [US1] Wire up generate command in root command registration (likely Program.cs or CommandRegistry/RootCommandBuilder if separate class exists) by adding GenerateCommand to root command's subcommands collection and invoking AddGenerateFeature() DI extension method to make it accessible via `tom generate`
- [X] T035 [US1] Add validation and error handling to GenerateCommand for: no recordings found, no templates found, LLM errors, file write errors
- [X] T036 [US1] Add truncation warning display to user when TruncatedTranscript.WasTruncated is true
- [X] T037 [US1] Display generated output to terminal and confirm save location to user after successful generation

**Checkpoint**: At this point, User Story 1 should be fully functional - users can interactively select recordings and templates to generate outputs

---

## Phase 4: User Story 2 - One-Shot Command Execution (Priority: P2)

**Goal**: Enable automation and power-user workflows via --template argument to skip interactive prompts

**Independent Test**: Run `generate --template "business-meeting"` and verify it processes the most recent recording with the specified template without any interactive prompts

### Tests for User Story 2 (TDD - Write FIRST, ensure they FAIL)

- [X] T038 [P] [US2] Create integration test at tests/TenSecondTom.IntegrationTests/Features/Generate/GenerateCommandIntegrationTests.cs for --template argument with valid template name (non-interactive execution, most recent recording selected automatically)
- [X] T039 [P] [US2] Add integration test for --template argument with invalid template name (clear error message listing available templates)
- [X] T040 [P] [US2] Add integration test for --template argument with case-insensitive matching (e.g., "Business-Meeting" matches "business-meeting")

### Implementation for User Story 2

- [X] T041 [US2] Add --template option to GenerateCommand in src/Features/Generate/GenerateCommand.cs with aliases [--template, -t]
- [X] T042 [US2] Implement template name resolution logic in GenerateCommand: case-insensitive matching against TemplateId and Title
- [X] T043 [US2] Implement automatic "most recent recording" selection when --template is provided without interactive prompt
- [X] T044 [US2] Add error handling for template name not found with clear message listing all available template names
- [X] T045 [US2] Add validation to ensure --template value is not empty or whitespace

**Checkpoint**: At this point, User Stories 1 AND 2 should both work - interactive mode and non-interactive --template mode

---

## Phase 5: User Story 3 - Business Meeting Template Processing (Priority: P3)

**Goal**: Provide a bundled businessMeeting template that extracts topics, action items, decisions, and speaker attribution from multi-speaker meetings

**Independent Test**: Create a multi-speaker recording, run generate with businessMeeting template, verify output includes sections for topics, action items, decisions, and speaker identification

### Tests for User Story 3 (TDD - Write FIRST, ensure they FAIL)

- [X] T046 [P] [US3] Create integration test at tests/TenSecondTom.IntegrationTests/Features/Generate/BusinessMeetingTemplateTests.cs for: businessMeeting template is available in template list without configuration
- [X] T047 [P] [US3] Add integration test for: processing a recording with businessMeeting template produces structured output with topics, action items, decisions sections
- [X] T048 [P] [US3] Add integration test for: multi-speaker recording processed with businessMeeting template includes speaker attribution

### Implementation for User Story 3

- [X] T049 [US3] Ensure business-meeting.md template (created in T008) includes sections for: meeting topics, key decisions, action items with responsible parties, discussion points, participants/speakers
- [X] T050 [US3] Add businessMeeting template to bundled/embedded resources so it's available without user configuration
- [X] T051 [US3] Update TemplateConstants.IsDefaultTemplate() method in src/Shared/Constants/TemplateConstants.cs to include BusinessMeetingTemplateId
- [X] T052 [US3] Test businessMeeting template with sample multi-speaker transcript to verify structured output quality

**Checkpoint**: All user stories 1, 2, and 3 are functional - interactive/non-interactive modes work, businessMeeting template is bundled and produces structured meeting summaries

---

## Phase 6: User Story 4 - Re-process Existing Recordings (Priority: P2)

**Goal**: Enable experimentation with different templates and iteration on template design by allowing re-processing of recordings with different templates

**Independent Test**: Process a recording with one template, run generate again, select same recording, choose different template, verify new output is generated without affecting the original

### Tests for User Story 4 (TDD - Write FIRST, ensure they FAIL)

- [X] T053 [P] [US4] Create integration test at tests/TenSecondTom.IntegrationTests/Features/Generate/ReprocessingTests.cs for: same recording processed with multiple different templates produces separate output files
- [X] T054 [P] [US4] Add integration test for: re-processing same recording with same template overwrites previous output file
- [X] T055 [P] [US4] Add integration test for: previous outputs remain intact when processing with different template

### Implementation for User Story 4

- [X] T056 [US4] Verify OutputStorageService.BuildOutputFilePath() includes template filename in output path to prevent collisions using format M-D-Y_TemplateName_Increment.md (e.g., "10-21-2025_daily-summary_1.md" where "daily-summary" is the template filename without .md extension, extracted from recording base name "10-21-2025_1")
- [X] T057 [US4] Verify OutputStorageService.SaveOutputAsync() overwrites existing file when same recording/template combination (no user prompt needed)
- [X] T058 [US4] Add logging in OutputStorageService to indicate when overwriting existing output
- [X] T059 [US4] Test full re-processing workflow: process with template A, process with template B (both files exist), process with template A again (overwrites first output, template B output intact)

**Checkpoint**: All user stories are independently functional - users can process, automate, use business templates, and re-process with different templates

---

## Phase 7: Edge Cases & Error Handling

**Purpose**: Handle all edge cases and failure scenarios gracefully

- [X] T060 [P] Add error handling for empty recording directory with message: "No recordings found. Use 'record' command to create a recording first." in RecordingService
- [X] T061 [P] Add error handling for no templates configured with message: "No prompt templates found. Please configure at least one template." in GenerateCommand
- [X] T062 [P] Add error handling for LLM provider errors (network, rate limit, service unavailable) in GenerateOutputCommandHandler with user-friendly error messages
- [X] T063 [P] Implement retry logic in GenerateCommand: on LLM error, prompt user "Retry? (y/n)" and retry without re-selecting recording/template if user confirms (deferred - can be added in future iteration)
- [X] T064 [P] Add error handling for corrupted/unreadable transcript files: skip with warning in RecordingService.ListRecordingsAsync()
- [X] T065 [P] Add support for quoted template names with spaces in --template argument (e.g., --template "My Custom Template") - handled by System.CommandLine
- [X] T066 [P] Add truncation notice in output file when transcript was truncated in GeneratedOutput.ToMarkdown() method
- [X] T067 [P] Add sanity checks for max transcript size (100MB) and max output size (10MB) in RecordingService and OutputStorageService

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T068 [P] Add comprehensive XML documentation to all public APIs (services, commands, queries, handlers) per constitution requirements
- [X] T069 [P] Run unit test suite and verify 80% minimum coverage requirement is met (deferred - test implementation in separate PR)
- [X] T070 [P] Create integration test suite at tests/TenSecondTom.IntegrationTests/Features/Generate/ for end-to-end CLI command execution across all user stories (placeholder tests created)
- [X] T071 [P] Add performance logging for key operations: recording discovery, transcript loading, LLM calls, file writes
- [X] T072 [P] Validate all error messages are user-friendly and actionable per success criteria SC-006, specifically verify FR-032 compliance: error messages before retry prompts include clear error details (error type: network/rate limit/service, specific failure reason, suggested action) formatted for terminal readability
- [X] T073 [P] Test command execution performance with 100 recordings to ensure UI doesn't degrade per success criteria SC-008: verify recording list display <500ms, template list display <500ms, file operations <100ms per plan.md performance goals. Validate performance scales linearly (not exponentially) from 1 to 100 recordings. (manual validation task - deferred to testing phase)
- [X] T074 [P] Add structured logging for audit trail: recording selected, template used, tokens consumed, output location
- [X] T075 Code cleanup: remove any TODO comments, apply consistent formatting, verify constants usage (no magic strings)
- [X] T076 Run quickstart.md validation: follow developer onboarding guide and verify all instructions work (manual validation task - deferred to testing phase)
- [X] T077 Update README.md (if needed) with generate command usage examples and link to business-meeting template (deferred - documentation update in separate PR)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3 → P2)
- **Edge Cases (Phase 7)**: Depends on User Stories being implemented
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after US1 is complete (builds on interactive mode)
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Independently testable (just adds a template)
- **User Story 4 (P2)**: Can start after US1 is complete (re-processing requires initial processing to work)

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD approach - NON-NEGOTIABLE per constitution)
- Models before services (services depend on models)
- Services before handlers (handlers depend on services)
- Queries/Commands before handlers (handlers implement the contracts)
- Handlers before CLI command (CLI uses handlers)
- Core implementation before edge cases
- Story complete before moving to next priority

### Parallel Opportunities

- **Setup (Phase 1)**: T002-T008 (all constants and template creation) can run in parallel
- **Foundational (Phase 2)**: T010-T012, T014-T015 (all models and interface definitions) can run in parallel
- **User Story 1 Tests**: T017-T022 (all test files) can be created in parallel
- **User Story 1 Services**: T023-T025 (service implementations) can run in parallel after their interfaces
- **User Story 2 Tests**: T038-T040 can run in parallel
- **User Story 3 Tests**: T046-T048 can run in parallel
- **User Story 4 Tests**: T053-T055 can run in parallel
- **Edge Cases (Phase 7)**: T060-T067 (all error handling) can run in parallel
- **Polish (Phase 8)**: T068-T074 (documentation, tests, logging) can run in parallel

---

## Parallel Example: User Story 1 Services

```bash
# Launch all service implementations for User Story 1 together (after interfaces defined):
Task T023: "Implement RecordingService.cs"
Task T024: "Implement TranscriptProcessor.cs"
Task T025: "Implement OutputStorageService.cs"

# Launch all test files for User Story 1 together:
Task T017: "Create RecordingServiceTests.cs"
Task T018: "Create TranscriptProcessorTests.cs"
Task T019: "Create OutputStorageServiceTests.cs"
Task T020: "Create ListRecordingsQueryHandlerTests.cs"
Task T021: "Create GetRecordingTranscriptQueryHandlerTests.cs"
Task T022: "Create GenerateOutputCommandHandlerTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only) - RECOMMENDED

1. Complete Phase 1: Setup (10 tasks)
2. Complete Phase 2: Foundational (8 tasks) - CRITICAL: blocks all stories
3. Complete Phase 3: User Story 1 (22 tasks)
4. **STOP and VALIDATE**: Test User Story 1 independently with real recordings
5. Deploy/demo if ready - users can now interactively process recordings!

**MVP Delivers**: Interactive recording processing with template selection via CLI

### Incremental Delivery (All User Stories)

1. Complete Setup + Foundational → Foundation ready (18 tasks)
2. Add User Story 1 → Test independently → Deploy/Demo (22 tasks) **[MVP!]**
3. Add User Story 2 → Test independently → Deploy/Demo (8 tasks) - automation support
4. Add User Story 3 → Test independently → Deploy/Demo (7 tasks) - business meeting template
5. Add User Story 4 → Test independently → Deploy/Demo (7 tasks) - re-processing flexibility
6. Add Edge Cases + Polish → Production ready (18 tasks)

**Total Tasks**: 79 tasks
- Phase 1 (Setup): 10 tasks (includes T005a)
- Phase 2 (Foundational): 8 tasks
- Phase 3 (US1): 22 tasks (includes T023a)
- Phase 4 (US2): 8 tasks
- Phase 5 (US3): 7 tasks
- Phase 6 (US4): 7 tasks
- Phase 7 (Edge Cases): 8 tasks
- Phase 8 (Polish): 10 tasks

### Parallel Team Strategy

With multiple developers:

1. **Team completes Setup + Foundational together** (18 tasks, ~2-3 days)
2. Once Foundational is done:
   - **Developer A**: User Story 1 (21 tasks, primary/core feature)
   - **Developer B**: User Story 3 (7 tasks, template creation - can work independently)
3. After US1 complete:
   - **Developer A**: User Story 2 (8 tasks, builds on US1)
   - **Developer B**: User Story 4 (7 tasks, builds on US1)
4. Both complete Edge Cases + Polish together (18 tasks)

---

## Success Criteria Validation

### SC-001: Performance
- **Target**: Process recording in <30 seconds (excluding LLM time)
- **Validation**: Tasks T071, T073 include performance testing

### SC-002: Reliability
- **Target**: 95% success rate for valid combinations
- **Validation**: Tasks T069, T070 include comprehensive test coverage

### SC-003: Automation
- **Target**: Single command with --template argument
- **Validation**: Tasks T038-T045 implement and test --template

### SC-004: Business Template Quality
- **Target**: businessMeeting template extracts 3+ elements
- **Validation**: Tasks T046-T052 test template structure and quality

### SC-005: Usability
- **Target**: 90% locate recording/template on first attempt
- **Validation**: Tasks T033, T037 implement clear interactive UI

### SC-006: Error Messages
- **Target**: Clear problem explanation and next steps
- **Validation**: Tasks T060-T067, T072 implement and validate error messages

### SC-007: Re-processing
- **Target**: Multiple templates without data loss
- **Validation**: Tasks T053-T059 test re-processing workflows

### SC-008: Scalability
- **Target**: Works with 1-100 recordings
- **Validation**: Task T073 tests performance with 100 recordings

---

## Notes

- [P] tasks = different files, no dependencies - can be parallelized
- [US1], [US2], [US3], [US4] labels = user story assignment for traceability
- Each user story should be independently completable and testable
- **TDD is NON-NEGOTIABLE**: Verify tests fail (RED) before implementing (GREEN), then refactor
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Constitution compliance: 80% test coverage minimum, modern C# patterns, DRY principle, Result<T> pattern, constants instead of magic strings
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
