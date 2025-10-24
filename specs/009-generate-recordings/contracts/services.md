# Service Contracts: Generate Feature

**Feature**: Generate Command for Recording Processing
**Date**: 2025-10-24
**Pattern**: Domain Services (business logic encapsulation)

## Overview

Services encapsulate domain logic and cross-cutting concerns. These interfaces define contracts for recording operations, transcript processing, and output storage.

---

## IRecordingService

**Purpose**: Recording discovery, validation, and transcript loading

**Location**: `src/Features/Generate/Services/IRecordingService.cs`

**Implementation**: `RecordingService.cs`

### Interface Definition

```csharp
namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for recording file operations including discovery and transcript loading.
/// Abstracts filesystem operations for testability.
/// </summary>
public interface IRecordingService
{
    /// <summary>
    /// Lists all available recordings from the recording directory.
    /// Returns recordings sorted by RecordedAt descending (newest first).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing list of recordings or error.</returns>
    Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the full transcript content from a transcript file.
    /// </summary>
    /// <param name="transcriptFilePath">Full path to transcript file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing transcript content or error.</returns>
    Task<Result<string>> GetTranscriptContentAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a transcript file exists and is readable.
    /// </summary>
    /// <param name="transcriptFilePath">Full path to transcript file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with error message.</returns>
    Task<Result> ValidateTranscriptFileAsync(
        string transcriptFilePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses recording metadata from filename.
    /// Expected format: recording-YYYYMMdd-HHmmss.txt
    /// </summary>
    /// <param name="filename">Filename to parse.</param>
    /// <returns>Result containing RecordedAt timestamp or error.</returns>
    Result<DateTimeOffset> ParseRecordingTimestamp(string filename);
}
```

### Implementation Contract

**Dependencies**:
- `IFileSystem` (System.IO.Abstractions)
- `IConfiguration` (for recording directory path)
- `ILogger<RecordingService>`

**Behavior**:
```csharp
public sealed class RecordingService : IRecordingService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _recordingDirectory;
    private readonly ILogger<RecordingService> _logger;

    public RecordingService(
        IFileSystem fileSystem,
        IConfiguration configuration,
        ILogger<RecordingService> logger)
    {
        _fileSystem = fileSystem;
        _recordingDirectory = Path.Combine(
            configuration[ConfigurationKeys.MemoryDirectory]!,
            DirectoryNames.Recording);
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<RecordingListItem>>> ListRecordingsAsync(
        CancellationToken cancellationToken = default)
    {
        // 1. Check directory exists
        if (!_fileSystem.Directory.Exists(_recordingDirectory))
            return Result<IReadOnlyList<RecordingListItem>>.Failure(
                $"Recording directory not found: {_recordingDirectory}");

        // 2. Scan for transcript files
        var files = _fileSystem.Directory.GetFiles(
            _recordingDirectory,
            "recording-*.txt",
            SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
            return Result<IReadOnlyList<RecordingListItem>>.Failure(
                "No recordings found. Use 'tom record' to create a recording first.");

        // 3. Parse and build RecordingListItem for each file
        var recordings = new List<RecordingListItem>();
        foreach (var filePath in files)
        {
            try
            {
                var fileInfo = _fileSystem.FileInfo.New(filePath);
                var baseName = Path.GetFileNameWithoutExtension(filePath);
                var timestampResult = ParseRecordingTimestamp(fileInfo.Name);

                if (!timestampResult.IsSuccess)
                {
                    _logger.LogWarning(
                        "Skipping file with invalid name: {Path}",
                        filePath);
                    continue;
                }

                var content = await _fileSystem.File.ReadAllTextAsync(
                    filePath,
                    cancellationToken);

                recordings.Add(new RecordingListItem
                {
                    RecordingBaseName = baseName,
                    TranscriptFilePath = filePath,
                    RecordedAt = timestampResult.Value,
                    FormattedDate = timestampResult.Value.ToString("MMM dd, yyyy h:mm tt"),
                    WordCount = CountWords(content),
                    FileSizeBytes = fileInfo.Length
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error processing recording file: {Path}",
                    filePath);
            }
        }

        // 4. Sort by date descending
        var sorted = recordings
            .OrderByDescending(r => r.RecordedAt)
            .ToList();

        return Result<IReadOnlyList<RecordingListItem>>.Success(sorted);
    }

    // Additional methods: GetTranscriptContentAsync, ValidateTranscriptFileAsync, etc.
}
```

---

## ITranscriptProcessor

**Purpose**: Token limit enforcement and intelligent truncation

**Location**: `src/Features/Generate/Services/ITranscriptProcessor.cs`

**Implementation**: `TranscriptProcessor.cs`

### Interface Definition

