using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Abstractions.Validation;

namespace TenSecondTom.Features.Setup;

/// <summary>
/// Extension methods for registering Setup feature services.
/// </summary>
public static class SetupFeatureExtensions
{
    /// <summary>
    /// Adds Setup feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to bind Setup options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSetupFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Command handlers (nested classes - auto-discovered by MediatR, but register for direct DI access)
        services.AddTransient<Setup.Handler>();

        // Application bootstrapper (coordinates startup/setup logic)
        services.AddTransient<ApplicationBootstrapper>();

        // API Key Validators (Setup feature-specific services)
        services.AddTransient<IApiKeyValidator, OpenAIApiKeyValidator>();
        services.AddTransient<IApiKeyValidator, AnthropicApiKeyValidator>();

        // Setup Wizard UI
        services.AddTransient<ISetupWizardUI, SpectreConsoleSetupWizard>();

        return services;
    }
}

