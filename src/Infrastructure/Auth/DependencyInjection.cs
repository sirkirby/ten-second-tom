using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Infrastructure.Auth.SshProviders;

namespace TenSecondTom.Infrastructure.Auth;

/// <summary>
/// Extension methods for registering authentication infrastructure services.
/// </summary>
public static class AuthenticationInfrastructureExtensions
{
    /// <summary>
    /// Adds authentication infrastructure services to the service collection.
    /// Includes SSH key detection, SSH agent clients, and authentication service factories.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthenticationInfrastructure(this IServiceCollection services)
    {
        // SSH Key Detectors - registered as both concrete types and interface for factory injection
        services.AddTransient<ISshKeyDetector, SystemSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, OnePasswordSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, SecretiveSshAgentDetector>();
        services.AddTransient<ISshKeyDetector, FileSystemSshKeyDetector>();
        services.AddSingleton<ISshKeyDetectorFactory, SshKeyDetectorFactory>();

        return services;
    }
}
