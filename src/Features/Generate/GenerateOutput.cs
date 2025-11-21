using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Generate.Models;
using TenSecondTom.Features.Generate.Services;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using MediatR;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Generate;

/// <summary>
/// Generates output from a recording transcript using a prompt template.
/// This is the main command for the 'tom generate' CLI operation.
/// </summary>
public static class GenerateOutput
{
    /// <summary>
    /// Command to generate output from a recording transcript using a prompt template.
    /// This is the main command for the 'tom generate' CLI operation.
    /// </summary>
    public sealed record Command : IRequest<Result<GeneratedOutput>>
    {
        /// <summary>
        /// Gets the path to the transcript file to process.
        /// Must be a valid path in the recording directory.
        /// </summary>
        public required string TranscriptFilePath { get; init; }

        /// <summary>
        /// Gets the base name of the input (without extension).
        /// Used for output file naming.
        /// Example: "10-21-2025_1" or "MyNote"
        /// </summary>
        public required string InputName { get; init; }

        /// <summary>
        /// Gets the type of input (Recording or Note).
        /// </summary>
        public required string InputType { get; init; }

        /// <summary>
        /// Gets the template ID to use for generation.
        /// Must match an existing template in the system.
        /// </summary>
        public required string TemplateId { get; init; }

        /// <summary>
        /// Gets the maximum input tokens allowed for LLM processing.
        /// Transcripts exceeding this limit will be truncated.
        /// </summary>
        public required int MaxInputTokens { get; init; }

        /// <summary>
        /// Gets optional cancellation token for async operations.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }
    }

    /// <summary>
    /// Handles generation of outputs from recordings using LLM providers.
    /// Orchestrates: transcript loading, template processing, LLM interaction, output storage.
    /// </summary>
    public sealed class Handler(
        IRecordingService recordingService,
        IPromptTemplateLoader templateLoader,
        ITranscriptProcessor transcriptProcessor,
        ILlmProviderFactory llmProviderFactory,
        IOptionsSnapshot<LlmOptions> llmOptions,
        IOutputStorageService outputStorageService,
        ILogger<Handler> logger)
        : IRequestHandler<Command, Result<GeneratedOutput>>
    {
        private readonly LlmOptions _llmOptions = llmOptions.Value;

        public async Task<Result<GeneratedOutput>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            var startTime = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "Starting generation for {InputType} {InputName} with template {Template}",
                request.InputType,
                request.InputName,
                request.TemplateId);

            // Get LLM provider from options
            string providerName = _llmOptions.Provider.ToString();

            ILlmProvider llmProvider = llmProviderFactory.CreateProvider(providerName);

            // 1. Validate transcript file
            var validateResult = await recordingService.ValidateTranscriptFileAsync(
                request.TranscriptFilePath,
                cancellationToken);

            if (!validateResult.IsSuccess)
            {
                return Result<GeneratedOutput>.Failure(validateResult.Error!);
            }

            // 2. Load template
            var templateResult = await templateLoader.LoadTemplateAsync(
                request.TemplateId,
                cancellationToken);

            if (!templateResult.IsSuccess)
            {
                return Result<GeneratedOutput>.Failure(templateResult.Error!);
            }

            var template = templateResult.Value;

            // 3. Load transcript content
            var transcriptResult = await recordingService.GetTranscriptContentAsync(
                request.TranscriptFilePath,
                cancellationToken);

            if (!transcriptResult.IsSuccess)
            {
                return Result<GeneratedOutput>.Failure(transcriptResult.Error!);
            }

            var transcriptContent = transcriptResult.Value;

            // 4. Process transcript (truncate if needed)
            var processedResult = await transcriptProcessor.ProcessTranscriptAsync(
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
                logger.LogWarning(
                    "Transcript truncated from {OriginalWords} to {FinalWords} words",
                    processed.OriginalWordCount,
                    processed.FinalWordCount);
            }

