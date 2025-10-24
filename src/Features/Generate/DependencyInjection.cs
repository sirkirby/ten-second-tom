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
    public static IServiceCollection AddGenerateFeature(this IServiceCollection services)
    {
        // Register domain services
        services.AddTransient<Services.IRecordingService, Services.RecordingService>();
        services.AddTransient<Services.ITranscriptProcessor, Services.TranscriptProcessor>();
        services.AddTransient<Services.IOutputStorageService, Services.OutputStorageService>();

        // Register command/query handlers
        services.AddTransient<Handlers.GenerateOutputCommandHandler>();
        services.AddTransient<Shared.Contracts.IRequestHandler<Commands.GenerateOutputCommand, Shared.Results.Result<Models.GeneratedOutput>>>(
            sp => sp.GetRequiredService<Handlers.GenerateOutputCommandHandler>());

        services.AddTransient<Handlers.ListRecordingsQueryHandler>();
        services.AddTransient<Shared.Contracts.IRequestHandler<Queries.ListRecordingsQuery, Shared.Results.Result<System.Collections.Generic.IReadOnlyList<Models.RecordingListItem>>>>(
            sp => sp.GetRequiredService<Handlers.ListRecordingsQueryHandler>());

        services.AddTransient<Handlers.GetRecordingTranscriptQueryHandler>();
        services.AddTransient<Shared.Contracts.IRequestHandler<Queries.GetRecordingTranscriptQuery, Shared.Results.Result<string>>>(
            sp => sp.GetRequiredService<Handlers.GetRecordingTranscriptQueryHandler>());

        return services;
    }
}
