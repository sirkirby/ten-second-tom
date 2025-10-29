using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Templates.Services;

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
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers all IRequestHandler implementations
    /// from co-located use case files (InstallDefaultTemplates.Handler, ListTemplates.Handler).
    ///
    /// Domain services (TemplateValidator) are registered explicitly for direct dependency injection.
    /// </remarks>
    public static IServiceCollection AddTemplatesFeature(this IServiceCollection services)
    {
        // Register domain services
        services.AddTransient<TemplateValidator>();

        // MediatR handlers are auto-discovered via assembly scanning
        // No explicit registration needed for:
        // - InstallDefaultTemplates.Handler
        // - ListTemplates.Handler

        return services;
    }
}

