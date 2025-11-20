using Microsoft.Extensions.DependencyInjection;

namespace TenSecondTom.Infrastructure.Configuration;

/// <summary>
/// Extension methods for registering configuration infrastructure services.
/// </summary>
public static class ConfigurationInfrastructureExtensions
{
    /// <summary>
    /// Adds configuration infrastructure services to the service collection.
    /// Includes configuration storage and configuration management utilities.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConfigurationInfrastructure(this IServiceCollection services)
    {
        // Generic Configuration Section Store - VSA-compliant, feature-agnostic infrastructure
        // Provides type-safe read/write of any configuration section with NO feature knowledge
        services.AddSingleton<IConfigurationSectionStore, ConfigurationSectionStore>();

        // Configuration Migration - Detects and cleans up legacy user secrets
        services.AddSingleton<ConfigurationMigrationService>();

        return services;
    }
}
