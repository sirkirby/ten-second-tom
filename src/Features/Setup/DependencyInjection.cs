using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Setup.Handlers;
using TenSecondTom.Features.Setup.Services;
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
        
        // Setup command factory (centralizes logic for creating SetupCommand with existing config)
        services.AddTransient<SetupCommandFactory>();
        
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
        
        // Configuration Storage - Unified service manages all configuration in appsettings.json
        services.AddSingleton<ConfigurationStorageService>();
        services.AddSingleton<IConfigurationStorageService>(sp => sp.GetRequiredService<ConfigurationStorageService>());
        services.AddSingleton<IAppSettingsStorageService>(sp => sp.GetRequiredService<ConfigurationStorageService>());

        // Configuration Migration - Detects and cleans up legacy user secrets
        services.AddSingleton<ConfigurationMigrationService>();

        // Setup Wizard UI
        services.AddTransient<ISetupWizardUI, SpectreConsoleSetupWizard>();
        
        return services;
    }
}

