using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Auth;
using TenSecondTom.Features.Search;
using TenSecondTom.Features.Setup;
using TenSecondTom.Features.Shell;
using TenSecondTom.Features.Templates;
using TenSecondTom.Features.ThisWeek;
using TenSecondTom.Features.Today;

namespace TenSecondTom.Infrastructure.DependencyInjection;

/// <summary>
/// Extension methods for registering all feature slices with the DI container.
/// </summary>
public static class FeatureRegistrationExtensions
{
    /// <summary>
    /// Registers all feature slices with the DI container.
    /// Features are grouped logically for clarity and maintainability.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAllFeatures(this IServiceCollection services)
    {
        // Core features (memory operations)
        services.AddTodayFeature();
        services.AddThisWeekFeature();
        services.AddSearchFeature();
        
        // Authentication & Security
        services.AddAuthFeature();
        
        // Configuration & Setup
        services.AddSetupFeature();
        services.AddTemplatesFeature();
        
        // Interactive Shell
        services.AddShellFeature();
        
        return services;
    }
}

