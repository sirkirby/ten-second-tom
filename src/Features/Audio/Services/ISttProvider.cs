using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Interface for speech-to-text provider implementations.
/// Provides audio transcription using local or remote STT engines.
/// </summary>
public interface ISttProvider
{
    /// <summary>
    /// Gets the STT engine type for this provider.
    /// </summary>
    SttEngine Engine { get; }

    /// <summary>
    /// Checks if the STT provider is available and properly configured.
    /// For local providers, verifies binaries and models exist.
    /// For remote providers, may check API key configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>True if the provider is available and ready; otherwise, false.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes an audio file to text.
    /// </summary>
    /// <param name="audioFilePath">The path to the audio file to transcribe.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>
    /// A result containing the <see cref="TranscriptionResult"/> if successful,
    /// or an error message if transcription failed.
    /// </returns>
    /// <remarks>
    /// The implementation should validate the audio file format and size before transcription.
    /// Supported formats: WAV, MP3, FLAC (16kHz, mono recommended for best results).
    /// </remarks>
    Task<Result<TranscriptionResult>> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default);
}
