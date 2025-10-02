using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// Factory for creating appropriate ILlmProvider instances based on configuration.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Factory converts all instantiation errors to Result")]
public sealed class LlmProviderFactory
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
    /// <returns>Result containing the provider instance on success, or error message on failure.</returns>
    public Result<ILlmProvider> Create(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return Result<ILlmProvider>.Failure("Provider name cannot be empty");
        }

        return providerName.Trim().ToUpperInvariant() switch
        {
            "OPENAI" => CreateProvider<OpenAILlmProvider>(),
            "ANTHROPIC" => CreateProvider<AnthropicLlmProvider>(),
            _ => Result<ILlmProvider>.Failure($"Unsupported LLM provider: {providerName}. Supported providers are: OpenAI, Anthropic")
        };
    }

    private Result<ILlmProvider> CreateProvider<T>() where T : ILlmProvider
    {
        try
        {
            T? provider = _serviceProvider.GetService<T>();
            
            if (provider == null)
            {
                return Result<ILlmProvider>.Failure($"{typeof(T).Name} is not registered in the service container");
            }

            return Result<ILlmProvider>.Success(provider);
        }
        catch (Exception ex)
        {
            return Result<ILlmProvider>.Failure($"Failed to create provider: {ex.Message}");
        }
    }
}
