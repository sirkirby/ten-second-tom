using Microsoft.Extensions.Logging;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Factory for creating and selecting STT provider instances.
/// Implements the STT selection strategy based on configuration.
/// </summary>
public sealed class SttProviderFactory : ISttProviderFactory
{
    private readonly ISttProvider _localProvider;
    private readonly ISttProvider _openAiProvider;
    private readonly ILogger<SttProviderFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SttProviderFactory"/> class.
    /// </summary>
    /// <param name="localProvider">The local whisper.cpp provider.</param>
    /// <param name="openAiProvider">The OpenAI Whisper API provider.</param>
    /// <param name="logger">Logger instance.</param>
    public SttProviderFactory(
        ISttProvider localProvider,
        ISttProvider openAiProvider,
        ILogger<SttProviderFactory> logger)
    {
        _localProvider = localProvider ?? throw new ArgumentNullException(nameof(localProvider));
        _openAiProvider = openAiProvider ?? throw new ArgumentNullException(nameof(openAiProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate that providers have correct engine types
        if (_localProvider.Engine != SttEngine.Local)
        {
            throw new ArgumentException($"Local provider must have Engine=Local, got {_localProvider.Engine}", nameof(localProvider));
        }

        if (_openAiProvider.Engine != SttEngine.OpenAI)
        {
            throw new ArgumentException($"OpenAI provider must have Engine=OpenAI, got {_openAiProvider.Engine}", nameof(openAiProvider));
        }
    }

    /// <inheritdoc/>
    public async Task<ISttProvider?> GetProviderAsync(
        AudioOptions configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration.SttProvider;
        var fallbackEnabled = configuration.SttFallbackEnabled;

        _logger.LogDebug(
            "Getting STT provider: Provider={Provider}, CloudFallback={FallbackEnabled}",
            provider,
            fallbackEnabled);

        // Normalize provider name for case-insensitive comparison
        var normalizedProvider = provider?.ToLowerInvariant();

        return normalizedProvider switch
        {
            SttProviders.WhisperCpp when fallbackEnabled =>
                await GetWhisperCppWithFallbackAsync(cancellationToken),
            SttProviders.WhisperCpp =>
                await GetWhisperCppOnlyAsync(cancellationToken),
            SttProviders.OpenAI =>
                await GetOpenAiProviderAsync(cancellationToken),
            _ => throw new ArgumentException(
                $"Invalid STT provider: {provider}. Valid values: {SttProviders.WhisperCpp}, {SttProviders.OpenAI}",
                nameof(configuration))
        };
    }

    /// <inheritdoc/>
    public ISttProvider GetProvider(SttEngine engine)
    {
        return engine switch
        {
            SttEngine.Local => _localProvider,
            SttEngine.OpenAI => _openAiProvider,
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Invalid STT engine")
        };
    }

    private async Task<ISttProvider?> GetWhisperCppWithFallbackAsync(CancellationToken cancellationToken)
    {
        // Try local first
        if (await _localProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using local whisper.cpp provider (fallback enabled)");
            return _localProvider;
        }

        _logger.LogDebug("Local whisper.cpp not available, attempting fallback to OpenAI");

        // Fallback to OpenAI
        if (await _openAiProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using OpenAI Whisper API provider (fallback from whisper.cpp)");
            return _openAiProvider;
        }

        _logger.LogWarning("No STT providers available (whisper.cpp unavailable, fallback failed)");
        return null;
    }

    private async Task<ISttProvider?> GetWhisperCppOnlyAsync(CancellationToken cancellationToken)
    {
        if (await _localProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using local whisper.cpp provider (fallback disabled)");
            return _localProvider;
        }

        _logger.LogWarning("Local whisper.cpp provider not available");
        return null;
    }

    private async Task<ISttProvider?> GetOpenAiProviderAsync(CancellationToken cancellationToken)
    {
        if (await _openAiProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using OpenAI Whisper API provider");
            return _openAiProvider;
        }

        _logger.LogWarning("OpenAI Whisper API provider not available");
        return null;
    }
}
