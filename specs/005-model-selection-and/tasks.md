# Tasks: Model Selection and Configuration

**Input**: Design documents from `/specs/005-model-selection-and/`
**Prerequisites**: plan.md, spec.md, data-model.md, research.md, contracts/

**Tests**: Tests are included as this is a TDD-required feature per constitutional requirements.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

## Path Conventions
- Single project at repository root
- `src/` for source code
- `tests/` for all test projects

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create model-related infrastructure shared by all user stories

- [X] T001 [P] Create `src/Shared/Constants/LlmConstants.cs` with cost tier constants and model identifiers
- [X] T002 [P] Create `src/Features/Setup/Models/SupportedModel.cs` record with Id, DisplayName, Provider, CostTier, Description, IsDefault properties

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model registry and validation infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Create `src/Features/Setup/Models/ModelRegistry.cs` static class with OpenAIModels, AnthropicModels collections
- [X] T004 Populate `ModelRegistry` with 3-4 OpenAI models (gpt-4o, gpt-4o-mini, gpt-3.5-turbo) with metadata
- [X] T005 Populate `ModelRegistry` with 3-4 Anthropic models (claude-3-5-sonnet, claude-3-5-haiku, claude-3-opus) with metadata
- [X] T006 Add `GetDefault(LlmProvider)` method to `ModelRegistry` returning default model per provider
- [X] T007 Add `IsValid(string modelId, LlmProvider provider)` method to `ModelRegistry` for validation
- [X] T008 Add `GetById(string modelId)` method to `ModelRegistry` returning SupportedModel or null
- [X] T009 Add `GetByProvider(LlmProvider provider)` method to `ModelRegistry` returning filtered model list
- [X] T010 Create `src/Features/Setup/Validation/ModelValidator.cs` class using ModelRegistry for validation

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Model Selection During Guided Setup (Priority: P1) 🎯 MVP

**Goal**: Users can select a curated model during guided setup wizard after choosing their LLM provider

**Independent Test**: Run `tom setup`, select provider, choose model from curated list, verify model is saved and used in subsequent AI operations

### Tests for User Story 1

**NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T011 [P] [US1] Unit test for `SupportedModel` record validation in `tests/TenSecondTom.Tests/Unit/Features/Setup/Models/SupportedModelTests.cs`
- [X] T012 [P] [US1] Unit tests for `ModelRegistry` static methods in `tests/TenSecondTom.Tests/Unit/Features/Setup/Models/ModelRegistryTests.cs`
- [X] T013 [P] [US1] Unit tests for `ModelValidator` validation logic in `tests/TenSecondTom.Tests/Unit/Features/Setup/Validation/ModelValidatorTests.cs`
- [X] T014 [P] [US1] Unit tests for `PromptForModelAsync` method in `tests/TenSecondTom.Tests/Unit/Features/Setup/Handlers/SpectreConsoleSetupWizardTests.cs`
- [X] T015 [US1] Integration test for end-to-end setup with model selection in `tests/TenSecondTom.IntegrationTests/Integration/Features/Setup/ModelSelectionFlowTests.cs`

### Implementation for User Story 1

- [X] T016 [US1] Add `PromptForModelAsync(LlmProvider, string? currentModelId, CancellationToken)` method to `src/Features/Setup/Handlers/SpectreConsoleSetupWizard.cs`
- [X] T017 [US1] Implement Spectre.Console SelectionPrompt for model selection with cost tier and description display in `SpectreConsoleSetupWizard.PromptForModelAsync`
- [X] T018 [US1] Integrate model selection into Step 3 (LLM Configuration) in `SpectreConsoleSetupWizard.RunAsync` after provider selection
- [X] T019 [US1] Update `SetupCommandHandler.Handle` to ensure model is passed to `UserSecretsStorageService.SaveAsync` in `src/Features/Setup/Handlers/SetupCommandHandler.cs`
- [X] T020 [US1] Verify `src/Infrastructure/Configuration/UserSecretsStorageService.cs` correctly saves Llm.Model to user secrets
- [X] T021 [US1] Verify `src/Infrastructure/Configuration/ConfigurationSettings.cs` LlmConfiguration.Model property binds from configuration hierarchy
- [X] T022 [US1] Add model validation to `src/Features/Setup/Validation/ConfigCommandValidator.cs` using ModelValidator
- [X] T023 [US1] Update `src/Infrastructure/Llm/LlmProviderFactory.cs` to pass configured model to provider constructors
- [X] T024 [US1] Update `src/Infrastructure/Llm/OpenAILlmProvider.cs` constructor to accept and use model parameter
- [X] T025 [US1] Update `src/Infrastructure/Llm/AnthropicLlmProvider.cs` constructor to accept and use model parameter
- [X] T026 [US1] Add default model fallback logic in `LlmProviderFactory.Create` using `ModelRegistry.GetDefault` when model is null/empty
- [X] T027 [US1] Add startup validation in `Program.cs` to validate configured model against ModelRegistry and fail with clear error if invalid

