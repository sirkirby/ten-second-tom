using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using MediatR;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today;

/// <summary>
/// Creates a daily reflection entry with LLM-powered summary.
/// </summary>
public static class CreateDailyEntry
{
    /// <summary>
    /// Command to create a daily reflection entry.
    /// Captures user's daily content and generates structured summary via LLM.
    /// </summary>
    public sealed record Command : IRequest<Result<DailyEntry>>
    {
        /// <summary>
        /// Gets the user's daily reflection content.
        /// Can be free-form text, multiple lines, or structured as the user prefers.
        /// Must not be null, empty, or whitespace-only.
        /// </summary>
        public required string Content { get; init; }

        /// <summary>
        /// Gets the optional template name to use for processing the daily entry.
        /// If specified, the handler will attempt to load this template.
        /// If the template is not found, falls back to the default template with a warning.
        /// </summary>
        public string? TemplateName { get; init; }

        /// <summary>
        /// Gets a value indicating whether to use the default template.
        /// When true, bypasses template selection UI and uses the default daily summary template directly.
        /// Useful for non-interactive scenarios or when the user prefers the default template.
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
    /// Handles the creation of daily reflection entries.
    /// Orchestrates validation, authentication, LLM interaction, and storage.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
    public sealed class Handler(
        IMemoryStorageProvider storage,
        ILlmProviderFactory llmFactory,
        IPromptTemplateLoader promptLoader,
        IOptionsSnapshot<LlmOptions> llmOptions,
        IAuthenticationService authService,
        ILogger<Handler> logger,
        ITemplateProvider templateProvider,
        ITemplateSelectionUI templateSelectionUI) : IRequestHandler<Command, Result<DailyEntry>>
    {
        // Use IOptionsSnapshot to reload configuration per request (important for shell mode)
        // Don't cache the value - access llmOptions.Value when needed to get fresh config
        private readonly IOptionsSnapshot<LlmOptions> _llmOptions = llmOptions;

        /// <summary>
        /// Handles the CreateDailyEntryCommand to create a new daily entry.
        /// </summary>
        /// <param name="request">The command containing user responses.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result containing the created DailyEntry or an error.</returns>
        public async Task<Result<DailyEntry>> Handle(
            Command request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // 1. Validate command
            Result<DailyEntry> validationResult = ValidateCommand(request);
            if (!validationResult.IsSuccess)
            {
                return validationResult;
            }

                    // 2. Check authentication
            bool isAuthenticated = await authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
            if (!isAuthenticated)
            {
                return Result<DailyEntry>.Failure("Authentication required. Please authenticate first.");
            }

            // 3. Determine entry number for today (using note directory for shared numbering)
            DateTime today = DateTime.UtcNow.Date;
        Result<int> countResult = await storage.CountEntriesAsync(CommandNames.Note, today, cancellationToken).ConfigureAwait(false);
            if (!countResult.IsSuccess)
            {
                return Result<DailyEntry>.Failure($"Failed to determine entry number: {countResult.Error}");
            }

            int entryNumber = countResult.Value + 1;

            // 4. Format user input for LLM
            string userInput = FormatUserInput(request.Content);

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
                            return Result<DailyEntry>.Failure("Template selection cancelled");
                        }

                        selectedTemplateId = userSelectedId;
                        logger.LogInformation("User selected daily template: {TemplateId}", selectedTemplateId);
                    }
                    catch (OperationCanceledException)
                    {
                        return Result<DailyEntry>.Failure("Template selection cancelled by user");
                    }
                }
            }

            // 6. Load selected prompt template (with fallback for user-specified invalid templates)
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
                        return Result<DailyEntry>.Failure($"Failed to load default template: {templateResult.Error}");
                    }
                }
                else
                {
                    return Result<DailyEntry>.Failure($"Failed to load prompt template: {templateResult.Error}");
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
                return Result<DailyEntry>.Failure($"Invalid LLM provider '{provider}'. Use 'OpenAI' or 'Anthropic'. Error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return Result<DailyEntry>.Failure($"Failed to create LLM provider '{provider}': {ex.Message}");
            }

            // Track processing time
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Result<LlmResponse> llmResult = await llmProvider.GenerateCompletionAsync(prompt, cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            TimeSpan processingDuration = stopwatch.Elapsed;

            if (!llmResult.IsSuccess)
            {
                // Save partial entry (user input only) for retry
                await SavePartialEntryAsync(userInput, today, entryNumber, cancellationToken).ConfigureAwait(false);
                return Result<DailyEntry>.Failure($"LLM provider error: {llmResult.Error}. User input saved for retry.");
            }

            // 8. Strip markdown code block wrappers if present (defensive measure)
            string cleanedResponse = llmResult.Value.Content.StripMarkdownCodeBlock();

            // 9. Create DailyEntry
            // Note: The prompt template defines the output structure.
            // The LlmResponse field contains the complete, unaltered output.
            var entry = new DailyEntry
            {
                EntryId = $"{CommandNames.Today}-{today:MM-dd-yyyy}-{entryNumber}",
                Command = CommandNames.Today,
                Timestamp = DateTimeOffset.UtcNow,
                EntryNumber = entryNumber,
                UserInput = userInput,
                LlmResponse = cleanedResponse,
                Metadata = CreateMetadata(
                    llmProvider.ProviderName,
                    llmProvider.ModelName,
                    llmResult.Value.TotalTokens,
                    processingDuration)
            };

            // 11. Save to storage
            Result<MemoryEntry> saveResult = await storage.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
            if (!saveResult.IsSuccess)
            {
                return Result<DailyEntry>.Failure($"Failed to save entry: {saveResult.Error}");
            }

            LogEntryCreated(entry.EntryId, provider);
            return Result<DailyEntry>.Success(entry);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging with structured properties")]
        private void LogEntryCreated(string entryId, string provider)
        {
            logger.LogInformation("Created daily entry {EntryId} using {Provider}", entryId, provider);
        }

        private static Result<DailyEntry> ValidateCommand(Command request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Result<DailyEntry>.Failure("Daily content cannot be null, empty, or whitespace");
            }

            if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
            {
                string provider = request.LlmProviderOverride.Trim();
                // Accept canonical lowercase constants but present user-friendly capitalized names in error message
                if (!provider.Equals(LlmProviders.OpenAI, StringComparison.OrdinalIgnoreCase) &&
                    !provider.Equals(LlmProviders.Anthropic, StringComparison.OrdinalIgnoreCase))
                {
                    return Result<DailyEntry>.Failure("Invalid LLM provider. Use 'OpenAI' or 'Anthropic'.");
                }
            }

            return Result<DailyEntry>.Success(null!); // Validation passed, but no entry created yet
        }

        private static string FormatUserInput(string content)
        {
            // Content is now provided directly by the user - return as-is to preserve formatting
            return content;
        }

        private static string RenderPrompt(PromptTemplate template, string userInput)
        {
            // Simple template variable substitution
            return template.Content.Replace("{{USER_INPUT}}", userInput, StringComparison.Ordinal);
        }

        private async Task SavePartialEntryAsync(string userInput, DateTime date, int entryNumber, CancellationToken cancellationToken)
        {
            try
            {
                var partialEntry = new DailyEntry
                {
                    EntryId = $"{CommandNames.Today}-{date:MM-dd-yyyy}-{entryNumber}",
                    Command = CommandNames.Today,
                    Timestamp = DateTimeOffset.UtcNow,
                    EntryNumber = entryNumber,
                    UserInput = userInput,
                    LlmResponse = string.Empty, // No LLM response due to failure
                    Metadata = new MemoryEntryMetadata
                    {
                        LlmProvider = "None",
                        LlmModel = "N/A",
                        TokensUsed = 0,
                        ProcessingDuration = TimeSpan.Zero,
                        CustomTags = new Dictionary<string, string>
                        {
                            ["Status"] = "Partial",
                            ["Reason"] = "LLM provider failed"
                        }
                    }
                };

                await storage.SaveAsync(partialEntry, cancellationToken).ConfigureAwait(false);
                LogPartialEntrySaved(partialEntry.EntryId);
            }
            catch (Exception ex)
            {
                LogPartialEntrySaveFailed(ex, date);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging with structured properties")]
        private void LogPartialEntrySaved(string entryId)
        {
            logger.LogWarning("Saved partial entry {EntryId} due to LLM failure", entryId);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging with structured properties")]
        private void LogPartialEntrySaveFailed(Exception ex, DateTime date)
        {
            logger.LogError(ex, "Failed to save partial entry for date {Date}", date);
        }


        private static MemoryEntryMetadata CreateMetadata(string provider, string model, int tokensUsed, TimeSpan processingDuration)
        {
            return new MemoryEntryMetadata
            {
                LlmProvider = provider,
                LlmModel = model,
                TokensUsed = tokensUsed,
                ProcessingDuration = processingDuration,
                CustomTags = new Dictionary<string, string>
                {
                    ["ProcessedAt"] = DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                }
            };
        }
    }
}
