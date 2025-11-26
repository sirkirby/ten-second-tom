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
    private readonly ISttProvider _builtInLocalProvider;
    private readonly ISttProvider _whisperCppProvider;
    private readonly ISttProvider _openAiProvider;
    private readonly ILogger<SttProviderFactory> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SttProviderFactory"/> class.
    /// </summary>
    /// <param name="builtInLocalProvider">The built-in local provider (Foundry Local SDK).</param>
    /// <param name="whisperCppProvider">The local whisper.cpp provider.</param>
    /// <param name="openAiProvider">The OpenAI Whisper API provider.</param>
    /// <param name="logger">Logger instance.</param>
    public SttProviderFactory(
        ISttProvider builtInLocalProvider,
        ISttProvider whisperCppProvider,
        ISttProvider openAiProvider,
        ILogger<SttProviderFactory> logger)
    {
        _builtInLocalProvider = builtInLocalProvider ?? throw new ArgumentNullException(nameof(builtInLocalProvider));
        _whisperCppProvider = whisperCppProvider ?? throw new ArgumentNullException(nameof(whisperCppProvider));
        _openAiProvider = openAiProvider ?? throw new ArgumentNullException(nameof(openAiProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Validate that providers have correct engine types
        if (_builtInLocalProvider.Engine != SttEngine.Local)
        {
            throw new ArgumentException($"Built-in local provider must have Engine=Local, got {_builtInLocalProvider.Engine}", nameof(builtInLocalProvider));
        }

        if (_whisperCppProvider.Engine != SttEngine.Local)
        {
            throw new ArgumentException($"Whisper.cpp provider must have Engine=Local, got {_whisperCppProvider.Engine}", nameof(whisperCppProvider));
        }

        if (_openAiProvider.Engine != SttEngine.OpenAI)
        {
            throw new ArgumentException($"OpenAI provider must have Engine=OpenAI, got {_openAiProvider.Engine}", nameof(openAiProvider));
        }
    }

    /// <inheritdoc/>
    public async Task<ISttProvider?> GetProviderAsync(
        TranscribeOptions configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = configuration.SttProvider;

        _logger.LogDebug("Getting STT provider: Provider={Provider}", provider);

        // Normalize provider name for case-insensitive comparison
        var normalizedProvider = provider?.ToLowerInvariant();

        return normalizedProvider switch
        {
            SttProviders.BuiltInLocal =>
                await GetBuiltInLocalProviderAsync(cancellationToken),
            SttProviders.WhisperCpp =>
                await GetWhisperCppProviderAsync(cancellationToken),
            SttProviders.OpenAI =>
                await GetOpenAiProviderAsync(cancellationToken),
            _ => throw new ArgumentException(
                $"Invalid STT provider: {provider}. Valid values: {SttProviders.BuiltInLocal}, {SttProviders.WhisperCpp}, {SttProviders.OpenAI}",
                nameof(configuration))
        };
    }

    /// <inheritdoc/>
    public ISttProvider GetProvider(SttEngine engine)
    {
        return engine switch
        {
            // For SttEngine.Local, prefer built-in local provider
            SttEngine.Local => _builtInLocalProvider,
            SttEngine.OpenAI => _openAiProvider,
            _ => throw new ArgumentOutOfRangeException(nameof(engine), engine, "Invalid STT engine")
        };
    }

    private Task<ISttProvider?> GetBuiltInLocalProviderAsync(CancellationToken cancellationToken)
    {
        // Built-in local provider using Microsoft AI Foundry Local SDK
        _logger.LogInformation("Using built-in local STT provider (Foundry Local SDK)");
        return Task.FromResult<ISttProvider?>(_builtInLocalProvider);
    }

    private async Task<ISttProvider?> GetWhisperCppProviderAsync(CancellationToken cancellationToken)
    {
        if (await _whisperCppProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using Whisper.NET local provider");
            return _whisperCppProvider;
        }

        _logger.LogWarning("Whisper.NET local provider not available (model not configured)");
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
