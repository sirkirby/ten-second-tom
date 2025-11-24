using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Audio;
using TenSecondTom.Features.Auth;
using TenSecondTom.Features.Config;
using TenSecondTom.Features.Generate;
using TenSecondTom.Features.Llm;
using TenSecondTom.Features.Search;
using TenSecondTom.Features.Setup;
using TenSecondTom.Features.Shell;
using TenSecondTom.Features.Templates;
using TenSecondTom.Features.ThisWeek;
using TenSecondTom.Features.Today;
using TenSecondTom.Infrastructure.Notifications;

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
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAllFeatures(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core features (memory operations)
        services.AddTodayFeature();
        services.AddThisWeekFeature();
        services.AddSearchFeature();

        // Audio & Voice features
        services.AddAudioFeature(configuration);
        services.AddGenerateFeature();

        // Authentication & Security
        services.AddAuthFeature();

        // LLM & AI features
        services.AddLlmFeature();

        // Configuration & Setup
        services.AddConfigFeature();
        services.AddSetupFeature(configuration);
        services.AddTemplatesFeature();

        // Interactive Shell
        services.AddShellFeature();

        // Notification infrastructure
        services.AddNotificationFeature(configuration);

        return services;
    }
}

