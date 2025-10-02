using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;

namespace TenSecondTom.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for configuring services in the DI container.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API for dependency injection")]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Ten Second Tom services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTenSecondTomServices(this IServiceCollection services)
    {
        // Infrastructure services
        services.AddSingleton<IMemoryStorageProvider, FileSystemStorageProvider>();
        services.AddSingleton<ILlmProviderFactory, LlmProviderFactory>();
        services.AddSingleton<IPromptTemplateLoader, EmbeddedPromptTemplateLoader>();
        services.AddSingleton<IAuthenticationService, SshKeyAuthenticationService>();

        // LLM providers
        services.AddTransient<OpenAILlmProvider>();
        services.AddTransient<AnthropicLlmProvider>();

        // Feature handlers
        services.AddTransient<CreateDailyEntryHandler>();

        return services;
    }
}
