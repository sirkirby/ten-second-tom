using Microsoft.Extensions.DependencyInjection;

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
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers IRequestHandler interfaces.
    /// Concrete handlers are registered here for direct dependency injection when needed.
    /// </remarks>
    public static IServiceCollection AddTodayFeature(this IServiceCollection services)
    {
        // Register concrete handlers for direct resolution
        // IRequestHandler interfaces are auto-registered by MediatR assembly scanning
        services.AddTransient<CreateDailyEntry.Handler>();
        services.AddTransient<CreateVoiceNoteEntry.Handler>();

        return services;
    }
}

