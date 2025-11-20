using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents STT engine selection strategy for audio transcription.
/// This enum provides the CLI interface for STT provider selection and maps to the underlying
/// AudioOptions.SttProvider and AudioOptions.SttFallbackEnabled settings.
/// </summary>
/// <remarks>
/// Mapping to configuration:
/// <list type="bullet">
/// <item><see cref="Auto"/>: SttProvider = "whisper-cpp" with SttFallbackEnabled = true</item>
/// <item><see cref="Local"/>: SttProvider = "whisper-cpp" with SttFallbackEnabled = false</item>
/// <item><see cref="OpenAI"/>: SttProvider = "openai"</item>
/// </list>
/// </remarks>
public enum SttSelection
{
    /// <summary>
    /// Automatically select the best available STT engine.
    /// Strategy: Try local whisper.cpp first, fallback to OpenAI if unavailable.
    /// Corresponds to CLI flag: --stt auto (default).
    /// </summary>
    Auto,

    /// <summary>
    /// Use local whisper.cpp only (no fallback).
    /// Fails if local whisper.cpp is not available or configured.
    /// Corresponds to CLI flag: --stt local.
    /// </summary>
    Local,

    /// <summary>
    /// Use OpenAI Whisper API only.
    /// Skips local whisper.cpp check and uses cloud-based transcription.
    /// Corresponds to CLI flag: --stt openai.
    /// </summary>
    OpenAI
}
