using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using MediatR;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today;

/// <summary>
/// Creates a daily entry from voice input with AI-powered summary.
/// </summary>
public static class CreateVoiceNoteEntry
{
    /// <summary>
    /// Command to create a daily entry from voice input.
    /// Processes voice transcription and generates LLM summary.
    /// </summary>
    public sealed record Command : IRequest<Result<VoiceNoteEntry>>
    {
        /// <summary>
        /// Gets the transcript text from speech-to-text.
        /// This will be used as the user input for LLM processing.
        /// </summary>
        public required string TranscriptText { get; init; }

        /// <summary>
        /// Gets the audio recording metadata.
        /// Contains information about the source audio file.
        /// </summary>
        public required AudioRecording Recording { get; init; }

        /// <summary>
        /// Gets the transcription result metadata.
        /// Contains information about the STT processing.
        /// </summary>
        public required TranscriptionResult Transcription { get; init; }

        /// <summary>
        /// Gets the optional template name to use for processing.
        /// If specified, the handler will attempt to load this template.
        /// If not found, falls back to the default template.
        /// </summary>
        public string? TemplateName { get; init; }

        /// <summary>
        /// Gets a value indicating whether to use the default template without prompting.
        /// When true, bypasses template selection UI.
        /// </summary>
        public bool UseDefaultTemplate { get; init; }

        /// <summary>
        /// Gets the optional LLM provider override.
        /// If not specified, uses the default provider from configuration.
        /// Valid values: "OpenAI", "Anthropic".
        /// </summary>
        public string? LlmProviderOverride { get; init; }
    }

