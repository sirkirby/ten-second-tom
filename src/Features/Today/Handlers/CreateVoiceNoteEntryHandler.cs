using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Today.Models;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Handlers;

/// <summary>
/// Handles the <see cref="CreateVoiceNoteEntryCommand"/> to create a voice note entry.
/// Creates a structured daily entry from voice transcription with AI-powered summary.
/// </summary>
public sealed class CreateVoiceNoteEntryHandler : IRequestHandler<CreateVoiceNoteEntryCommand, Result<VoiceNoteEntry>>
{
    private readonly IMemoryStorageProvider _storage;
    private readonly ILlmProviderFactory _llmFactory;
    private readonly IPromptTemplateLoader _promptLoader;
    private readonly IAuthenticationService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateVoiceNoteEntryHandler> _logger;
    private readonly TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler _listTemplatesHandler;
    private readonly ITemplateSelectionUI _templateSelectionUI;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVoiceNoteEntryHandler"/> class.
    /// </summary>
    public CreateVoiceNoteEntryHandler(
        IMemoryStorageProvider storage,
        ILlmProviderFactory llmFactory,
        IPromptTemplateLoader promptLoader,
        IAuthenticationService authService,
        IConfiguration configuration,
        ILogger<CreateVoiceNoteEntryHandler> logger,
        TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler listTemplatesHandler,
        ITemplateSelectionUI templateSelectionUI)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _llmFactory = llmFactory ?? throw new ArgumentNullException(nameof(llmFactory));
        _promptLoader = promptLoader ?? throw new ArgumentNullException(nameof(promptLoader));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _listTemplatesHandler = listTemplatesHandler ?? throw new ArgumentNullException(nameof(listTemplatesHandler));
        _templateSelectionUI = templateSelectionUI ?? throw new ArgumentNullException(nameof(templateSelectionUI));
    }

    /// <summary>
    /// Handles the CreateVoiceNoteEntryCommand to create a voice note entry with AI summary.
    /// </summary>
    /// <param name="request">The command containing voice note data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the created VoiceNoteEntry or an error.</returns>
    public async Task<Result<VoiceNoteEntry>> Handle(
        CreateVoiceNoteEntryCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate transcript text
        if (string.IsNullOrWhiteSpace(request.TranscriptText))
        {
            return Result<VoiceNoteEntry>.Failure("Voice note transcript cannot be empty or whitespace");
        }

        // 2. Check authentication
        bool isAuthenticated = await _authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!isAuthenticated)
        {
            return Result<VoiceNoteEntry>.Failure("Authentication required. Please authenticate first.");
        }

        // 3. Determine entry number for today
        DateTime today = DateTime.UtcNow.Date;
        Result<int> countResult = await _storage.CountEntriesAsync(CommandNames.Today, today, cancellationToken).ConfigureAwait(false);
        if (!countResult.IsSuccess)
        {
            return Result<VoiceNoteEntry>.Failure($"Failed to determine entry number: {countResult.Error}");
        }

        int entryNumber = countResult.Value + 1;

        _logger.LogInformation(
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
            _logger.LogDebug("Using user-specified template: {TemplateId}", selectedTemplateId);
        }
        else if (request.UseDefaultTemplate)
        {
            // User requested default template via --use-default-template flag
            selectedTemplateId = TemplateConstants.DailySummaryTemplateId;
            _logger.LogDebug("Using default template as requested: {TemplateId}", selectedTemplateId);
        }
        else
        {
            // Existing flow: list templates and auto-select or prompt user
            Result<ListTemplatesQueryResult> templatesResult = await _listTemplatesHandler.Handle(
                new ListTemplatesQuery(FilterByType: TemplateType.Daily),
                cancellationToken).ConfigureAwait(false);

            if (!templatesResult.IsSuccess || templatesResult.Value.Templates.Count == 0)
            {
                // Fall back to embedded default template
                _logger.LogWarning("No daily templates found, falling back to embedded default template");
                selectedTemplateId = TemplateConstants.DailySummaryTemplateId;
            }
            else if (templatesResult.Value.Templates.Count == 1)
            {
                // Auto-select single template
                selectedTemplateId = templatesResult.Value.Templates[0].TemplateId;
                _logger.LogDebug("Auto-selected single daily template: {TemplateId}", selectedTemplateId);
            }
            else
            {
                // Multiple templates - prompt user to select
                try
                {
                    string? userSelectedId = await _templateSelectionUI.SelectTemplateAsync(
                        templatesResult.Value.Templates,
                        "today",
                        cancellationToken).ConfigureAwait(false);

                    if (string.IsNullOrWhiteSpace(userSelectedId))
                    {
                        return Result<VoiceNoteEntry>.Failure("Template selection cancelled");
                    }

                    selectedTemplateId = userSelectedId;
                    _logger.LogInformation("User selected daily template: {TemplateId}", selectedTemplateId);
                }
                catch (OperationCanceledException)
                {
                    return Result<VoiceNoteEntry>.Failure("Template selection cancelled by user");
                }
            }
        }

        // 6. Load selected prompt template
        Result<PromptTemplate> templateResult = await _promptLoader.LoadTemplateAsync(selectedTemplateId, cancellationToken).ConfigureAwait(false);
        if (!templateResult.IsSuccess)
        {
            // If user specified a template name that doesn't exist, fall back to default
            if (!string.IsNullOrWhiteSpace(request.TemplateName))
            {
                _logger.LogWarning("Template '{TemplateId}' not found, falling back to default template", selectedTemplateId);
                selectedTemplateId = TemplateConstants.DailySummaryTemplateId;
                templateResult = await _promptLoader.LoadTemplateAsync(selectedTemplateId, cancellationToken).ConfigureAwait(false);

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

        // 7. Determine LLM provider
        string provider;
        if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
        {
            provider = request.LlmProviderOverride;
        }
        else
        {
            string? configuredProvider = _configuration[ConfigurationKeys.LlmProviderKey];
            if (!string.IsNullOrWhiteSpace(configuredProvider))
            {
                provider = configuredProvider;
                _logger.LogDebug("Using LLM provider from configuration: {Provider}", provider);
            }
            else
            {
                provider = LlmProviders.OpenAI;
                _logger.LogDebug("No LLM provider configured, defaulting to OpenAI");
            }
        }

        ILlmProvider llmProvider;
        try
        {
            llmProvider = _llmFactory.CreateProvider(provider);
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
        string cleanedResponse = llmResult.Value.Content.StripMarkdownCodeBlock();

        // 10. Parse LLM response into DailySummary
        DailySummary summary = ParseDailySummary(cleanedResponse);

        // 11. Create VoiceNoteEntry with voice-specific metadata
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
            },

            // DailyEntry properties
            Summary = summary
        };

        // 12. Save to storage
        Result<MemoryEntry> saveResult = await _storage.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result<VoiceNoteEntry>.Failure($"Failed to save entry: {saveResult.Error}");
        }

        _logger.LogInformation(
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

    /// <summary>
    /// Parses LLM response into DailySummary.
    /// Extracts structured information from markdown-formatted response.
    /// </summary>
    private DailySummary ParseDailySummary(string llmResponse)
    {
        var summary = new DailySummary
        {
            KeyEvents = new List<string>(),
            Themes = new List<string>(),
            TodoItems = new List<TodoItem>(),
            ImportantPeople = new List<string>(),
            NotableTasks = new List<string>()
        };

        try
        {
            var lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentSection = null;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Detect section headers
                if (trimmedLine.Contains("Key Events", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "events";
                    continue;
                }
                if (trimmedLine.Contains("Themes", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "themes";
                    continue;
                }
                if (trimmedLine.Contains("Todo", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Contains("Action Items", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "todos";
                    continue;
                }
                if (trimmedLine.Contains("People", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "people";
                    continue;
                }
                if (trimmedLine.Contains("Tasks", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "tasks";
                    continue;
                }

                // Parse list items
                if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* ") || trimmedLine.StartsWith("• "))
                {
                    string itemText = trimmedLine[2..].Trim();
                    if (string.IsNullOrWhiteSpace(itemText)) continue;

                    switch (currentSection)
                    {
                        case "events":
                            ((List<string>)summary.KeyEvents).Add(itemText);
                            break;
                        case "themes":
                            ((List<string>)summary.Themes).Add(itemText);
                            break;
                        case "todos":
                            bool isCompleted = itemText.StartsWith("[x]", StringComparison.OrdinalIgnoreCase) ||
                                             itemText.StartsWith("[X]", StringComparison.OrdinalIgnoreCase);
                            string todoText = itemText.StartsWith('[') ? itemText[3..].Trim() : itemText;
                            ((List<TodoItem>)summary.TodoItems).Add(new TodoItem { Description = todoText, IsCompleted = isCompleted });
                            break;
                        case "people":
                            ((List<string>)summary.ImportantPeople).Add(itemText);
                            break;
                        case "tasks":
                            ((List<string>)summary.NotableTasks).Add(itemText);
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response into structured summary, returning empty summary");
        }

        return summary;
    }
}
