namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents STT engine selection strategy for audio transcription.
/// </summary>
public enum SttSelection
{
    /// <summary>
    /// Automatically select the best available STT engine.
    /// Strategy: Try local whisper.cpp first, fallback to OpenAI if unavailable.
    /// </summary>
    Auto,

    /// <summary>
    /// Use local whisper.cpp only.
    /// Fails if local whisper.cpp is not available or configured.
    /// </summary>
    Local,

    /// <summary>
    /// Use OpenAI Whisper API only.
    /// Skips local whisper.cpp check and uses cloud-based transcription.
    /// </summary>
    OpenAI
}
