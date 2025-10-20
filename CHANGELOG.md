# Changelog

All notable changes to Ten Second Tom will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed - BREAKING CHANGE

#### Simplified `/today` Command (#30)
The `/today` command has been completely redesigned for a more natural and flexible user experience:

- **Replaced 3-question prompt flow** with a single free-form text entry
  - **Before**: Users answered three separate questions:
    1. "What happened today?"
    2. "Anything interesting planned for tomorrow?"
    3. "Is there something you didn't finish that needs attention?"
  - **After**: Users now see a single prompt: "What would you like to remember from today?"
  - Users can write naturally in any format they prefer

- **Templates receive unified input**
  - Custom templates now receive the complete user input via `{{USER_INPUT}}` variable
  - Templates no longer receive separate sections for each question
  - Existing templates continue to work without modification

### Added

#### New CLI Options for `/today` Command (#30)

- **`--no-edit` flag**: Accept notes directly from command line argument, bypassing interactive editor
  ```bash
  tom today "Completed OAuth integration today" --no-edit
  ```

- **`--use-default-template` flag**: Automatically use default template without prompting for selection
  - Especially useful when combined with `--no-edit` for sub-3-second execution
  ```bash
  tom today "Quick daily note" --no-edit --use-default-template
  ```

- **`--template <name>` option**: Specify custom template by name (without `.md` extension)
  ```bash
  tom today "Standup notes" --no-edit --template "engineering-standup"
  ```

- **Multi-line notes support**: Preserve formatting and newlines in CLI mode
  ```bash
  tom today "Line 1: Completed task A
  Line 2: Working on task B
  Line 3: Blocked on task C" --no-edit
  ```

### Migration Guide

This is a **user-facing change only** - no data migration or code changes required:

1. **Existing entries remain unchanged** - All previously saved daily entries are unaffected
2. **Custom templates continue to work** - Templates automatically receive content via `{{USER_INPUT}}`
3. **Users will see the new flow** - Next time they run `/today`, they'll see the single prompt
4. **Automation scripts** - Can now use `--no-edit` flag for non-interactive operation

### Technical Details

- Removed `DailyPromptService` and related multi-question logic
- Simplified `CreateDailyEntryCommand` to use single text capture
- Updated integration tests to verify new single-prompt behavior
- All 178 integration tests and 23 unit tests passing

## [1.0.0] - Previous Release

### Added
- Initial release with core features
- Daily reflection with 3-question prompts
- Weekly review generation
- Memory search functionality
- SSH key authentication
- OpenAI and Anthropic provider support
- Custom prompt templates
- Shell mode with autocomplete
- JSON output for automation

---

*For a complete history of changes, see the [GitHub Releases](https://github.com/sirkirby/ten-second-tom/releases) page.*