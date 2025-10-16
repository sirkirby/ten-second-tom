using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Templates.Commands;
using TenSecondTom.Features.Templates.Handlers;
using TenSecondTom.Features.Templates.Services;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Templates;

/// <summary>
/// Extension methods for registering Templates feature services.
/// </summary>
public static class TemplatesFeatureExtensions
{
    /// <summary>
    /// Adds Templates feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTemplatesFeature(this IServiceCollection services)
    {
        services.AddTransient<InstallDefaultTemplatesHandler>();
        services.AddTransient<IRequestHandler<InstallDefaultTemplatesCommand, Result<InstallDefaultTemplatesResult>>>(
            sp => sp.GetRequiredService<InstallDefaultTemplatesHandler>());
        services.AddTransient<ListTemplatesQueryHandler>();
        services.AddTransient<TemplateMigrationService>();
        return services;
    }
}

