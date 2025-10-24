# Command Contracts: Generate Feature

**Feature**: Generate Command for Recording Processing
**Date**: 2025-10-24
**Pattern**: CQRS Commands (mutations/writes)

## Overview

Commands represent actions that change state or trigger side effects. In the generate feature, commands handle the core generation workflow including LLM interaction and file output.

---

## GenerateOutputCommand

**Purpose**: Process a recording transcript with a template via LLM and save the output

**Type**: Command (write operation)

**Handler**: `GenerateOutputCommandHandler`

### Command Definition

```csharp
namespace TenSecondTom.Features.Generate.Commands;

/// <summary>
/// Command to generate output from a recording transcript using a prompt template.
/// This is the main command for the 'tom generate' CLI operation.
/// </summary>
public sealed record GenerateOutputCommand : IRequest<Result<GeneratedOutput>>
{
    /// <summary>
    /// Gets the path to the transcript file to process.
    /// Must be a valid path in the recording directory.
    /// </summary>
    public required string TranscriptFilePath { get; init; }

    /// <summary>
    /// Gets the base name of the recording (without extension).
    /// Used for output file naming.
    /// Example: "recording-20251024-143022"
    /// </summary>
    public required string RecordingBaseName { get; init; }

    /// <summary>
    /// Gets the template ID to use for generation.
    /// Must match an existing template in the system.
    /// </summary>
    public required string TemplateId { get; init; }

    /// <summary>
    /// Gets the maximum input tokens allowed for LLM processing.
    /// Transcripts exceeding this limit will be truncated.
    /// </summary>
    public required int MaxInputTokens { get; init; }

    /// <summary>
    /// Gets optional cancellation token for async operations.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = default;
}
```

### Handler Contract

```csharp
namespace TenSecondTom.Features.Generate.Handlers;

/// <summary>
/// Handles generation of outputs from recordings using LLM providers.
/// Orchestrates: transcript loading, template processing, LLM interaction, output storage.
/// </summary>
public sealed class GenerateOutputCommandHandler
    : IRequestHandler<GenerateOutputCommand, Result<GeneratedOutput>>
{
    private readonly IRecordingService _recordingService;
    private readonly ITemplateService _templateService;  // From Templates feature
    private readonly ITranscriptProcessor _transcriptProcessor;
    private readonly ILlmProvider _llmProvider;
    private readonly IOutputStorageService _outputStorageService;
    private readonly ILogger<GenerateOutputCommandHandler> _logger;

    public GenerateOutputCommandHandler(
        IRecordingService recordingService,
        ITemplateService templateService,
        ITranscriptProcessor transcriptProcessor,
        ILlmProvider llmProvider,
        IOutputStorageService outputStorageService,
        ILogger<GenerateOutputCommandHandler> logger)
    {
        _recordingService = recordingService;
        _templateService = templateService;
        _transcriptProcessor = transcriptProcessor;
        _llmProvider = llmProvider;
        _outputStorageService = outputStorageService;
        _logger = logger;
    }

    public async Task<Result<GeneratedOutput>> Handle(
        GenerateOutputCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate transcript file
        var validateResult = await _recordingService.ValidateTranscriptFileAsync(
            request.TranscriptFilePath,
            cancellationToken);

        if (!validateResult.IsSuccess)
            return Result<GeneratedOutput>.Failure(validateResult.Error);

        // 2. Load template
        var templateResult = await _templateService.GetTemplateByIdAsync(
            request.TemplateId,
            cancellationToken);

        if (!templateResult.IsSuccess)
            return Result<GeneratedOutput>.Failure(templateResult.Error);

        var template = templateResult.Value;

        // 3. Load transcript content
        var transcriptResult = await _recordingService.GetTranscriptContentAsync(
            request.TranscriptFilePath,
            cancellationToken);

        if (!transcriptResult.IsSuccess)
            return Result<GeneratedOutput>.Failure(transcriptResult.Error);

        var transcriptContent = transcriptResult.Value;

        // 4. Process transcript (truncate if needed)
        var processedResult = await _transcriptProcessor.ProcessTranscriptAsync(
            transcriptContent,
            request.MaxInputTokens,
            cancellationToken);

        if (!processedResult.IsSuccess)
            return Result<GeneratedOutput>.Failure(processedResult.Error);

        var processed = processedResult.Value;

        // 5. Display truncation warning if applicable
        if (processed.WasTruncated)
        {
            _logger.LogWarning(
                "Transcript truncated from {OriginalWords} to {FinalWords} words",
                processed.OriginalWordCount,
                processed.FinalWordCount);
        }

        // 6. Build prompt by substituting template variables
        var prompt = template.Content.Replace("{{TRANSCRIPT}}", processed.Content);

        // 7. Call LLM provider
        var llmResult = await _llmProvider.GenerateCompletionAsync(
            prompt,
            cancellationToken);

        if (!llmResult.IsSuccess)
            return Result<GeneratedOutput>.Failure(llmResult.Error);

        var llmResponse = llmResult.Value;

        // 8. Build GeneratedOutput
        var output = new GeneratedOutput
        {
            Content = llmResponse.Content,
            RecordingBaseName = request.RecordingBaseName,
            TemplateId = template.TemplateId,
            TemplateTitle = template.Metadata?.Title ?? template.TemplateId,
            GeneratedAt = DateTimeOffset.UtcNow,
            ProviderName = _llmProvider.ProviderName,
            ModelName = _llmProvider.ModelName,
            InputTokens = llmResponse.InputTokens,
            OutputTokens = llmResponse.OutputTokens,
            WasTruncated = processed.WasTruncated,
            OriginalWordCount = processed.OriginalWordCount
        };

        // 9. Save output to filesystem
        var saveResult = await _outputStorageService.SaveOutputAsync(
            output,
            cancellationToken);

        if (!saveResult.IsSuccess)
            return Result<GeneratedOutput>.Failure(saveResult.Error);

        output = output with { OutputFilePath = saveResult.Value };

        // 10. Return success
        _logger.LogInformation(
            "Generated output for {Recording} using {Template}: {OutputPath}",
            request.RecordingBaseName,
            template.TemplateId,
            output.OutputFilePath);

        return Result<GeneratedOutput>.Success(output);
    }
}
```

