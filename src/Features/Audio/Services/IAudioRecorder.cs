using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Interface for audio recording implementations.
/// Provides audio capture functionality using system audio input devices.
/// </summary>
public interface IAudioRecorder
{
    /// <summary>
    /// Checks if the audio recorder is available on the system.
    /// Verifies that the recording binary (e.g., FFmpeg) exists and is executable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if the recorder is available; otherwise, false.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts recording audio to the specified output path.
    /// Returns when the user stops recording (e.g., presses Enter).
    /// </summary>
    /// <param name="outputPath">The file path where the audio should be saved.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing the <see cref="AudioRecording"/> if successful,
    /// or an error message if recording failed.
    /// </returns>
    /// <remarks>
    /// This is a blocking operation that waits for user input to stop recording.
    /// The implementation should provide visual feedback to the user during recording.
    /// </remarks>
    Task<Result<AudioRecording>> RecordAsync(
        string outputPath,
        CancellationToken cancellationToken = default);
}
