using Microsoft.Extensions.DependencyInjection;

namespace TenSecondTom.Features.Retry;

/// <summary>
/// Extension methods for registering Retry feature services.
/// </summary>
public static class RetryFeatureExtensions
{
    /// <summary>
    /// Adds Retry feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers IRequestHandler interfaces.
    /// Concrete handlers are registered here for direct dependency injection when needed.
    /// </remarks>
    public static IServiceCollection AddRetryFeature(this IServiceCollection services)
    {
        // Register concrete handler for direct resolution
        // IRequestHandler interface is auto-registered by MediatR assembly scanning
        services.AddTransient<RetryFailedSummarization.Handler>();

        return services;
    }
}
