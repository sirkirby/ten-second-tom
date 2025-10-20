using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Today.Handlers;

/// <summary>
/// Handles the creation of daily reflection entries.
/// Orchestrates validation, authentication, LLM interaction, and storage.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
public sealed class CreateDailyEntryHandler : IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>
{
    private readonly IMemoryStorageProvider _storage;
    private readonly ILlmProviderFactory _llmFactory;
    private readonly IPromptTemplateLoader _promptLoader;
    private readonly IAuthenticationService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateDailyEntryHandler> _logger;
    private readonly TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler _listTemplatesHandler;
    private readonly ITemplateSelectionUI _templateSelectionUI;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDailyEntryHandler"/> class.
    /// </summary>
    /// <param name="storage">The memory storage provider.</param>
    /// <param name="llmFactory">The LLM provider factory.</param>
    /// <param name="promptLoader">The prompt template loader.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="configuration">The application configuration (includes user secrets + environment variable overrides).</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="listTemplatesHandler">Handler for listing available templates.</param>
    /// <param name="templateSelectionUI">UI for interactive template selection.</param>
    public CreateDailyEntryHandler(
        IMemoryStorageProvider storage,
        ILlmProviderFactory llmFactory,
        IPromptTemplateLoader promptLoader,
        IAuthenticationService authService,
        IConfiguration configuration,
        ILogger<CreateDailyEntryHandler> logger,
        TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler listTemplatesHandler,
        ITemplateSelectionUI templateSelectionUI)
    {
        _storage = storage;
        _llmFactory = llmFactory;
        _promptLoader = promptLoader;
        _authService = authService;
        _configuration = configuration;
        _logger = logger;
        _listTemplatesHandler = listTemplatesHandler;
        _templateSelectionUI = templateSelectionUI;
    }

    /// <summary>
    /// Handles the CreateDailyEntryCommand to create a new daily entry.
    /// </summary>
    /// <param name="request">The command containing user responses.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the created DailyEntry or an error.</returns>
    public async Task<Result<DailyEntry>> Handle(
        CreateDailyEntryCommand request,
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
        bool isAuthenticated = await _authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!isAuthenticated)
        {
            return Result<DailyEntry>.Failure("Authentication required. Please authenticate first.");
        }

