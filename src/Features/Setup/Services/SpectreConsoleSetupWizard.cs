using MediatR;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Audio;
using TenSecondTom.Features.Setup.Constants;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Setup.Services;

/// <summary>
/// Spectre.Console-based implementation of the setup wizard UI
/// Provides rich, interactive terminal experience
/// </summary>
public sealed class SpectreConsoleSetupWizard : ISetupWizardUI
{
    private static readonly string[] LogLevelChoices =
    [
        SetupConstants.LogLevelDisplayNames.Debug,
        SetupConstants.LogLevelDisplayNames.Information,
        SetupConstants.LogLevelDisplayNames.Warning,
        SetupConstants.LogLevelDisplayNames.Error
    ];

    private readonly IAnsiConsole _console;
    private readonly ILogger<SpectreConsoleSetupWizard> _logger;
    private readonly IMediator _mediator;

    public SpectreConsoleSetupWizard(
        IAnsiConsole console,
        ILogger<SpectreConsoleSetupWizard> logger,
        IMediator mediator)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// Prompts for a selection with escape key support. Returns null if escape was pressed.
    /// </summary>
    private static T? PromptSelectionWithEscape<T>(SelectionPrompt<T> prompt) where T : class
    {
        var console = new EscapeCancellableConsole();
        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Prompts for text input with escape key support. Returns null if escape was pressed.
    /// </summary>
    private static string? PromptTextWithEscape(TextPrompt<string> prompt)
    {
        var console = new EscapeCancellableConsole();
        try
        {
            return console.Prompt(prompt);
        }
        catch (PromptCancelledException)
        {
            return null;
        }
    }

    public Task<SshKeyInfo?> PromptForSshKeyAsync(
        IReadOnlyList<SshKeyInfo> availableKeys,
        SshKeyInfo? currentKey,
        CancellationToken cancellationToken)
    {
        if (!availableKeys.Any())
        {
            ShowWarning("No SSH keys detected automatically.");
            _console.MarkupLine("[grey]ℹ️  Ten Second Tom needs an ED25519 SSH key to authenticate with GitHub.[/]");
            _console.MarkupLine("[grey]   You can:[/]");
            _console.MarkupLine("[grey]   • Generate a new key: ssh-keygen -t ed25519 -C \"your-email@example.com\"[/]");
            _console.MarkupLine("[grey]   • Add an existing key to your SSH agent[/]");
            _console.MarkupLine("[grey]   • Specify a manual path in the next step[/]");
            _console.MarkupLine("[grey]   • Exit setup and run 'tom setup' again later[/]");
            _console.WriteLine();
            _console.MarkupLine("[grey]   Learn more: https://docs.github.com/en/authentication/connecting-to-github-with-ssh[/]");
            return Task.FromResult<SshKeyInfo?>(null);
        }

        var choices = availableKeys.ToDictionary(
            k => k.DisplayName.EscapeMarkup(),
            k => k
        );

        var prompt = new SelectionPrompt<string>()
            .Title("Select SSH key to use:")
            .PageSize(10)
            .AddChoices(choices.Keys);

        if (currentKey != null)
        {
            prompt.HighlightStyle(new Style(Color.Green));
        }

        var selected = PromptSelectionWithEscape(prompt);
        if (selected is null) return Task.FromResult<SshKeyInfo?>(null);
        return Task.FromResult<SshKeyInfo?>(choices[selected]);
    }

    public Task<LlmProvider?> PromptForLlmProviderAsync(
        LlmProvider? currentProvider,
        CancellationToken cancellationToken)
    {
        var choices = new Dictionary<string, LlmProvider>
        {
            ["Local (Ollama, llama.cpp, LM Studio)"] = LlmProvider.LocalOpenAiCompatible,
            ["Local (Built-in) [[experimental]]"] = LlmProvider.BuiltInLocal,
            ["Cloud - OpenAI"] = LlmProvider.OpenAI,
            ["Cloud - Anthropic (Claude)"] = LlmProvider.Anthropic
        };

        var prompt = new SelectionPrompt<string>()
            .Title("Choose your AI provider:")
            .AddChoices(choices.Keys);

        if (currentProvider.HasValue)
        {
            var currentKey = choices.FirstOrDefault(x => x.Value == currentProvider.Value).Key;
            if (currentKey != null)
            {
                prompt.HighlightStyle(new Style(Color.Green));
            }
        }

        var selected = PromptSelectionWithEscape(prompt);
        if (selected is null) return Task.FromResult<LlmProvider?>(null);
        return Task.FromResult<LlmProvider?>(choices[selected]);
    }

    public Task<SupportedModel?> PromptForModelAsync(
        LlmProvider provider,
        string? currentModelId,
        CancellationToken cancellationToken)
    {
        // Get models for the selected provider
        var models = ModelRegistry.GetByProvider(provider);

        if (!models.Any())
        {
            ShowWarning($"No models found for {provider}");
            return Task.FromResult<SupportedModel?>(null);
        }

        // Create choices with formatted display: "DisplayName [CostTier] - Description"
        // Using square brackets now that we ensure escaping of markup-sensitive content.
        // Dictionary maps the formatted choice back to the model instance.
        var choiceToModel = new Dictionary<string, SupportedModel>();
        var choices = new List<string>();

        foreach (var model in models)
        {
            // Build the choice string and escape the entire thing to prevent Spectre.Console
            // from interpreting square brackets as markup (e.g., [Balanced] would be treated as a style)
            var choice = $"{model.DisplayName} [{model.CostTier}] - {model.Description}".EscapeMarkup();
            choices.Add(choice);
            choiceToModel[choice] = model;
        }

        var prompt = new SelectionPrompt<string>()
            .Title($"Select a model for {provider}:")
            .PageSize(10)
            .AddChoices(choices);

        // Highlight current model if one is configured
        if (!string.IsNullOrEmpty(currentModelId))
        {
            var currentModel = ModelRegistry.GetById(currentModelId);
            if (currentModel != null && currentModel.Provider == provider)
            {
                prompt.HighlightStyle(new Style(Color.Green));
            }
        }

        var selected = PromptSelectionWithEscape(prompt);
        if (selected is null) return Task.FromResult<SupportedModel?>(null);

        // Find the model using the dictionary mapping
        if (choiceToModel.TryGetValue(selected, out var selectedModel))
        {
            return Task.FromResult<SupportedModel?>(selectedModel);
        }

        // Fallback: shouldn't happen, but log and return null if mapping fails
        _logger.LogError("Failed to map model selection to model object: {Selection}", selected);
        return Task.FromResult<SupportedModel?>(null);
    }

    public Task<string?> PromptForApiKeyAsync(
        LlmProvider provider,
        string? currentApiKey,
        CancellationToken cancellationToken)
    {
        // Convert enum to display name
        var displayName = provider == LlmProvider.OpenAI
            ? "OpenAI"
            : "Anthropic";

        var prompt = new TextPrompt<string>($"Enter your {displayName} API key:")
            .Secret();

        if (!string.IsNullOrEmpty(currentApiKey))
        {
            prompt.DefaultValue(currentApiKey);
        }

        var apiKey = PromptTextWithEscape(prompt);
        return Task.FromResult(apiKey);
    }

    public Task<string?> PromptForMemoryDirectoryAsync(
        string? currentDirectory,
        CancellationToken cancellationToken)
    {
        var defaultDir = currentDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DirectoryNames.ApplicationRoot);

        var prompt = new TextPrompt<string>("Where should I store your memories?")
            .DefaultValue(defaultDir)
            .AllowEmpty();

        var directory = PromptTextWithEscape(prompt);
        return Task.FromResult(directory);
    }

