using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Audio.Models;

/// <summary>
/// Represents STT engine selection strategy for audio transcription.
/// This enum provides the CLI interface for STT provider selection and maps to the underlying
/// TranscribeOptions.SttProvider setting.
/// </summary>
/// <remarks>
/// Mapping to configuration:
/// <list type="bullet">
/// <item><see cref="Auto"/>: SttProvider = "built-in-local" (default, uses Whisper.NET)</item>
/// <item><see cref="Local"/>: SttProvider = "whisper-cpp" (external whisper.cpp binary)</item>
/// <item><see cref="OpenAI"/>: SttProvider = "openai" (cloud-based OpenAI Whisper API)</item>
/// </list>
/// </remarks>
public enum SttSelection
{
    /// <summary>
    /// Automatically select the best available STT engine.
    /// Uses built-in local AI (Microsoft Foundry) by default.
    /// Corresponds to CLI flag: --stt auto (default).
    /// </summary>
    Auto,

    /// <summary>
    /// Use local whisper.cpp binary (external installation required).
    /// Fails if whisper-cpp is not available or configured.
    /// Corresponds to CLI flag: --stt local.
    /// </summary>
    Local,

    /// <summary>
    /// Use OpenAI Whisper API only.
    /// Uses cloud-based transcription (requires API key).
    /// Corresponds to CLI flag: --stt openai.
    /// </summary>
    OpenAI
}