**Checkpoint**: At this point, User Story 1 should be fully functional - users can select models during setup and use them in AI operations

---

## Phase 4: User Story 2 - Model Configuration via Config Command (Priority: P2)

**Goal**: Users can change their model selection via `tom config llm` command without re-running full setup

**Independent Test**: Run `tom config llm`, select provider and model, verify configuration is updated and subsequent AI operations use the new model

### Tests for User Story 2

- [X] T028 [P] [US2] Unit tests for ConfigCommand llm action handling in `tests/TenSecondTom.Tests/Unit/Features/Setup/Handlers/ConfigCommandHandlerTests.cs`
- [X] T029 [US2] Integration test for end-to-end `tom config llm` command flow in `tests/TenSecondTom.IntegrationTests/Integration/Features/Setup/ConfigLlmCommandTests.cs`

### Implementation for User Story 2

- [X] T030 [US2] Extend `src/Features/Setup/Commands/ConfigCommand.cs` to support "llm" as SettingName for interactive model selection
- [X] T031 [US2] Extend `src/Features/Setup/Handlers/ConfigCommandHandler.cs` to handle SettingName == "llm" action
- [X] T032 [US2] Implement interactive provider selection in ConfigCommandHandler when SettingName is "llm" (reuse SpectreConsoleSetupWizard.PromptForLlmProviderAsync)
- [X] T033 [US2] Implement interactive model selection in ConfigCommandHandler after provider selection (reuse SpectreConsoleSetupWizard.PromptForModelAsync)
- [X] T034 [US2] Update model configuration in user secrets via UserSecretsStorageService in ConfigCommandHandler
- [X] T035 [US2] Display success message with selected provider and model after update
- [X] T036 [US2] Add current model highlighting in SelectionPrompt when model is already configured
- [X] T037 [US2] Add CLI subcommand `tom config llm` in `src/Infrastructure/Cli/CommandRegistry.cs` for easy access

**Checkpoint**: At this point, User Stories 1 AND 2 should both work - users can configure models via setup or config command

**Bugfixes Applied (2025-10-13)**:
- **Model Selection Parsing Bug**: Fixed `PromptForModelAsync` in `SpectreConsoleSetupWizard.cs` where model names containing parentheses (e.g., "Claude Sonnet 4.5 (2025-09-29)") were incorrectly parsed, causing selection to fail and return null. Solution: Replaced string parsing with dictionary-based mapping of formatted choices to model objects for robust, O(1) lookup.
- **Spectre.Console Markup Injection**: Fixed `ShowSuccess`, `ShowError`, and `ShowWarning` methods in `SpectreConsoleSetupWizard.cs` to escape user-provided content using `.EscapeMarkup()` to prevent API keys or model names with special characters from being interpreted as markup codes, which caused `InvalidOperationException` errors.
- **CommandRegistry Error Display**: Fixed all error message displays in `CommandRegistry.cs` (lines 448, 507, 566, 615, 661) to escape `result.Error` content before displaying. This prevents validation error messages (e.g., "Expected format: [32+ characters]") from being interpreted as Spectre.Console markup, which caused crashes when displaying API key validation failures.
- **Anthropic API Key Validation Regex**: Fixed `AnthropicApiKeyValidator.cs` regex pattern to accept underscores in API keys. Changed from `[a-zA-Z0-9\-]{32,}` to `[a-zA-Z0-9\-_]{32,}`. Real Anthropic keys contain underscores in their format, and the overly restrictive regex was rejecting valid keys. Updated tests to verify underscores are accepted.

---

## Phase 5: User Story 3 - Model Configuration via Environment Variables (Priority: P3)

**Goal**: Advanced users and CI/CD can configure model via environment variables with validation

