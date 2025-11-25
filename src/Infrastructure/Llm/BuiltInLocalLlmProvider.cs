using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Shared.Abstractions.LocalAi;
using TenSecondTom.Shared.Abstractions.Models;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Infrastructure.Llm;

/// <summary>
/// LLM provider implementation for Tom's built-in local engine.
/// </summary>
public sealed class BuiltInLocalLlmProvider : ILlmProvider, ISupportsModelManagement
{
    private readonly ILocalAiEngine _localAiEngine;
    private readonly LlmOptions _options;

    public BuiltInLocalLlmProvider(
        ILocalAiEngine localAiEngine,
        IOptions<LlmOptions> options)
    {
        _localAiEngine = localAiEngine;
        _options = options.Value;
    }

    public string ProviderName => "BuiltInLocal";

    // Get model from BuiltInLocal provider config, with fallback to default
    public string ModelName => _options.GetModel(LlmProvider.BuiltInLocal) ?? "phi-3.5-mini-instruct";

    public async Task<Result<LlmResponse>> GenerateCompletionAsync(
        string prompt,
        CancellationToken cancellationToken,
        int? maxTokens = null,
        double? temperature = null)
    {
        var result = await _localAiEngine.CompleteAsync(
            ModelName,
            prompt,
            maxTokens,
            temperature,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<LlmResponse>.Failure(result.Error ?? "Unknown error generating completion");
        }

        return Result<LlmResponse>.Success(new LlmResponse
        {
            Content = result.Value,
            InputTokens = 0,
            OutputTokens = 0
        });
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
