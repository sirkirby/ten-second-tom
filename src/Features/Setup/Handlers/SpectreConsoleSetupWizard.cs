using Microsoft.Extensions.Logging;
using Spectre.Console;
using TenSecondTom.Features.Setup.Models;

namespace TenSecondTom.Features.Setup.Handlers;

/// <summary>
/// Spectre.Console-based implementation of the setup wizard UI
/// Provides rich, interactive terminal experience
/// </summary>
public sealed class SpectreConsoleSetupWizard : ISetupWizardUI
{
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
            .AddChoices(new[]
            {
                "OpenAI (GPT-4, GPT-3.5)",
                "Anthropic (Claude 3.5)"
            });

        if (currentProvider.HasValue)
        {
            var currentChoice = currentProvider.Value == LlmProvider.OpenAI
                ? "OpenAI (GPT-4, GPT-3.5)"
                : "Anthropic (Claude 3.5)";
            prompt.HighlightStyle(new Style(Color.Green));
        }

        var selected = _console.Prompt(prompt);
        var provider = selected.StartsWith("OpenAI", StringComparison.Ordinal) ? LlmProvider.OpenAI : LlmProvider.Anthropic;
        
        return Task.FromResult<LlmProvider?>(provider);
    }

    public Task<string?> PromptForApiKeyAsync(
        LlmProvider provider,
        string? currentApiKey,
        CancellationToken cancellationToken)
    {
        var providerName = provider == LlmProvider.OpenAI ? "OpenAI" : "Anthropic";
        var prompt = new TextPrompt<string>($"Enter your {providerName} API key:")
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
                ".memory", "ten-second-tom");

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
            .AddChoices(new[]
            {
                "Debug (verbose)",
                "Information (recommended)",
                "Warning (quiet)",
                "Error (silent)"
            });

        var selected = _console.Prompt(prompt);
        
        var logLevel = selected switch
        {
            "Debug (verbose)" => Microsoft.Extensions.Logging.LogLevel.Debug,
            "Information (recommended)" => Microsoft.Extensions.Logging.LogLevel.Information,
            "Warning (quiet)" => Microsoft.Extensions.Logging.LogLevel.Warning,
            "Error (silent)" => Microsoft.Extensions.Logging.LogLevel.Error,
            _ => Microsoft.Extensions.Logging.LogLevel.Information
        };

        return Task.FromResult<Microsoft.Extensions.Logging.LogLevel?>(logLevel);
    }

    public Task<int?> PromptForRetentionDaysAsync(
        int? currentDays,
        CancellationToken cancellationToken)
    {
        _console.MarkupLine("[grey]ℹ️  Choose how long to keep your memories before automatic deletion.[/]");
        _console.MarkupLine("[grey]   Enter 'unlimited' to keep all memories forever (recommended).[/]");
        _console.WriteLine();

        var prompt = new TextPrompt<string>("How long should memories be kept? (enter 'unlimited' or number of days)")
            .DefaultValue(currentDays.HasValue && currentDays.Value > 0 ? currentDays.Value.ToString() : "unlimited")
            .AllowEmpty();

        var input = _console.Prompt(prompt);
        
        // Parse input: "unlimited", "forever", "0" -> -1, otherwise parse as number
        if (string.IsNullOrWhiteSpace(input) || 
            input.Equals("unlimited", StringComparison.OrdinalIgnoreCase) ||
            input.Equals("forever", StringComparison.OrdinalIgnoreCase) ||
            input == "0")
        {
            return Task.FromResult<int?>(-1); // -1 means unlimited
        }
        
        if (int.TryParse(input, out var days) && days > 0)
        {
            return Task.FromResult<int?>(days);
        }
        
        ShowWarning("Invalid input. Please enter a positive number or 'unlimited'. Using unlimited retention.");
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
            ?? "Not set";
        
        table.AddRow("SSH Key", sshKeyDisplay.EscapeMarkup());
        table.AddRow("LLM Provider", settings.Llm.Provider.ToString());
        table.AddRow("API Key", MaskApiKey(settings.Llm.ApiKey));
        table.AddRow("Memory Directory", settings.Storage.MemoryDirectory);
        table.AddRow("Log Level", settings.Optional.LogLevel.ToString());
        
        // Display retention: -1 or 0 means unlimited, otherwise show days
        var retentionDisplay = settings.Optional.RetentionDays <= 0 
            ? "Unlimited (never delete)" 
            : $"{settings.Optional.RetentionDays} days";
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
        _console.MarkupLine($"[grey]ℹ️  {message}[/]");
    }

    public void ShowSuccess(string message)
    {
        _console.MarkupLine($"[green]✓ {message}[/]");
    }

    public void ShowError(string message)
    {
        _console.MarkupLine($"[red]✗ {message}[/]");
    }

    public void ShowWarning(string message)
    {
        _console.MarkupLine($"[yellow]⚠️  {message}[/]");
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return "Not set";

        if (apiKey.Length <= 4)
            return new string('*', apiKey.Length);

        return apiKey[..^4].Select(_ => '*').Aggregate("", (a, b) => a + b) + apiKey[^4..];
    }
}
