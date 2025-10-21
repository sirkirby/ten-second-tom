namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents available speech-to-text engines for audio transcription.
/// </summary>
public enum SttEngine
{
    /// <summary>
    /// Local transcription using whisper.cpp.
    /// Provides offline, privacy-focused transcription with no API costs.
    /// </summary>
    Local,

    /// <summary>
    /// Remote transcription using OpenAI Whisper API.
    /// Provides cloud-based transcription with high accuracy and language support.
    /// </summary>
    OpenAI
}
