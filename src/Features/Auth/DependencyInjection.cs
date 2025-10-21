using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Auth.Commands;
using TenSecondTom.Features.Auth.Handlers;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Auth;

/// <summary>
/// Extension methods for registering Auth feature services.
/// </summary>
public static class AuthFeatureExtensions
{
    /// <summary>
    /// Adds Auth feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        // Register command handlers (dual registration: concrete + interface)
        services.AddTransient<LoginCommandHandler>();
        services.AddTransient<IRequestHandler<LoginCommand, Result<UserSession>>>(
            sp => sp.GetRequiredService<LoginCommandHandler>());

        services.AddTransient<LogoutCommandHandler>();
        services.AddTransient<IRequestHandler<LogoutCommand, Result<bool>>>(
            sp => sp.GetRequiredService<LogoutCommandHandler>());

        return services;
    }
}

