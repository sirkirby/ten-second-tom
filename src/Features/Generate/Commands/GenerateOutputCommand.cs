using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

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
    /// Example: "10-21-2025_1"
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
    public CancellationToken CancellationToken { get; init; }
}
