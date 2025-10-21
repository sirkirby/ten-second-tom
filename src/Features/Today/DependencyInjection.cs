using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Handlers;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today;

/// <summary>
/// Extension methods for registering Today feature services.
/// </summary>
public static class TodayFeatureExtensions
{
    /// <summary>
    /// Adds Today feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTodayFeature(this IServiceCollection services)
    {
        // Register command handlers (dual registration: concrete + interface)
        // Both handlers have full dependencies for LLM processing
        services.AddTransient<CreateDailyEntryHandler>();
        services.AddTransient<IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>>(
            sp => sp.GetRequiredService<CreateDailyEntryHandler>());

        services.AddTransient<CreateVoiceNoteEntryHandler>();
        services.AddTransient<IRequestHandler<CreateVoiceNoteEntryCommand, Result<VoiceNoteEntry>>>(
            sp => sp.GetRequiredService<CreateVoiceNoteEntryHandler>());

        return services;
    }
}

