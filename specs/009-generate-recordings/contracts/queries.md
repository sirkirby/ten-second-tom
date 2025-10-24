# Query Contracts: Generate Feature

**Feature**: Generate Command for Recording Processing
**Date**: 2025-10-24
**Pattern**: CQRS Queries (read-only operations)

## Overview

Queries represent read-only operations that retrieve data without side effects. In the generate feature, queries handle recording discovery and transcript loading.

---

## ListRecordingsQuery

**Purpose**: Retrieve all available recordings from the recording directory

**Type**: Query (read-only)

**Handler**: `ListRecordingsQueryHandler`

### Query Definition

```csharp
namespace TenSecondTom.Features.Generate.Queries;

/// <summary>
/// Query to list all available recordings sorted by date (newest first).
/// Used for interactive selection UI.
/// </summary>
public sealed record ListRecordingsQuery : IRequest<Result<IReadOnlyList<RecordingListItem>>>
{
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
/// Handles listing of available recordings from the recording directory.
/// Scans filesystem, parses metadata, sorts by date.
/// </summary>
public sealed class ListRecordingsQueryHandler
    : IRequestHandler<ListRecordingsQuery, Result<IReadOnlyList<RecordingListItem>>>
{
    private readonly IRecordingService _recordingService;
    private readonly ILogger<ListRecordingsQueryHandler> _logger;

    public ListRecordingsQueryHandler(
        IRecordingService recordingService,
        ILogger<ListRecordingsQueryHandler> logger)
    {
        _recordingService = recordingService;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<RecordingListItem>>> Handle(
        ListRecordingsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Listing recordings from recording directory");

        var result = await _recordingService.ListRecordingsAsync(cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError("Failed to list recordings: {Error}", result.Error);
            return result;
        }

        var recordings = result.Value;

        _logger.LogInformation("Found {Count} recordings", recordings.Count);

        return Result<IReadOnlyList<RecordingListItem>>.Success(recordings);
    }
}
```

### Success Response

```csharp
Result<IReadOnlyList<RecordingListItem>>.Success(recordings)
```

Where `recordings` is a list sorted by `RecordedAt` descending (newest first).

Example:
```csharp
[
    RecordingListItem {
        RecordingBaseName = "recording-20251024-143022",
        TranscriptFilePath = "/path/to/recording-20251024-143022.txt",
        RecordedAt = 2025-10-24T14:30:22Z,
        FormattedDate = "Oct 24, 2025 2:30 PM",
        WordCount = 234,
        FileSizeBytes = 1245
    },
    RecordingListItem {
        RecordingBaseName = "recording-20251023-091500",
        TranscriptFilePath = "/path/to/recording-20251023-091500.txt",
        RecordedAt = 2025-10-23T09:15:00Z,
        FormattedDate = "Oct 23, 2025 9:15 AM",
        WordCount = 567,
        FileSizeBytes = 3024
    }
]
```

### Failure Responses

| Error Scenario | Error Message | HTTP Equivalent |
|----------------|---------------|-----------------|
| Recording directory not found | "Recording directory not found: {path}" | 404 Not Found |
| Recording directory unreadable | "Unable to read recording directory: {reason}" | 500 Internal Server Error |
| No recordings found | "No recordings found in {directory}. Use 'record' command to create a recording." | 404 Not Found |
| File parsing error | "Unable to parse recording file: {filename}" | 422 Unprocessable Entity |

### Side Effects

None (pure read operation)

### Example Usage

```csharp
var query = new ListRecordingsQuery
{
    CancellationToken = cancellationToken
};

var result = await mediator.Send(query);

if (result.IsSuccess)
{
    foreach (var recording in result.Value)
    {
        Console.WriteLine($"{recording.FormattedDate} - {recording.WordCount} words");
    }
}
else
{
    Console.Error.WriteLine($"Error: {result.Error}");
}
```

---

## GetRecordingTranscriptQuery

**Purpose**: Load the full transcript content for a specific recording

**Type**: Query (read-only)

**Handler**: `GetRecordingTranscriptQueryHandler`

### Query Definition