```csharp
namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for processing transcripts to fit within LLM token limits.
/// Handles token estimation and intelligent truncation.
/// </summary>
public interface ITranscriptProcessor
{
    /// <summary>
    /// Processes a transcript to fit within token limits.
    /// Truncates intelligently if needed, preserving beginning of content.
    /// </summary>
    /// <param name="transcriptContent">Full transcript content.</param>
    /// <param name="maxInputTokens">Maximum tokens allowed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing processed transcript with truncation metadata.</returns>
    Task<Result<TruncatedTranscript>> ProcessTranscriptAsync(
        string transcriptContent,
        int maxInputTokens,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates token count from text using conservative heuristic.
    /// Formula: words * 1.3
    /// </summary>
    /// <param name="text">Text to analyze.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTokenCount(string text);

    /// <summary>
    /// Counts words in text using whitespace splitting.
    /// </summary>
    /// <param name="text">Text to analyze.</param>
    /// <returns>Word count.</returns>
    int CountWords(string text);

    /// <summary>
    /// Truncates text to fit within target word count while preserving sentence boundaries.
    /// Algorithm:
    /// 1. Split text into words
    /// 2. Take first N words up to targetWordCount
    /// 3. Search for last period (.) within final 10% of truncated content
    /// 4. If period found, trim to period + 1 (include period)
    /// 5. If no period, return hard word boundary
    /// </summary>
    /// <param name="text">Text to truncate.</param>
    /// <param name="targetWordCount">Target word count after truncation.</param>
    /// <returns>Truncated text.</returns>
    string TruncateToWordCount(string text, int targetWordCount);
}
```

### Implementation Contract

**Dependencies**:
- `ILogger<TranscriptProcessor>`

**Behavior**:
```csharp
public sealed class TranscriptProcessor : ITranscriptProcessor
{
    private readonly ILogger<TranscriptProcessor> _logger;

    public TranscriptProcessor(ILogger<TranscriptProcessor> logger)
    {
        _logger = logger;
    }

    public async Task<Result<TruncatedTranscript>> ProcessTranscriptAsync(
        string transcriptContent,
        int maxInputTokens,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcriptContent))
            return Result<TruncatedTranscript>.Failure("Transcript content is empty");

        if (maxInputTokens <= 0)
            return Result<TruncatedTranscript>.Failure("MaxInputTokens must be positive");

        var originalWordCount = CountWords(transcriptContent);
        var estimatedTokens = EstimateTokenCount(transcriptContent);

        // Apply safety factor (keep at 80% of limit)
        var safeTokenLimit = (int)(maxInputTokens * LlmConstants.TruncationSafetyFactor);

        if (estimatedTokens <= safeTokenLimit)
        {
            // No truncation needed
            return Result<TruncatedTranscript>.Success(new TruncatedTranscript
            {
                Content = transcriptContent,
                WasTruncated = false,
                OriginalWordCount = originalWordCount,
                FinalWordCount = originalWordCount,
                EstimatedTokenCount = estimatedTokens
            });
        }

        // Truncation needed
        _logger.LogWarning(
            "Transcript exceeds token limit: {Estimated} > {Limit}. Truncating...",
            estimatedTokens,
            safeTokenLimit);

        // Calculate target word count
        var targetWordCount = (int)(safeTokenLimit / LlmConstants.TokensPerWord);
        var truncatedContent = TruncateToWordCount(transcriptContent, targetWordCount);
        var finalWordCount = CountWords(truncatedContent);
        var finalTokens = EstimateTokenCount(truncatedContent);

        return Result<TruncatedTranscript>.Success(new TruncatedTranscript
        {
            Content = truncatedContent,
            WasTruncated = true,
            OriginalWordCount = originalWordCount,
            FinalWordCount = finalWordCount,
            EstimatedTokenCount = finalTokens
        });
    }

    public int EstimateTokenCount(string text)
    {
        var words = CountWords(text);
        return (int)(words * LlmConstants.TokensPerWord);
    }

    public int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        return text.Split(
                [' ', '\t', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    public string TruncateToWordCount(string text, int targetWordCount)
    {
        var words = text.Split(
            [' ', '\t', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries);

        if (words.Length <= targetWordCount)
            return text;

        var truncated = string.Join(" ", words.Take(targetWordCount));

        // Try to end on sentence boundary
        var lastPeriod = truncated.LastIndexOf('.');
        if (lastPeriod > truncated.Length * 0.9) // Only if near the end
            truncated = truncated[..(lastPeriod + 1)];

        return truncated;
    }
}
```

---

## IOutputStorageService

**Purpose**: Saving generated outputs to filesystem

**Location**: `src/Features/Generate/Services/IOutputStorageService.cs`

**Implementation**: `OutputStorageService.cs`

### Interface Definition

