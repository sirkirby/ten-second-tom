using Microsoft.Extensions.DependencyInjection;
using TenSecondTom.Features.Shell.Services;

namespace TenSecondTom.Features.Shell;

/// <summary>
/// Extension methods for registering Shell feature services.
/// </summary>
public static class ShellFeatureExtensions
{
    /// <summary>
    /// Adds Shell feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddShellFeature(this IServiceCollection services)
    {
        // Shell services (Singletons for session persistence during app lifetime)
        services.AddSingleton<IReplLoop, ReplLoop>();
        services.AddSingleton<ICommandRouter, CommandRouter>();
        services.AddSingleton<IHistoryStore, HistoryStore>();
        services.AddSingleton<ISessionManager, SessionManager>();
        services.AddSingleton<IAutocompleteEngine, AutocompleteEngine>();
        services.AddSingleton<IOutputPaginator, OutputPaginator>();

        // Enhanced input reader services (for Tab completion, history navigation, escape)
        services.AddSingleton<IConsoleKeyReader, SystemConsoleKeyReader>();
        services.AddSingleton<IEnhancedInputReader, EnhancedInputReader>();

        return services;
    }
}

