namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed configuration key constants.
///
/// USAGE EXAMPLES:
/// - IConfiguration: config["TenSecondTom:Audio:Recorder:InputVolume"]
/// - Or use: config[ConfigurationKeys.AudioRecorderInputVolumeKey]
/// - Root directory: config[ConfigurationKeys.RootDirectoryKey]
///
/// NOTE: For LLM and Auth configuration, use the Options Pattern instead:
/// - Inject IOptions&lt;LlmOptions&gt; instead of accessing "TenSecondTom:Llm:*" keys
/// - Inject IOptions&lt;AuthOptions&gt; instead of accessing "TenSecondTom:Auth:*" keys
/// </summary>
public static class ConfigurationKeys
{
    // ═══════════════════════════════════════════════════════════════
    // ROOT LEVEL (for IConfiguration access)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuration section root for TenSecondTom specific settings.
    /// </summary>
    public const string Root = "TenSecondTom";

    /// <summary>
    /// Configuration key for the application root directory where config, memories, and all app data is stored.
    /// This is the base directory for all application data, not storage-specific.
    /// Environment variable: TenSecondTom__RootDirectory
    /// </summary>
    public const string RootDirectoryKey = "TenSecondTom:RootDirectory";

    /// <summary>
    /// Configuration key for the memory directory (legacy, backward compatibility only).
    /// Use RootDirectoryKey for new configurations.
    /// Environment variable: TenSecondTom__MemoryDirectory
    /// </summary>
    [Obsolete("Use RootDirectoryKey instead. This constant is for backward compatibility only.", false)]
    public const string MemoryDirectoryKey = "TenSecondTom:MemoryDirectory";

    // ═══════════════════════════════════════════════════════════════
    // AUDIO FEATURE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Configuration section for audio recording and transcription settings.
    /// Used for binding AudioConfiguration.
    /// Environment variable prefix: TenSecondTom__Audio__*
    /// </summary>
    public const string AudioSectionKey = "TenSecondTom:Audio";

    /// <summary>
    /// Configuration key for audio recorder input volume (0.0 to 2.0).
    /// Environment variable: TenSecondTom__Audio__Recorder__InputVolume
    /// </summary>
    public const string AudioRecorderInputVolumeKey = "TenSecondTom:Audio:Recorder:InputVolume";

    /// <summary>
    /// Configuration key for enabling noise reduction during recording (boolean).
    /// Environment variable: TenSecondTom__Audio__Recorder__EnableNoiseReduction
    /// </summary>
    public const string AudioRecorderEnableNoiseReductionKey = "TenSecondTom:Audio:Recorder:EnableNoiseReduction";

    /// <summary>
    /// Configuration key for enabling frequency filters during recording (boolean).
    /// Environment variable: TenSecondTom__Audio__Recorder__EnableFrequencyFilters
    /// </summary>
    public const string AudioRecorderEnableFrequencyFiltersKey = "TenSecondTom:Audio:Recorder:EnableFrequencyFilters";

    /// <summary>
    /// Configuration key for enabling silence removal preprocessing (boolean).
    /// Environment variable: TenSecondTom__Audio__Preprocessing__RemoveSilence
    /// </summary>
    public const string AudioPreprocessingRemoveSilenceKey = "TenSecondTom:Audio:Preprocessing:RemoveSilence";

    /// <summary>
    /// Configuration key for silence detection threshold in decibels (integer).
    /// Environment variable: TenSecondTom__Audio__Preprocessing__SilenceThresholdDb
    /// </summary>
    public const string AudioPreprocessingSilenceThresholdDbKey = "TenSecondTom:Audio:Preprocessing:SilenceThresholdDb";

    /// <summary>
    /// Configuration key for minimum silence duration to remove in milliseconds (integer).
    /// Environment variable: TenSecondTom__Audio__Preprocessing__MinimumSilenceDurationMs
    /// </summary>
    public const string AudioPreprocessingMinimumSilenceDurationMsKey = "TenSecondTom:Audio:Preprocessing:MinimumSilenceDurationMs";

    /// <summary>
    /// Configuration key for the .NET environment name (Development, Production, etc.).
    /// Matches the built-in DOTNET_ENVIRONMENT variable.
    /// </summary>
    public const string DotNetEnvironment = "DOTNET_ENVIRONMENT";

    /// <summary>
    /// Configuration key for the user secrets ID.
    /// </summary>
    public const string UserSecretsId = "ten-second-tom-secrets";
}
