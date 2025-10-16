using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Search.Handlers;

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
    public static IServiceCollection AddSearchFeature(this IServiceCollection services)
    {
        services.AddTransient<SearchMemoriesQueryHandler>();
        return services;
    }
}

