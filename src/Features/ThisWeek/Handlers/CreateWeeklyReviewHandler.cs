using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TenSecondTom.Features.ThisWeek.Commands;
using TenSecondTom.Features.Templates.Queries;
using TenSecondTom.Infrastructure.Auth;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Llm;
using TenSecondTom.Infrastructure.Prompts;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Extensions;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.ThisWeek.Handlers;

/// <summary>
/// Handles the creation of weekly review entries by aggregating daily entries.
/// Orchestrates validation, authentication, data retrieval, LLM interaction, and storage.
/// </summary>
public sealed class CreateWeeklyReviewHandler : IRequestHandler<CreateWeeklyReviewCommand, Result<WeeklyEntry>>
{
    private readonly IMemoryStorageProvider _storage;
    private readonly ILlmProviderFactory _llmFactory;
    private readonly IPromptTemplateLoader _promptLoader;
    private readonly IAuthenticationService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CreateWeeklyReviewHandler> _logger;
    private readonly TenSecondTom.Features.Templates.Handlers.ListTemplatesQueryHandler _listTemplatesHandler;
    private readonly ITemplateSelectionUI _templateSelectionUI;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateWeeklyReviewHandler"/> class.
    /// </summary>
    /// <param name="storage">The memory storage provider.</param>
    /// <param name="llmFactory">The LLM provider factory.</param>
    /// <param name="promptLoader">The prompt template loader.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="configuration">The application configuration (includes user secrets + environment variable overrides).</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="listTemplatesHandler">Handler for listing available templates.</param>
    /// <param name="templateSelectionUI">UI for interactive template selection.</param>
    public CreateWeeklyReviewHandler(
        IMemoryStorageProvider storage,
        ILlmProviderFactory llmFactory,
        IPromptTemplateLoader promptLoader,
        IAuthenticationService authService,
        IConfiguration configuration,
        ILogger<CreateWeeklyReviewHandler> logger,
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
    /// Handles the CreateWeeklyReviewCommand to create a new weekly review.
    /// </summary>
    /// <param name="request">The command containing optional date range and provider override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A Result containing the created WeeklyEntry or an error.</returns>
    public async Task<Result<WeeklyEntry>> Handle(
        CreateWeeklyReviewCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate command
        Result<WeeklyEntry> validationResult = ValidateCommand(request);
        if (!validationResult.IsSuccess)
        {
            return validationResult;
        }

        // 2. Check authentication
        bool isAuthenticated = await _authService.IsAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        if (!isAuthenticated)
        {
            return Result<WeeklyEntry>.Failure("Authentication required. Please authenticate first.");
        }

        // 3. Determine date range (custom or last 7 days)
        DateRange dateRange = request.CustomDateRange ?? GetLastSevenDays();

        // 4. Retrieve daily entries from storage
        Result<IReadOnlyList<MemoryEntry>> entriesResult = await _storage.GetEntriesAsync(
            CommandNames.Today,
            dateRange.StartDate.DateTime,
            dateRange.EndDate.DateTime,
            cancellationToken).ConfigureAwait(false);

        if (!entriesResult.IsSuccess)
        {
            return Result<WeeklyEntry>.Failure($"Failed to retrieve daily entries: {entriesResult.Error}");
        }

        // 5. Return error if no entries found
        if (entriesResult.Value.Count == 0)
        {
            return Result<WeeklyEntry>.Failure(
                $"No daily entries found for the period {dateRange.StartDate:yyyy-MM-dd} to {dateRange.EndDate:yyyy-MM-dd}. " +
                "Please create some daily entries first using the 'tom today' command.");
        }

        // 6. Aggregate daily summaries
        string aggregatedContent = AggregateDailyEntries(entriesResult.Value);

        // 7. Select prompt template
        string selectedTemplateId;
        Result<ListTemplatesQueryResult> templatesResult = await _listTemplatesHandler.Handle(
            new ListTemplatesQuery(FilterByType: TemplateType.Weekly),
            cancellationToken).ConfigureAwait(false);

        if (!templatesResult.IsSuccess || templatesResult.Value.Templates.Count == 0)
        {
            // Fall back to embedded default template
            _logger.LogWarning("No weekly templates found, falling back to embedded default template");
            // Note: User notification is handled by CompositeTemplateLoader logging
            // Console output would break separation of concerns - CLI handlers should display warnings
            selectedTemplateId = "weekly-review";
        }
        else if (templatesResult.Value.Templates.Count == 1)
        {
            // Auto-select single template
            selectedTemplateId = templatesResult.Value.Templates[0].TemplateId;
            _logger.LogDebug("Auto-selected single weekly template: {TemplateId}", selectedTemplateId);
        }
        else
        {
            // Multiple templates - prompt user to select
            try
            {
                string? userSelectedId = await _templateSelectionUI.SelectTemplateAsync(
                    templatesResult.Value.Templates,
                    "thisweek",
                    cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(userSelectedId))
                {
                    return Result<WeeklyEntry>.Failure("Template selection cancelled");
                }

                selectedTemplateId = userSelectedId;
                _logger.LogInformation("User selected weekly template: {TemplateId}", selectedTemplateId);
            }
            catch (OperationCanceledException)
            {
                return Result<WeeklyEntry>.Failure("Template selection cancelled by user");
            }
        }

        // 8. Load selected weekly review template
        Result<PromptTemplate> templateResult = await _promptLoader.LoadTemplateAsync(
            selectedTemplateId,
            cancellationToken).ConfigureAwait(false);

        if (!templateResult.IsSuccess)
        {
            return Result<WeeklyEntry>.Failure($"Failed to load prompt template: {templateResult.Error}");
        }

        string prompt = RenderPrompt(templateResult.Value, aggregatedContent, dateRange, entriesResult.Value.Count);

        // 9. Determine LLM provider (use override, or load from config, or default to OpenAI)
        string provider;
        if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
        {
            provider = request.LlmProviderOverride;
        }
        else
        {
            // Read from IConfiguration which includes environment variable overrides
            string? configuredProvider = _configuration[ConfigurationKeys.LlmProviderKey];
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
            return Result<WeeklyEntry>.Failure($"Invalid LLM provider '{provider}'. Use 'OpenAI' or 'Anthropic'. Error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return Result<WeeklyEntry>.Failure($"Failed to create LLM provider '{provider}': {ex.Message}");
        }

        _logger.LogDebug("Calling LLM provider {Provider} for weekly review", provider);

        // Track processing time
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        Result<LlmResponse> completionResult;
        try
        {
            completionResult = await llmProvider.GenerateCompletionAsync(
                prompt,
                cancellationToken,
                maxTokens: 3000,
                temperature: 0.7).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM provider threw unexpected exception");
            return Result<WeeklyEntry>.Failure($"LLM service error: {ex.Message}");
        }
        
        stopwatch.Stop();
        TimeSpan processingDuration = stopwatch.Elapsed;

        if (!completionResult.IsSuccess)
        {
            _logger.LogError("LLM completion failed: {Error}", completionResult.Error);
            return Result<WeeklyEntry>.Failure($"Failed to generate weekly review: {completionResult.Error}");
        }

        // 10. Parse response and validate 3+3 structure
        Result<WeeklySummary> summaryResult = ParseWeeklySummary(completionResult.Value.Content);
        if (!summaryResult.IsSuccess)
        {
            return Result<WeeklyEntry>.Failure($"Failed to parse LLM response: {summaryResult.Error}");
        }

        // Validate exactly 3 accomplishments and 3 challenges
        if (summaryResult.Value.TopAccomplishments.Count != 3)
        {
            return Result<WeeklyEntry>.Failure(
                $"Weekly review must contain exactly 3 top accomplishments, but found {summaryResult.Value.TopAccomplishments.Count}");
        }

        if (summaryResult.Value.TopChallenges.Count != 3)
        {
            return Result<WeeklyEntry>.Failure(
                $"Weekly review must contain exactly 3 top challenges, but found {summaryResult.Value.TopChallenges.Count}");
        }

        // 11. Strip markdown code block wrappers if present (defensive measure)
        string cleanedResponse = completionResult.Value.Content.StripMarkdownCodeBlock();

        // 12. Create WeeklyEntry
        int entryNumber = await GetNextEntryNumber(dateRange, cancellationToken).ConfigureAwait(false);

        WeeklyEntry weeklyEntry = new()
        {
            EntryId = $"{CommandNames.ThisWeek}-{dateRange.StartDate:yyyy-MM-dd}-{entryNumber}",
            Command = CommandNames.ThisWeek,
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = entryNumber,
            UserInput = $"Weekly review for {dateRange.StartDate:yyyy-MM-dd} to {dateRange.EndDate:yyyy-MM-dd} ({entriesResult.Value.Count} daily entries)",
            LlmResponse = cleanedResponse,
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = llmProvider.ProviderName,
                LlmModel = llmProvider.ModelName,
                TokensUsed = completionResult.Value.TotalTokens,
                ProcessingDuration = processingDuration
            },
            Summary = summaryResult.Value
        };

        // 13. Save to storage
        Result<MemoryEntry> saveResult = await _storage.SaveAsync(weeklyEntry, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result<WeeklyEntry>.Failure($"Failed to save weekly entry: {saveResult.Error}");
        }

        _logger.LogInformation(
            "Created weekly review {EntryId} for period {StartDate} to {EndDate} with {EntryCount} daily entries",
            weeklyEntry.EntryId,
            dateRange.StartDate,
            dateRange.EndDate,
            entriesResult.Value.Count);

        // 13. Return Result<WeeklyEntry>
        return Result<WeeklyEntry>.Success(weeklyEntry);
    }

    private static Result<WeeklyEntry> ValidateCommand(CreateWeeklyReviewCommand request)
    {
        if (request.CustomDateRange != null)
        {
            DateRange range = request.CustomDateRange;

            // Validate Start < End
            if (range.StartDate >= range.EndDate)
            {
                return Result<WeeklyEntry>.Failure("CustomDateRange Start must be before End");
            }

            // Validate End not in future
            if (range.EndDate > DateTimeOffset.UtcNow)
            {
                return Result<WeeklyEntry>.Failure("CustomDateRange End cannot be in the future");
            }

            // Validate duration 3-10 days
            TimeSpan duration = range.EndDate - range.StartDate;
            if (duration.TotalDays < 3)
            {
                return Result<WeeklyEntry>.Failure("CustomDateRange must span at least 3 days");
            }

            if (duration.TotalDays > 10)
            {
                return Result<WeeklyEntry>.Failure("CustomDateRange must not exceed 10 days");
            }
        }

        // Validate LlmProviderOverride if set
        if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
        {
            string provider = request.LlmProviderOverride.Trim();
            if (!provider.Equals(LlmProviders.OpenAI, StringComparison.OrdinalIgnoreCase) &&
                !provider.Equals(LlmProviders.Anthropic, StringComparison.OrdinalIgnoreCase))
            {
                return Result<WeeklyEntry>.Failure($"Invalid LLM provider '{provider}'. Must be 'OpenAI' or 'Anthropic'.");
            }
        }

        return Result<WeeklyEntry>.Success(null!); // Validation passed
    }

    private static DateRange GetLastSevenDays()
    {
        DateTimeOffset end = DateTimeOffset.UtcNow;
        DateTimeOffset start = end.AddDays(-7);

        return new DateRange
        {
            StartDate = start,
            EndDate = end
        };
    }

    private static string AggregateDailyEntries(IReadOnlyList<MemoryEntry> entries)
    {
        System.Text.StringBuilder sb = new();

        foreach (MemoryEntry entry in entries)
        {
            sb.AppendLine($"## {entry.Timestamp:yyyy-MM-dd} - Entry #{entry.EntryNumber}");
            sb.AppendLine();
            sb.AppendLine("### User Input");
            sb.AppendLine(entry.UserInput);
            sb.AppendLine();
            sb.AppendLine("### Summary");
            sb.AppendLine(entry.LlmResponse);
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string RenderPrompt(
        PromptTemplate template,
        string aggregatedContent,
        DateRange dateRange,
        int entryCount)
    {
        return template.Content
            .Replace("{{DAILY_ENTRIES}}", aggregatedContent, StringComparison.Ordinal)
            .Replace("{{START_DATE}}", dateRange.StartDate.ToString("yyyy-MM-dd"), StringComparison.Ordinal)
            .Replace("{{END_DATE}}", dateRange.EndDate.ToString("yyyy-MM-dd"), StringComparison.Ordinal)
            .Replace("{{ENTRY_COUNT}}", entryCount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private async Task<int> GetNextEntryNumber(DateRange dateRange, CancellationToken cancellationToken)
    {
        // For weekly entries, we need to count entries for the same year-week-day combination
        // File naming: YYYY-WW-DayOfWeek-N.md (e.g., 2025-42-Fri-1.md)
        // Each day in the week starts at 1, so Friday entry 1, Saturday entry 1, etc.
        
        var calendar = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        
        int currentWeekNumber = calendar.GetWeekOfYear(
            now.DateTime,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
        int currentYear = now.Year;
        DayOfWeek currentDay = now.DayOfWeek;

        // Get all thisweek entries in the week range
        Result<IReadOnlyList<MemoryEntry>> entriesResult = await _storage.GetEntriesAsync(
            CommandNames.ThisWeek,
            dateRange.StartDate.DateTime,
            dateRange.EndDate.DateTime,
            cancellationToken).ConfigureAwait(false);

        if (!entriesResult.IsSuccess)
        {
            _logger.LogWarning("Failed to get existing weekly entries: {Error}", entriesResult.Error);
            return 1;
        }

        // Count entries that belong to the same year, week, AND day of week
        int count = entriesResult.Value.Count(entry =>
        {
            int entryWeekNumber = calendar.GetWeekOfYear(
                entry.Timestamp.DateTime,
                System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
            int entryYear = entry.Timestamp.Year;
            DayOfWeek entryDay = entry.Timestamp.DayOfWeek;
            
            return entryYear == currentYear 
                && entryWeekNumber == currentWeekNumber 
                && entryDay == currentDay;
        });

        return count + 1;
    }

    private static Result<WeeklySummary> ParseWeeklySummary(string llmResponse)
    {
        try
        {
            List<string> topAccomplishments = [];
            List<string> topChallenges = [];
            List<string> keyInsights = [];
            List<string> goalsForNextWeek = [];

            string[] lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string? currentSection = null;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("## Top 3 Accomplishments", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "accomplishments";
                }
                else if (trimmedLine.StartsWith("## Top 3 Challenges", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "challenges";
                }
                else if (trimmedLine.StartsWith("## Recurring Themes", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.StartsWith("## Key Insights", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "insights";
                }
                else if (trimmedLine.StartsWith("## Interaction Patterns", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "insights"; // Map to insights
                }
                else if (trimmedLine.StartsWith("## Next Week Suggestions", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.StartsWith("## Goals for Next Week", StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = "goals";
                }
                else if (!trimmedLine.StartsWith('#') && !string.IsNullOrWhiteSpace(trimmedLine))
                {
                    // Parse list items
                    string content = trimmedLine.TrimStart('-', '*', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ' ');

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        switch (currentSection)
                        {
                            case "accomplishments":
                                topAccomplishments.Add(content);
                                break;
                            case "challenges":
                                topChallenges.Add(content);
                                break;
                            case "insights":
                                keyInsights.Add(content);
                                break;
                            case "goals":
                                goalsForNextWeek.Add(content);
                                break;
                        }
                    }
                }
            }

            // Extract date range from context - for now use current week
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateRange dateRange = new()
            {
                StartDate = now.AddDays(-7),
                EndDate = now
            };

            WeeklySummary summary = new()
            {
                TopAccomplishments = topAccomplishments,
                TopChallenges = topChallenges,
                DateRange = dateRange,
                KeyInsights = keyInsights.Count > 0 ? keyInsights : null,
                GoalsForNextWeek = goalsForNextWeek.Count > 0 ? goalsForNextWeek : null
            };

            return Result<WeeklySummary>.Success(summary);
        }
        catch (Exception ex)
        {
            return Result<WeeklySummary>.Failure($"Failed to parse weekly summary: {ex.Message}");
        }
    }
}
