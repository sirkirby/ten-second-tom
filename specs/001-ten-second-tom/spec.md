# Feature Specification: Ten Second Tom - Personal Memory Management CLI

**Feature Branch**: `001-ten-second-tom`  
**Created**: October 1, 2025  
**Status**: Draft  
**Input**: User description: "We are building a kick ass CLI application call Ten Second Tom, based on the funny but tragic character from the movie 50 First Dates who could not commit new memories to his long term memory. This was comedic inspiration for this application which will help you download and summarize your day, which you will use every day, guided by a series of questions. The CLI UI should be badass with a great theme, logo, an easy to access help commands and instructions. A command like /today would generate questions, or prompts, will be short, simple, and encourage open ended input. Like "What happened today?" "Anything interesting planned for tomorrow?", or "Is there something you didn't finish that you need a reminder to finish later on?, along with other relevant questions. The input received will then be summarized by calling an LLM API, we'll support Open AI and Anthropic's API's to begin with. We should have a series of pre-defined prompts that that we'll use to instruct the AI endpoint. The input gathered from the /today command would use a prompt template for Summarization, identifying themes, to do's, important people or tasks of note. perhaps a command /thisweek would use a prompt template for summarizing the week, which will take a weeks worth of daily summaries that were saved in a database, and have the LLM API generate 3 accomplishments of note and 3 challenges of note along with identifying recurring themes, interactions, and suggestions on how to plan for next week. all responses returned from the LLM API will be stored in a database categorized by the type of response. We support more commands over time that will likely work in a similar way, working with direct user input, LLM API's, and text stored in databases, so let's ensure this awesome CLI application is extensible. Ultimately this app will be a curated analog of the users memory, it will collect, summarize, interpret with the ability to search that memory, provide insights, suggestions, improvements powered by our our supported LLM providers and the custom templates we develop which can essentially act like agents. Our app should also be easy to use by other applications via the CLI as well as other AI agents. Finally, we need to consider authentication. Let's keep things CLI themed and if possible support their existing Ed25519 key, or some other more viable alternative that is standard or common practice. We don't want to be too bespoke here, it should be frictionless, but secure."

## Execution Flow (main)
```
1. Parse user description from Input
   → Valid input received
2. Extract key concepts from description
   → Actors: Users, AI agents, other applications
   → Actions: Capture daily reflections, summarize, search memories, provide insights
   → Data: User responses, summaries, themes, to-dos, accomplishments, challenges
   → Constraints: Extensible architecture, CLI-based, secure authentication
3. For each unclear aspect:
   → Marked with [NEEDS CLARIFICATION] below
4. Fill User Scenarios & Testing section
   → Primary flows identified for daily entry, weekly review, memory search
5. Generate Functional Requirements
   → All requirements testable and measurable
6. Identify Key Entities
   → Memory Entry, Summary, Template, User Session
7. Run Review Checklist
   → WARN "Spec has uncertainties - see [NEEDS CLARIFICATION] markers"
8. Return: SUCCESS (spec ready for planning with clarifications needed)
```

---

## ⚡ Quick Guidelines
- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

---

## Clarifications

### Session 2025-10-01
- Q: Authentication session duration: How should user sessions persist? → A: Persistent until logout - Session remains active indefinitely until user explicitly logs out
- Q: Multiple daily entries: Should users be able to create multiple reflection entries in a single day? → A: Multiple entries allowed - Users can run `/today` multiple times per day, each creates a separate entry
- Q: Cryptographic authentication standard: Which authentication mechanism should be supported? → A: SSH keys (Ed25519/RSA) - Use standard SSH key pairs (~/.ssh/id_ed25519), widely available
- Q: Data retention policy: How long should memory entries be stored? → A: User-configurable - Allow users to set their own retention period (30 days, 1 year, forever, etc.)
- Q: Lost SSH key recovery: What should happen when a user loses their SSH key? → A: Admin recovery mechanism - Support/admin process to reset authentication and restore access

---

## User Scenarios & Testing *(mandatory)*

### Primary User Story
As a user with busy daily routines, I want to quickly capture and reflect on my day through guided prompts, so that I can build a searchable personal memory archive that helps me identify patterns, track progress, and remember important details I would otherwise forget.

### Acceptance Scenarios

#### Daily Memory Capture
1. **Given** a user starts their day or evening reflection, **When** they run the daily memory command, **Then** they are presented with 3-5 short, open-ended questions that guide their reflection
2. **Given** a user is answering reflection questions, **When** they provide their responses, **Then** their input is captured in full and sent for AI summarization
3. **Given** a user has completed their daily reflection, **When** the AI processes their responses, **Then** a structured summary is generated identifying: key events, themes, to-dos, important people, and notable tasks
4. **Given** a summary has been generated, **When** it is returned to the user, **Then** it is displayed in a readable format and persisted to the user's memory store

