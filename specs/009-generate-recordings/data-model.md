# Data Model: Generate Command

**Feature Branch**: `009-generate-recordings`
**Date**: 2025-10-24
**Phase**: 1 (Design & Contracts)

## Overview

This document defines the core entities, value objects, and their relationships for the generate command feature. The data model follows Domain-Driven Design principles with rich domain models containing behavior, not just data.

## Core Entities

### 1. RecordingListItem

**Purpose**: Lightweight display model for recording selection UI

**Type**: Record (immutable value object)

**Properties**:
```csharp
public sealed record RecordingListItem
{
    /// <summary>
    /// Gets the base name of the recording (without extension).
    /// Format: M-D-Y_Increment
    /// Example: "10-21-2025_1"
    /// </summary>
    public required string RecordingBaseName { get; init; }

    /// <summary>
    /// Gets the full path to the transcript file.
    /// </summary>
    public required string TranscriptFilePath { get; init; }

    /// <summary>
    /// Gets the recording timestamp parsed from filename.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets the formatted display date for UI.
    /// Format: "Oct 24, 2025 2:30 PM"
    /// </summary>
    public required string FormattedDate { get; init; }

    /// <summary>
    /// Gets the word count of the transcript.
    /// </summary>
    public required int WordCount { get; init; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>
    /// Gets formatted file size for display (e.g., "12.5 KB").
    /// </summary>
    public string FormattedFileSize => FormatFileSize(FileSizeBytes);

    /// <summary>
    /// Gets the display label for selection UI.
    /// Format: "Oct 24, 2025 2:30 PM • 234 words • 1.2 KB"
    /// </summary>
    public string DisplayLabel => $"{FormattedDate} • {WordCount} words • {FormattedFileSize}";

    private static string FormatFileSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
}
```

**Validation Rules**:
- RecordingBaseName must start with "recording-" prefix
- RecordedAt must be valid past date (not future)
- WordCount must be non-negative
- FileSizeBytes must be positive
- TranscriptFilePath must exist and be readable

**Factory Pattern**:
```csharp
public static class RecordingListItemFactory
{
    public static Result<RecordingListItem> Create(
        string transcriptFilePath,
        FileInfo fileInfo)
    {
        // Parse filename, validate, create instance
    }
}
```

### 2. GeneratedOutput

**Purpose**: Result of LLM processing with metadata

**Type**: Record (immutable value object)

**Properties**:
```csharp
public sealed record GeneratedOutput
{
    /// <summary>
    /// Gets the generated content from LLM.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets the base name of the source recording.
    /// </summary>
    public required string RecordingBaseName { get; init; }

    /// <summary>
    /// Gets the template ID used for generation.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the template title for display.
    /// </summary>
    public required string TemplateTitle { get; init; }

    /// <summary>
    /// Gets the timestamp when output was generated.
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Gets the LLM provider used.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// Gets the model used for generation.
    /// </summary>
    public required string ModelName { get; init; }

    /// <summary>
    /// Gets the number of input tokens consumed.
    /// </summary>
    public required int InputTokens { get; init; }

    /// <summary>
    /// Gets the number of output tokens generated.
    /// </summary>
    public required int OutputTokens { get; init; }

    /// <summary>
    /// Gets whether the input was truncated due to token limits.
    /// </summary>
    public required bool WasTruncated { get; init; }

    /// <summary>
    /// Gets the original transcript word count (before truncation).
    /// </summary>
    public required int OriginalWordCount { get; init; }

    /// <summary>
    /// Gets the output file path where content was saved.
    /// </summary>
    public string? OutputFilePath { get; init; }

    /// <summary>
    /// Gets the total tokens used.
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// Formats the output as markdown with metadata header.
    /// </summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!-- Generated by Ten Second Tom -->");
        sb.AppendLine($"<!-- Recording: {RecordingBaseName}.txt -->");
        sb.AppendLine($"<!-- Template: {TemplateId} ({TemplateTitle}) -->");
        sb.AppendLine($"<!-- Generated: {GeneratedAt:O} -->");
        sb.AppendLine($"<!-- Provider: {ProviderName} ({ModelName}) -->");
        sb.AppendLine($"<!-- Tokens: {InputTokens} input, {OutputTokens} output -->");
        if (WasTruncated)
        {
            sb.AppendLine($"<!-- Truncated: Yes (original {OriginalWordCount} words) -->");
        }
        sb.AppendLine();
        sb.AppendLine(Content);
        return sb.ToString();
    }
}
```

