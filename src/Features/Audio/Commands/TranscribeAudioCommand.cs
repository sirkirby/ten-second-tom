using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
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
    /// Gets the audio configuration for STT provider selection.
    /// This includes the STT provider, API key, and fallback settings.
    /// </summary>
    public required AudioConfiguration AudioConfig { get; init; }
}