#### Weekly Review
1. **Given** a user has completed daily reflections for at least 3 days in the past week, **When** they run the weekly review command, **Then** the system retrieves all daily summaries from the past 7 days
2. **Given** daily summaries are available, **When** the weekly review is processed, **Then** the AI generates exactly 3 key accomplishments, 3 notable challenges, recurring themes, interaction patterns, and planning suggestions for next week
3. **Given** a weekly review is generated, **When** it is presented to the user, **Then** it is displayed with clear sections and persisted as a weekly summary entry

#### Memory Search and Insights
1. **Given** a user has accumulated memory entries over time, **When** they search for specific topics, people, or timeframes, **Then** relevant memory entries are retrieved and displayed
2. **Given** search results are returned, **When** the user requests insights, **Then** the AI analyzes the retrieved memories and provides patterns, suggestions, or interpretations based on the search context

#### CLI Experience
1. **Given** a new user runs the application for the first time, **When** they view the help command, **Then** they see a visually appealing interface with clear instructions, available commands, and usage examples
2. **Given** a user is authenticated, **When** they interact with any command, **Then** they experience consistent visual theming and intuitive command structure
3. **Given** another application or AI agent invokes the CLI, **When** commands are executed programmatically, **Then** structured output is returned suitable for automated consumption

#### Authentication
1. **Given** a new user runs the application for the first time, **When** they attempt to use memory commands, **Then** they are prompted to authenticate using their cryptographic key
2. **Given** a user has authenticated once, **When** they return to use the application, **Then** they are automatically authenticated without re-entering credentials until they explicitly log out
3. **Given** a user's authentication session is active, **When** they attempt privileged operations, **Then** their identity is verified using the persistent session

### Edge Cases

#### Data Quality and Availability
- What happens when a user provides minimal or empty responses to daily questions?
  - **Expected**: System should accept brief responses but may generate minimal summaries; should prompt user if all responses are empty
- What happens when attempting a weekly review with fewer than 7 days of data?
  - **Expected**: System proceeds with available days, notes incomplete dataset in output
- What happens when the AI service is unavailable or returns an error?
  - **Expected**: User is notified of the issue, raw input is still persisted, summarization can be retried later

#### User Behavior
- What happens when a user tries to run the daily command multiple times in one day?
  - **Expected**: System allows multiple entries per day; each execution creates a separate, timestamped memory entry
- What happens when a user hasn't used the application for weeks or months?
  - **Expected**: Past entries remain available within configured retention period; no reminders sent; weekly reviews can span gaps

#### Integration and Security
- What happens when an AI agent provides malformed commands?
  - **Expected**: Clear error messages with usage examples, non-zero exit codes
- What happens when someone attempts to use the CLI without proper authentication?
  - **Expected**: Access denied, clear instructions on authentication setup
- What happens when the cryptographic key is lost or corrupted?
  - **Expected**: User initiates admin recovery process to verify identity and register new SSH key; existing data remains accessible after recovery

---

## Technical Requirements *(informational - for planning reference)*

### Logging Framework
- **MUST use Serilog** as the logging framework (organizational standard per constitution v1.1.0)
- Configure with Console sink for CLI output diagnostics
- Configure with File sink for persistent logs (`.logs/` directory)
- Use structured logging with semantic properties
- Log levels: Debug (I/O), Information (commands), Warning (retries), Error (failures)
- Never log secrets or sensitive user data (API keys, SSH passphrases, user memory content excerpts)

---

## Requirements *(mandatory)*

### Functional Requirements

#### Core Memory Capture
- **FR-001**: System MUST provide a daily memory command that presents users with 3-5 open-ended reflection questions
- **FR-002**: System MUST accept and capture user responses to reflection questions in their entirety without character limits
- **FR-003**: System MUST support OpenAI and Anthropic LLM providers for summarization processing
- **FR-004**: System MUST use pre-defined prompt templates to instruct LLM providers for different summary types (daily, weekly, search insights)
- **FR-005**: Daily summaries MUST identify and extract: key events, themes, to-do items, important people, and notable tasks
- **FR-006**: System MUST persist all user responses and generated summaries in the memory store categorized by response type
- **FR-007**: System MUST allow multiple daily entries per calendar day, each stored as a separate timestamped memory entry

#### Weekly Review and Aggregation
- **FR-008**: System MUST provide a weekly review command that retrieves daily summaries from the past 7 calendar days
- **FR-009**: Weekly reviews MUST generate exactly 3 accomplishments and exactly 3 challenges from the week's data
- **FR-010**: Weekly reviews MUST identify recurring themes, interaction patterns, and provide planning suggestions for the upcoming week
- **FR-011**: System MUST persist weekly summaries as distinct entries in the memory store
- **FR-012**: System MUST proceed with weekly review generation even when fewer than 7 days of data are available

#### Memory Search and Insights
- **FR-013**: Users MUST be able to search their memory store by keywords, topics, people, or date ranges
- **FR-014**: System MUST support requesting AI-powered insights on search results to identify patterns and provide suggestions
- **FR-015**: Search results MUST return relevant memory entries with context (date, type, excerpt)