**Validation Rules**:
- Content must not be null or empty
- Token counts must be non-negative
- GeneratedAt must be valid timestamp
- ProviderName and ModelName must match configured values

### 3. GenerationRequest

**Purpose**: Input parameters for generation operation

**Type**: Record (command parameter object)

**Properties**:
```csharp
public sealed record GenerationRequest
{
    /// <summary>
    /// Gets the transcript file path to process.
    /// </summary>
    public required string TranscriptFilePath { get; init; }

    /// <summary>
    /// Gets the base name of the recording.
    /// </summary>
    public required string RecordingBaseName { get; init; }

    /// <summary>
    /// Gets the template to use for generation.
    /// </summary>
    public required PromptTemplate Template { get; init; }

    /// <summary>
    /// Gets the maximum input tokens allowed.
    /// </summary>
    public required int MaxInputTokens { get; init; }

    /// <summary>
    /// Gets optional cancellation token.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = default;
}
```

**Validation Rules**:
- TranscriptFilePath must exist
- RecordingBaseName must be valid format
- Template must be non-null with valid content
- MaxInputTokens must be positive

### 4. TruncatedTranscript

**Purpose**: Transcript processed for token limit compliance

**Type**: Record (value object)

**Properties**:
```csharp
public sealed record TruncatedTranscript
{
    /// <summary>
    /// Gets the (possibly truncated) transcript content.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets whether truncation occurred.
    /// </summary>
    public required bool WasTruncated { get; init; }

    /// <summary>
    /// Gets the original word count before truncation.
    /// </summary>
    public required int OriginalWordCount { get; init; }

    /// <summary>
    /// Gets the final word count after truncation.
    /// </summary>
    public required int FinalWordCount { get; init; }

    /// <summary>
    /// Gets the estimated token count of the content.
    /// </summary>
    public required int EstimatedTokenCount { get; init; }

    /// <summary>
    /// Creates a warning message if truncation occurred.
    /// </summary>
    public string? GetTruncationWarning() =>
        WasTruncated
            ? $"⚠️  Transcript truncated from {OriginalWordCount} to {FinalWordCount} words to fit within token limit"
            : null;
}
```

## Domain Services

### IRecordingService

**Purpose**: Recording file discovery and transcript loading

**Operations**:
```csharp
public interface IRecordingService
{
    /// <summary>
    /// Lists all available recordings sorted by date (newest first).
    /// </summary>
    Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the transcript content for a specific recording.
    /// </summary>
    Task<Result<string>> GetTranscriptContentAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a transcript file exists and is readable.
    /// </summary>
    Task<Result> ValidateTranscriptFileAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default);
}
```

### IOutputStorageService

**Purpose**: Saving generated outputs to filesystem

**Operations**:
```csharp
public interface IOutputStorageService
{
    /// <summary>
    /// Saves generated output to recording directory.
    /// Overwrites if file already exists (same recording + template).
    /// </summary>
    Task<Result<string>> SaveOutputAsync(
        GeneratedOutput output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if output file already exists for recording/template combination.
    /// </summary>
    Task<bool> OutputExistsAsync(
        string recordingBaseName,
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the output file path for a recording/template combination.
    /// </summary>
    string BuildOutputFilePath(string recordingBaseName, string templateId);
}
```

### ITranscriptProcessor

**Purpose**: Token limit enforcement and transcript truncation

**Operations**:
```csharp
public interface ITranscriptProcessor
{
    /// <summary>
    /// Processes transcript to fit within token limits.
    /// Truncates intelligently if needed.
    /// </summary>
    Task<Result<TruncatedTranscript>> ProcessTranscriptAsync(
        string transcriptContent,
        int maxInputTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates token count from text.
    /// Uses heuristic: words * 1.3
    /// </summary>
    int EstimateTokenCount(string text);

    /// <summary>
    /// Estimates word count from text.
    /// </summary>
    int CountWords(string text);
}
```

## Relationships

### Entity Relationship Diagram

