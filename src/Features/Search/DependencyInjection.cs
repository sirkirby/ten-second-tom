using Microsoft.Extensions.DependencyInjection;

namespace TenSecondTom.Features.Search;

/// <summary>
/// Extension methods for registering Search feature services.
/// </summary>
public static class SearchFeatureExtensions
{
    /// <summary>
    /// Adds Search feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers IRequestHandler interfaces.
    /// Concrete handlers are registered here for direct dependency injection when needed.
    /// </remarks>
    public static IServiceCollection AddSearchFeature(this IServiceCollection services)
    {
        // Register concrete handler for direct resolution
        // IRequestHandler interface is auto-registered by MediatR assembly scanning
        services.AddTransient<SearchMemories.Handler>();

        return services;
    }
}