    public Task<Microsoft.Extensions.Logging.LogLevel?> PromptForLogLevelAsync(
        Microsoft.Extensions.Logging.LogLevel? currentLevel,
        CancellationToken cancellationToken)
    {
        var prompt = new SelectionPrompt<string>()
            .Title("Select logging level:")
            .AddChoices(LogLevelChoices);

        var selected = PromptSelectionWithEscape(prompt);
        if (selected is null) return Task.FromResult<Microsoft.Extensions.Logging.LogLevel?>(null);

        var logLevel = selected switch
        {
            SetupConstants.LogLevelDisplayNames.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            SetupConstants.LogLevelDisplayNames.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            SetupConstants.LogLevelDisplayNames.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            SetupConstants.LogLevelDisplayNames.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        return Task.FromResult<Microsoft.Extensions.Logging.LogLevel?>(logLevel);
    }

    public Task<int?> PromptForRetentionDaysAsync(
        int? currentDays,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Choose how long to keep your memories before automatic deletion.[/]");
        _console.MarkupLine($"[grey]   Enter '{SetupConstants.RetentionKeywords.Unlimited}' to keep all memories forever (recommended).[/]");
        _console.WriteLine();

        var prompt = new TextPrompt<string>("How long should memories be kept? (enter 'unlimited' or number of days)")
            .DefaultValue(currentDays.HasValue && currentDays.Value > 0
                ? currentDays.Value.ToString()
                : SetupConstants.RetentionKeywords.Unlimited)
            .AllowEmpty();

        var input = PromptTextWithEscape(prompt);
        if (input is null) return Task.FromResult<int?>(null);

        // Parse input: "unlimited", "forever", "0" -> -1, otherwise parse as number
        if (string.IsNullOrWhiteSpace(input) ||
            input.Equals(SetupConstants.RetentionKeywords.Unlimited, StringComparison.OrdinalIgnoreCase) ||
            input.Equals(SetupConstants.RetentionKeywords.Forever, StringComparison.OrdinalIgnoreCase) ||
            input == SetupConstants.RetentionKeywords.Zero)
        {
            return Task.FromResult<int?>(-1); // -1 means unlimited
        }

        if (int.TryParse(input, out var days) && days > 0)
        {
            return Task.FromResult<int?>(days);
        }

        ShowWarning($"Invalid input. Please enter a positive number or '{SetupConstants.RetentionKeywords.Unlimited}'. Using unlimited retention.");
        return Task.FromResult<int?>(-1);
    }

    public Task<bool> ShowSummaryAndConfirmAsync(
        Features.Setup.Models.SetupSummary summary,
        CancellationToken cancellationToken)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        table.AddRow("SSH Key", summary.SshKeyDisplay.EscapeMarkup());
        table.AddRow("LLM Provider", summary.LlmProvider);
        table.AddRow("API Key", MaskApiKey(summary.ApiKey));
        table.AddRow("Memory Directory", summary.RootDirectory);
        table.AddRow("Log Level", summary.LogLevel);

        // Display retention: -1 or 0 means unlimited, otherwise show days
        var retentionDisplay = summary.RetentionDays <= 0
            ? SetupConstants.RetentionKeywords.UnlimitedDisplay
            : $"{summary.RetentionDays} {SetupConstants.DisplayStrings.Days}";
        table.AddRow("Retention Days", retentionDisplay);

        _console.Write(new Rule("[yellow]Configuration Summary[/]"));
        _console.Write(table);

        var confirm = _console.Confirm("Save this configuration?", true);
        return Task.FromResult(confirm);
    }

