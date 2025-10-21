using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Command to record audio to a specified file path.
/// </summary>
public sealed record RecordAudioCommand : IRequest<Result<AudioRecording>>
{
    /// <summary>
    /// Gets the output path where the audio file should be saved.
    /// </summary>
    public required string OutputPath { get; init; }
}
