using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Interface for speech-to-text provider implementations.
/// Implementations handle transcription using different engines (whisper.cpp, OpenAI, etc.).
/// </summary>
public interface ISttProvider
{
    /// <summary>
    /// Gets the STT engine type this provider implements.
    /// </summary>
    SttEngine Engine { get; }
    
    /// <summary>
    /// Checks if the STT provider is available and properly configured.
    /// For local whisper.cpp: verifies binary exists and model is configured.
    /// For OpenAI: verifies API key is configured (does not make API call).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True if the provider is available and ready to transcribe.
    /// False if the provider is not installed, not configured, or not accessible.
    /// </returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Transcribes an audio file to text.
    /// </summary>
    /// <param name="audioFilePath">Full path to the audio file to transcribe.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// Result containing TranscriptionResult on success.
    /// Result with error message on failure (e.g., file not found, transcription error, API error).
    /// </returns>
    Task<Result<TranscriptionResult>> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default);
}

