using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.ThisWeek.Commands;
using TenSecondTom.Features.ThisWeek.Handlers;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.ThisWeek;

/// <summary>
/// Extension methods for registering ThisWeek feature services.
/// </summary>
public static class ThisWeekFeatureExtensions
{
    /// <summary>
    /// Adds ThisWeek feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddThisWeekFeature(this IServiceCollection services)
    {
        // Register command handler (dual registration: concrete + interface)
        services.AddTransient<CreateWeeklyReviewHandler>();
        services.AddTransient<IRequestHandler<CreateWeeklyReviewCommand, Result<WeeklyEntry>>>(
            sp => sp.GetRequiredService<CreateWeeklyReviewHandler>());

        return services;
    }
}

