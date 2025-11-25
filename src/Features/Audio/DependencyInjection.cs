using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Abstractions.Audio;
using TenSecondTom.Shared.Options;

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

        // Register Whisper.NET model manager for model listing and downloading
        // Uses Whisper.NET's built-in Hugging Face downloader - no external binary needed
        services.AddSingleton<IWhisperNetModelManager, WhisperNetModelManager>();

        // Legacy whisper.cpp model manager (kept for backward compatibility during migration)
        services.AddSingleton<IWhisperCppModelManager, WhisperCppModelManager>();

        // Register audio library discovery service
        services.AddScoped<IAudioLibraryService, AudioLibraryService>();

        // Register STT provider implementations
        services.AddScoped<BuiltInLocalSttProvider>();
        services.AddScoped<WhisperNetSttProvider>();  // Whisper.NET-based (no external binary)
        services.AddScoped<LocalWhisperSttProvider>(); // Legacy whisper-cli based
        services.AddScoped<OpenAiSttProvider>();

        // Register STT provider factory with named providers
        // Uses WhisperNetSttProvider as the default for whisper-cpp (no installation required)
        services.AddScoped<ISttProviderFactory>(sp =>
        {
            var builtInLocalProvider = sp.GetRequiredService<BuiltInLocalSttProvider>();
            var whisperNetProvider = sp.GetRequiredService<WhisperNetSttProvider>();
            var openAiProvider = sp.GetRequiredService<OpenAiSttProvider>();
            var logger = sp.GetRequiredService<ILogger<SttProviderFactory>>();
            return new SttProviderFactory(builtInLocalProvider, whisperNetProvider, openAiProvider, logger);
        });

        // Register concrete handlers for direct resolution
        // IRequestHandler interfaces are auto-registered by MediatR assembly scanning
        services.AddScoped<RecordAudio.Handler>();
        services.AddScoped<TranscribeAudio.Handler>();
        services.AddScoped<Record.Handler>();
        services.AddScoped<TranscribeLibraryAudio.Handler>();
        services.AddScoped<GetAudioConfiguration.Handler>();
        services.AddScoped<UpdateAudioConfiguration.Handler>();

        return services;
    }
}
