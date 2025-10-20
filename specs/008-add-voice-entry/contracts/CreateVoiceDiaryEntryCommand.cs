using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Commands;

/// <summary>
/// Command to create a voice note entry from transcribed audio input.
/// Combines transcription results with LLM summarization to generate a structured note.
/// </summary>
public sealed record CreateVoiceNoteEntryCommand : IRequest<Result<VoiceNoteEntry>>
{
    /// <summary>
    /// Gets the transcript text from speech-to-text processing.
    /// This will be used as the user input for LLM summarization.
    /// Must not be null, empty, or whitespace-only.
    /// </summary>
    public required string TranscriptText { get; init; }
    
    /// <summary>
    /// Gets the audio recording metadata.
    /// Used to populate voice-specific fields in the note entry.
    /// </summary>
    public required AudioRecording Recording { get; init; }
    
    /// <summary>
    /// Gets the transcription result metadata.
    /// Includes STT engine, model, processing duration, etc.
    /// </summary>
    public required TranscriptionResult Transcription { get; init; }
    
    /// <summary>
    /// Gets the optional template name to use for processing the note.
    /// If specified, the handler will attempt to load this template.
    /// If not found, falls back to the default template with a warning.
    /// </summary>
    public string? TemplateName { get; init; }
    
    /// <summary>
    /// Gets a value indicating whether to use the default template.
    /// When true, bypasses template selection UI and uses the default daily summary template.
    /// Useful for non-interactive scenarios or when the user prefers the default template.
    /// </summary>
    public bool UseDefaultTemplate { get; init; }
    
    /// <summary>
    /// Gets the optional LLM provider override.
    /// If not specified, uses the default provider from configuration.
    /// Valid values: "OpenAI", "Anthropic".
    /// </summary>
    public string? LlmProviderOverride { get; init; }
}

/// <summary>
/// Represents a voice note entry created from transcribed audio.
/// Extends DailyEntry with voice-specific metadata.
/// </summary>
public sealed record VoiceNoteEntry : DailyEntry
{
    /// <summary>
    /// Gets the filename of the audio recording.
    /// Example: "note-20251020-143000.wav"
    /// </summary>
    public required string AudioFilename { get; init; }
    
    /// <summary>
    /// Gets the duration of the audio recording.
    /// </summary>
    public required TimeSpan AudioDuration { get; init; }
    
    /// <summary>
    /// Gets the full transcript text from speech-to-text.
    /// This matches the UserInput property inherited from DailyEntry.
    /// </summary>
    public required string TranscriptText { get; init; }
    
    /// <summary>
    /// Gets the STT engine used for transcription.
    /// </summary>
    public required SttEngine SttEngine { get; init; }
    
    /// <summary>
    /// Gets the model identifier used for transcription.
    /// Example: "ggml-base.en" for whisper.cpp, "whisper-1" for OpenAI.
    /// </summary>
    public string? SttModel { get; init; }
}

