# Feature Specification: Improved Prompt Template Support

**Feature Branch**: `007-improved-prompt-template`
**Created**: 2025-10-15
**Status**: Draft
**Input**: User description: "Improved prompt template support. Right now, embed 2 default prompt templates, one for the daily summary and one for the weekly summary, this is fine for basic operation. With this feature, we want to add the templates the default templates to the configured memory, so that they are are automatically installed once the user has completed the guided setup process. In the case of the user upgrading, where they already have configuration, then we should have a simply way to migrate them to the latest version of the configuration, ideally, this happens seamlessly. Perhaps we automatically run the config validate command and that will determine the migration path or if a full setup rerun is required. Let's keep this simple and intuitive and not overcomplicate it. This new template directory under our configured memory directory will contain our default templates files. This will give the user the ability to edit those files since they are not on the file system and will be used the next time an llm summary is requested. Once this is in place, we also want the ability to for the user to create a new template, which for now would be them manually adding a markdown file to this new directory. Because of this, we should now let the user select the template they would like to use with using either today or thisweek commands. For example, a user will complete all the today inputs and after that instead of automatically calling out the llm for a summary using the baked in template, we'll now ask the user to select the template they would like to use. We should be smart enough to only show them the prompt templates designed for the command they are using, so they should not be able to select a `today` template when using /thisweek command. The prompt templates should contain some type of header metadata that indicates this and so it can be filtered on dynamically."

## Clarifications

### Session 2025-10-15

- Q: What format should template metadata use? → A: YAML front matter (delimited by `---`) - Standard markdown metadata format
- Q: How should the system handle a deleted templates directory? → A: Automatically recreate directory with default templates on next command - Self-healing
- Q: How should the system handle templates with duplicate names? → A: Treat filename as unique identifier, reject/skip duplicates - Simple, filesystem-enforced
- Q: What happens when all templates for a command type are deleted? → A: Fall back to embedded default template, notify user - Self-healing with notification
- Q: What is the maximum file size limit for template files? → A: 1MB maximum file size per template - Reasonable limit, easy to validate

## User Scenarios & Testing *(mandatory)*

### User Story 1 - New User Setup with Default Templates (Priority: P1)

A new user completes the guided setup process and receives default prompt templates automatically installed in their configured memory directory, ready to use immediately for generating daily and weekly summaries.

**Why this priority**: This is the foundational capability - without default templates being properly installed during setup, the feature cannot function. It delivers immediate value by providing working templates out-of-the-box.

**Independent Test**: Can be fully tested by running the guided setup process and verifying that default templates are created in the correct directory and are immediately usable for summary generation.

**Acceptance Scenarios**:

1. **Given** a user has no existing configuration, **When** they complete the guided setup process, **Then** default daily and weekly prompt templates are automatically created in the templates directory within their configured memory location
2. **Given** default templates have been installed during setup, **When** the user runs a summary command for the first time, **Then** they can select from the available default templates appropriate for their command type
3. **Given** default templates have been installed, **When** the user opens the templates directory, **Then** they can view and edit the template files on their file system

---

### User Story 2 - Template Selection for Summary Generation (Priority: P1)

A user who has completed data input for a daily or weekly summary can select which prompt template to use for generating their summary, with only contextually appropriate templates shown.

**Why this priority**: This is the core user interaction for the feature - allowing users to choose templates. Without this, users cannot benefit from having multiple templates available.

**Independent Test**: Can be fully tested by completing data input for either a today or thisweek command and verifying that template selection is prompted with filtered options appropriate to the command context.

**Acceptance Scenarios**:

1. **Given** a user has completed all inputs for the today command, **When** they reach the summary generation step, **Then** they are prompted to select a prompt template before the LLM summary is generated
2. **Given** template selection is prompted for the today command, **When** available templates are displayed, **Then** only templates marked for daily summaries are shown
3. **Given** template selection is prompted for the thisweek command, **When** available templates are displayed, **Then** only templates marked for weekly summaries are shown
4. **Given** a user selects a template, **When** the LLM summary is generated, **Then** the selected template is used to format the prompt
5. **Given** only one appropriate template exists for the command type, **When** template selection is prompted, **Then** that template is selected automatically without user intervention

