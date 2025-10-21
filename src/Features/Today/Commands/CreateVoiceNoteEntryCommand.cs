using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Commands;

/// <summary>
/// Command to create a daily entry from voice input.
/// Processes voice transcription and generates LLM summary.
/// </summary>
public sealed record CreateVoiceNoteEntryCommand : IRequest<Result<VoiceNoteEntry>>
{
    /// <summary>
    /// Gets the transcript text from speech-to-text.
    /// This will be used as the user input for LLM processing.
    /// </summary>
    public required string TranscriptText { get; init; }

    /// <summary>
    /// Gets the audio recording metadata.
    /// Contains information about the source audio file.
    /// </summary>
    public required AudioRecording Recording { get; init; }

    /// <summary>
    /// Gets the transcription result metadata.
    /// Contains information about the STT processing.
    /// </summary>
    public required TranscriptionResult Transcription { get; init; }

    /// <summary>
    /// Gets the optional template name to use for processing.
    /// If specified, the handler will attempt to load this template.
    /// If not found, falls back to the default template.
    /// </summary>
    public string? TemplateName { get; init; }

    /// <summary>
    /// Gets a value indicating whether to use the default template without prompting.
    /// When true, bypasses template selection UI.
    /// </summary>
    public bool UseDefaultTemplate { get; init; }

    /// <summary>
    /// Gets the optional LLM provider override.
    /// If not specified, uses the default provider from configuration.
    /// Valid values: "OpenAI", "Anthropic".
    /// </summary>
    public string? LlmProviderOverride { get; init; }
}
