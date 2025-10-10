using Microsoft.Extensions.Logging;
using TenSecondTom.Features.Today.Commands;
using TenSecondTom.Infrastructure.Auth;
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Structured logging pattern")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging calls, delegate overhead not justified")]
public sealed class CreateDailyEntryHandler : IRequestHandler<CreateDailyEntryCommand, Result<DailyEntry>>
{
    private readonly IMemoryStorageProvider _storage;
    private readonly ILlmProviderFactory _llmFactory;
    private readonly IPromptTemplateLoader _promptLoader;
    private readonly IAuthenticationService _authService;
    private readonly IConfigurationStorageService _configService;
    private readonly ILogger<CreateDailyEntryHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateDailyEntryHandler"/> class.
    /// </summary>
    /// <param name="storage">The memory storage provider.</param>
    /// <param name="llmFactory">The LLM provider factory.</param>
    /// <param name="promptLoader">The prompt template loader.</param>
    /// <param name="authService">The authentication service.</param>
    /// <param name="configService">The configuration storage service.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateDailyEntryHandler(
        IMemoryStorageProvider storage,
        ILlmProviderFactory llmFactory,
        IPromptTemplateLoader promptLoader,
        IAuthenticationService authService,
        IConfigurationStorageService configService,
        ILogger<CreateDailyEntryHandler> logger)
    {
        _storage = storage;
        _llmFactory = llmFactory;
        _promptLoader = promptLoader;
        _authService = authService;
        _configService = configService;
        _logger = logger;
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
        string userInput = FormatUserInput(request.Responses);

        // 5. Load prompt template
        Result<PromptTemplate> templateResult = await _promptLoader.LoadTemplateAsync("daily-summary", cancellationToken).ConfigureAwait(false);
        if (!templateResult.IsSuccess)
        {
            return Result<DailyEntry>.Failure($"Failed to load prompt template: {templateResult.Error}");
        }

        string prompt = RenderPrompt(templateResult.Value, userInput);

        // 6. Determine LLM provider (use override, or load from config, or default to OpenAI)
        string provider;
        if (!string.IsNullOrWhiteSpace(request.LlmProviderOverride))
        {
            provider = request.LlmProviderOverride;
        }
        else
        {
            // Load from configuration
            Result<Features.Setup.Models.ConfigurationSettings> configResult = await _configService.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (configResult.IsSuccess && configResult.Value.Llm.Provider != Features.Setup.Models.LlmProvider.OpenAI)
            {
                // Convert enum to string
                provider = configResult.Value.Llm.Provider.ToString();
            }
            else
            {
                // Default to OpenAI
                provider = LlmProviders.OpenAI;
                if (!configResult.IsSuccess)
                {
                    _logger.LogDebug("Could not load configuration, defaulting to OpenAI: {Error}", configResult.Error);
                }
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

        Result<string> llmResult = await llmProvider.GenerateCompletionAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (!llmResult.IsSuccess)
        {
            // Save partial entry (user input only) for retry
            await SavePartialEntryAsync(userInput, today, entryNumber, cancellationToken).ConfigureAwait(false);
            return Result<DailyEntry>.Failure($"LLM provider error: {llmResult.Error}. User input saved for retry.");
        }

        // 7. Parse LLM response into DailySummary
        DailySummary summary = ParseDailySummary(llmResult.Value);

        // 8. Create DailyEntry
        var entry = new DailyEntry
        {
            EntryId = $"{CommandNames.Today}-{today:MM-dd-yyyy}-{entryNumber}",
            Command = CommandNames.Today,
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = entryNumber,
            UserInput = userInput,
            LlmResponse = llmResult.Value,
            Metadata = CreateMetadata(provider, "gpt-4"), // TODO: Get model from config
            Summary = summary
        };

        // 9. Save to storage
        Result<MemoryEntry> saveResult = await _storage.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
        if (!saveResult.IsSuccess)
        {
            return Result<DailyEntry>.Failure($"Failed to save entry: {saveResult.Error}");
        }

        LogEntryCreated(entry.EntryId, provider);
        return Result<DailyEntry>.Success(entry);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging call")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging with structured properties")]
    private void LogEntryCreated(string entryId, string provider)
    {
        _logger.LogInformation("Created daily entry {EntryId} using {Provider}", entryId, provider);
    }

    private static Result<DailyEntry> ValidateCommand(CreateDailyEntryCommand request)
    {
        if (request.Responses == null || request.Responses.Count == 0)
        {
            return Result<DailyEntry>.Failure("Daily responses cannot be empty");
        }

        if (request.Responses.Count < 3 || request.Responses.Count > 5)
        {
            return Result<DailyEntry>.Failure("Daily reflection requires 3-5 responses");
        }

        foreach (KeyValuePair<string, string> kvp in request.Responses)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                return Result<DailyEntry>.Failure("Question text cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(kvp.Value))
            {
                return Result<DailyEntry>.Failure("Answer cannot be empty or whitespace");
            }
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

    private static string FormatUserInput(Dictionary<string, string> responses)
    {
        var lines = new List<string>();
        foreach (KeyValuePair<string, string> kvp in responses)
        {
            lines.Add($"Q: {kvp.Key}");
            lines.Add($"A: {kvp.Value}");
            lines.Add(string.Empty); // Blank line between Q&A pairs
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string RenderPrompt(PromptTemplate template, string userInput)
    {
        // Simple template variable substitution
        return template.Content.Replace("{{USER_INPUT}}", userInput, StringComparison.Ordinal);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Best effort logging on partial save")]
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging call")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Logging with structured properties")]
    private void LogPartialEntrySaved(string entryId)
    {
        _logger.LogWarning("Saved partial entry {EntryId} due to LLM failure", entryId);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA1848:Use the LoggerMessage delegates", Justification = "Simple logging call")]
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

    private static MemoryEntryMetadata CreateMetadata(string provider, string model)
    {
        return new MemoryEntryMetadata
        {
            LlmProvider = provider,
            LlmModel = model,
            TokensUsed = 0, // TODO: Track actual token usage
            ProcessingDuration = TimeSpan.Zero, // TODO: Track actual duration
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
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
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