```text
┌─────────────────────────┐
│  RecordingListItem      │
│  (Selection UI)         │
└────────────┬────────────┘
             │ selected by user
             │
             ▼
┌─────────────────────────┐      ┌─────────────────────────┐
│  PromptTemplate         │◄─────│  GenerationRequest      │
│  (from Templates)       │      │  (Input Parameters)     │
└─────────────────────────┘      └────────────┬────────────┘
                                              │
                                              │ processed by
                                              │
                                              ▼
                                 ┌─────────────────────────┐
                                 │  ILlmProvider           │
                                 │  (Infrastructure)       │
                                 └────────────┬────────────┘
                                              │
                                              │ produces
                                              │
                                              ▼
                                 ┌─────────────────────────┐
                                 │  GeneratedOutput        │
                                 │  (Result + Metadata)    │
                                 └────────────┬────────────┘
                                              │
                                              │ saved by
                                              │
                                              ▼
                                 ┌─────────────────────────┐
                                 │  IOutputStorageService  │
                                 │  (Filesystem)           │
                                 └─────────────────────────┘
```

### Data Flow

```text
1. User runs 'tom generate' or 'tom generate --template "name"'
                    │
                    ▼
2. List recordings via IRecordingService
   → Returns List<RecordingListItem>
                    │
                    ▼
3. User selects recording (interactive or auto-select if --template)
                    │
                    ▼
4. List templates via Templates.ListTemplatesQuery
   → Returns List<TemplateListItem>
                    │
                    ▼
5. User selects template (interactive or use --template value)
                    │
                    ▼
6. Load transcript content via IRecordingService
                    │
                    ▼
7. Process transcript via ITranscriptProcessor
   → Returns TruncatedTranscript (possibly truncated)
                    │
                    ▼
8. Build prompt by substituting {{TRANSCRIPT}} in template
                    │
                    ▼
9. Call ILlmProvider.GenerateCompletionAsync()
   → Returns Result<LlmResponse>
                    │
                    ▼
10. Build GeneratedOutput with metadata
                    │
                    ▼
11. Save via IOutputStorageService
    → Returns output file path
                    │
                    ▼
12. Display output to user + confirm save location
```

## Constants and Configuration

### New Constants Required

**CommandNames.cs**:
```csharp
public const string Generate = "generate";
```

**TemplateConstants.cs**:
```csharp
/// <summary>
/// Template identifier for bundled business meeting template.
/// This is the template FILENAME ("business-meeting" from "business-meeting.md"),
/// not the template TYPE (TemplateType.BusinessMeeting enum value).
/// Template selection and output filenames use the filename, not the type.
/// </summary>
public const string BusinessMeetingTemplateId = "business-meeting";

// Add to IsDefaultTemplate method
public static bool IsDefaultTemplate(string templateId)
{
    return templateId.Equals(DailySummaryTemplateId, StringComparison.OrdinalIgnoreCase) ||
           templateId.Equals(WeeklyReviewTemplateId, StringComparison.OrdinalIgnoreCase) ||
           templateId.Equals(BusinessMeetingTemplateId, StringComparison.OrdinalIgnoreCase);
}
```

