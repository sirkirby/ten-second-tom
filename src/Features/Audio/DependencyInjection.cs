using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Options;
using TenSecondTom.Features.Audio.Services;

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
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAudioFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register feature-owned configuration using Options Pattern
        services.AddOptions<AudioOptions>()
            .BindConfiguration(AudioOptions.SectionPath)
            .ValidateOnStart();

        // Register audio configuration validator
        services.AddSingleton<IAudioConfigurationValidator, AudioConfigurationValidator>();

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

        // Register concrete handlers for direct resolution
        // IRequestHandler interfaces are auto-registered by MediatR assembly scanning
        services.AddScoped<RecordAudio.Handler>();
        services.AddScoped<TranscribeAudio.Handler>();
        services.AddScoped<Record.Handler>();
        services.AddScoped<GetAudioConfiguration.Handler>();
        services.AddScoped<UpdateAudioConfiguration.Handler>();

        return services;
    }
}
