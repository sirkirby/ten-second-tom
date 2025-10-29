namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed configuration key constants for both:
/// 1. Reading from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> (full paths like "TenSecondTom:Llm:Provider")
/// 2. JSON serialization property names (individual names like nested classes: Llm.Provider = "Provider")
/// 
/// USAGE EXAMPLES:
/// - IConfiguration: config[ConfigurationKeys.LlmProviderKey] → "TenSecondTom:Llm:Provider"
/// - JSON Parsing: element.TryGetProperty(ConfigurationKeys.Llm.Section, ...) → "Llm"
/// - JSON Parsing: element.TryGetProperty(ConfigurationKeys.Llm.Provider, ...) → "Provider"
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

    /// <summary>
    /// Configuration key for the LLM provider (OpenAI, Anthropic).
    /// Environment variable: TenSecondTom__Llm__Provider
    /// </summary>
    public const string LlmProviderKey = "TenSecondTom:Llm:Provider";

    /// <summary>
    /// Configuration key for the LLM API key.
    /// Environment variable: TenSecondTom__Llm__ApiKey
    /// </summary>
    public const string LlmApiKeyKey = "TenSecondTom:Llm:ApiKey";

    /// <summary>
    /// Configuration key for the LLM model selection.
    /// Environment variable: TenSecondTom__Llm__Model
    /// </summary>
    public const string LlmModelKey = "TenSecondTom:Llm:Model";

    /// <summary>
    /// Configuration key for maximum input tokens for LLM processing.
    /// Environment variable: TenSecondTom__Llm__MaxInputTokens
    /// </summary>
    public const string LlmMaxInputTokensKey = "TenSecondTom:Llm:MaxInputTokens";

    /// <summary>
    /// Configuration key for SSH key file path.
    /// Environment variable: TenSecondTom__Ssh__KeyPath
    /// </summary>
    public const string SshKeyPathKey = "TenSecondTom:Ssh:KeyPath";

    /// <summary>
    /// Configuration key for SSH key source (ManualPath, SshAgent, etc.).
    /// Environment variable: TenSecondTom__Ssh__KeySource
    /// </summary>
    public const string SshKeySourceKey = "TenSecondTom:Ssh:KeySource";

    /// <summary>
    /// Configuration key for SSH agent socket path.
    /// Environment variable: TenSecondTom__Ssh__AgentSocketPath
    /// </summary>
    public const string SshAgentSocketPathKey = "TenSecondTom:Ssh:AgentSocketPath";

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
