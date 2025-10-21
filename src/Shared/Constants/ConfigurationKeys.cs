namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed configuration key constants used when reading
/// from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> and environment variables.
/// All configuration follows standard .NET pattern: TenSecondTom:Section:Key
/// </summary>
public static class ConfigurationKeys
{
    // ═══════════════════════════════════════════════════════════════
    // STANDARD KEYS (Use These)
    // ═══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Configuration section root for TenSecondTom specific settings.
    /// </summary>
    public const string Root = "TenSecondTom";

    /// <summary>
    /// Configuration key for the memory directory where all data is stored.
    /// Environment variable: TenSecondTom__MemoryDirectory
    /// </summary>
    public const string MemoryDirectory = "TenSecondTom:MemoryDirectory";

    /// <summary>
    /// Configuration key for the LLM provider (OpenAI, Anthropic).
    /// Environment variable: TenSecondTom__Llm__Provider
    /// </summary>
    public const string LlmProvider = "TenSecondTom:Llm:Provider";

    /// <summary>
    /// Configuration key for the LLM API key.
    /// Environment variable: TenSecondTom__Llm__ApiKey
    /// </summary>
    public const string LlmApiKey = "TenSecondTom:Llm:ApiKey";

    /// <summary>
    /// Configuration key for the LLM model selection.
    /// Environment variable: TenSecondTom__Llm__Model
    /// </summary>
    public const string LlmModel = "TenSecondTom:Llm:Model";

    /// <summary>
    /// Configuration key for SSH key file path.
    /// Environment variable: TenSecondTom__Ssh__KeyPath
    /// </summary>
    public const string SshKeyPath = "TenSecondTom:Ssh:KeyPath";

    /// <summary>
    /// Configuration key for SSH key source (ManualPath, SshAgent, etc.).
    /// Environment variable: TenSecondTom__Ssh__KeySource
    /// </summary>
    public const string SshKeySource = "TenSecondTom:Ssh:KeySource";

    /// <summary>
    /// Configuration key for SSH agent socket path.
    /// Environment variable: TenSecondTom__Ssh__AgentSocketPath
    /// </summary>
    public const string SshAgentSocketPath = "TenSecondTom:Ssh:AgentSocketPath";

    /// <summary>
    /// Configuration section for audio recording and transcription settings.
    /// Used for binding AudioConfiguration.
    /// Environment variable prefix: TenSecondTom__Audio__*
    /// </summary>
    public const string AudioSection = "TenSecondTom:Audio";

    // ═══════════════════════════════════════════════════════════════
    // OTHER KEYS
    // ═══════════════════════════════════════════════════════════════

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
