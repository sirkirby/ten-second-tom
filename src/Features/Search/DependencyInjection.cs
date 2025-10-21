using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Search.Handlers;
using TenSecondTom.Features.Search.Queries;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

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
        // Register query handler (dual registration: concrete + interface)
        services.AddTransient<SearchMemoriesQueryHandler>();
        services.AddTransient<IRequestHandler<SearchMemoriesQuery, Result<IReadOnlyList<MemoryEntry>>>>(
            sp => sp.GetRequiredService<SearchMemoriesQueryHandler>());

        return services;
    }
}

