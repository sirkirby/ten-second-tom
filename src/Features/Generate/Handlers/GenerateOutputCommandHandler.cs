using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Generate.Commands;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate.Handlers;

/// <summary>
/// Handles generation of outputs from recordings using LLM providers.
/// Orchestrates: transcript loading, template processing, LLM interaction, output storage.
/// </summary>
public sealed class GenerateOutputCommandHandler
    : IRequestHandler<GenerateOutputCommand, Result<GeneratedOutput>>
{
    private readonly IRecordingService _recordingService;
    private readonly IPromptTemplateLoader _templateLoader;
    private readonly ITranscriptProcessor _transcriptProcessor;
    private readonly ILlmProviderFactory _llmProviderFactory;
    private readonly IConfiguration _configuration;
    private readonly IOutputStorageService _outputStorageService;
    private readonly ILogger<GenerateOutputCommandHandler> _logger;

    public GenerateOutputCommandHandler(
        IRecordingService recordingService,
        IPromptTemplateLoader templateLoader,
        ITranscriptProcessor transcriptProcessor,
        ILlmProviderFactory llmProviderFactory,
        IConfiguration configuration,
        IOutputStorageService outputStorageService,
        ILogger<GenerateOutputCommandHandler> logger)
    {
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _templateLoader = templateLoader ?? throw new ArgumentNullException(nameof(templateLoader));
        _transcriptProcessor = transcriptProcessor ?? throw new ArgumentNullException(nameof(transcriptProcessor));
        _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _outputStorageService = outputStorageService ?? throw new ArgumentNullException(nameof(outputStorageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<GeneratedOutput>> Handle(
        GenerateOutputCommand request,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        _logger.LogInformation(
            "Starting generation for recording {Recording} with template {Template}",
            request.RecordingBaseName,
            request.TemplateId);

        // Get LLM provider from configuration
        string? providerName = _configuration[ConfigurationKeys.LlmProvider];
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return Result<GeneratedOutput>.Failure(
                "LLM provider not configured. Please run 'tom setup' to configure your LLM provider.");
        }

        ILlmProvider llmProvider = _llmProviderFactory.CreateProvider(providerName);

        // 1. Validate transcript file
        var validateResult = await _recordingService.ValidateTranscriptFileAsync(
            request.TranscriptFilePath,
            cancellationToken);

        if (!validateResult.IsSuccess)
        {
            return Result<GeneratedOutput>.Failure(validateResult.Error!);
        }

        // 2. Load template
        var templateResult = await _templateLoader.LoadTemplateAsync(
            request.TemplateId,
            cancellationToken);

        if (!templateResult.IsSuccess)
        {
            return Result<GeneratedOutput>.Failure(templateResult.Error!);
        }

        var template = templateResult.Value;

        // 3. Load transcript content
        var transcriptResult = await _recordingService.GetTranscriptContentAsync(
            request.TranscriptFilePath,
            cancellationToken);

        if (!transcriptResult.IsSuccess)
        {
            return Result<GeneratedOutput>.Failure(transcriptResult.Error!);
        }

        var transcriptContent = transcriptResult.Value;

        // 4. Process transcript (truncate if needed)
        var processedResult = await _transcriptProcessor.ProcessTranscriptAsync(
            transcriptContent,
            request.MaxInputTokens,
            cancellationToken);

        if (!processedResult.IsSuccess)
        {
            return Result<GeneratedOutput>.Failure(processedResult.Error!);
        }

        var processed = processedResult.Value;

        // 5. Display truncation warning if applicable
        if (processed.WasTruncated)
        {
            _logger.LogWarning(
                "Transcript truncated from {OriginalWords} to {FinalWords} words",
                processed.OriginalWordCount,
                processed.FinalWordCount);
        }

        // 6. Build prompt by substituting template variables
        var prompt = template.Content.Replace("{{TRANSCRIPT}}", processed.Content);

        // 7. Call LLM provider with comprehensive error handling
        Result<Infrastructure.Llm.LlmResponse> llmResult;

        try
        {
            llmResult = await llmProvider.GenerateCompletionAsync(
                prompt,
                cancellationToken);

            if (!llmResult.IsSuccess)
            {
                _logger.LogError(
                    "LLM provider returned error for {Recording}: {Error}",
                    request.RecordingBaseName,
                    llmResult.Error);

                return Result<GeneratedOutput>.Failure(
                    $"LLM generation failed: {llmResult.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("LLM request cancelled for {Recording}", request.RecordingBaseName);
            return Result<GeneratedOutput>.Failure("Operation was cancelled");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(
                ex,
                "LLM request timed out for {Recording}",
                request.RecordingBaseName);

            return Result<GeneratedOutput>.Failure(
                "LLM request timed out. The service may be experiencing delays. Please try again.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Network error during LLM request for {Recording}",
                request.RecordingBaseName);

            return Result<GeneratedOutput>.Failure(
                "Network error: Unable to reach LLM service. Please check your internet connection and try again.");
        }
        catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                ex,
                "Rate limit exceeded for {Recording}",
                request.RecordingBaseName);

            return Result<GeneratedOutput>.Failure(
                "Rate limit exceeded. Please wait a moment and try again.");
        }
        catch (Exception ex) when (ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                ex,
                "API quota exceeded for {Recording}",
                request.RecordingBaseName);

            return Result<GeneratedOutput>.Failure(
                "API quota exceeded. Please check your account limits.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during LLM request for {Recording}",
                request.RecordingBaseName);

            return Result<GeneratedOutput>.Failure(
                $"LLM service error: {ex.Message}. Please try again.");
        }

        var llmResponse = llmResult.Value;

        // 8. Strip markdown code block wrappers if present (defensive measure)
        string cleanedContent = llmResponse.Content.StripMarkdownCodeBlock();

        // 9. Build GeneratedOutput
        var output = new GeneratedOutput
        {
            Content = cleanedContent,
            RecordingBaseName = request.RecordingBaseName,
            TemplateId = template.TemplateId,
            TemplateTitle = template.Metadata?.Title ?? template.TemplateId,
            GeneratedAt = DateTimeOffset.UtcNow,
            ProviderName = llmProvider.ProviderName,
            ModelName = llmProvider.ModelName,
            InputTokens = llmResponse.InputTokens,
            OutputTokens = llmResponse.OutputTokens,
            WasTruncated = processed.WasTruncated,
            OriginalWordCount = processed.OriginalWordCount
        };

        // 10. Save output to filesystem
        var saveResult = await _outputStorageService.SaveOutputAsync(
            output,
            cancellationToken);

        if (!saveResult.IsSuccess)
        {
            return Result<GeneratedOutput>.Failure(saveResult.Error!);
        }

        output = output with { OutputFilePath = saveResult.Value };

        // 11. Log completion with performance and audit trail
        var duration = DateTimeOffset.UtcNow - startTime;

        _logger.LogInformation(
            "Generation completed for {Recording} using {Template}: {OutputPath} " +
            "(Duration: {Duration:F2}s, Provider: {Provider}, Model: {Model}, " +
            "Tokens: {InputTokens} input + {OutputTokens} output = {TotalTokens} total, " +
            "Truncated: {WasTruncated})",
            request.RecordingBaseName,
            template.TemplateId,
            output.OutputFilePath,
            duration.TotalSeconds,
            output.ProviderName,
            output.ModelName,
            output.InputTokens,
            output.OutputTokens,
            output.TotalTokens,
            output.WasTruncated);

        return Result<GeneratedOutput>.Success(output);
    }
}