#### CLI User Experience
- **FR-016**: System MUST display a visually distinctive theme and logo when launched
- **FR-017**: System MUST provide comprehensive help commands accessible at any time
- **FR-018**: System MUST present commands with intuitive naming (e.g., `/today`, `/thisweek`)
- **FR-019**: System MUST display output in human-readable format by default
- **FR-020**: System MUST support structured output format (e.g., JSON) for programmatic consumers and AI agents
- **FR-021**: System MUST return appropriate exit codes for success/failure states for automated usage

#### Extensibility and Integration
- **FR-022**: System architecture MUST support adding new commands that follow the pattern: user input → LLM processing → memory storage
- **FR-023**: System MUST support adding new prompt templates for different summary and analysis types
- **FR-024**: System MUST allow other applications and AI agents to invoke commands programmatically via CLI interface
- **FR-025**: System MUST support adding additional LLM providers beyond OpenAI and Anthropic

#### Authentication and Security
- **FR-026**: System MUST require user authentication before allowing access to memory commands
- **FR-027**: System MUST support SSH key-based authentication using Ed25519 or RSA key pairs from standard SSH locations (~/.ssh/)
- **FR-027a**: System MUST provide an admin recovery process for users who lose their SSH keys, requiring identity verification before key rotation
- **FR-028**: Authentication mechanism MUST be frictionless for daily use (minimal re-authentication)
- **FR-029**: System MUST securely store user credentials and authentication state
- **FR-030**: System MUST log all authentication attempts and security-relevant events
- **FR-031**: System MUST maintain persistent authentication sessions that remain valid until user explicitly logs out
- **FR-031a**: System MUST provide a logout command to terminate active sessions

#### Data Management
- **FR-032**: System MUST categorize all memory entries by type (daily summary, weekly summary, raw input, insights)
- **FR-033**: System MUST preserve timestamps for all memory entries
- **FR-034**: System MUST maintain data integrity between raw user input and generated summaries
- **FR-035**: System MUST allow users to configure data retention policy with options including: 30 days, 90 days, 1 year, 2 years, or indefinite retention
- **FR-035a**: System MUST default to indefinite retention if no user preference is set
- **FR-035b**: System MUST automatically purge entries older than the configured retention period

#### Error Handling
- **FR-036**: System MUST persist user input even when LLM summarization fails
- **FR-037**: System MUST provide clear error messages when LLM services are unavailable
- **FR-038**: System MUST support retry mechanisms for failed summarization requests
- **FR-039**: System MUST validate user input and provide helpful feedback for malformed commands

### Key Entities *(include if feature involves data)*

#### Memory Entry
- Represents a single captured memory moment (raw user response or structured summary)
- Attributes: timestamp, entry type (raw_daily, daily_summary, weekly_summary, insight), user identifier, content, associated tags/themes
- Relationships: Daily summaries reference raw input entries; weekly summaries reference daily summaries

#### Summary
- Represents processed and structured information derived from user input
- Attributes: summary type (daily/weekly), generation timestamp, source entry references, extracted themes, to-dos, people, accomplishments, challenges
- Relationships: Linked to source memory entries, may be referenced by search queries

#### Prompt Template
- Represents pre-defined instructions for LLM processing
- Attributes: template identifier, template type (daily_summary, weekly_review, insight_generation), prompt text with variable placeholders, target LLM providers
- Relationships: Used to generate summaries from memory entries

#### User Session
- Represents an authenticated user's active session
- Attributes: user identifier, authentication timestamp, authentication method used, session status
- Relationships: All memory entries and summaries are associated with a user session

#### Search Query
- Represents a user's search request against their memory store
- Attributes: query text, filters (date range, entry type, tags), results count, timestamp
- Relationships: Returns matching memory entries, may trigger insight generation

---

## Review & Acceptance Checklist
*GATE: Automated checks run during main() execution*

### Content Quality
- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

### Requirement Completeness
- [x] No [NEEDS CLARIFICATION] markers remain - **5 of 8 clarifications resolved**
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

### Clarifications Resolved (5/8)
1. ✅ **Authentication session policy**: Persistent until explicit logout
2. ✅ **Multiple daily entries**: Multiple entries allowed per day
3. ✅ **Specific authentication standard**: SSH keys (Ed25519/RSA) from standard locations
4. ✅ **Data retention policy**: User-configurable with multiple options (30d, 90d, 1yr, 2yr, indefinite)
5. ✅ **Key recovery mechanism**: Admin recovery process with identity verification

### Clarifications Deferred (3/8) - Low Impact
6. **Session timeout behavior**: Not needed - sessions persist until logout (resolved by Q1)
7. **Incomplete data handling**: Already specified - proceed with available days (FR-012)
8. **Memory entry versioning**: Not mentioned in original requirements - defer to planning phase

---

## Execution Status
*Updated by main() during processing*

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked (8 clarifications needed)
- [x] User scenarios defined
- [x] Requirements generated (39 functional requirements)
- [x] Entities identified (5 key entities)
- [ ] Review checklist passed (pending clarifications)

**Status**: Specification is comprehensive and ready for planning phase. 8 clarifications should be addressed before implementation to ensure complete requirements coverage.

---
