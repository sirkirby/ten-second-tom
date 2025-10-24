# Research: Generate Command for Recording Processing

**Feature Branch**: `009-generate-recordings`
**Date**: 2025-10-24
**Phase**: 0 (Outline & Research)

## Overview

This document captures research findings and design decisions for the generate command implementation. All "NEEDS CLARIFICATION" items from Technical Context have been resolved through codebase analysis and pattern review.

## Research Areas

### 1. LLM Provider Integration

**Decision**: Use existing `ILlmProvider` interface for all LLM operations

**Rationale**:
- Interface already defined at `src/Infrastructure/Llm/ILlmProvider.cs`
- Provides `GenerateCompletionAsync(string prompt, CancellationToken, int? maxTokens, double? temperature)`
- Returns `Result<LlmResponse>` with Content, InputTokens, OutputTokens
- Abstracts OpenAI and Anthropic providers behind common interface
- Factory pattern via `LlmProviderFactory` handles provider selection based on configuration

**Alternatives considered**:
- Creating a new specialized interface for recording processing - rejected because it would duplicate functionality and violate DRY principle
- Direct SDK usage - rejected because it would bypass existing abstraction and make testing harder

**Implementation notes**:
- Inject `ILlmProvider` into `GenerateOutputCommandHandler`
- Use `maxTokens` parameter to enforce limits
- Handle Result<LlmResponse> for error cases (network, rate limit, service unavailable)
- Extract `Content` property for generated output

### 2. Token Limit Handling

**Decision**: Implement configurable token limits with intelligent truncation

**Rationale**:
- OpenAI models (GPT-4o, GPT-4o Mini) have context windows: 128K tokens
- Anthropic models (Claude 3-4 series) have context windows: 200K tokens (standard) or 1M tokens (Sonnet 4 with API)
- Provider-specific limits must be respected
- Users need control over cost/length tradeoffs

**Actual Supported Models** (from ModelRegistry):
- **OpenAI Budget**: GPT-4o Mini (128K input context, 16K max output)
- **OpenAI Balanced**: GPT-4o (128K total context, 16K max output), ChatGPT-4o Latest
- **Anthropic Budget**: Claude 3 Haiku (200K context, 8K output), Claude 3.5 Haiku (200K context, 8K output)
- **Anthropic Balanced**: Claude Sonnet 4.0 (200K-1M context, 8K typical output), Claude Sonnet 4.5 (200K-1M context, 8K typical output)
- **Anthropic Premium**: Claude Opus 4.0 (200K context, 8K output), Claude Opus 4.1 (200K context, 8K output)

**Configuration approach**:
```csharp
// Add to ConfigurationKeys.cs
public const string LlmMaxInputTokens = "TenSecondTom:Llm:MaxInputTokens";

// Default values by model tier (LlmConstants.cs):
// OpenAI GPT-4o Mini (Budget): 50,000 tokens (safe limit for 128K context)
// OpenAI GPT-4o (Balanced): 50,000 tokens (safe limit for 128K context)
// Anthropic Haiku (Budget): 80,000 tokens (safe limit for 200K context)
// Anthropic Sonnet (Balanced): 80,000 tokens (safe limit for 200K context)
// Anthropic Opus (Premium): 80,000 tokens (safe limit for 200K context)
```

**Truncation strategy**:
1. Estimate token count: `words * 1.3` (conservative heuristic)
2. If exceeds limit, keep first N words to stay within 80% of limit
3. Append truncation notice to transcript before sending to LLM
4. Add metadata to output file indicating truncation occurred

**Alternatives considered**:
- Chunking and multi-pass processing - rejected as overly complex for v1
- Reject processing entirely - rejected as poor user experience
- Semantic truncation (keep important parts) - rejected as too complex for initial implementation

### 3. Recording Discovery and Listing

**Decision**: Reuse patterns from existing `StoredRecording` model and file discovery

**Rationale**:
- `StoredRecording` model already exists at `src/Features/Audio/Models/StoredRecording.cs`
- Defines recording structure: AudioFilePath, TranscriptionFilePath, RecordedAt, Duration, metadata
- Naming pattern: `recording-YYYYMMdd-HHmmss.*`
- Files stored in recording directory (configured via MemoryDirectory)

**Recording listing approach**:
1. Scan recording directory for `recording-*.txt` transcript files
2. Parse filename to extract timestamp (RecordedAt)
3. Sort by RecordedAt descending (newest first)
4. Create `RecordingListItem` for UI display with:
   - Display name: formatted timestamp
   - File path for loading
   - Metadata: word count, size