```csharp
namespace TenSecondTom.Features.Generate.Queries;

/// <summary>
/// Query to retrieve the transcript content for a specific recording.
/// Used after user selects a recording to load full content.
/// </summary>
public sealed record GetRecordingTranscriptQuery : IRequest<Result<string>>
{
    /// <summary>
    /// Gets the full path to the transcript file.
    /// </summary>
    public required string TranscriptFilePath { get; init; }

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
/// Handles loading transcript content from filesystem.
/// Validates file existence and readability.
/// </summary>
public sealed class GetRecordingTranscriptQueryHandler
    : IRequestHandler<GetRecordingTranscriptQuery, Result<string>>
{
    private readonly IRecordingService _recordingService;
    private readonly ILogger<GetRecordingTranscriptQueryHandler> _logger;

    public GetRecordingTranscriptQueryHandler(
        IRecordingService recordingService,
        ILogger<GetRecordingTranscriptQueryHandler> logger)
    {
        _recordingService = recordingService;
        _logger = logger;
    }

    public async Task<Result<string>> Handle(
        GetRecordingTranscriptQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TranscriptFilePath))
            return Result<string>.Failure("TranscriptFilePath is required");

        _logger.LogDebug("Loading transcript from {Path}", request.TranscriptFilePath);

        var result = await _recordingService.GetTranscriptContentAsync(
            request.TranscriptFilePath,
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "Failed to load transcript {Path}: {Error}",
                request.TranscriptFilePath,
                result.Error);
            return result;
        }

        _logger.LogDebug(
            "Loaded transcript: {Length} characters",
            result.Value.Length);

        return result;
    }
}
```

### Success Response

```csharp
Result<string>.Success(transcriptContent)
```

Where `transcriptContent` is the full text content of the transcript file.

Example:
```
"This is the transcript from my recording session. I discussed several key topics including project timelines, budget considerations, and team assignments. The main action items are..."
```

### Failure Responses

| Error Scenario | Error Message | HTTP Equivalent |
|----------------|---------------|-----------------|
| File not found | "Transcript file not found: {path}" | 404 Not Found |
| File unreadable | "Unable to read transcript: {reason}" | 500 Internal Server Error |
| Empty file | "Transcript file is empty: {path}" | 422 Unprocessable Entity |
| File too large | "Transcript file exceeds maximum size: {size}" | 413 Payload Too Large |

### Validation

```csharp
if (string.IsNullOrWhiteSpace(request.TranscriptFilePath))
    return Result<string>.Failure("TranscriptFilePath is required");

if (!File.Exists(request.TranscriptFilePath))
    return Result<string>.Failure($"Transcript file not found: {request.TranscriptFilePath}");
```

### Side Effects

None (pure read operation)

### Example Usage

```csharp
var query = new GetRecordingTranscriptQuery
{
    TranscriptFilePath = "/path/to/recording-20251024-143022.txt",
    CancellationToken = cancellationToken
};

var result = await mediator.Send(query);

if (result.IsSuccess)
{
    var transcript = result.Value;
    Console.WriteLine($"Loaded transcript: {transcript.Length} characters");
}
else
{
    Console.Error.WriteLine($"Error: {result.Error}");
}
```

---

## Query Optimization

### Caching Strategy

**ListRecordingsQuery**:
- No caching (to always show latest recordings)
- Directory scan is fast (< 100ms for 100+ files)
- Trade-off: slight delay vs. always current data

**GetRecordingTranscriptQuery**:
- No caching (transcripts are loaded once per generation)
- File reads are fast (< 50ms for typical transcript)
- Memory consideration: don't cache large transcripts

### Performance Targets

| Query | Expected Response Time | Max File Size |
|-------|----------------------|---------------|
| ListRecordingsQuery | < 200ms for 100 recordings | N/A |
| GetRecordingTranscriptQuery | < 100ms for 50KB file | 100MB |

---

## Testing Checklist

### Unit Tests

**ListRecordingsQuery**:
- [x] Returns empty list when no recordings exist
- [x] Returns recordings sorted by date (newest first)
- [x] Handles corrupted filename gracefully (skips file)
- [x] Returns failure when directory doesn't exist
- [x] Returns failure when directory is unreadable

**GetRecordingTranscriptQuery**:
- [x] Returns transcript content for valid file
- [x] Returns failure when file doesn't exist
- [x] Returns failure when file is empty
- [x] Returns failure when file is unreadable
- [x] Handles large files (> 1MB) correctly

### Integration Tests
- [x] ListRecordings with real filesystem
- [x] GetTranscript with real filesystem
- [x] Query performance with 100+ recordings
- [x] Concurrent query execution

---

## Future Queries (Out of Scope for v1)

### SearchRecordingsQuery
**Purpose**: Search recordings by date range, keywords, or metadata
**When**: Future iteration for large recording libraries

### GetRecordingMetadataQuery
**Purpose**: Load metadata without reading full transcript
**When**: Future iteration for performance optimization

---

**Related Contracts**: See [commands.md](./commands.md) for write operations
