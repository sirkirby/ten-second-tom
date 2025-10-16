using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.ThisWeek.Handlers;

namespace TenSecondTom.Features.ThisWeek;

/// <summary>
/// Extension methods for registering ThisWeek feature services.
/// </summary>
public static class ThisWeekFeatureExtensions
{
    /// <summary>
    /// Adds ThisWeek feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddThisWeekFeature(this IServiceCollection services)
    {
        services.AddTransient<CreateWeeklyReviewHandler>();
        return services;
    }
}

