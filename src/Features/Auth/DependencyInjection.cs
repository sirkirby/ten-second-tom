using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Auth.Handlers;

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
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddTransient<LoginCommandHandler>();
        services.AddTransient<LogoutCommandHandler>();
        return services;
    }
}

