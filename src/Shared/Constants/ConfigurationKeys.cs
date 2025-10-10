namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed configuration key constants used when reading
/// from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> and environment variables.
/// </summary>
public static class ConfigurationKeys
{
    /// <summary>
    /// Configuration key for the LLM API key.
    /// Environment variable: Llm__ApiKey
    /// </summary>
    public const string LlmApiKey = "Llm:ApiKey";

    /// <summary>
    /// Configuration key for the LLM provider (OpenAI, Anthropic).
    /// Environment variable: Llm__Provider
    /// </summary>
    public const string LlmProvider = "Llm:Provider";

    /// <summary>
    /// Configuration key for the LLM model selection.
    /// Environment variable: Llm__Model
    /// </summary>
    public const string LlmModel = "Llm:Model";

    /// <summary>
    /// Configuration key for the .NET environment name (Development, Production, etc.).
    /// Matches the built-in DOTNET_ENVIRONMENT variable.
    /// </summary>
    public const string DotNetEnvironment = "DOTNET_ENVIRONMENT";

    /// <summary>
    /// Configuration section root for TenSecondTom specific settings.
    /// </summary>
    public const string Root = "TenSecondTom";
}
