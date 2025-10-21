using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Command to transcribe an audio file to text using the selected STT engine.
/// Supports automatic engine selection (local-first with fallback) or explicit engine choice.
/// </summary>
public sealed record TranscribeAudioCommand : IRequest<Result<TranscriptionResult>>
{
    /// <summary>
    /// Gets the path to the audio file to transcribe.
    /// Must be a valid, readable audio file in a supported format.
    /// For whisper.cpp: WAV 16kHz mono required.
    /// For OpenAI STT: WAV, MP3, M4A supported.
    /// </summary>
    public required string AudioFilePath { get; init; }
    
    /// <summary>
    /// Gets the STT engine selection strategy.
    /// - Auto: Try local whisper.cpp first, fallback to OpenAI if unavailable
    /// - Local: Use whisper.cpp only (fail if unavailable)
    /// - OpenAI: Use OpenAI STT only (skip local)
    /// Default: Auto
    /// </summary>
    public SttSelection SttSelection { get; init; } = SttSelection.Auto;
    
    /// <summary>
    /// Gets the optional audio recording metadata.
    /// Provides additional context for transcription (duration, format, etc.).
    /// </summary>
    public AudioRecording? Recording { get; init; }
}

/// <summary>
/// Represents the result of speech-to-text transcription.
/// </summary>
public sealed record TranscriptionResult
{
    /// <summary>
    /// Gets the reference to the source audio file (path or identifier).
    /// </summary>
    public required string AudioReference { get; init; }
    
    /// <summary>
    /// Gets the full transcript text.
    /// May contain multiple sentences or paragraphs.
    /// </summary>
    public required string TranscriptText { get; init; }
    
    /// <summary>
    /// Gets the STT engine that was used for transcription.
    /// </summary>
    public required SttEngine SttEngine { get; init; }
    
    /// <summary>
    /// Gets the model identifier used for transcription.
    /// Examples: "ggml-base.en" (whisper.cpp), "whisper-1" (OpenAI)
    /// </summary>
    public string? SttModel { get; init; }
    
    /// <summary>
    /// Gets the confidence score if available (0.0 to 1.0).
    /// Note: whisper.cpp CLI does not provide confidence scores.
    /// </summary>
    public float? ConfidenceScore { get; init; }
    
    /// <summary>
    /// Gets the time taken to process the transcription.
    /// </summary>
    public required TimeSpan ProcessingDuration { get; init; }
    
    /// <summary>
    /// Gets the timestamp when transcription completed.
    /// </summary>
    public required DateTimeOffset TranscribedAt { get; init; }
    
    /// <summary>
    /// Gets the number of words in the transcript.
    /// </summary>
    public required int WordCount { get; init; }
    
    /// <summary>
    /// Gets the detected language if available.
    /// Example: "en" for English
    /// </summary>
    public string? Language { get; init; }
    
    /// <summary>
    /// Gets a value indicating whether the transcript is empty.
    /// </summary>
    public bool IsEmpty => WordCount == 0;
    
    /// <summary>
    /// Gets the processing speed as a ratio of audio duration to processing time.
    /// Higher values indicate faster-than-realtime processing.
    /// Requires Recording metadata to calculate.
    /// </summary>
    public double? ProcessingSpeed { get; init; }
}

/// <summary>
/// STT engine selection strategy.
/// </summary>
public enum SttSelection
{
    /// <summary>
    /// Automatically select: try local first, fallback to OpenAI.
    /// </summary>
    Auto,
    
    /// <summary>
    /// Use local whisper.cpp only (fail if unavailable).
    /// </summary>
    Local,
    
    /// <summary>
    /// Use OpenAI STT only (skip local).
    /// </summary>
    OpenAI
}

/// <summary>
/// STT engine type.
/// </summary>
public enum SttEngine
{
    /// <summary>
    /// Local transcription using whisper.cpp.
    /// </summary>
    Local,
    
    /// <summary>
    /// Remote transcription using OpenAI API.
    /// </summary>
    OpenAI
}

