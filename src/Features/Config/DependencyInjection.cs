using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Config.Services;
using TenSecondTom.Shared.Abstractions.Configuration;

namespace TenSecondTom.Features.Config;

/// <summary>
/// Dependency injection registration for the Config feature.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers all Config feature services.
    /// MediatR handlers are auto-discovered via assembly scanning.
    /// </summary>
    public static IServiceCollection AddConfigFeature(this IServiceCollection services)
    {
        // MediatR auto-discovers ShowConfig.Handler via assembly scanning
        // But CLI also needs direct DI resolution, so register explicitly
        services.AddTransient<ShowConfig.Handler>();
        services.AddTransient<IConfigOperationService, ConfigOperationService>();

        return services;
    }
}
