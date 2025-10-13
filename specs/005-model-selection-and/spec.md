# Feature Specification: Model Selection and Configuration

**Feature Branch**: `005-model-selection-and`  
**Created**: 2025-10-11  
**Status**: Draft  
**Input**: User description: "Model selection and configuration with curated cost-effective options from Anthropic and OpenAI"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Model Selection During Guided Setup (Priority: P1)

A user runs the guided setup wizard and is presented with a curated list of recommended, cost-effective models from their chosen provider (OpenAI or Anthropic). They select their preferred model, which is saved to their configuration and used for all subsequent AI operations.

**Why this priority**: This is the primary way users configure the application after Homebrew installation. Without proper model configuration during setup, the application cannot function correctly, as evidenced by the current bug where models aren't being set properly.

**Independent Test**: Can be fully tested by running `tom setup`, selecting a provider, choosing a model from the curated list, and verifying the model is saved and used in subsequent AI operations.

**Acceptance Scenarios**:

1. **Given** user is in the guided setup on Step 3 (LLM provider configuration), **When** they select OpenAI as their provider, **Then** they are presented with 3-4 curated OpenAI model options with cost and capability descriptions
2. **Given** user is viewing model options during setup, **When** they select a model from the list, **Then** that model is saved to their user secrets configuration and used by default for all AI operations
3. **Given** user completes setup with a selected model, **When** they run `tom config show`, **Then** the selected model is displayed in the configuration output
4. **Given** user completes setup with a selected model, **When** they use any AI feature (today, thisweek, search), **Then** the application uses the configured model without errors

---

### User Story 2 - Model Configuration via Config Command (Priority: P2)

A user who has already completed setup wants to change their model selection without re-running the entire setup wizard. They use the `tom config llm` command which prompts them to select their provider, then presents models for that provider to choose from.

**Why this priority**: Users need flexibility to adjust their model choice based on cost, performance, or availability without redoing full setup. This addresses the "support setting the model via the /config set command" requirement.

**Independent Test**: Can be fully tested by running `tom config llm`, selecting a provider and model, verifying the configuration is updated, and confirming subsequent AI operations use the new model.

**Acceptance Scenarios**:

1. **Given** user has a configured application, **When** they run `tom config llm`, **Then** they are prompted to select a provider (OpenAI or Anthropic), then shown model options for that provider
2. **Given** user selects a provider in config llm command, **When** they choose a model from the list, **Then** the model configuration is updated in user secrets and confirmed with a success message
3. **Given** user changes their model via config llm command, **When** they run `tom today` or another AI feature, **Then** the new model is used for the AI operation
4. **Given** user runs `tom config llm`, **When** they view the model selection, **Then** the currently configured model (if any) is indicated/highlighted in the list

---

### User Story 3 - Model Configuration via Environment Variables (Priority: P3)

An advanced user or CI/CD system configures the model using environment variables instead of the guided setup, selecting from the same curated list of cost-effective models.

**Why this priority**: Supports advanced users and automated deployments, but most users will use the guided setup or config command. This ensures consistency across all configuration methods.

**Independent Test**: Can be fully tested by setting `TenSecondTom__Llm__Model` environment variable, running the application, and verifying the specified model is used.

**Acceptance Scenarios**:

1. **Given** user sets `TenSecondTom__Llm__Model=gpt-4o-mini` environment variable, **When** they run `tom today`, **Then** the application uses the gpt-4o-mini model
2. **Given** user sets an invalid model via environment variable, **When** they run any AI command, **Then** they receive a clear error message indicating the model is not supported, with a list of valid options
3. **Given** user has both user secrets and environment variable model configuration, **When** they run an AI command, **Then** the environment variable takes precedence (standard .NET configuration hierarchy)

---

### User Story 4 - Model List Validation and Documentation (Priority: P3)

Users can view and select from the complete list of supported models using the interactive `tom config llm` command, which first asks for provider selection then shows models with pricing tier and capability information, helping them make informed choices.

**Why this priority**: Provides transparency and helps users understand their options, but doesn't block core functionality.

**Independent Test**: Can be fully tested by running `tom config llm` and verifying the provider selection and curated model list is displayed with descriptions.

**Acceptance Scenarios**:

1. **Given** user runs `tom config llm`, **When** they select OpenAI, **Then** they see only OpenAI models with cost tier and capability info
2. **Given** user runs `tom config llm`, **When** they select Anthropic, **Then** they see only Anthropic models with cost tier and capability info
3. **Given** user views model options in `tom config llm`, **When** they review each model, **Then** they can understand the cost/performance tradeoffs to make an informed choice

---

### Edge Cases

- What happens when a user has an outdated model name in their configuration (e.g., a model that has been deprecated)?
  - System should detect the invalid model, log a warning, provide a clear error message with current valid options, and suggest running `tom config llm` to update
- What happens when the model configuration is missing entirely (neither in user secrets, appsettings, nor environment variables)?
  - System should provide a default model from the curated list for the configured provider (gpt-4o-mini for OpenAI, claude-3-5-haiku for Anthropic) and log an informational message about using the default
- What happens when a user switches providers (OpenAI to Anthropic) but keeps the old model value?
  - System should detect provider/model mismatch at startup and prompt user to run `tom config llm` to select a compatible model
- What happens when the curated model list needs updating (new models released, old ones deprecated)?
  - Model list should be maintained as a versioned constant/configuration that can be updated without breaking existing user configurations; system should validate user's saved model against current list
