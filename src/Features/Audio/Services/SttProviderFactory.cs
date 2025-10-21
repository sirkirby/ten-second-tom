using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Audio.Models;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// Factory for creating and selecting STT provider instances.
/// Implements the STT selection strategy (auto, local, or remote).
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
        SttSelection selection = SttSelection.Auto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting STT provider for selection: {Selection}", selection);

        return selection switch
        {
            SttSelection.Auto => await GetAutoProviderAsync(cancellationToken),
            SttSelection.Local => await GetLocalProviderAsync(cancellationToken),
            SttSelection.OpenAI => await GetOpenAiProviderAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, "Invalid STT selection")
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

    private async Task<ISttProvider?> GetAutoProviderAsync(CancellationToken cancellationToken)
    {
        // Try local first
        if (await _localProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Auto-selection: Using local whisper.cpp provider");
            return _localProvider;
        }

        _logger.LogDebug("Local whisper.cpp not available, fallback to OpenAI");

        // Fallback to OpenAI
        if (await _openAiProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Auto-selection: Fallback to OpenAI Whisper API provider (local unavailable)");
            return _openAiProvider;
        }

        _logger.LogWarning("No STT providers available (auto-selection)");
        return null;
    }

    private async Task<ISttProvider?> GetLocalProviderAsync(CancellationToken cancellationToken)
    {
        if (await _localProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using local whisper.cpp provider (explicit selection)");
            return _localProvider;
        }

        _logger.LogWarning("Local whisper.cpp provider not available");
        return null;
    }

    private async Task<ISttProvider?> GetOpenAiProviderAsync(CancellationToken cancellationToken)
    {
        if (await _openAiProvider.IsAvailableAsync(cancellationToken))
        {
            _logger.LogInformation("Using OpenAI Whisper API provider (explicit selection)");
            return _openAiProvider;
        }

        _logger.LogWarning("OpenAI Whisper API provider not available");
        return null;
    }
}