        // 3. Determine entry number for today
        DateTime today = DateTime.UtcNow.Date;
    Result<int> countResult = await _storage.CountEntriesAsync(CommandNames.Today, today, cancellationToken).ConfigureAwait(false);
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
                        return Result<DailyEntry>.Failure("Template selection cancelled");
                    }

                    selectedTemplateId = userSelectedId;
                    _logger.LogInformation("User selected daily template: {TemplateId}", selectedTemplateId);
                }
                catch (OperationCanceledException)
                {
                    return Result<DailyEntry>.Failure("Template selection cancelled by user");
                }
            }
        }

        // 6. Load selected prompt template (with fallback for user-specified invalid templates)
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
                    return Result<DailyEntry>.Failure($"Failed to load default template: {templateResult.Error}");
                }
            }
            else
            {
                return Result<DailyEntry>.Failure($"Failed to load prompt template: {templateResult.Error}");
            }
        }

        string prompt = RenderPrompt(templateResult.Value, userInput);

        // 7. Determine LLM provider (use override, or load from config, or default to OpenAI)
        string provider;
        if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
        {
            provider = request.LlmProviderOverride;
        }
        else
        {
            // Read from IConfiguration which includes environment variable overrides
            string? configuredProvider = _configuration["Llm:Provider"];
            if (!string.IsNullOrWhiteSpace(configuredProvider))
            {
                provider = configuredProvider;
                _logger.LogDebug("Using LLM provider from configuration: {Provider}", provider);
            }
            else
            {
                // Default to OpenAI if not configured
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

        // 8. Parse LLM response into DailySummary
        DailySummary summary = ParseDailySummary(llmResult.Value.Content);

        // 9. Create DailyEntry
        var entry = new DailyEntry
        {
            EntryId = $"{CommandNames.Today}-{today:MM-dd-yyyy}-{entryNumber}",
            Command = CommandNames.Today,
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = entryNumber,
            UserInput = userInput,
            LlmResponse = llmResult.Value.Content,
            Metadata = CreateMetadata(
                llmProvider.ProviderName, 
                llmProvider.ModelName, 
                llmResult.Value.TotalTokens,
                processingDuration),
            Summary = summary
        };

        // 10. Save to storage
        Result<MemoryEntry> saveResult = await _storage.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
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
        _logger.LogInformation("Created daily entry {EntryId} using {Provider}", entryId, provider);
    }

    private static Result<DailyEntry> ValidateCommand(CreateDailyEntryCommand request)
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
                },
                Summary = new DailySummary() // Empty summary
            };

            await _storage.SaveAsync(partialEntry, cancellationToken).ConfigureAwait(false);
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
        _logger.LogWarning("Saved partial entry {EntryId} due to LLM failure", entryId);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging with structured properties")]
    private void LogPartialEntrySaveFailed(Exception ex, DateTime date)
    {
        _logger.LogError(ex, "Failed to save partial entry for date {Date}", date);
    }

    private static DailySummary ParseDailySummary(string llmResponse)
    {
        // Simple parsing logic - extract sections from markdown-style response
        var keyEvents = new List<string>();
        var themes = new List<string>();
        var todoItems = new List<TodoItem>();
        var importantPeople = new List<string>();
        var notableTasks = new List<string>();

        string[] lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? currentSection = null;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            // Detect section headers
            if (trimmed.StartsWith("##", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("**", StringComparison.OrdinalIgnoreCase))
            {
                string header = trimmed.Replace("#", string.Empty, StringComparison.Ordinal)
                    .Replace("*", string.Empty, StringComparison.Ordinal)
                    .Trim()
                    .ToUpperInvariant();

                if (header.Contains("KEY EVENT", StringComparison.OrdinalIgnoreCase) || header.Contains("EVENTS", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "events";
                }
                else if (header.Contains("THEME", StringComparison.OrdinalIgnoreCase) || header.Contains("PATTERN", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "themes";
                }
                else if (header.Contains("TODO", StringComparison.OrdinalIgnoreCase) || header.Contains("TO-DO", StringComparison.OrdinalIgnoreCase) || header.Contains("TASK", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "todos";
                }
                else if (header.Contains("PEOPLE", StringComparison.OrdinalIgnoreCase) || header.Contains("PERSON", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "people";
                }
                else if (header.Contains("NOTABLE", StringComparison.OrdinalIgnoreCase) || header.Contains("FOLLOW", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "notable";
                }

                continue;
            }

            // Extract bullet points or numbered items
            if (trimmed.StartsWith('-') || 
                trimmed.StartsWith('*') || 
                char.IsDigit(trimmed.FirstOrDefault()))
            {
                string content = trimmed.TrimStart('-', '*', ' ', '\t');
                if (content.Length > 0 && char.IsDigit(content[0]))
                {
                    int dotIndex = content.IndexOf('.', StringComparison.Ordinal);
                    if (dotIndex > 0)
                    {
                        content = content[(dotIndex + 1)..].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                switch (currentSection)
                {
                    case "events":
                        keyEvents.Add(content);
                        break;
                    case "themes":
                        themes.Add(content);
                        break;
                    case "todos":
                        todoItems.Add(new TodoItem { Description = content, IsCompleted = false });
                        break;
                    case "people":
                        importantPeople.Add(content);
                        break;
                    case "notable":
                        notableTasks.Add(content);
                        break;
                }
            }
        }

        return new DailySummary
        {
            KeyEvents = keyEvents,
            Themes = themes,
            TodoItems = todoItems,
            ImportantPeople = importantPeople,
            NotableTasks = notableTasks
        };
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

/// <summary>
/// Marker interface for request handlers.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Handles the request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