    /// <summary>
    /// Handles the CreateVoiceNoteEntryCommand to create a voice note entry with AI summary.
    /// Creates a structured daily entry from voice transcription with AI-powered summary.
    /// </summary>
    public sealed class Handler(
        IMemoryStorageProvider storage,
        ILlmProviderFactory llmFactory,
        IPromptTemplateLoader promptLoader,
        IAuthenticationService authService,
        IOptionsSnapshot<LlmOptions> llmOptions,
        ILogger<Handler> logger,
        ITemplateProvider templateProvider,
        ITemplateSelectionUI templateSelectionUI) : IRequestHandler<Command, Result<VoiceNoteEntry>>
    {
        // Use IOptionsSnapshot to reload configuration per request (important for shell mode)
        // Don't cache the value - access llmOptions.Value when needed to get fresh config
        private readonly IOptionsSnapshot<LlmOptions> _llmOptions = llmOptions;

        /// <summary>
        /// Handles the CreateVoiceNoteEntryCommand to create a voice note entry with AI summary.
        /// </summary>
        /// <param name="request">The command containing voice note data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result containing the created VoiceNoteEntry or an error.</returns>
        public async Task<Result<VoiceNoteEntry>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // 1. Validate transcript text
            if (string.IsNullOrWhiteSpace(request.TranscriptText))
            {
                return Result<VoiceNoteEntry>.Failure("Voice note transcript cannot be empty or whitespace");
            }

            // 2. Check authentication
            bool isAuthenticated = await authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
            if (!isAuthenticated)
            {
                return Result<VoiceNoteEntry>.Failure("Authentication required. Please authenticate first.");
            }

            // 3. Determine entry number for today
            DateTime today = DateTime.UtcNow.Date;
            Result<int> countResult = await storage.CountEntriesAsync(CommandNames.Today, today, cancellationToken).ConfigureAwait(false);
            if (!countResult.IsSuccess)
            {
                return Result<VoiceNoteEntry>.Failure($"Failed to determine entry number: {countResult.Error}");
            }

            int entryNumber = countResult.Value + 1;

            logger.LogInformation(
                "Creating voice note entry: AudioFile={AudioFile}, TranscriptLength={Length}, EntryNumber={EntryNumber}",
                request.Recording.Filename,
                request.TranscriptText.Length,
                entryNumber);

            // 4. Use transcript as user input
            string userInput = request.TranscriptText.Trim();

            // 5. Select prompt template
            string selectedTemplateId;

            if (!string.IsNullOrWhiteSpace(request.TemplateName))
            {
                // User specified a template name via --template flag
                selectedTemplateId = request.TemplateName;
                logger.LogDebug("Using user-specified template: {TemplateId}", selectedTemplateId);
            }
            else if (request.UseDefaultTemplate)
            {
                // User requested default template via --use-default-template flag
                selectedTemplateId = TemplateConstants.DailySummaryTemplateId;
                logger.LogDebug("Using default template as requested: {TemplateId}", selectedTemplateId);
            }
            else
            {
                // Existing flow: list templates and auto-select or prompt user
                Result<IReadOnlyList<TemplateInfo>> templatesResult = await templateProvider.ListTemplatesAsync(
                    filterByType: TemplateType.Daily,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!templatesResult.IsSuccess || templatesResult.Value.Count == 0)
                {
                    // Fall back to embedded default template
                    logger.LogWarning("No daily templates found, falling back to embedded default template");
                    selectedTemplateId = TemplateConstants.DailySummaryTemplateId;
                }
                else if (templatesResult.Value.Count == 1)
                {
                    // Auto-select single template
                    selectedTemplateId = templatesResult.Value[0].TemplateId;
                    logger.LogDebug("Auto-selected single daily template: {TemplateId}", selectedTemplateId);
                }
                else
                {
                    // Multiple templates - prompt user to select
                    try
                    {
                        string? userSelectedId = await templateSelectionUI.SelectTemplateAsync(
                            templatesResult.Value,
                            "today",
                            cancellationToken).ConfigureAwait(false);

                        if (string.IsNullOrWhiteSpace(userSelectedId))
                        {
                            return Result<VoiceNoteEntry>.Failure("Template selection cancelled");
                        }

                        selectedTemplateId = userSelectedId;
                        logger.LogInformation("User selected daily template: {TemplateId}", selectedTemplateId);
                    }
                    catch (OperationCanceledException)
                    {
                        return Result<VoiceNoteEntry>.Failure("Template selection cancelled by user");
                    }
                }
            }

            // 6. Load selected prompt template
            Result<PromptTemplate> templateResult = await promptLoader.LoadTemplateAsync(selectedTemplateId, cancellationToken).ConfigureAwait(false);
            if (!templateResult.IsSuccess)
            {
                // If user specified a template name that doesn't exist, fall back to default
                if (!string.IsNullOrWhiteSpace(request.TemplateName))
                {
                    logger.LogWarning("Template '{TemplateId}' not found, falling back to default template", selectedTemplateId);
                    selectedTemplateId = TemplateConstants.DailySummaryTemplateId;
                    templateResult = await promptLoader.LoadTemplateAsync(selectedTemplateId, cancellationToken).ConfigureAwait(false);

                    if (!templateResult.IsSuccess)
                    {
                        return Result<VoiceNoteEntry>.Failure($"Failed to load default template: {templateResult.Error}");
                    }
                }
                else
                {
                    return Result<VoiceNoteEntry>.Failure($"Failed to load prompt template: {templateResult.Error}");
                }
            }

            string prompt = RenderPrompt(templateResult.Value, userInput);

            // 7. Determine LLM provider (use override, or load from options)
            string provider;
            if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
            {
                provider = request.LlmProviderOverride;
            }
            else
            {
                // Use strongly-typed configuration from LlmOptions
                // Access .Value to get fresh config (IOptionsSnapshot reloads per request)
                provider = _llmOptions.Value.Provider.ToString();
                logger.LogDebug("Using LLM provider from configuration: {Provider}", provider);
            }

            ILlmProvider llmProvider;
            try
            {
                llmProvider = llmFactory.CreateProvider(provider);
            }
            catch (ArgumentException ex)
            {
                return Result<VoiceNoteEntry>.Failure($"Invalid LLM provider '{provider}'. Use 'OpenAI' or 'Anthropic'. Error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<VoiceNoteEntry>.Failure($"Failed to create LLM provider '{provider}': {ex.Message}");
            }

            // 8. Call LLM to generate summary
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Result<LlmResponse> llmResult = await llmProvider.GenerateCompletionAsync(prompt, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            TimeSpan processingDuration = stopwatch.Elapsed;

            if (!llmResult.IsSuccess)
            {
                return Result<VoiceNoteEntry>.Failure($"LLM provider error: {llmResult.Error}. Voice transcription preserved.");
            }

            // 9. Strip markdown code block wrappers if present (defensive measure)
            string cleanedResponse = llmResult.Value.Content
                .StripMarkdownCodeBlock()
                .NormalizeReasoningTags();

            // 10. Create VoiceNoteEntry with voice-specific metadata
            // Note: The prompt template defines the output structure.
            // The LlmResponse field contains the complete, unaltered output.
            var entry = new VoiceNoteEntry
            {
                // Voice-specific properties
                AudioFilename = request.Recording.Filename,
                AudioDuration = request.Recording.Duration,
                TranscriptText = userInput,
                SttEngine = request.Transcription.SttEngine,
                SttModel = request.Transcription.SttModel,

                // MemoryEntry base properties
                EntryId = $"{CommandNames.Today}-{today:MM-dd-yyyy}-{entryNumber}",
                Command = CommandNames.Today,
                Timestamp = DateTimeOffset.UtcNow,
                EntryNumber = entryNumber,
                UserInput = userInput,
                LlmResponse = cleanedResponse,
                Metadata = new MemoryEntryMetadata
                {
                    LlmProvider = llmProvider.ProviderName,
                    LlmModel = llmProvider.ModelName,
                    TokensUsed = llmResult.Value.TotalTokens,
                    ProcessingDuration = processingDuration,
                    CustomTags = new Dictionary<string, string>
                    {
                        ["input-method"] = "voice",
                        ["audio-file"] = request.Recording.Filename,
                        ["audio-duration"] = request.Recording.Duration.TotalSeconds.ToString("F2"),
                        ["stt-engine"] = request.Transcription.SttEngine.ToString(),
                        ["stt-model"] = request.Transcription.SttModel ?? "unknown",
                        ["word-count"] = request.Transcription.WordCount.ToString()
                    }
                }
            };

            // 12. Save to storage
            Result<MemoryEntry> saveResult = await storage.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
            if (!saveResult.IsSuccess)
            {
                return Result<VoiceNoteEntry>.Failure($"Failed to save entry: {saveResult.Error}");
            }

            logger.LogInformation(
                "Voice note entry created: EntryId={EntryId}, Provider={Provider}, AudioDuration={Duration}s",
                entry.EntryId,
                provider,
                entry.AudioDuration.TotalSeconds);

            return Result<VoiceNoteEntry>.Success(entry);
        }

        /// <summary>
        /// Renders the prompt template with user input.
        /// </summary>
        private static string RenderPrompt(PromptTemplate template, string userInput)
        {
            return template.Content
                .Replace("{{USER_INPUT}}", userInput, StringComparison.Ordinal);
        }
    }
}
