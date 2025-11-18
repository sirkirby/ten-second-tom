using TenSecondTom.Shared.Models;
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
    /// Returns when the user stops recording (e.g., presses Enter) or timeout is reached.
    /// </summary>
    /// <param name="outputPath">The file path where the audio should be saved.</param>
    /// <param name="maxDurationSeconds">Maximum recording duration in seconds. If null, records indefinitely. If specified, prompts user to continue when timeout is reached.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing the <see cref="AudioRecording"/> if successful,
    /// or an error message if recording failed.
    /// </returns>
    /// <remarks>
    /// This is a blocking operation that waits for user input to stop recording.
    /// The implementation should provide visual feedback to the user during recording.
    /// When maxDurationSeconds is reached, the user is prompted to continue or stop.
    /// </remarks>
    Task<Result<AudioRecording>> RecordAsync(
        string outputPath,
        int? maxDurationSeconds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the default/system microphone that will be used for recording.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing the microphone name if successful, or an error message if unable to determine.
    /// </returns>
    Task<Result<string>> GetDefaultMicrophoneNameAsync(CancellationToken cancellationToken = default);
}
