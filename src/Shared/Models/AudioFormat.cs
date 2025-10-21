namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents supported audio file formats.
/// </summary>
/// <remarks>
/// These formats represent fundamental input methods for voice-based user input,
/// alongside text-based input. Stored in Shared because audio recording is a
/// core application capability used across multiple features.
/// </remarks>
public enum AudioFormat
{
    /// <summary>
    /// WAV format (Waveform Audio File Format).
    /// Required for local whisper.cpp transcription.
    /// Uncompressed, high quality, larger file size.
    /// </summary>
    Wav,

    /// <summary>
    /// MP3 format (MPEG Audio Layer 3).
    /// Supported by OpenAI STT API.
    /// Compressed, smaller file size, widely compatible.
    /// </summary>
    Mp3,

    /// <summary>
    /// M4A format (MPEG-4 Audio).
    /// Supported by OpenAI STT API.
    /// Compressed, good quality, common for voice recordings.
    /// </summary>
    M4a
}

