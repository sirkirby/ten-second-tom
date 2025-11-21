using Microsoft.Extensions.DependencyInjection;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// Extension methods for registering Generate feature services.
/// </summary>
public static class GenerateFeatureExtensions
{
    /// <summary>
    /// Adds Generate feature services to the service collection.
    /// Registers all commands, queries, handlers, and domain services required for the Generate feature.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// MediatR assembly scanning automatically discovers and registers IRequestHandler interfaces.
    /// Concrete handlers are registered here for direct dependency injection when needed.
    /// </remarks>
    public static IServiceCollection AddGenerateFeature(this IServiceCollection services)
    {
        // Register domain services
        services.AddTransient<Services.IRecordingService, Services.RecordingService>();
        services.AddTransient<Services.ITranscriptProcessor, Services.TranscriptProcessor>();
        services.AddTransient<Services.IOutputStorageService, Services.OutputStorageService>();

        // Register concrete handlers for direct resolution
        // IRequestHandler interfaces are auto-registered by MediatR assembly scanning
        services.AddTransient<GenerateOutput.Handler>();
        services.AddTransient<ListRecordings.Handler>();
        services.AddTransient<ListNotes.Handler>();
        services.AddTransient<GetRecordingTranscript.Handler>();

        return services;
    }
}
