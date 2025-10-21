namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents available speech-to-text engines for audio transcription.
/// </summary>
/// <remarks>
/// These engines represent fundamental methods for processing voice input,
/// a core capability alongside text input. Stored in Shared because speech-to-text
/// is a cross-cutting concern used by multiple features.
/// </remarks>
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

