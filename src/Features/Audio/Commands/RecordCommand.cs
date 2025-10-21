using TenSecondTom.Features.Audio.Models;
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
    /// Gets the STT selection strategy (auto, local, or openai).
    /// Defaults to Auto for automatic selection.
    /// </summary>
    public SttSelection SttSelection { get; init; } = SttSelection.Auto;

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
