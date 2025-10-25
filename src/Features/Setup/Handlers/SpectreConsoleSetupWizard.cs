using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Setup.Models;
using TenSecondTom.Shared.Constants;

namespace TenSecondTom.Features.Setup.Handlers;

/// <summary>
/// Spectre.Console-based implementation of the setup wizard UI
/// Provides rich, interactive terminal experience
/// </summary>
public sealed class SpectreConsoleSetupWizard : ISetupWizardUI
{
    private static readonly string[] LogLevelChoices =
    [
        SetupWizardConstants.LogLevelDisplayNames.Debug,
        SetupWizardConstants.LogLevelDisplayNames.Information,
        SetupWizardConstants.LogLevelDisplayNames.Warning,
        SetupWizardConstants.LogLevelDisplayNames.Error
    ];

    private readonly IAnsiConsole _console;
    private readonly ILogger<SpectreConsoleSetupWizard> _logger;

    public SpectreConsoleSetupWizard(
        IAnsiConsole console,
        ILogger<SpectreConsoleSetupWizard> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

        var selected = _console.Prompt(prompt);
        return Task.FromResult<SshKeyInfo?>(choices[selected]);
    }

    public Task<LlmProvider?> PromptForLlmProviderAsync(
        LlmProvider? currentProvider,
        CancellationToken cancellationToken)
    {
        var prompt = new SelectionPrompt<string>()
            .Title("Choose your AI provider:")
            .AddChoices(SetupWizardConstants.ProviderDisplayNames.GetAvailableDisplayNames());

        if (currentProvider.HasValue)
        {
            // Only highlight if the current provider is available (OpenAI)
            if (currentProvider.Value == LlmProvider.OpenAI)
            {
                prompt.HighlightStyle(new Style(Color.Green));
            }
        }

        var selected = _console.Prompt(prompt);

        // Since only OpenAI is available, we know the selection must be OpenAI
        // But we still parse it for consistency
        var provider = selected.StartsWith("OpenAI", StringComparison.Ordinal) ? LlmProvider.OpenAI : LlmProvider.OpenAI;

        return Task.FromResult<LlmProvider?>(provider);
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

        var selected = _console.Prompt(prompt);
        
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
        // Convert enum to provider name constant, then to display name
        var providerName = provider == LlmProvider.OpenAI 
            ? LlmProviders.OpenAI 
            : LlmProviders.Anthropic;
        var displayName = SetupWizardConstants.ProviderDisplayNames.GetDisplayName(providerName);
        
        var prompt = new TextPrompt<string>($"Enter your {displayName} API key:")
            .Secret();

        if (!string.IsNullOrEmpty(currentApiKey))
        {
            prompt.DefaultValue(MaskApiKey(currentApiKey));
        }

        var apiKey = _console.Prompt(prompt);
        return Task.FromResult<string?>(apiKey);
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

        var directory = _console.Prompt(prompt);
        return Task.FromResult<string?>(directory);
    }

