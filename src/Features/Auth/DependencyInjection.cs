using Microsoft.Extensions.DependencyInjection;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Extension methods for registering Auth feature services.
/// </summary>
public static class AuthFeatureExtensions
{
    /// <summary>
    /// Adds Auth feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers IRequestHandler interfaces.
    /// Concrete handlers are registered here for direct dependency injection when needed.
    /// </remarks>
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        // Register concrete handlers for direct resolution
        // IRequestHandler interfaces are auto-registered by MediatR assembly scanning
        services.AddTransient<Login.Handler>();
        services.AddTransient<Logout.Handler>();

        return services;
    }
}

