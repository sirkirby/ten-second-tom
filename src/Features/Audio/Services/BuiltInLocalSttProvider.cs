using Microsoft.Extensions.Options;
using TenSecondTom.Features.Audio.Services;
using TenSecondTom.Shared.Abstractions.LocalAi;
using TenSecondTom.Shared.Abstractions.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Audio.Services;

/// <summary>
/// STT provider implementation for Tom's built-in local engine.
/// </summary>
public sealed class BuiltInLocalSttProvider : ISttProvider, ISupportsModelManagement
{
    private readonly ILocalAiEngine _localAiEngine;
    private readonly AudioOptions _options;

    public BuiltInLocalSttProvider(
        ILocalAiEngine localAiEngine,
        IOptions<AudioOptions> options)
    {
        _localAiEngine = localAiEngine;
        _options = options.Value;
    }

    public SttEngine Engine => SttEngine.Local; // Closest match, though it's generic local

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // For built-in, availability depends on the SDK being initialized and model being present.
        // We can consider it "available" if the engine is registered.
        // A more robust check would be to see if the model is downloaded.
        return true;
    }

    public async Task<Result<TranscriptionResult>> TranscribeAsync(
        string audioFilePath,
        CancellationToken cancellationToken = default)
    {
        var modelId = _options.GetSttModel() ?? "openai/whisper"; // Default if not set

        return await _localAiEngine.TranscribeAsync(
            modelId,
            audioFilePath,
            cancellationToken);
    }

    public async Task<IEnumerable<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        return await _localAiEngine.ListAvailableModelsAsync(cancellationToken);
    }

    public async Task<Result> DownloadModelAsync(
        string modelId,
        Action<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await _localAiEngine.EnsureModelAvailableAsync(modelId, progress, cancellationToken);
    }
}
