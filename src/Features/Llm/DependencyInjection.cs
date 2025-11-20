using Microsoft.Extensions.DependencyInjection;

namespace TenSecondTom.Features.Llm;

/// <summary>
/// Dependency injection configuration for the LLM feature slice.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds LLM feature services to the service collection.
    /// MediatR handlers are automatically discovered via assembly scanning.
    /// </summary>
    public static IServiceCollection AddLlmFeature(this IServiceCollection services)
    {
        // Future: Add LLM provider services, clients, etc.
        // For now, MediatR will auto-discover ConfigureLlm.Handler

        return services;
    }
}