- What happens when a user manually edits their configuration file to an invalid state?
  - System should validate configuration at startup, detect invalid model, and provide actionable error message directing them to use `tom config llm` or `tom setup`

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST maintain a curated list of 3-4 cost-effective models for OpenAI including model identifier, display name, cost tier (e.g., "Budget", "Balanced", "Premium"), and capability description
- **FR-002**: System MUST maintain a curated list of 3-4 cost-effective models for Anthropic including model identifier, display name, cost tier, and capability description
- **FR-003**: Guided setup wizard MUST present model selection as a step after provider selection, showing only models for the chosen provider
- **FR-004**: Model selection during setup MUST display each model's name, cost tier, and brief capability description to help users make informed choices
- **FR-005**: System MUST save the selected model to user secrets (or appropriate configuration storage) in a format consistent with .NET configuration hierarchy
- **FR-006**: System MUST support reading model configuration from user secrets, appsettings.json, and environment variables with proper precedence (env vars > user secrets > appsettings)
- **FR-007**: System MUST provide a `tom config llm` command that interactively prompts user to select provider first, then model from the curated list for that provider
- **FR-008**: When setting model via config llm command, system MUST validate the selected model identifier against the curated list for the chosen provider
- **FR-009**: System MUST provide clear error messages when an invalid model is specified, including the list of valid options for the current provider
- **FR-010**: System MUST read the configured model value from the unified configuration location and pass it to the LLM provider factories during initialization
- **FR-011**: System MUST fall back to a sensible default model if none is explicitly configured (gpt-4o-mini for OpenAI, claude-3-5-haiku for Anthropic)
- **FR-012**: System MUST validate model configuration at application startup and fail with clear error message if model is invalid
- **FR-013**: System MUST display currently configured model in `tom config show` output
- **FR-014**: The `tom config llm` command MUST display all available models for the user-selected provider with cost tier and capability descriptions
- **FR-015**: Model configuration MUST work consistently whether user configured via guided setup, config llm command, or environment variables

### Key Entities

- **SupportedModel**: Represents a model in the curated list with properties: identifier (string, e.g., "gpt-4o-mini"), display name (string, e.g., "GPT-4o Mini"), provider (OpenAI or Anthropic), cost tier (Budget/Balanced/Premium), capability description (string, brief explanation of use case), and is_default (boolean)

- **ModelConfiguration**: Represents the user's model selection with properties: provider (OpenAI or Anthropic), model identifier (string), configuration source (UserSecrets/AppSettings/EnvironmentVariable), is_valid (boolean, validated against curated list)

## Clarifications

### Session 2025-10-11

- Q: What is the exact command syntax for displaying available models? → A: `tom config llm` - dedicated subcommand where user selects provider first, then model; same pattern used in guided setup

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users completing guided setup successfully configure a model 100% of the time (currently failing with model not set error)
- **SC-002**: Users can change their model configuration in under 30 seconds using the `tom config llm` command
- **SC-003**: Model configuration is read correctly from any of the three sources (user secrets, appsettings, environment variables) in 100% of cases
- **SC-004**: Users receive clear, actionable error messages within 2 seconds when attempting to use an invalid model identifier
- **SC-005**: 95% of users select from the curated list of cost-effective models, reducing unexpected API costs
- **SC-006**: Zero configuration synchronization issues between setup method (.env vs user secrets) and model selection
- **SC-007**: Documentation and in-app help display current model options in under 1 second
- **SC-008**: Switching models via config command works without requiring application restart or re-authentication

## Assumptions

- Users typically prefer cost-effective models over the most expensive options unless they have specific advanced requirements
- The curated model list should focus on currently available, production-ready models (not preview/experimental versions)
- OpenAI's recommended cost-effective models as of 2025: gpt-4o, gpt-4o-mini, gpt-3.5-turbo
- Anthropic's recommended cost-effective models as of 2025: claude-3-5-sonnet, claude-3-5-haiku, claude-3-opus
- Model identifiers from providers will remain stable enough that storing them in configuration is safe
- Users understand basic concepts of "Budget", "Balanced", and "Premium" cost tiers without detailed pricing information
- Most users (80%+) will use guided setup for initial configuration, with config command and environment variables as secondary methods
- The current bug is likely due to model configuration not being read from the correct location or not being set at all during setup

## Research Notes

Based on current Anthropic and OpenAI documentation (as of late 2024/early 2025):

**OpenAI Recommended Models:**
1. **gpt-4o** (Balanced) - Latest optimized GPT-4 model, good balance of cost and performance
2. **gpt-4o-mini** (Budget) - Smaller, faster, more cost-effective for simple tasks
3. **gpt-3.5-turbo** (Budget) - Lowest cost option, suitable for basic AI features

**Anthropic Recommended Models:**
1. **claude-3-5-sonnet-20241022** (Balanced) - Latest Sonnet, good all-around performance
2. **claude-3-5-haiku-20241022** (Budget) - Fast and economical for straightforward tasks
3. **claude-3-opus-20240229** (Premium) - Most capable but highest cost

**Default Recommendations:**
- OpenAI default: gpt-4o-mini (best cost/performance for most users)
- Anthropic default: claude-3-5-haiku (fast and economical)

## Dependencies

- Existing configuration infrastructure (UserSecretsStorageService, ConfigurationSettings models)
- Existing setup wizard (SpectreConsoleSetupWizard)
- Existing ConfigCommand and ConfigCommandHandler
- LlmProviderFactory and provider implementations (OpenAILlmProvider, AnthropicLlmProvider)
- .NET configuration system integration

## Out of Scope

- Allowing users to specify arbitrary custom models not in the curated list (could be future enhancement with advanced mode)
- Real-time pricing information or cost estimation
- Model performance benchmarking or testing
- Automatic model recommendation based on user's usage patterns
- Support for additional providers beyond OpenAI and Anthropic
- Per-command model override (using different models for different features)
