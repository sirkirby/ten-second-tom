using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Queries;
using TenSecondTom.Features.Setup.Services;
using TenSecondTom.Features.Setup.Validation;
using TenSecondTom.Infrastructure.Auth.SshProviders;
using TenSecondTom.Infrastructure.Configuration;

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
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSetupFeature(this IServiceCollection services)
    {
        // Command handlers
        services.AddTransient<SetupCommandHandler>();
        services.AddTransient<ConfigCommandHandler>();
        
        // Application bootstrapper (coordinates startup/setup logic)
        services.AddTransient<ApplicationBootstrapper>();
        
        // SSH Key Detectors - registered as both concrete types and interface for factory injection
        services.AddTransient<ISshKeyDetector, SystemSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, OnePasswordSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, SecretiveSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, FileSystemSshKeyDetector>();
        services.AddSingleton<ISshKeyDetectorFactory, SshKeyDetectorFactory>();
        
        // API Key Validators
        services.AddTransient<IApiKeyValidator, OpenAIApiKeyValidator>();
        services.AddTransient<IApiKeyValidator, AnthropicApiKeyValidator>();
        
        // Configuration Storage
        services.AddSingleton<IConfigurationStorageService, UserSecretsStorageService>();
        services.AddSingleton<IAppSettingsStorageService, AppSettingsStorageService>();

        // Setup Wizard UI
        services.AddTransient<ISetupWizardUI, SpectreConsoleSetupWizard>();
        
        return services;
    }
}