---

### User Story 3 - Existing User Configuration Migration (Priority: P2)

A user with existing configuration upgrades to the new version and has their configuration seamlessly migrated to include the templates directory and default templates without manual intervention.

**Why this priority**: Critical for existing users to adopt the feature without disruption, but not required for new users or MVP functionality.

**Independent Test**: Can be fully tested by simulating an existing configuration, running config validation, and verifying automatic migration occurs without user intervention.

**Acceptance Scenarios**:

1. **Given** a user has existing configuration without a templates directory, **When** config validation runs automatically on startup or command execution, **Then** the system detects the missing templates directory
2. **Given** the system detects missing templates during validation, **When** migration is simple (only adding templates directory), **Then** the migration happens automatically and default templates are installed
3. **Given** the system detects configuration that requires complex migration, **When** automatic migration is not possible, **Then** the user is informed that a full setup rerun is required
4. **Given** automatic migration completes successfully, **When** the user next runs a summary command, **Then** they can immediately use template selection without further action
5. **Given** migration fails or is too complex, **When** the user is notified, **Then** clear instructions are provided on how to rerun the setup process

---

### User Story 4 - Custom Template Creation (Priority: P3)

A user can create their own custom prompt templates by adding new markdown files to the templates directory, which become immediately available for selection in summary commands.

**Why this priority**: Enhances flexibility and customization but is not required for basic operation - users can edit existing templates initially.

**Independent Test**: Can be fully tested by manually creating a new markdown template file with proper metadata, running a summary command, and verifying the custom template appears in the selection list.

**Acceptance Scenarios**:

1. **Given** a user wants to create a custom template, **When** they add a new markdown file to the templates directory with proper header metadata, **Then** the template is recognized by the system on the next summary command
2. **Given** a custom template is created with daily summary metadata, **When** the today command prompts for template selection, **Then** the custom template appears alongside default templates
3. **Given** a custom template is created with weekly summary metadata, **When** the thisweek command prompts for template selection, **Then** the custom template appears in the selection list
4. **Given** a custom template has malformed or missing metadata, **When** the system attempts to load templates, **Then** the invalid template is skipped and a warning is logged, but other templates remain available
5. **Given** multiple custom templates exist, **When** template selection is prompted, **Then** templates are listed in a logical order (e.g., default templates first, then custom templates alphabetically)

---

### User Story 5 - Template Editing and Updates (Priority: P3)

A user can edit existing templates in the templates directory, and their changes take effect immediately on the next summary generation without requiring application restart or reconfiguration.

**Why this priority**: Provides user control and customization but assumes basic functionality is already working. Users can start with defaults and refine over time.

**Independent Test**: Can be fully tested by modifying an existing template file, running a summary command, and verifying the updated template content is used.

**Acceptance Scenarios**:

1. **Given** a user has edited a template file, **When** they run a summary command and select that template, **Then** the updated template content is used for prompt generation
2. **Given** a user has edited template metadata to change its type (daily to weekly), **When** template selection is prompted, **Then** the template appears in the appropriate command context based on updated metadata
3. **Given** a user saves changes to a template while the application is running, **When** they next generate a summary, **Then** the changes are reflected without requiring application restart

---

### Edge Cases