**Independent Test**: Set `TenSecondTom__Llm__Model` environment variable, run application, verify the specified model is used

### Tests for User Story 3

- [X] T037 [P] [US3] Unit tests for environment variable model configuration in `tests/TenSecondTom.Tests/Unit/Infrastructure/Configuration/ConfigurationSettingsTests.cs`
- [X] T038 [US3] Integration test for environment variable precedence over user secrets in `tests/TenSecondTom.IntegrationTests/Integration/Features/Setup/EnvironmentVariableConfigTests.cs`

### Implementation for User Story 3

- [X] T039 [US3] Verify environment variable model is validated at startup by existing validation logic from US1 (no code changes needed)
- [X] T040 [US3] Add clear error message when invalid model is set via environment variable, suggesting valid options for current provider
- [X] T041 [US3] Update documentation in `docs/CONFIGURATION.md` with environment variable format and examples

**Checkpoint**: All three configuration methods now work - setup wizard, config command, and environment variables

---

## Phase 6: User Story 4 - Model List Documentation (Priority: P3)

**Goal**: Users can view complete list of supported models with descriptions via `tom config llm` interactive prompt

**Independent Test**: Run `tom config llm`, verify provider selection and curated model list displays with cost tiers and descriptions

### Tests for User Story 4

- [ ] T042 [P] [US4] Unit tests for model display formatting in `tests/TenSecondTom.Tests/Unit/Features/Setup/Models/ModelRegistryTests.cs`
- [ ] T043 [US4] Integration test verifying model list display includes all required metadata in `tests/TenSecondTom.IntegrationTests/Integration/Features/Setup/ConfigLlmCommandTests.cs`

### Implementation for User Story 4

- [ ] T044 [US4] Ensure SelectionPrompt converter in SpectreConsoleSetupWizard.PromptForModelAsync displays format: "DisplayName [CostTier] - Description"
- [ ] T045 [US4] Update `tom config show` command to display currently configured model in LLM section in `src/Features/Setup/Handlers/ConfigCommandHandler.cs`
- [ ] T046 [US4] Add model information to config show output format with provider, model name, and cost tier
- [ ] T047 [US4] Update README.md with model selection feature and available models documentation

**Checkpoint**: All user stories should now be independently functional with complete documentation

---

## Phase 7: Edge Cases & Error Handling

**Purpose**: Handle deprecated models, missing configuration, and provider/model mismatches

- [ ] T048 [P] Add unit tests for deprecated/invalid model detection in `tests/TenSecondTom.Tests/Unit/Infrastructure/Llm/LlmProviderFactoryTests.cs`
- [ ] T049 [P] Add unit tests for missing model configuration default fallback in `tests/TenSecondTom.Tests/Unit/Infrastructure/Llm/LlmProviderFactoryTests.cs`
- [ ] T050 [P] Add unit tests for provider/model mismatch detection in `tests/TenSecondTom.Tests/Unit/Features/Setup/Validation/ModelValidatorTests.cs`
- [ ] T051 Implement outdated model detection in LlmProviderFactory.Create with warning log and error message listing valid options
- [ ] T052 Implement provider/model mismatch detection at startup with actionable error directing user to run `tom config llm`
- [ ] T053 Add XML documentation comments to all new public APIs (SupportedModel, ModelRegistry, ModelValidator, new methods)
- [ ] T054 Update appsettings.json with commented-out Llm.Model example and security warning about using environment variables instead

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T055 [P] Update `docs/CONFIGURATION.md` with complete model configuration documentation
- [ ] T056 [P] Update `README.md` with model selection feature overview and quick start
- [ ] T057 [P] Add logging throughout model selection and validation flow using Serilog with structured context
- [ ] T058 Code cleanup and refactoring of model selection code for consistency
- [ ] T059 Run coverage analysis to ensure 80%+ coverage for all new code
- [ ] T060 Run quickstart.md validation scenarios from `specs/005-model-selection-and/quickstart.md`
- [ ] T061 Manual testing of all four user stories end-to-end per quickstart.md test scenarios
- [ ] T062 Update `specs/005-model-selection-and/COMPLETION.md` with implementation notes and deployment readiness

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-6)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (P1 → P2 → P3 → P3)
- **Edge Cases (Phase 7)**: Can proceed in parallel with later user stories or after all stories complete
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Reuses components from US1 (SpectreConsoleSetupWizard methods) but independently testable
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Leverages validation from US1, independently testable
- **User Story 4 (P3)**: Can start after Foundational (Phase 2) - Enhances US2 display, independently testable

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD requirement)
- Models and validation before handlers
- Handlers before integration
- Core implementation before polish
- Story complete before moving to next priority