    public Task<Microsoft.Extensions.Logging.LogLevel?> PromptForLogLevelAsync(
        Microsoft.Extensions.Logging.LogLevel? currentLevel,
        CancellationToken cancellationToken)
    {
        var prompt = new SelectionPrompt<string>()
            .Title("Select logging level:")
            .AddChoices(LogLevelChoices);

        var selected = _console.Prompt(prompt);
        
        var logLevel = selected switch
        {
            SetupWizardConstants.LogLevelDisplayNames.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            SetupWizardConstants.LogLevelDisplayNames.Information => Microsoft.Extensions.Logging.LogLevel.Information,
            SetupWizardConstants.LogLevelDisplayNames.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            SetupWizardConstants.LogLevelDisplayNames.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        return Task.FromResult<Microsoft.Extensions.Logging.LogLevel?>(logLevel);
    }

    public Task<int?> PromptForRetentionDaysAsync(
        int? currentDays,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Choose how long to keep your memories before automatic deletion.[/]");
        _console.MarkupLine($"[grey]   Enter '{SetupWizardConstants.RetentionKeywords.Unlimited}' to keep all memories forever (recommended).[/]");
        _console.WriteLine();

        var prompt = new TextPrompt<string>("How long should memories be kept? (enter 'unlimited' or number of days)")
            .DefaultValue(currentDays.HasValue && currentDays.Value > 0 
                ? currentDays.Value.ToString() 
                : SetupWizardConstants.RetentionKeywords.Unlimited)
            .AllowEmpty();

        var input = _console.Prompt(prompt);
        
        // Parse input: "unlimited", "forever", "0" -> -1, otherwise parse as number
        if (string.IsNullOrWhiteSpace(input) || 
            input.Equals(SetupWizardConstants.RetentionKeywords.Unlimited, StringComparison.OrdinalIgnoreCase) ||
            input.Equals(SetupWizardConstants.RetentionKeywords.Forever, StringComparison.OrdinalIgnoreCase) ||
            input == SetupWizardConstants.RetentionKeywords.Zero)
        {
            return Task.FromResult<int?>(-1); // -1 means unlimited
        }
        
        if (int.TryParse(input, out var days) && days > 0)
        {
            return Task.FromResult<int?>(days);
        }
        
        ShowWarning($"Invalid input. Please enter a positive number or '{SetupWizardConstants.RetentionKeywords.Unlimited}'. Using unlimited retention.");
        return Task.FromResult<int?>(-1);
    }

    public Task<bool> ShowSummaryAndConfirmAsync(
        ConfigurationSettings settings,
        CancellationToken cancellationToken)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        // Show SSH key display name, or fall back to key path, or "Not set"
        var sshKeyDisplay = settings.Ssh.KeyDisplayName 
            ?? settings.Ssh.KeyPath 
            ?? SetupWizardConstants.DisplayStrings.NotSet;
        
        table.AddRow("SSH Key", sshKeyDisplay.EscapeMarkup());
        table.AddRow("LLM Provider", settings.Llm.Provider.ToString());
        table.AddRow("API Key", MaskApiKey(settings.Llm.ApiKey));
        table.AddRow("Memory Directory", settings.Storage.MemoryDirectory);
        table.AddRow("Log Level", settings.Optional.LogLevel.ToString());
        
        // Display retention: -1 or 0 means unlimited, otherwise show days
        var retentionDisplay = settings.Optional.RetentionDays <= 0 
            ? SetupWizardConstants.RetentionKeywords.UnlimitedDisplay 
            : $"{settings.Optional.RetentionDays} {SetupWizardConstants.DisplayStrings.Days}";
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

        var prompt = new TextPrompt<string>("Enter input volume (0.0 - 2.0):")
            .DefaultValue((currentValue ?? 1.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
            .Validate(input =>
            {
                if (double.TryParse(input, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var volume))
                {
                    if (volume >= 0.0 && volume <= 2.0)
                        return Spectre.Console.ValidationResult.Success();
                    return Spectre.Console.ValidationResult.Error("[red]Volume must be between 0.0 and 2.0[/]");
                }
                return Spectre.Console.ValidationResult.Error("[red]Please enter a valid number[/]");
            });

        var input = _console.Prompt(prompt);
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
        var defaultChoice = (currentValue ?? true) ? "Enabled" : "Disabled";

        var selectionPrompt = new SelectionPrompt<string>()
            .Title(prompt)
            .AddChoices(choices);

        var selected = _console.Prompt(selectionPrompt);
        return Task.FromResult<bool?>(selected == "Enabled");
    }

    public Task<int?> PromptForIntAsync(
        string prompt,
        int? currentValue,
        int min,
        int max,
        CancellationToken cancellationToken)
    {
        var textPrompt = new TextPrompt<string>(prompt)
            .DefaultValue((currentValue ?? min).ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Validate(input =>
            {
                if (int.TryParse(input, out var value))
                {
                    if (value >= min && value <= max)
                        return Spectre.Console.ValidationResult.Success();
                    return Spectre.Console.ValidationResult.Error($"[red]Value must be between {min} and {max}[/]");
                }
                return Spectre.Console.ValidationResult.Error("[red]Please enter a valid integer[/]");
            });

        var input = _console.Prompt(textPrompt);
        if (int.TryParse(input, out var result))
        {
            return Task.FromResult<int?>(result);
        }

        return Task.FromResult<int?>(null);
    }

    public Task<string?> PromptForSttPreferenceAsync(
        string? currentValue,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Speech-to-Text engine preference:[/]");
        _console.MarkupLine("[grey]   • auto: Try local (whisper.cpp) first, fallback to OpenAI[/]");
        _console.MarkupLine("[grey]   • local: Always use whisper.cpp (requires installation)[/]");
        _console.MarkupLine("[grey]   • openai: Always use OpenAI Whisper API[/]");
        _console.WriteLine();

        var choices = new[] { "auto", "local", "openai" };
        var defaultChoice = currentValue ?? "auto";

        var prompt = new SelectionPrompt<string>()
            .Title("Select STT preference:")
            .AddChoices(choices);

        var selected = _console.Prompt(prompt);
        return Task.FromResult<string?>(selected);
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return SetupWizardConstants.DisplayStrings.NotSet;

        if (apiKey.Length <= 4)
            return new string('*', apiKey.Length);

        return apiKey[..^4].Select(_ => '*').Aggregate("", (a, b) => a + b) + apiKey[^4..];
    }
}
