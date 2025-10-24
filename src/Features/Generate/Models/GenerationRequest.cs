using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Generate.Models;

/// <summary>
/// Input parameters for generation operation.
/// Encapsulates all required information to process a recording with a template.
/// </summary>
public sealed record GenerationRequest
{
    /// <summary>
    /// Gets the transcript file path to process.
    /// </summary>
    public required string TranscriptFilePath { get; init; }

    /// <summary>
    /// Gets the base name of the recording.
    /// Format: M-D-Y_Increment (e.g., "10-21-2025_1")
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
    public CancellationToken CancellationToken { get; init; }
}
