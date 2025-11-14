namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed constants for audio recording and transcription features.
/// These constants ensure consistency across configuration, services, and documentation.
/// </summary>
public static class AudioConstants
{
    /// <summary>
    /// Default binary name for FFmpeg audio recording tool.
    /// Assumes ffmpeg is available on the system PATH.
    /// </summary>
    public const string FfmpegBinaryName = "ffmpeg";

    /// <summary>
    /// Default binary name for whisper.cpp CLI tool.
    /// Note: Homebrew installs whisper-cpp package as 'whisper-cli' binary.
    /// </summary>
    public const string WhisperCliBinaryName = "whisper-cli";

    /// <summary>
    /// Default cache directory name for Whisper models.
    /// Located at: ~/.cache/whisper/
    /// </summary>
    public const string WhisperCacheDirectory = ".cache/whisper";

    /// <summary>
    /// Default GGML model filename for whisper.cpp.
    /// Base.en model: English-only, 142 MB, balanced speed/accuracy.
    /// </summary>
    public const string DefaultWhisperModelFilename = "ggml-base.en.bin";

    /// <summary>
    /// Gets the default full path to the Whisper model file.
    /// Returns: ~/.cache/whisper/ggml-base.en.bin
    /// This path uses Unix-style home directory notation (~) which is expanded at runtime.
    /// </summary>
    public static string DefaultWhisperModelPath =>
        $"~/{WhisperCacheDirectory}/{DefaultWhisperModelFilename}";

    /// <summary>
    /// Maximum allowed duration (seconds) for 'today --voice' recording timeout.
    /// Set to 8 hours (28800 seconds) to allow reasonable values while ensuring eventual timeout.
    /// </summary>
    public const int MaxTodayTimeoutSeconds = 28800; // 8 hours

    /// <summary>
    /// Maximum allowed duration (seconds) for 'record' command timeout.
    /// Set to 24 hours (86400 seconds) to allow reasonable values while ensuring eventual timeout.
    /// </summary>
    public const int MaxRecordTimeoutSeconds = 86400; // 24 hours

    /// <summary>
    /// Minimum allowed duration (seconds) for 'today --voice' recording timeout.
    /// Set to 30 seconds to ensure reasonable minimum duration.
    /// </summary>
    public const int MinTodayTimeoutSeconds = 30;

    /// <summary>
    /// Minimum allowed duration (seconds) for 'record' command timeout.
    /// Set to 60 seconds to ensure reasonable minimum duration.
    /// </summary>
    public const int MinRecordTimeoutSeconds = 60;
}