- **Deleted templates directory**: System automatically recreates the templates directory with default templates on the next command execution, ensuring seamless recovery
- **Duplicate template names**: Template filenames serve as unique identifiers; filesystem prevents true duplicates in the same directory
- **All templates deleted for command type**: System falls back to embedded default template for that type and notifies the user of the recovery action
- **Large template files**: System enforces a 1MB maximum file size limit per template; templates exceeding this limit are skipped with a warning
- What happens when a template file is corrupted or contains invalid markdown?
- What happens when template metadata is missing or malformed?
- How does the system handle templates during concurrent access (e.g., user editing while command is reading)?
- What happens when the configured memory directory location changes after templates are installed?
- What happens when a template file has incorrect file permissions (read-only)?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST create a templates directory within the configured memory directory during guided setup process
- **FR-002**: System MUST copy default daily and weekly prompt template files to the templates directory during guided setup
- **FR-003**: System MUST automatically validate configuration on startup or command execution to detect missing templates directory
- **FR-004**: System MUST automatically migrate existing configurations by adding templates directory and default templates when migration is straightforward
- **FR-005**: System MUST notify users when configuration migration requires a full setup rerun and provide clear instructions
- **FR-006**: Users MUST be able to view and edit template files directly from the file system in the templates directory
- **FR-007**: System MUST read and parse YAML front matter (delimited by `---`) from template files to determine template type (daily or weekly)
- **FR-008**: System MUST prompt users to select a prompt template after completing data input and before generating LLM summary
- **FR-009**: System MUST filter template selection to show only templates appropriate for the current command context (today vs thisweek)
- **FR-010**: System MUST automatically select a template without prompting when only one appropriate template exists for the command type
- **FR-011**: System MUST support custom templates created by users through manual file addition to the templates directory
- **FR-012**: System MUST recognize and load new or modified templates without requiring application restart
- **FR-013**: System MUST handle malformed templates gracefully by skipping them and continuing with valid templates
- **FR-014**: System MUST log warnings for invalid or malformed templates without disrupting normal operation
- **FR-015**: Template YAML front matter MUST include a field indicating template type (daily/weekly) to enable filtering by command context
- **FR-016**: System MUST use the selected template content when generating prompts for LLM summary requests
- **FR-017**: System MUST display template names in a logical order during selection (defaults first, then custom alphabetically)
- **FR-018**: System MUST fall back to embedded default template when no valid templates exist for the current command type and notify the user of the recovery action
- **FR-019**: System MUST automatically recreate the templates directory with default templates when the directory is detected as missing during validation
- **FR-020**: System MUST treat template filenames as unique identifiers, with the filesystem enforcing uniqueness within the templates directory
- **FR-021**: System MUST maintain embedded copies of default templates as fallback when file-based templates are unavailable
- **FR-022**: System MUST enforce a 1MB maximum file size limit per template file, skipping templates that exceed this limit with a warning message

### Key Entities

- **Prompt Template**: A markdown file containing LLM prompt content and YAML front matter metadata. Unique identifier: filename (enforced by filesystem). Key attributes include: filename (unique), template type (daily/weekly), template content, creation date, last modified date. Relationships: belongs to a templates directory, is selected by users during summary commands.
- **Template Directory**: A directory location within the configured memory path that stores all prompt template files. Key attributes include: directory path, parent configured memory location. Relationships: contains multiple prompt templates, is created during setup, is validated during config checks.
- **Template Metadata**: YAML front matter within each template file that describes the template. Format: delimited by `---` at the beginning of the file. Key attributes include: template type field (daily/weekly/both), template name/title, optional description. Relationships: is embedded in prompt template files as YAML front matter, is parsed to enable filtering.
- **Configuration Migration**: A process that updates existing user configurations to include new features. Key attributes include: migration type (simple/complex), migration status (success/failure/required), validation trigger (startup/command execution). Relationships: validates existing configuration, determines migration path, creates template directory if missing.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: New users completing guided setup receive working default templates within 2 seconds of setup completion
- **SC-002**: 100% of existing users with valid configurations are automatically migrated to include templates directory without requiring manual intervention, or receive clear migration instructions if automatic migration is not possible
- **SC-003**: Users can complete template selection in under 10 seconds from prompt to selection confirmation
- **SC-004**: Custom templates added to the templates directory are recognized and available for selection within 1 second of the next summary command execution
- **SC-005**: Template filtering correctly shows only contextually appropriate templates 100% of the time (no daily templates for weekly commands and vice versa)
- **SC-006**: Edited template content is reflected in the next summary generation 100% of the time without requiring application restart
- **SC-007**: Users can successfully edit and customize templates in any text editor without encountering file access or permission issues
- **SC-008**: System handles invalid or malformed templates without crashing or blocking valid templates from being used
- **SC-009**: Template files exceeding 1MB are rejected with clear warning messages, allowing other valid templates to remain functional