            // 6. Build prompt by substituting standard template variables
            // Parse date from input name if possible, otherwise use current date
            DateTimeOffset inputDate = ParseInputDate(request.InputName);
            string dateString = inputDate.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

            var prompt = template.Content
                .Replace("{{USER_INPUT}}", processed.Content)
                .Replace("{{DATE}}", dateString);

            // 7. Call LLM provider with comprehensive error handling
            Result<Infrastructure.Llm.LlmResponse> llmResult;

            try
            {
                llmResult = await llmProvider.GenerateCompletionAsync(
                    prompt,
                    cancellationToken);

                if (!llmResult.IsSuccess)
                {
                    logger.LogError(
                        "LLM provider returned error for {InputName}: {Error}",
                        request.InputName,
                        llmResult.Error);

                    return Result<GeneratedOutput>.Failure(
                        $"LLM generation failed: {llmResult.Error}");
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("LLM request cancelled for {InputName}", request.InputName);
                return Result<GeneratedOutput>.Failure("Operation was cancelled");
            }
            catch (TimeoutException ex)
            {
                logger.LogError(
                    ex,
                    "LLM request timed out for {InputName}",
                    request.InputName);

                return Result<GeneratedOutput>.Failure(
                    "LLM request timed out. The service may be experiencing delays. Please try again.");
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(
                    ex,
                    "Network error during LLM request for {InputName}",
                    request.InputName);

                return Result<GeneratedOutput>.Failure(
                    "Network error: Unable to reach LLM service. Please check your internet connection and try again.");
            }
            catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    ex,
                    "Rate limit exceeded for {InputName}",
                    request.InputName);

                return Result<GeneratedOutput>.Failure(
                    "Rate limit exceeded. Please wait a moment and try again.");
            }
            catch (Exception ex) when (ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError(
                    ex,
                    "API quota exceeded for {InputName}",
                    request.InputName);

                return Result<GeneratedOutput>.Failure(
                    "API quota exceeded. Please check your account limits.");
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error during LLM request for {InputName}",
                    request.InputName);

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
                InputName = request.InputName,
                InputType = request.InputType,
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
            var saveResult = await outputStorageService.SaveOutputAsync(
                output,
                cancellationToken);

            if (!saveResult.IsSuccess)
            {
                return Result<GeneratedOutput>.Failure(saveResult.Error!);
            }

            output = output with { OutputFilePath = saveResult.Value };

            // 11. Log completion with performance and audit trail
            var duration = DateTimeOffset.UtcNow - startTime;

            logger.LogInformation(
                "Generation completed for {InputName} using {Template}: {OutputPath} " +
                "(Duration: {Duration:F2}s, Provider: {Provider}, Model: {Model}, " +
                "Tokens: {InputTokens} input + {OutputTokens} output = {TotalTokens} total, " +
                "Truncated: {WasTruncated})",
                request.InputName,
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

        /// <summary>
        /// Parses the date from the input name if it matches the recording format: M-D-Y_Increment.
        /// Otherwise returns current date.
        /// </summary>
        /// <param name="inputName">The input base name.</param>
        /// <returns>The parsed date as DateTimeOffset.</returns>
        private static DateTimeOffset ParseInputDate(string inputName)
        {
            // Format: M-D-Y_Increment (e.g., "10-22-2025_1")
            // Extract the date part before the underscore
            var parts = inputName.Split('_');
            if (parts.Length > 0)
            {
                var datePart = parts[0];
                var dateComponents = datePart.Split('-');

                if (dateComponents.Length == 3 &&
                    int.TryParse(dateComponents[0], out int month) &&
                    int.TryParse(dateComponents[1], out int day) &&
                    int.TryParse(dateComponents[2], out int year))
                {
                    return new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
                }
            }

            // Fallback to current date if parsing fails
            return DateTimeOffset.UtcNow;
        }
    }
}
