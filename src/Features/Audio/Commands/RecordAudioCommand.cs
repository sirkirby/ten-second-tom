using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Marker interface for request/response pattern.
/// Indicates this command returns a specific response type.
/// </summary>
public interface IRequest<out TResponse>
{
}

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