    public void ShowStepHeader(int currentStep, int totalSteps, string stepName)
    {
        _console.Clear();
        _console.Write(new Rule($"[blue]Step {currentStep} of {totalSteps}: {stepName}[/]"));
        _console.WriteLine();
    }

    public void ShowStatus(string message)
    {
        _console.MarkupLine($"[grey]ℹ️  {message.EscapeMarkup()}[/]");
    }

    public async Task RunWithProgressAsync(
        string taskDescription,
        Func<Action<double>, Task> operation,
        CancellationToken cancellationToken)
    {
        await _console.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask(taskDescription.EscapeMarkup(), maxValue: 100);

                await operation(progress =>
                {
                    progressTask.Value = progress;
                });

                // Ensure we show 100% completion
                progressTask.Value = 100;
            });
    }

    public void ShowSuccess(string message)
    {
        _console.MarkupLine($"[green]✓ {message.EscapeMarkup()}[/]");
    }

    public void ShowError(string message)
    {
        _console.MarkupLine($"[red]✗ {message.EscapeMarkup()}[/]");
    }

    public void ShowWarning(string message)
    {
        _console.MarkupLine($"[yellow]⚠️  {message.EscapeMarkup()}[/]");
    }

    public Task<double?> PromptForInputVolumeAsync(
        double? currentValue,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Input volume multiplier (0.0 to 2.0)[/]");
        _console.MarkupLine("[grey]   • Laptop/built-in mics: 1.0-1.2[/]");
        _console.MarkupLine("[grey]   • Dynamic mics (SM7B): 0.7-0.8[/]");
        _console.MarkupLine("[grey]   • Condenser/USB mics: 0.9-1.0[/]");
        _console.WriteLine();

        var input = CancellablePrompt.Text("Enter input volume (0.0 - 2.0):", p => p
            .DefaultValue((currentValue ?? 1.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
            .Validate(val =>
            {
                if (double.TryParse(val, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var volume))
                {
                    if (volume >= 0.0 && volume <= 2.0)
                        return Spectre.Console.ValidationResult.Success();
                    return Spectre.Console.ValidationResult.Error("[red]Volume must be between 0.0 and 2.0[/]");
                }
                return Spectre.Console.ValidationResult.Error("[red]Please enter a valid number[/]");
            }));

        if (input is null)
        {
            return Task.FromResult<double?>(null); // Escape pressed
        }

        if (double.TryParse(input, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
        {
            return Task.FromResult<double?>(result);
        }

        return Task.FromResult<double?>(null);
    }

    public Task<bool?> PromptForBooleanAsync(
        string prompt,
        bool? currentValue,
        CancellationToken cancellationToken)
    {
        var choices = new[] { "Enabled", "Disabled" };

        var selected = CancellablePrompt.Selection<string>(p => p
            .Title(prompt)
            .AddChoices(choices));

        if (selected is null)
        {
            return Task.FromResult<bool?>(null); // Escape pressed
        }

        return Task.FromResult<bool?>(selected == "Enabled");
    }

    public Task<int?> PromptForIntAsync(
        string prompt,
        int? currentValue,
        int min,
        int max,
        CancellationToken cancellationToken)
    {
        var input = CancellablePrompt.Text(prompt, p => p
            .DefaultValue((currentValue ?? min).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Validate(val =>
            {
                if (int.TryParse(val, out var value))
                {
                    if (value >= min && value <= max)
                        return Spectre.Console.ValidationResult.Success();
                    return Spectre.Console.ValidationResult.Error($"[red]Value must be between {min} and {max}[/]");
                }
                return Spectre.Console.ValidationResult.Error("[red]Please enter a valid integer[/]");
            }));

        if (input is null)
        {
            return Task.FromResult<int?>(null); // Escape pressed
        }

        if (int.TryParse(input, out var result))
        {
            return Task.FromResult<int?>(result);
        }

        return Task.FromResult<int?>(null);
    }

    public Task<string?> PromptForSttProviderAsync(
        string? currentProvider,
        CancellationToken cancellationToken)
    {
        // Order: whisper-cpp first (recommended), OpenAI second, Built-in last (experimental)
        var choices = new Dictionary<string, string>
        {
            ["Local (whisper.cpp)"] = SttProviders.WhisperCpp,
            ["OpenAI (Cloud)"] = SttProviders.OpenAI
            //["Local (Built-in) [[experimental]]"] = SttProviders.BuiltInLocal
        };

        var prompt = new SelectionPrompt<string>()
            .Title("Select your speech-to-text provider:")
            .AddChoices(choices.Keys);

        if (!string.IsNullOrEmpty(currentProvider))
        {
            var currentKey = choices.FirstOrDefault(x => x.Value == currentProvider).Key;
            if (currentKey != null)
            {
                prompt.HighlightStyle(new Style(Color.Green));
            }
        }

        var selected = PromptSelectionWithEscape(prompt);
        if (selected is null) return Task.FromResult<string?>(null);
        return Task.FromResult<string?>(choices[selected]);
    }

    public Task<string?> PromptForSttApiKeyAsync(
        string provider,
        string? currentApiKey,
        CancellationToken cancellationToken)
    {
        // Prompt for API key
        var providerName = provider == "openai" ? "OpenAI" : provider;
        var prompt = new TextPrompt<string>($"Enter your {providerName} API key for STT:")
            .Secret();

        if (!string.IsNullOrEmpty(currentApiKey))
        {
            prompt.DefaultValue(currentApiKey);
        }

        var apiKey = PromptTextWithEscape(prompt);
        return Task.FromResult(apiKey);
    }



    public Task<string?> PromptForRootDirectoryAsync(
        string? currentDirectory,
        CancellationToken cancellationToken)
    {
        var defaultDir = currentDirectory ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DirectoryNames.ApplicationRoot);

        _console.MarkupLine("[grey]ℹ️  Root Directory:[/]");
        _console.MarkupLine("[grey]   This is the base directory for all Ten Second Tom data (config, memories, templates).[/]");
        _console.WriteLine();

        var prompt = new TextPrompt<string>("Where should I store your Ten Second Tom data?")
            .DefaultValue(defaultDir)
            .AllowEmpty();

        var directory = PromptTextWithEscape(prompt);
        return Task.FromResult(directory);
    }

    public Task<Infrastructure.Storage.StorageProviderMetadata?> PromptForStorageProviderAsync(
        IReadOnlyList<Infrastructure.Storage.StorageProviderMetadata> availableProviders,
        string? currentProviderId,
        CancellationToken cancellationToken)
    {
        if (!availableProviders.Any())
        {
            ShowWarning("No storage providers available.");
            return Task.FromResult<Infrastructure.Storage.StorageProviderMetadata?>(null);
        }

        _console.MarkupLine("[grey]ℹ️  Storage Provider:[/]");
        _console.MarkupLine("[grey]   Choose where to store your memory entries:[/]");
        _console.MarkupLine("[grey]   • Default: TST-native file structure (recommended for new users)[/]");
        _console.MarkupLine("[grey]   • Obsidian: Store entries in your Obsidian vault for seamless note integration[/]");
        _console.WriteLine();

        var choices = availableProviders.ToDictionary(
            p => $"{p.DisplayName} - {p.Description}".EscapeMarkup(),
            p => p
        );

        var prompt = new SelectionPrompt<string>()
            .Title("Select storage provider:")
            .PageSize(10)
            .AddChoices(choices.Keys);

        // Highlight current provider if set
        if (!string.IsNullOrEmpty(currentProviderId))
        {
            var currentProvider = availableProviders.FirstOrDefault(p =>
                p.ProviderId.Equals(currentProviderId, StringComparison.OrdinalIgnoreCase));
            if (currentProvider != null)
            {
                prompt.HighlightStyle(new Style(Color.Green));
            }
        }

        var selected = PromptSelectionWithEscape(prompt);
        if (selected is null) return Task.FromResult<Infrastructure.Storage.StorageProviderMetadata?>(null);
        return Task.FromResult<Infrastructure.Storage.StorageProviderMetadata?>(choices[selected]);
    }

    public Task<string?> PromptForObsidianVaultPathAsync(
        string? currentPath,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Obsidian Vault Path:[/]");
        _console.MarkupLine("[grey]   Enter the path to your Obsidian vault directory.[/]");
        _console.MarkupLine("[grey]   The vault must contain a .obsidian directory to be valid.[/]");
        _console.WriteLine();

        var defaultPath = currentPath ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Obsidian");

        var prompt = new TextPrompt<string>("Obsidian vault path:")
            .DefaultValue(defaultPath)
            .AllowEmpty();

        var vaultPath = PromptTextWithEscape(prompt);
        if (vaultPath is null) return Task.FromResult<string?>(null);

        // Basic validation - check if .obsidian directory exists
        if (!string.IsNullOrWhiteSpace(vaultPath))
        {
            var obsidianDir = Path.Combine(vaultPath, ".obsidian");
            if (!Directory.Exists(obsidianDir))
            {
                ShowWarning($"Warning: .obsidian directory not found at {obsidianDir}");
                _console.MarkupLine("[yellow]This may not be a valid Obsidian vault. Continue anyway? (y/n)[/]");
                var response = Console.ReadLine();
                if (!response?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    return Task.FromResult<string?>(null);
                }
            }
        }

        return Task.FromResult<string?>(vaultPath);
    }

    public Task<string?> PromptForSubdirectoryAsync(
        string prompt,
        string? currentValue,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  TST Subdirectory:[/]");
        _console.MarkupLine("[grey]   Optional: Store Ten Second Tom entries in a subdirectory of your vault.[/]");
        _console.MarkupLine("[grey]   Leave empty to store entries at the root level of your vault.[/]");
        _console.WriteLine();

        var textPrompt = new TextPrompt<string>(prompt)
            .DefaultValue(currentValue ?? "ten-second-tom")
            .AllowEmpty();

        var subdirectory = PromptTextWithEscape(textPrompt);
        if (subdirectory is null) return Task.FromResult<string?>(null);

        // Return null if empty (root level)
        return Task.FromResult(string.IsNullOrWhiteSpace(subdirectory) ? null : subdirectory);
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return SetupConstants.DisplayStrings.NotSet;

        if (apiKey.Length <= 4)
            return new string('*', apiKey.Length);

        return apiKey[..^4].Select(_ => '*').Aggregate("", (a, b) => a + b) + apiKey[^4..];
    }

    public Task<string?> PromptForStringAsync(
        string prompt,
        string? defaultValue,
        CancellationToken cancellationToken)
    {
        var result = CancellablePrompt.Text(prompt, p =>
        {
            p.AllowEmpty();

            if (!string.IsNullOrEmpty(defaultValue))
            {
                p.DefaultValue(defaultValue);
            }
        });

        return Task.FromResult(result);
    }

    public Task<T?> PromptForSelectionAsync<T>(
        string prompt,
        IReadOnlyList<T> options,
        Func<T, string> displaySelector,
        CancellationToken cancellationToken)
        where T : class
    {
        var result = CancellablePrompt.Selection<T>(p => p
            .Title(prompt)
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .UseConverter(displaySelector)
            .AddChoices(options));

        return Task.FromResult(result);
    }

    public async Task<(string baseUrl, string modelName)?> PromptForLocalLlmConfigurationAsync(
        string? currentBaseUrl,
        string? currentModel,
        IHttpClientFactory httpClientFactory,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Local LLM Configuration:[/]");
        _console.MarkupLine("[grey]   Configure connection to your local OpenAI-compatible server.[/]");
        _console.WriteLine();

        // Step 1: Select server type to get default URL
        var serverTypes = new[]
        {
            new { Name = "Ollama", Url = "http://127.0.0.1:11434/v1", SupportsApiTags = true },
            new { Name = "LM Studio", Url = "http://127.0.0.1:1234/v1", SupportsApiTags = false },
            new { Name = "llama.cpp / llama-server", Url = "http://127.0.0.1:8080/v1", SupportsApiTags = false },
            new { Name = "LocalAI", Url = "http://127.0.0.1:8080/v1", SupportsApiTags = false },
            new { Name = "Generic (Custom URL)", Url = "http://127.0.0.1:8080/v1", SupportsApiTags = false }
        };

        var selectedServer = await PromptForSelectionAsync(
            "Select your local LLM server type:",
            serverTypes,
            x => x.Name,
            cancellationToken);

        if (selectedServer == null)
        {
            return null; // User cancelled
        }

        // For known server types, always use the server's default URL
        // For "Generic (Custom URL)", use currentBaseUrl as fallback if available
        string defaultBaseUrl = selectedServer.Name == "Generic (Custom URL)" && !string.IsNullOrWhiteSpace(currentBaseUrl)
            ? currentBaseUrl
            : selectedServer.Url;

        // Step 2: Prompt for Base URL
        var baseUrlPrompt = new TextPrompt<string>("Base URL:")
            .DefaultValue(defaultBaseUrl)
            .AllowEmpty();

        var baseUrl = PromptTextWithEscape(baseUrlPrompt);

        if (baseUrl is null)
        {
            return null; // Escape pressed
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _console.MarkupLine("[yellow]Base URL is required for local LLM configuration.[/]");
            return null;
        }

        // Step 3: Verify connectivity and fetch available models
        _console.WriteLine();

        var verificationResult = await _console.Status()
            .StartAsync("Verifying connection to local LLM server...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                ctx.SpinnerStyle(Style.Parse("green"));

                try
                {
                    using var client = httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5);

                    baseUrl = baseUrl.TrimEnd('/');

                    // Try OpenAI-compatible /v1/models endpoint first
                    string modelsUrl = baseUrl.EndsWith("/v1")
                        ? $"{baseUrl}/models"
                        : $"{baseUrl}/v1/models";

                    using var response = await client.GetAsync(modelsUrl, cancellationToken);

                    if (response.IsSuccessStatusCode)
                    {
                        ctx.Status("✓ Connected! Fetching available models...");

                        var content = await response.Content.ReadAsStringAsync(cancellationToken);
                        var models = ParseModelsFromResponse(content);

                        if (models.Count > 0)
                        {
                            return (success: true, models: models, error: (string?)null);
                        }
                        else
                        {
                            return (success: false, models: new List<string>(), error: "Connected but couldn't fetch model list.");
                        }
                    }
                    else
                    {
                        return (success: false, models: new List<string>(), error: $"Server responded with {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    return (success: false, models: new List<string>(), error: $"Could not connect: {ex.Message}");
                }
            });

        // Now we're outside the Status block - safe to show prompts
        if (verificationResult.success)
        {
            _console.MarkupLine("[green]✓ Successfully connected to local LLM[/]");
            _console.MarkupLine($"[grey]  Found {verificationResult.models.Count} available model(s)[/]");

            // Step 4: Prompt for model selection using SelectionPrompt
            _console.WriteLine();

            // Add an option to enter custom model name
            var modelChoices = new List<string>(verificationResult.models);
            const string customOption = "⌨️  Enter custom model name...";
            modelChoices.Add(customOption);

            var selectionPrompt = new SelectionPrompt<string>()
                .Title("Select model:")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more models)[/]");

            // Set default to current model if it exists in the list
            if (!string.IsNullOrWhiteSpace(currentModel))
            {
                // Try exact match or fuzzy match with tag
                var matchingModel = modelChoices.FirstOrDefault(m =>
                    m.Equals(currentModel, StringComparison.OrdinalIgnoreCase) ||
                    m.StartsWith($"{currentModel}:", StringComparison.OrdinalIgnoreCase));

                if (matchingModel != null)
                {
                    // Move matching model to top so it's selected by default
                    modelChoices.Remove(matchingModel);
                    modelChoices.Insert(0, matchingModel);

                    // Also highlight it
                    selectionPrompt.HighlightStyle(new Style(Color.Green));
                }
            }

            selectionPrompt.AddChoices(modelChoices);

            var selectedModel = PromptSelectionWithEscape(selectionPrompt);
            if (selectedModel is null) return null;

            string modelName;
            if (selectedModel == customOption)
            {
                // User wants to enter a custom model name
                var customPrompt = new TextPrompt<string>("Enter model name:")
                    .DefaultValue(currentModel ?? "local-model")
                    .AllowEmpty();

                var customModelName = PromptTextWithEscape(customPrompt);
                if (customModelName is null) return null;
                modelName = customModelName;
                if (string.IsNullOrWhiteSpace(modelName))
                {
                    modelName = "local-model";
                }

                _console.MarkupLine("[yellow]⚠ Custom model name entered - ensure it exists on your server[/]");
            }
            else
            {
                modelName = selectedModel;
            }

            return (baseUrl, modelName);
        }
        else
        {
            // Verification failed - show error and ask if user wants to continue
            _console.MarkupLine($"[yellow]⚠ {verificationResult.error}[/]");
            _console.MarkupLine("[yellow]  Note: Connectivity verification failed.[/]");
            _console.MarkupLine("[yellow]  Ensure your local server is running.[/]");
            _console.WriteLine();
            _console.MarkupLine("[yellow]Continue with configuration anyway? (y/n)[/]");

            var continueResponse = Console.ReadLine();
            if (continueResponse?.Equals("y", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                var modelPrompt = new TextPrompt<string>("Model name:")
                    .DefaultValue(currentModel ?? "local-model")
                    .AllowEmpty();

                var modelName = PromptTextWithEscape(modelPrompt);
                if (modelName is null) return null;
                return (baseUrl, modelName ?? "local-model");
            }

            return null; // User chose not to continue
        }
    }

    private static List<string> ParseModelsFromResponse(string jsonResponse)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(jsonResponse);
            var models = new List<string>();

            // Try OpenAI format: { "data": [ { "id": "model-name" } ] }
            if (doc.RootElement.TryGetProperty("data", out var dataArray))
            {
                using var dataEnumerator = dataArray.EnumerateArray();
                foreach (var item in dataEnumerator)
                {
                    if (item.TryGetProperty("id", out var id))
                    {
                        models.Add(id.GetString() ?? "");
                    }
                }
            }
            // Try Ollama format: { "models": [ { "name": "model:tag" } ] }
            else if (doc.RootElement.TryGetProperty("models", out var modelsArray))
            {
                using var modelsEnumerator = modelsArray.EnumerateArray();
                foreach (var item in modelsEnumerator)
                {
                    if (item.TryGetProperty("name", out var name))
                    {
                        models.Add(name.GetString() ?? "");
                    }
                }
            }

            return models.Where(m => !string.IsNullOrWhiteSpace(m)).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }
}
