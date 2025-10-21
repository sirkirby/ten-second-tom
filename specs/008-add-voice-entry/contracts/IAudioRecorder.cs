using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Interface for audio recording implementations.
/// Implementations handle platform-specific audio capture (e.g., FFmpeg with AVFoundation/ALSA/DirectShow).
/// </summary>
public interface IAudioRecorder
{
    /// <summary>
    /// Checks if the recorder is available and properly configured on the system.
    /// For FFmpeg implementation: verifies ffmpeg is on PATH and executable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True if the recorder is available and ready to use.
    /// False if the recorder is not installed or not accessible.
    /// </returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Starts recording audio from the system's default microphone to the specified output path.
    /// Recording continues until the user stops it (e.g., presses Enter) or cancellation is requested.
    /// </summary>
    /// <param name="outputPath">Full path where the audio file will be saved.</param>
    /// <param name="cancellationToken">Cancellation token to stop recording.</param>
    /// <returns>
    /// Result containing AudioRecording metadata on success.
    /// Result with error message on failure (e.g., FFmpeg not found, device not available).
    /// </returns>
    Task<Result<AudioRecording>> RecordAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}

