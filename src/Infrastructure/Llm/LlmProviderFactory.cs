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
/// </summary>
public sealed class LlmProviderFactory : ILlmProviderFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="LlmProviderFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    public LlmProviderFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
            _ => throw new ArgumentException($"Unsupported LLM provider: {providerName}. Supported providers are: OpenAI, Anthropic", nameof(providerName))
        };
    }

    private ILlmProvider GetProvider<T>() where T : ILlmProvider
    {
        try
        {
            T? provider = _serviceProvider.GetService<T>();
            
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