**LlmConstants.cs** (extend existing file):
```csharp
public static class LlmConstants
{
    // Existing model identifiers, display names, etc. already defined in this file

    /// <summary>
    /// Default maximum input tokens for OpenAI models (safe limit for 128K context).
    /// GPT-4o and GPT-4o Mini both support 128K context windows.
    /// </summary>
    public const int DefaultMaxInputTokensOpenAI = 50_000;

    /// <summary>
    /// Default maximum input tokens for Anthropic models (safe limit for 200K context).
    /// Claude 3/3.5/4 Haiku, Sonnet, and Opus support 200K standard context windows.
    /// Note: Sonnet 4 can use up to 1M tokens via API at higher cost.
    /// </summary>
    public const int DefaultMaxInputTokensAnthropic = 80_000;

    /// <summary>
    /// Token estimation multiplier (conservative).
    /// Estimated tokens = words * TokensPerWord
    /// Based on typical English text tokenization.
    /// </summary>
    public const double TokensPerWord = 1.3;

    /// <summary>
    /// Truncation safety factor (keep input at 80% of limit).
    /// Provides buffer for template content and prompt formatting.
    /// </summary>
    public const double TruncationSafetyFactor = 0.8;

    /// <summary>
    /// Context window sizes by model (for reference and validation).
    /// </summary>
    public static class ContextWindows
    {
        // OpenAI Models
        public const int Gpt4oMini = 128_000;        // 128K input + output combined
        public const int Gpt4o = 128_000;            // 128K input + output combined

        // Anthropic Models (standard context)
        public const int Claude3Haiku = 200_000;     // 200K context
        public const int Claude35Haiku = 200_000;    // 200K context
        public const int ClaudeSonnet4 = 200_000;    // 200K standard (1M via API)
        public const int ClaudeSonnet45 = 200_000;   // 200K standard (1M beta)
        public const int ClaudeOpus4 = 200_000;      // 200K context
        public const int ClaudeOpus41 = 200_000;     // 200K context
    }

    /// <summary>
    /// Maximum output tokens by model (for reference).
    /// </summary>
    public static class MaxOutputTokens
    {
        // OpenAI Models
        public const int Gpt4oMini = 16_384;
        public const int Gpt4o = 16_384;

        // Anthropic Models (typical output limits)
        public const int Claude3Haiku = 8_192;
        public const int Claude35Haiku = 8_192;
        public const int ClaudeSonnet4 = 8_192;      // Typical, can be higher
        public const int ClaudeSonnet45 = 8_192;     // Typical, can be higher
        public const int ClaudeOpus4 = 8_192;        // Typical, can be higher
        public const int ClaudeOpus41 = 8_192;       // Typical, can be higher
    }
}
```

**ConfigurationKeys.cs**:
```csharp
/// <summary>
/// Configuration key for maximum input tokens for LLM processing.
/// Environment variable: TenSecondTom__Llm__MaxInputTokens
/// </summary>
public const string LlmMaxInputTokens = "TenSecondTom:Llm:MaxInputTokens";
```

**DirectoryNames.cs** (verify if exists):
```csharp
public const string Recording = "recording";
```

### TemplateType Enum Extension

```csharp
public enum TemplateType
{
    Daily,
    DailySummary = Daily,
    Weekly,
    WeeklySummary = Weekly,
    SystemPrompt,
    BusinessMeeting  // NEW
}
```

## Validation Rules Summary

### Recording Selection
- Transcript file must exist
- File must be readable
- Filename must match pattern: `recording-YYYYMMdd-HHmmss.txt`
- File must not be empty

### Template Selection
- Template must exist in system
- Template must have valid content
- Template must have {{TRANSCRIPT}} placeholder (or similar variable)
- Template name matching is case-insensitive

### Token Processing
- Estimated tokens must not exceed MaxInputTokens * 1.2 (some buffer)
- If truncation needed, warn user before proceeding
- Truncation preserves beginning of transcript (chronological order)

### Output Storage
- Output directory must be writable
- Filename must not contain invalid characters
- Overwrite existing output for same recording/template without prompt

## Error Handling

### Expected Errors (Return Result<T>.Failure)
- No recordings found in directory
- No templates configured
- Template name not found (--template argument)
- Transcript file unreadable
- LLM provider network error
- LLM provider rate limit
- Token limit exceeded despite truncation
- File write permission denied

### Unexpected Errors (Throw Exception)
- Null reference in required dependencies
- Configuration corruption
- File system unavailable

## Performance Considerations

### Caching Strategies
- Template list: Cache after first load (invalidate on template changes)
- Recording list: Re-scan on each invocation (to catch new recordings)
- No caching of LLM responses (each generation is unique)

### Resource Limits
- Max transcript size: 100MB (sanity check)
- Max output size: 10MB (sanity check)
- Timeout for LLM calls: 120 seconds (configurable)

## Testing Strategy

### Unit Tests Required
- RecordingListItemFactory.Create() with various filename formats
- TruncatedTranscript token estimation accuracy
- GeneratedOutput.ToMarkdown() formatting
- TranscriptProcessor truncation logic
- OutputStorageService path building

### Integration Tests Required
- End-to-end: recording selection → template selection → generation → file save
- Error scenarios: missing recordings, missing templates, LLM failures
- File system operations with System.IO.Abstractions mocks

### Test Data
- Sample transcripts: 100 words, 1000 words, 10000 words
- Sample templates: minimal, with metadata, with multiple variables
- Mock LLM responses: success, error, timeout scenarios

---

**Next Phase**: Generate API contracts defining command/query signatures
