using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Commands;
using TenSecondTom.Features.Audio.Handlers;
using TenSecondTom.Features.Audio.Models;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio;

/// <summary>
/// Extension methods for registering Audio feature services.
/// </summary>
public static class AudioFeatureExtensions
{
    /// <summary>
    /// Adds Audio feature services to the service collection.
    /// Registers audio recording, transcription, and related services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureAudioServices(this IServiceCollection services)
    {
        // Register audio recorder implementation
        services.AddScoped<IAudioRecorder, FfmpegAudioRecorder>();

        // Register audio preprocessor implementation
        services.AddScoped<IAudioPreprocessor, FfmpegAudioPreprocessor>();

        // Register STT provider implementations
        services.AddScoped<LocalWhisperSttProvider>();
        services.AddScoped<OpenAiSttProvider>();

        // Register STT provider factory with named providers
        services.AddScoped<ISttProviderFactory>(sp =>
        {
            var localProvider = sp.GetRequiredService<LocalWhisperSttProvider>();
            var openAiProvider = sp.GetRequiredService<OpenAiSttProvider>();
            var logger = sp.GetRequiredService<ILogger<SttProviderFactory>>();
            return new SttProviderFactory(localProvider, openAiProvider, logger);
        });

        // Register command handlers (dual registration: concrete + interface)
        services.AddScoped<RecordAudioCommandHandler>();
        services.AddScoped<IRequestHandler<RecordAudioCommand, Result<AudioRecording>>>(
            sp => sp.GetRequiredService<RecordAudioCommandHandler>());

        services.AddScoped<TranscribeAudioCommandHandler>();
        services.AddScoped<IRequestHandler<TranscribeAudioCommand, Result<TranscriptionResult>>>(
            sp => sp.GetRequiredService<TranscribeAudioCommandHandler>());

        services.AddScoped<RecordCommandHandler>();
        services.AddScoped<IRequestHandler<RecordCommand, Result<StoredRecording>>>(
            sp => sp.GetRequiredService<RecordCommandHandler>());

        return services;
    }
}
