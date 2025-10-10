namespace TenSecondTom.Shared.Constants;

/// <summary>
/// Provides strongly-typed configuration key constants used when reading
/// from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> and environment variables.
/// </summary>
public static class ConfigurationKeys
{
    /// <summary>
    /// Configuration key (or environment variable) for the OpenAI API key.
    /// </summary>
    public const string OpenAIApiKey = "OPENAI_API_KEY";

    /// <summary>
    /// Configuration key (or environment variable) for the Anthropic API key.
    /// </summary>
    public const string AnthropicApiKey = "ANTHROPIC_API_KEY";

    /// <summary>
    /// Configuration key for selecting the active LLM provider (e.g. OpenAI, Anthropic).
    /// </summary>
    public const string LlmProvider = "TenSecondTom:LlmProvider";

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