### Parallel Opportunities

- Phase 1: T001 and T002 can run in parallel (different files)
- Phase 2: T004 and T005 can run in parallel after T003 (populating different collections)
- User Story 1 Tests: T011, T012, T013, T014 can run in parallel (different test files)
- User Story 1 Implementation: T024 and T025 can run in parallel (different provider files)
- User Story 2 Tests: T028 and T029 can run in parallel (different test files)
- User Story 3 Tests: T037 and T038 can run in parallel (different test files)
- User Story 4 Tests: T042 and T043 can run in parallel (different test files)
- Phase 7: T048, T049, T050 can run in parallel (different test files)
- Phase 8: T055, T056, T057 can run in parallel (different documentation/logging files)

---

## Parallel Example: User Story 1 Tests

```bash
# Launch all unit tests for User Story 1 together:
Task T011: "Unit test for SupportedModel record validation"
Task T012: "Unit tests for ModelRegistry static methods"
Task T013: "Unit tests for ModelValidator validation logic"
Task T014: "Unit tests for PromptForModelAsync method"
# Then:
Task T015: "Integration test for end-to-end setup with model selection"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational (T003-T010) - CRITICAL
3. Complete Phase 3: User Story 1 (T011-T027)
4. **STOP and VALIDATE**: Test User Story 1 independently per quickstart.md
5. Deploy/demo if ready - users can now select models during setup

### Incremental Delivery

1. Complete Setup + Foundational (T001-T010) → Foundation ready
2. Add User Story 1 (T011-T027) → Test independently → Deploy/Demo (MVP!)
   - **Value**: Fixes current bug where models aren't configured during setup
3. Add User Story 2 (T028-T036) → Test independently → Deploy/Demo
   - **Value**: Users can change models without re-running setup
4. Add User Story 3 (T037-T041) → Test independently → Deploy/Demo
   - **Value**: Advanced users and CI/CD can use environment variables
5. Add User Story 4 (T042-T047) → Test independently → Deploy/Demo
   - **Value**: Better discoverability and documentation
6. Add Edge Cases (T048-T054) → Test independently → Deploy/Demo
   - **Value**: Robust error handling for deprecated models and misconfigurations
7. Polish (T055-T062) → Final validation and documentation

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (T001-T010)
2. Once Foundational is done:
   - Developer A: User Story 1 (T011-T027)
   - Developer B: User Story 2 (T028-T036) - can start after US1 T016-T017 complete (needs PromptForModelAsync)
   - Developer C: User Story 3 (T037-T041) - can start after US1 validation complete
   - Developer D: User Story 4 (T042-T047) - can start after US2 complete
3. All stories integrate independently

---

## Summary Statistics

- **Total Tasks**: 62
- **Setup Phase**: 2 tasks
- **Foundational Phase**: 8 tasks (BLOCKS all stories)
- **User Story 1 (P1)**: 17 tasks (5 tests, 12 implementation)
- **User Story 2 (P2)**: 9 tasks (2 tests, 7 implementation)
- **User Story 3 (P3)**: 5 tasks (2 tests, 3 implementation)
- **User Story 4 (P3)**: 6 tasks (2 tests, 4 implementation)
- **Edge Cases**: 7 tasks (3 tests, 4 implementation)
- **Polish**: 8 tasks (documentation and validation)

**Parallelization Potential**:
- 15 tasks explicitly marked [P] for parallel execution
- All 4 user stories can be worked on in parallel after Foundational phase (if team capacity allows)
- Estimated 25-30% time savings with parallel execution

**Test Coverage**:
- 14 test tasks ensuring 80%+ coverage
- TDD approach with tests before implementation
- Unit, integration, and end-to-end test coverage

**Independent Delivery**:
- Each user story is independently testable
- MVP can be delivered with just User Story 1
- Incremental value delivery with each story completion

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (TDD requirement)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- XML documentation required for all public APIs (constitutional requirement)
- 80% test coverage minimum (constitutional requirement)
- Serilog used for all logging (organizational standard)
