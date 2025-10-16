using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Today.Handlers;

namespace TenSecondTom.Features.Today;

/// <summary>
/// Extension methods for registering Today feature services.
/// </summary>
public static class TodayFeatureExtensions
{
    /// <summary>
    /// Adds Today feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTodayFeature(this IServiceCollection services)
    {
        services.AddTransient<CreateDailyEntryHandler>();
        return services;
    }
}

