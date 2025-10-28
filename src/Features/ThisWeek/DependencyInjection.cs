using Microsoft.Extensions.DependencyInjection;

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
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers IRequestHandler interfaces.
    /// Concrete handlers are registered here for direct dependency injection when needed.
    /// </remarks>
    public static IServiceCollection AddThisWeekFeature(this IServiceCollection services)
    {
        // Register concrete handler for direct resolution
        // IRequestHandler interface is auto-registered by MediatR assembly scanning
        services.AddTransient<CreateWeeklyReview.Handler>();

        return services;
    }
}

