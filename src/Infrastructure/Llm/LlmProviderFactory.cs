using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// Interface for creating ILlmProvider instances based on provider name.
/// </summary>
public interface ILlmProviderFactory
{
    /// <summary>
    /// Creates an ILlmProvider instance for the specified provider name.
    /// </summary>
    /// <param name="providerName">The name of the provider ("OpenAI" or "Anthropic").</param>
    /// <returns>The provider instance.</returns>
    /// <exception cref="ArgumentException">Thrown when provider name is invalid.</exception>
    ILlmProvider CreateProvider(string providerName);
}

/// <summary>
/// Factory for creating appropriate ILlmProvider instances based on configuration.
/// Uses IServiceScopeFactory to ensure scoped services (like IOptionsSnapshot) work correctly.
/// </summary>
public sealed class LlmProviderFactory : ILlmProviderFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderFactory"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped service providers.</param>
    public LlmProviderFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <summary>
    /// Creates an ILlmProvider instance for the specified provider name.
    /// </summary>
    /// <param name="providerName">The name of the provider ("OpenAI" or "Anthropic").</param>
    /// <returns>The provider instance.</returns>
    /// <exception cref="ArgumentException">Thrown when provider name is invalid or provider cannot be created.</exception>
    public ILlmProvider CreateProvider(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            throw new ArgumentException("Provider name cannot be empty", nameof(providerName));
        }

        return providerName.Trim().ToUpperInvariant() switch
        {
            "OPENAI" => GetProvider<OpenAILlmProvider>(),
            "ANTHROPIC" => GetProvider<AnthropicLlmProvider>(),
            "LOCALOPENAICOMPATIBLE" => GetProvider<LocalOpenAiCompatibleLlmProvider>(),
            _ => throw new ArgumentException($"Unsupported LLM provider: {providerName}. Supported providers are: OpenAI, Anthropic, LocalOpenAiCompatible", nameof(providerName))
        };
    }

    private ILlmProvider GetProvider<T>() where T : ILlmProvider
    {
        try
        {
            // Create a scope to ensure IOptionsSnapshot gets fresh configuration
            // This is critical for shell mode where configuration can change between commands
            using var scope = _scopeFactory.CreateScope();
            T? provider = scope.ServiceProvider.GetService<T>();

            if (provider == null)
            {
                throw new InvalidOperationException($"{typeof(T).Name} is not registered in the service container");
            }

            return provider;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create provider: {ex.Message}", ex);
        }
    }
}
