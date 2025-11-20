using TenSecondTom.Shared.Options;

namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents available speech-to-text engines for audio transcription.
/// </summary>
/// <remarks>
/// Type alias for Audio feature's SttEngine enum.
/// This allows Shared models to reference STT engines without creating a circular dependency.
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