```csharp
namespace TenSecondTom.Features.Generate.Services;

/// <summary>
/// Service for storing generated outputs to the recording directory.
/// Handles file naming, overwrite behavior, and metadata embedding.
/// </summary>
public interface IOutputStorageService
{
    /// <summary>
    /// Saves generated output to recording directory as markdown file.
    /// Overwrites existing file if already present (same recording + template).
    /// </summary>
    /// <param name="output">Generated output with content and metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing output file path or error.</returns>
    Task<Result<string>> SaveOutputAsync(
        GeneratedOutput output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if output file already exists for recording/template combination.
    /// </summary>
    /// <param name="recordingBaseName">Recording base name.</param>
    /// <param name="templateId">Template ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if file exists, false otherwise.</returns>
    Task<bool> OutputExistsAsync(
        string recordingBaseName,
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the output file path for a recording/template combination.
    /// Format: M-D-Y_TemplateName_Increment.md
    /// Example: recordingBaseName="10-21-2025_1", templateFilename="daily-summary" → "10-21-2025_daily-summary_1.md"
    /// </summary>
    /// <param name="recordingBaseName">Recording base name in M-D-Y_Increment format (e.g., "10-21-2025_1").</param>
    /// <param name="templateId">Template filename without extension (e.g., "daily-summary").</param>
    /// <returns>Full file path in recording directory.</returns>
    string BuildOutputFilePath(string recordingBaseName, string templateId);
}
```

### Implementation Contract

**Dependencies**:
- `IFileSystem` (System.IO.Abstractions)
- `IConfiguration` (for recording directory path)
- `ILogger<OutputStorageService>`

**Behavior**:
```csharp
public sealed class OutputStorageService : IOutputStorageService
{
    private readonly IFileSystem _fileSystem;
    private readonly string _recordingDirectory;
    private readonly ILogger<OutputStorageService> _logger;

    public OutputStorageService(
        IFileSystem fileSystem,
        IConfiguration configuration,
        ILogger<OutputStorageService> logger)
    {
        _fileSystem = fileSystem;
        _recordingDirectory = Path.Combine(
            configuration[ConfigurationKeys.MemoryDirectory]!,
            DirectoryNames.Recording);
        _logger = logger;
    }

    public async Task<Result<string>> SaveOutputAsync(
        GeneratedOutput output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate directory exists
            if (!_fileSystem.Directory.Exists(_recordingDirectory))
                return Result<string>.Failure(
                    $"Recording directory not found: {_recordingDirectory}");

            // 2. Build file path
            var outputPath = BuildOutputFilePath(
                output.RecordingBaseName,
                output.TemplateId);

            // 3. Format content with metadata
            var markdown = output.ToMarkdown();

            // 4. Write file (overwrite if exists)
            await _fileSystem.File.WriteAllTextAsync(
                outputPath,
                markdown,
                cancellationToken);

            _logger.LogInformation(
                "Saved output to: {Path} ({Size} bytes)",
                outputPath,
                markdown.Length);

            return Result<string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save output for {Recording}/{Template}",
                output.RecordingBaseName,
                output.TemplateId);

            return Result<string>.Failure(
                $"Failed to save output: {ex.Message}");
        }
    }

    public Task<bool> OutputExistsAsync(
        string recordingBaseName,
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var outputPath = BuildOutputFilePath(recordingBaseName, templateId);
        return Task.FromResult(_fileSystem.File.Exists(outputPath));
    }

    public string BuildOutputFilePath(string recordingBaseName, string templateId)
    {
        var fileName = $"{recordingBaseName}_{templateId}.md";
        return Path.Combine(_recordingDirectory, fileName);
    }
}
```

---

## Service Registration

All services must be registered in `DependencyInjection.cs`:

```csharp
namespace TenSecondTom.Features.Generate;

public static class GenerateFeatureExtensions
{
    public static IServiceCollection AddGenerateFeature(this IServiceCollection services)
    {
        // Register services
        services.AddTransient<IRecordingService, RecordingService>();
        services.AddTransient<ITranscriptProcessor, TranscriptProcessor>();
        services.AddTransient<IOutputStorageService, OutputStorageService>();

        // Register command/query handlers
        services.AddTransient<GenerateOutputCommandHandler>();
        services.AddTransient<IRequestHandler<GenerateOutputCommand, Result<GeneratedOutput>>>(
            sp => sp.GetRequiredService<GenerateOutputCommandHandler>());

        services.AddTransient<ListRecordingsQueryHandler>();
        services.AddTransient<IRequestHandler<ListRecordingsQuery, Result<IReadOnlyList<RecordingListItem>>>>(
            sp => sp.GetRequiredService<ListRecordingsQueryHandler>());

        services.AddTransient<GetRecordingTranscriptQueryHandler>();
        services.AddTransient<IRequestHandler<GetRecordingTranscriptQuery, Result<string>>>(
            sp => sp.GetRequiredService<GetRecordingTranscriptQueryHandler>());

        return services;
    }
}
```

---

## Testing Strategy

### Unit Tests

**IRecordingService**:
- Mock `IFileSystem` to test file operations without actual filesystem
- Test filename parsing with valid/invalid formats
- Test sorting by date
- Test error handling for missing directories

**ITranscriptProcessor**:
- Test token estimation accuracy
- Test truncation at various word counts
- Test sentence boundary preservation
- Test edge cases (empty, very short, very long transcripts)

**IOutputStorageService**:
- Mock `IFileSystem` for file write tests
- Test file path building
- Test overwrite behavior
- Test metadata formatting

### Integration Tests
- Test services with real `FileSystem` implementation
- Test end-to-end workflows
- Test concurrent operations

---

**Related Contracts**: See [commands.md](./commands.md) and [queries.md](./queries.md) for CQRS operations
