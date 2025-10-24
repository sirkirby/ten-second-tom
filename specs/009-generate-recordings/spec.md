# Feature Specification: Generate Command for Recording Processing

**Feature Branch**: `001-generate-recordings`
**Created**: 2025-10-24
**Status**: Draft
**Input**: User description: "Now that we have recording via the record command, and we have the ability to define custom templates, we need a new command to process and re-process recordings based on those prompt templates. The new command will be `generate` and it browse and allow you to select a recording transcript from the stored recordings and transcripts from the record command in the recording directory, then ask which prompt template to us in order to generate the output based on. We will support any templateType, which are currently `daily` and `weekly` as defined in the template header and support any new template types that are defined. We will add new template types `businessMeeting` and along with a new bundled prompt template which specialized in summarizing a meeting with multiple speakers, and pulling out topics, action items, and and other information relevant to a business meeting. Right now, the template type is not relevant, as our new command can use any template type to generate the output, however we may have commands in the future that are specialized to that type, like `today` and `thisweek`. For reference to existing patterns, we support this flow in a similar way with the today command, where if multiple today type templates are detected, then we display them to the user, who selects the one they want and then its process against the configured LLM provider and model. We also need to support similar command arguments like --template 'template-name' so that we can one-shot the command."

## Clarifications

### Session 2025-10-24

- Q: When a recording is processed with a template, where should the generated output be stored? → A: Store in recording directory with filename format including the template name used (option B with template name distinction)
- Q: If a user processes the same recording with the same template multiple times (e.g., after editing the template), what should happen to the output file? → A: Overwrite previous output (option A)
- Q: When a recording transcript exceeds the LLM provider's token limit, how should the system handle it? → A: Warn and truncate intelligently at provider limits, with token limit being well understood and configurable (option B with configuration requirement)
- Q: When LLM processing fails (network error, rate limit, service unavailable), how should the system handle retry? → A: Offer manual retry without reselection (option B)
- Q: What file format should be used for storing generated outputs? → A: Markdown (.md) format (option A)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Process Single Recording Interactively (Priority: P1)

A user has completed a voice recording session and wants to generate a summary or structured output from that recording using an existing prompt template. They want to select the recording and template through an interactive menu interface.

**Why this priority**: This is the core value proposition of the feature - enabling users to process their recordings with custom templates. Without this, the feature provides no value.

**Independent Test**: Can be fully tested by creating a recording, running `generate` command, selecting the recording from the menu, selecting a template from the menu, and verifying the output is generated based on the template prompt.

**Acceptance Scenarios**:

1. **Given** the recording directory contains one or more transcript files, **When** user runs `generate` command without arguments, **Then** system displays a browsable list of available recordings sorted by date (newest first)
2. **Given** user has selected a recording, **When** system prompts for template selection, **Then** system displays all available prompt templates regardless of template type
3. **Given** user has selected both a recording and template, **When** system processes the request, **Then** output is generated using the selected template's prompt applied to the recording transcript via the configured LLM provider
4. **Given** generation completes successfully, **When** output is displayed, **Then** user sees the generated content in the terminal and output is saved to recording directory with template name in filename

---

### User Story 2 - One-Shot Command Execution (Priority: P2)

A user wants to automate or quickly process a specific recording with a specific template without interactive prompts, useful for scripting or repeated workflows.

**Why this priority**: Enables automation and power-user workflows, but core functionality works without it. Provides significant efficiency gains for repeated tasks.

**Independent Test**: Can be tested independently by running `generate --template "template-name"` and verifying it processes the most recent recording (or specified recording) with the named template without any interactive prompts.

**Acceptance Scenarios**:

1. **Given** user provides `--template "template-name"` argument, **When** command executes, **Then** system uses the specified template without prompting user for selection
2. **Given** user provides template name via `--template` argument, **When** template name matches an existing template, **Then** system processes using that template
3. **Given** user provides template name via `--template` argument, **When** template name does not match any existing template, **Then** system displays clear error message listing available templates
4. **Given** user provides `--template` argument but does not specify a recording, **When** command executes, **Then** system either prompts for recording selection or uses the most recent recording based on sensible default behavior

---

### User Story 3 - Business Meeting Template Processing (Priority: P3)

A user has recorded a business meeting with multiple speakers and wants to generate a structured summary that extracts topics discussed, action items, decisions made, and participant contributions.

**Why this priority**: Adds valuable domain-specific functionality for a common use case, but the core generate command works with any template type. This is primarily about providing a good bundled template rather than new technical capability.

**Independent Test**: Can be tested by creating a multi-speaker recording, running generate with the businessMeeting template, and verifying the output includes sections for topics, action items, decisions, and speaker identification.

**Acceptance Scenarios**:

