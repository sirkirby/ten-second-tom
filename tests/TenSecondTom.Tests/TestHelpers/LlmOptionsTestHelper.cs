using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.TestHelpers;

/// <summary>
/// Helper methods for creating LlmOptions in tests.
/// </summary>
public static class LlmOptionsTestHelper
{
    /// <summary>
    /// Creates an LlmOptions configured for testing with the specified provider.
    /// </summary>
    public static LlmOptions Create(
        LlmProvider provider = LlmProvider.OpenAI,
        string? apiKey = "test-api-key",
        string model = "test-model",
        int? maxInputTokens = 100000)
    {
        var options = new LlmOptions
        {
            Provider = provider,
            Providers = new Dictionary<string, Dictionary<string, string>>()
        };

        var providerName = provider.ToString();
        var providerConfig = new Dictionary<string, string>
        {
            ["Model"] = model
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            providerConfig["ApiKey"] = apiKey;
        }

        if (maxInputTokens.HasValue)
        {
            providerConfig["MaxInputTokens"] = maxInputTokens.Value.ToString();
        }

        options.Providers[providerName] = providerConfig;

        return options;
    }

    /// <summary>
    /// Creates an LlmOptions with no configuration (unconfigured state).
    /// </summary>
    public static LlmOptions CreateUnconfigured(LlmProvider provider = LlmProvider.OpenAI)
    {
        return new LlmOptions
        {
            Provider = provider,
            Providers = new Dictionary<string, Dictionary<string, string>>()
        };
    }
}