### Success Response

```csharp
Result<GeneratedOutput>.Success(output)
```

Where `output` contains:
- `Content`: Generated text from LLM
- `OutputFilePath`: Path where file was saved
- Token usage metadata
- Truncation information (if applicable)

### Failure Responses

| Error Scenario | Error Message | HTTP Equivalent |
|----------------|---------------|-----------------|
| Transcript file not found | "Transcript file not found: {path}" | 404 Not Found |
| Template not found | "Template not found: {templateId}" | 404 Not Found |
| Transcript unreadable | "Unable to read transcript: {reason}" | 500 Internal Server Error |
| Token limit exceeded | "Transcript too large even after truncation" | 413 Payload Too Large |
| LLM provider error | "LLM generation failed: {error}" | 502 Bad Gateway |
| Network timeout | "LLM request timed out after {seconds}s" | 504 Gateway Timeout |
| Rate limit exceeded | "Rate limit exceeded. Try again in {seconds}s" | 429 Too Many Requests |
| Output save failed | "Failed to save output: {reason}" | 500 Internal Server Error |

### Validation

Validation occurs in handler (not separate validator class for simplicity):

```csharp
// Pre-handler validation checks:
if (string.IsNullOrWhiteSpace(request.TranscriptFilePath))
    return Result<GeneratedOutput>.Failure("TranscriptFilePath is required");

if (string.IsNullOrWhiteSpace(request.RecordingBaseName))
    return Result<GeneratedOutput>.Failure("RecordingBaseName is required");

if (string.IsNullOrWhiteSpace(request.TemplateId))
    return Result<GeneratedOutput>.Failure("TemplateId is required");

if (request.MaxInputTokens <= 0)
    return Result<GeneratedOutput>.Failure("MaxInputTokens must be positive");
```

### Side Effects

1. **File System**: Creates/overwrites output file in recording directory
2. **LLM Provider**: Consumes API tokens (cost implications)
3. **Logging**: Structured logs for audit trail

### Example Usage

```csharp
// In CLI handler or test
var command = new GenerateOutputCommand
{
    TranscriptFilePath = "/path/to/recording-20251024-143022.txt",
    RecordingBaseName = "recording-20251024-143022",
    TemplateId = "business-meeting",
    MaxInputTokens = 8000,
    CancellationToken = cancellationToken
};

var result = await mediator.Send(command);

if (result.IsSuccess)
{
    Console.WriteLine($"Generated output saved to: {result.Value.OutputFilePath}");
    Console.WriteLine($"Tokens used: {result.Value.TotalTokens}");
}
else
{
    Console.Error.WriteLine($"Error: {result.Error}");
}
```

---

## Future Commands (Out of Scope for v1)

### RegenerateOutputCommand
**Purpose**: Regenerate output with modified template (same recording)
**When**: Future iteration for template tuning

### BatchGenerateCommand
**Purpose**: Process multiple recordings with same template
**When**: Future iteration for bulk processing

---

## Testing Checklist

### Unit Tests
- [x] Command handler with valid inputs returns Success
- [x] Command handler with missing transcript returns Failure
- [x] Command handler with missing template returns Failure
- [x] Command handler with LLM error returns appropriate Failure
- [x] Command handler with truncated transcript logs warning
- [x] Command handler saves output to correct path
- [x] Command handler includes metadata in GeneratedOutput

### Integration Tests
- [x] End-to-end command execution via CLI
- [x] File system interaction (reading transcript, writing output)
- [x] LLM provider interaction (mock responses)
- [x] Error handling for network failures
- [x] Retry behavior on transient errors

---

**Related Contracts**: See [queries.md](./queries.md) for read operations