1. **Given** a businessMeeting template is available, **When** user selects it from the template list, **Then** template appears in the selection menu with appropriate description
2. **Given** user processes a recording with businessMeeting template, **When** generation completes, **Then** output includes clearly structured sections for: meeting topics, action items, decisions made, and key discussion points
3. **Given** recording contains multiple speakers, **When** processed with businessMeeting template, **Then** output identifies and attributes statements to different speakers when possible
4. **Given** businessMeeting template is bundled with application, **When** user first runs generate command, **Then** template is available without additional configuration

---

### User Story 4 - Re-process Existing Recordings (Priority: P2)

A user has previously processed a recording but wants to generate new output using a different template, or re-generate output after modifying a template's prompt.

**Why this priority**: Enables experimentation with different templates and iteration on template design. Core functionality works without it, but adds significant flexibility for users refining their workflows.

**Independent Test**: Can be tested by processing a recording with one template, then running generate again, selecting the same recording, choosing a different template, and verifying new output is generated without affecting the original.

**Acceptance Scenarios**:

1. **Given** a recording has been previously processed, **When** user runs generate and selects that recording again, **Then** system allows re-processing with any available template
2. **Given** user processes the same recording with multiple templates, **When** outputs are generated, **Then** each output is independently stored using template name in filename without overwriting previous results
3. **Given** user has modified a template's prompt, **When** user re-processes a recording with that template, **Then** new output reflects the updated template prompt and overwrites the previous output file for that template

---

### Edge Cases

- What happens when the recording directory is empty (no transcripts available)?
  - System should display a clear message: "No recordings found. Use 'record' command to create a recording first."

- What happens when no prompt templates are available?
  - System should display a clear error: "No prompt templates found. Please configure at least one template."

- What happens when a recording transcript file is corrupted or unreadable?
  - System should skip that recording with a warning and continue displaying other available recordings.

- What happens when LLM provider fails or is unavailable during generation?
  - System displays clear error details (connection/service/rate limit), then prompts "Retry? (y/n)" to allow manual retry without re-selecting recording and template. If user declines, command exits gracefully.

- What happens when user provides `--template` argument with template name that has spaces or special characters?
  - System should support quoted template names (e.g., `--template "My Custom Template"`) and match case-insensitively for user convenience.

- What happens when a recording transcript is extremely long (e.g., multi-hour meeting)?
  - System detects token limit exceeded based on configured limit, displays clear warning about truncation, truncates intelligently to fit within limit (keeping beginning portion), proceeds with processing, and marks output file with truncation notice.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a `generate` command accessible from the CLI
- **FR-002**: System MUST read recording transcripts from the configured recording directory
- **FR-003**: System MUST display a browsable list of available recordings when command is run interactively
- **FR-004**: System MUST display recordings sorted by date with newest first, showing recording date/time and identifier
- **FR-005**: System MUST allow user to select a recording from the list using an interactive menu interface
- **FR-006**: System MUST read all available prompt templates from the template storage location
- **FR-007**: System MUST display a list of available templates for user selection, showing template name and description
- **FR-008**: System MUST support templates of any type (daily, weekly, businessMeeting, custom types) without type-specific filtering in the generate command
- **FR-009**: System MUST process the selected recording transcript with the selected template prompt via the configured LLM provider and model
- **FR-010**: System MUST display the generated output to the user in the terminal
- **FR-011**: System MUST support a `--template "template-name"` command argument to specify template without interactive prompt
- **FR-012**: System MUST validate that the specified template name (when using `--template` argument) exists and provide clear error message with available template names if not found
- **FR-013**: System MUST include a bundled businessMeeting template with the application
- **FR-014**: businessMeeting template MUST be designed to extract topics, action items, decisions, and speaker attribution from multi-speaker meetings
- **FR-015**: System MUST handle cases where recording directory is empty with a user-friendly message
- **FR-016**: System MUST handle cases where no templates exist with a clear error message
- **FR-017**: System MUST handle LLM provider errors gracefully with appropriate error messages
- **FR-018**: System MUST support case-insensitive template name matching when using `--template` argument
- **FR-019**: System MUST allow re-processing of previously processed recordings with different templates
- **FR-020**: When using `--template` argument without specifying a recording, system MUST default to processing the most recent recording (by creation date)
- **FR-021**: System MUST store generated outputs in the recording directory alongside the source transcript
- **FR-022**: System MUST name output files using the same base filename as the source recording with template filename inserted between date and increment, saved as markdown files following pattern M-D-Y_TemplateName_Increment.md (e.g., `10-21-2025_daily-summary_1.md` where "daily-summary" is the template filename without .md extension)
- **FR-023**: System MUST preserve all generated outputs when re-processing recordings with different templates, avoiding overwrites
- **FR-024**: System MUST overwrite existing output file when re-processing the same recording with the same template (replacing previous version)
- **FR-025**: System MUST support configurable token limit settings for LLM provider processing
- **FR-026**: System MUST detect when a recording transcript exceeds the configured token limit before sending to LLM provider
- **FR-027**: System MUST display a clear warning to user when transcript will be truncated due to token limit
- **FR-028**: System MUST intelligently truncate transcripts that exceed token limits (e.g., keeping beginning portion within limit) and proceed with processing

