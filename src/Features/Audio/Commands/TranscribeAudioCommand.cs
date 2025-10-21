using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Command to transcribe an audio file to text.
/// </summary>
public sealed record TranscribeAudioCommand : IRequest<Result<TranscriptionResult>>
{
    /// <summary>
    /// Gets the path to the audio file to transcribe.
    /// </summary>
    public required string AudioFilePath { get; init; }

    /// <summary>
    /// Gets the STT engine selection strategy.
    /// </summary>
    public SttSelection Selection { get; init; } = SttSelection.Auto;
}
