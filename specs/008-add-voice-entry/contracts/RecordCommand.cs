using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Command to record audio and transcribe it, storing both for future processing.
/// This is the "raw recording" mode that doesn't create a note entry.
/// Files are saved to the recording/ subdirectory and persisted permanently.
/// </summary>
public sealed record RecordCommand : IRequest<Result<StoredRecording>>
{
    /// <summary>
    /// Gets the STT engine selection strategy for transcription.
    /// Default: Auto (try local, fallback to OpenAI)
    /// </summary>
    public SttSelection SttSelection { get; init; } = SttSelection.Auto;
    
    /// <summary>
    /// Gets a value indicating whether to output JSON format to stdout.
    /// When true, outputs recording metadata as JSON for scripting/piping.
    /// When false, displays user-friendly text output.
    /// </summary>
    public bool JsonOutput { get; init; }
}

/// <summary>
/// Represents a stored recording with both audio and transcription files.
/// Stored in the recording/ subdirectory for future reprocessing.
/// </summary>
public sealed record StoredRecording
{
    /// <summary>
    /// Gets the full path to the audio file.
    /// Example: "/Users/chris/.memory/ten-second-tom/recording/recording-20251020-150000.wav"
    /// </summary>
    public required string AudioFilePath { get; init; }
    
    /// <summary>
    /// Gets the full path to the transcription text file.
    /// Example: "/Users/chris/.memory/ten-second-tom/recording/recording-20251020-150000.txt"
    /// </summary>
    public required string TranscriptionFilePath { get; init; }
    
    /// <summary>
    /// Gets the timestamp when the recording was created.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }
    
    /// <summary>
    /// Gets the duration of the audio recording.
    /// </summary>
    public required TimeSpan Duration { get; init; }
    
    /// <summary>
    /// Gets the size of the audio file in bytes.
    /// </summary>
    public required long FileSizeBytes { get; init; }
    
    /// <summary>
    /// Gets the number of words in the transcription.
    /// </summary>
    public required int TranscriptionWordCount { get; init; }
    
    /// <summary>
    /// Gets the STT engine used for transcription.
    /// </summary>
    public required SttEngine SttEngine { get; init; }
    
    /// <summary>
    /// Gets the model identifier used for transcription.
    /// Example: "ggml-base.en", "whisper-1"
    /// </summary>
    public string? SttModel { get; init; }
    
    /// <summary>
    /// Gets the full transcript text.
    /// Convenience property (also available by reading TranscriptionFilePath).
    /// </summary>
    public required string TranscriptText { get; init; }
}