**Service structure**:
```csharp
public interface IRecordingService
{
    Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(CancellationToken);
    Task<Result<string>> GetTranscriptContentAsync(string transcriptPath, CancellationToken);
}
```

**Alternatives considered**:
- Database-backed storage - rejected as violates CLI-first principle and adds complexity
- Metadata sidecar files - rejected as not needed for v1, filenames contain enough info

### 4. Template Selection Pattern

**Decision**: Follow exact pattern from Today command's template selection

**Rationale**:
- Today command already implements template listing and selection
- Uses `ListTemplatesQuery` and `ListTemplatesQueryHandler` from Templates feature
- Returns `IReadOnlyList<TemplateListItem>` with TemplateId, Title, Description, TemplateType
- Spectre.Console selection prompt provides interactive UI
- Case-insensitive matching for `--template` argument

**Pattern to replicate**:
1. Query templates via `ListTemplatesQuery`
2. Filter by type if needed (or allow all types for generate command)
3. Present selection UI with template Title and Description
4. Return selected template's TemplateId
5. Load full PromptTemplate via existing template services

**Code reference**: `src/Features/Today/Handlers/CreateDailyEntryHandler.cs`

**Alternatives considered**:
- Custom template selection UI - rejected to maintain consistency
- Direct file scanning - rejected because Templates feature already provides this

### 5. Output File Storage Strategy

**Decision**: Store outputs in recording directory with template-name suffix

**Rationale**:
- Keeps all recording artifacts (audio, transcript, outputs) together
- Filename pattern enables discovery: `recording-YYYYMMdd-HHmmss_{template-id}.md`
- Multiple outputs per recording (different templates) without collision
- Overwrite behavior when re-processing with same template

**File naming examples**:
- Input: `recording-20251024-143022.txt`
- Template: `business-meeting`
- Output: `recording-20251024-143022_business-meeting.md`

**Output file structure**:
```markdown
<!-- Generated by Ten Second Tom -->
<!-- Recording: recording-20251024-143022.txt -->
<!-- Template: business-meeting -->
<!-- Generated: 2025-10-24T14:32:05Z -->
<!-- Tokens: 1234 input, 567 output -->

[LLM-generated content here]
```

**Service structure**:
```csharp
public interface IOutputStorageService
{
    Task<Result<string>> SaveOutputAsync(
        string recordingBaseName,
        string templateId,
        string content,
        GenerationMetadata metadata,
        CancellationToken cancellationToken);

    bool OutputExists(string recordingBaseName, string templateId);
}
```

**Alternatives considered**:
- Separate outputs directory - rejected as reduces discoverability
- Timestamp in output filename - rejected as redundant (recording already has timestamp)
- Database storage - rejected as violates local-first principle

### 6. Business Meeting Template Design

**Decision**: Create prompt optimized for multi-speaker meeting summarization

**Rationale**:
- Common use case for voice recordings
- Requires structured extraction: topics, action items, decisions, speakers
- Template type: new enum value or reuse existing?

**Template structure**:
```yaml
---
templateType: businessMeeting  # New TemplateType enum value
title: Business Meeting Summary
description: Extracts topics, action items, decisions, and speaker contributions from multi-speaker meetings
author: Ten Second Tom
version: 1.0.0
tags: [meeting, business, action-items, multi-speaker]
---

You are analyzing a transcript from a business meeting. Your task is to create a structured summary.

## Meeting Transcript
{{TRANSCRIPT}}

## Instructions
Extract and organize the following information:

1. **Meeting Topics**: List the main topics discussed
2. **Key Decisions**: Document any decisions made
3. **Action Items**: List action items with responsible parties (if identifiable)
4. **Discussion Points**: Summarize key discussion points and conclusions
5. **Participants**: Identify speakers if possible from context

Format your response as a structured markdown document with clear sections.
```

**TemplateType enum extension**:
```csharp
public enum TemplateType
{
    Daily,
    Weekly,
    SystemPrompt,
    BusinessMeeting  // NEW
}
```

**Location**: `src/Infrastructure/Prompts/Templates/business-meeting.md`

**Alternatives considered**:
- Generic meeting template - rejected as less useful for business context
- Multiple specialized templates (standup, planning, retrospective) - deferred to future iterations
- Not bundling a template - rejected as requirement explicitly calls for bundled template

### 7. Interactive Menu UI with Spectre.Console

**Decision**: Use Spectre.Console for interactive selection prompts

**Rationale**:
- Already used throughout codebase (Today, Setup commands)
- Provides rich, cross-platform terminal UI
- SelectionPrompt<T> component for list selection
- Clear, professional appearance