**Truncation Strategy Details** (FR-028):
- Calculate safe token limit: `maxInputTokens * 0.8` (80% safety factor for template overhead)
- Estimate tokens: `wordCount * 1.3` (conservative heuristic per research.md)
- If estimated tokens exceed safe limit:
  1. Calculate target word count: `safeTokenLimit / 1.3`
  2. Truncate to first N words to reach target
  3. Attempt sentence boundary preservation: if a period exists within last 10% of truncated content, trim to that period
  4. If no period found in last 10%, keep hard word boundary
- Append truncation marker to content before LLM processing
- **FR-029**: System MUST include truncation notification in the generated output file indicating content was truncated
- **FR-030**: System MUST offer manual retry option when LLM provider processing fails (network error, rate limit, service unavailable)
- **FR-031**: System MUST preserve user's recording and template selection when offering retry, avoiding need to reselect
- **FR-032**: System MUST display clear error details before offering retry option to help user decide whether to retry

### Key Entities

- **Recording Transcript**: A text file containing the transcribed speech from a recording session, stored in the recording directory. Includes metadata such as timestamp/date of recording creation.

- **Prompt Template**: A reusable template definition that contains a prompt structure for LLM processing, template name, description, and template type designation (daily, weekly, businessMeeting, etc.). Templates are stored in template storage location and can be custom-created or bundled with the application.

- **Template Type**: A classification/category for templates (e.g., "daily", "weekly", "businessMeeting") that may be used by specialized commands in the future, but does not restrict which templates can be used with the generate command.

- **Generated Output**: The result of processing a recording transcript through a prompt template via the LLM provider. Stored as markdown (.md) files in the recording directory with filename following pattern M-D-Y_TemplateName_Increment.md (e.g., `10-21-2025_daily-summary_1.md`) to enable future reference, comparison, and audit trail of different template applications. The TemplateName component uses the template's filename (e.g., "daily-summary.md" → "daily-summary"), not the template type enum value.

- **LLM Provider Configuration**: Connection and authentication details for the configured language model service that processes the template prompts. Includes configurable token limit settings to control maximum transcript size sent to the provider.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can select and process a recording with a template in under 30 seconds from command invocation to seeing output (excluding LLM processing time). Interactive operations (recording list display, template list display, menu navigation, file I/O, token estimation) MUST respond in <500ms as specified in plan.md performance goals. The 30-second budget includes user interaction time; system response time MUST be <500ms per operation.

- **SC-002**: System successfully processes recordings and generates output for 95% of valid recording/template combinations

- **SC-003**: Users can successfully use the `--template` argument to process recordings without interactive prompts in a single command

- **SC-004**: The businessMeeting template successfully identifies and extracts at least 3 of these 4 elements when present in a recording: topics discussed, action items, decisions made, speaker attribution

- **SC-005**: 90% of users can locate and select their desired recording and template on first attempt without errors or confusion

- **SC-006**: Error messages for common failure scenarios (no recordings, no templates, invalid template name, LLM errors) clearly explain the problem and next steps to resolve

- **SC-007**: Users can re-process the same recording with different templates multiple times without data loss or corruption

- **SC-008**: Command execution completes successfully whether recordings directory contains 1 recording or 100 recordings (performance scales gracefully)

## Assumptions

1. **Recording Storage**: Recordings and transcripts from the `record` command are stored in a consistent, discoverable location configured in the application

2. **Template Discovery**: Prompt templates follow a consistent structure with header metadata (including templateType) that can be parsed programmatically

3. **LLM Provider Configuration**: Users have already configured a valid LLM provider and model via application configuration before using the generate command

4. **Interactive Terminal**: The command runs in a terminal environment that supports interactive menu selection (not purely batch/scripting mode, though `--template` argument enables non-interactive use)

5. **Template Format**: Templates use a consistent format similar to the existing `today` command templates, allowing the system to extract name, description, type, and prompt content

6. **Default Recording Behavior**: When `--template` is provided without a recording specification, defaulting to the most recent recording is the most intuitive behavior (addressing FR-020 clarification)

7. **Template Name Uniqueness**: Template names are unique within the template storage location to avoid ambiguity during selection

8. **Single Template Selection**: Users select one template per generation operation (not multiple templates in a single invocation)

9. **Synchronous Processing**: Generation is a synchronous operation where user waits for LLM response rather than background/asynchronous processing

10. **businessMeeting Template Scope**: The bundled businessMeeting template uses general prompt engineering techniques for speaker identification and topic extraction, not specialized speech recognition or speaker diarization technology beyond what the LLM can infer from transcript text
