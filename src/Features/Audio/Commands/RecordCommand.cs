using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Commands;

/// <summary>
/// Command to record audio, transcribe it, and store both files in the recording/ directory.
/// This command is used by the 'tom record' CLI command.
/// </summary>
public sealed record RecordCommand : IRequest<Result<StoredRecording>>
{
    /// <summary>
    /// Gets the audio configuration for STT provider selection.
    /// This includes the STT provider, API key, and fallback settings.
    /// </summary>
    public required AudioConfiguration AudioConfig { get; init; }

    /// <summary>
    /// Gets the maximum recording duration in seconds.
    /// If not specified, uses the configured default from Audio:Timeouts:RecordSeconds.
    /// </summary>
    public int? MaxDurationSeconds { get; init; }

    /// <summary>
    /// Validates the command.
    /// </summary>
    /// <returns>True if valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return MaxDurationSeconds is null or > 0;
    }
}