**Pattern for recording selection**:
```csharp
var recordingPrompt = new SelectionPrompt<RecordingListItem>()
    .Title("Select a recording to process:")
    .PageSize(10)
    .MoreChoicesText("[grey](Move up and down to see more recordings)[/]")
    .AddChoices(recordings)
    .UseConverter(r => $"{r.FormattedDate} - {r.WordCount} words");

var selectedRecording = AnsiConsole.Prompt(recordingPrompt);
```

**Pattern for template selection** (reuse from Today):
```csharp
var templatePrompt = new SelectionPrompt<TemplateListItem>()
    .Title("Select a template:")
    .PageSize(10)
    .AddChoices(templates)
    .UseConverter(t => $"{t.Title} - {t.Description}");

var selectedTemplate = AnsiConsole.Prompt(templatePrompt);
```

**Error display**:
```csharp
AnsiConsole.MarkupLine("[red]Error:[/] {0}", error.EscapeMarkup());
```

**Alternatives considered**:
- Terminal.Gui - rejected as Spectre.Console is project standard
- Simple Console.ReadLine - rejected as poor user experience
- Custom UI framework - rejected as unnecessary complexity

### 8. Command-Line Argument Structure

**Decision**: Follow System.CommandLine patterns with `--template` option

**Rationale**:
- System.CommandLine already used for all commands
- Command structure: `tom generate [--template TEMPLATE_NAME]`
- Optional recording selection flag could be added later: `[--recording RECORDING_ID]`

**Command definition**:
```csharp
var generateCommand = new Command("generate", "Process a recording with a prompt template");

var templateOption = new Option<string?>(
    aliases: ["--template", "-t"],
    description: "Template name to use (bypasses interactive selection)")
{
    IsRequired = false
};

generateCommand.AddOption(templateOption);

generateCommand.SetHandler(async (string? templateName) =>
{
    // Handler logic
}, templateOption);
```

**Template name matching**:
- Case-insensitive comparison
- Match against TemplateId or Title
- Clear error if not found, listing available templates

**Alternatives considered**:
- Positional argument for template - rejected as less discoverable
- Separate command per template type - rejected as not scalable
- Config file for defaults - deferred to future iteration

## Summary of Key Decisions

| Area | Decision | Key Reason |
|------|----------|------------|
| LLM Integration | Use ILlmProvider interface | Existing abstraction, already tested |
| Token Limits | Configurable with intelligent truncation | Provider-agnostic, user control |
| Recording Discovery | File-based scanning with StoredRecording model | Consistent with existing patterns |
| Template Selection | Reuse Templates feature services | DRY principle, consistency |
| Output Storage | Recording directory with template suffix | Keeps artifacts together |
| Business Template | Bundled meeting summary template | Common use case, value add |
| UI Framework | Spectre.Console | Project standard |
| CLI Structure | System.CommandLine with --template option | Consistent with other commands |

## Technical Dependencies Confirmed

- **System.CommandLine 2.0-rc**: Command and option definitions
- **Spectre.Console 0.51.1**: Interactive prompts and UI
- **ILlmProvider**: Existing abstraction from Infrastructure/Llm
- **PromptTemplate model**: Shared.Models.PromptTemplate with TemplateType enum
- **StoredRecording model**: Audio.Models.StoredRecording for recording metadata
- **TemplateListItem**: Templates.Models.TemplateListItem for template selection
- **Result<T> pattern**: Shared.Results.Result for error handling
- **System.IO.Abstractions**: File operations (testability)

## Open Questions Resolved

1. **Token limit defaults**: Will use provider-specific safe defaults in LlmConstants
2. **LLM service interface**: ILlmProvider from Infrastructure.Llm
3. **Recording directory location**: From configuration (MemoryDirectory/recording/)
4. **Template storage location**: Embedded resources + MemoryDirectory/templates/
5. **Output file format**: Markdown (.md) with metadata comments

## Implementation Risks

### Low Risk
- Recording file discovery: Well-established pattern
- Template integration: Reusing proven Templates feature
- File I/O operations: Abstracted and testable

### Medium Risk
- Token limit estimation: Heuristic-based, may be imprecise
  - Mitigation: Conservative estimates, user configuration option
- LLM provider errors: Network, rate limits
  - Mitigation: Result<T> pattern with retry prompt, clear error messages

### Low Risk (Mitigated)
- Filename collisions: Template suffix prevents collision
- Overwrite behavior: Explicit in spec, user expectation clear

## Next Steps (Phase 1)

With all research complete, proceed to Phase 1 design artifacts:
1. data-model.md: Define entities and relationships
2. contracts/: Define command/query contracts
3. quickstart.md: Developer onboarding guide
