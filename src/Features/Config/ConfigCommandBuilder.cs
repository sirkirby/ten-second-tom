using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Abstractions.UI;

namespace TenSecondTom.Features.Config;

/// <summary>
/// Provides the /config command via ICommandBuilder discovery.
/// </summary>
public sealed class ConfigCommandBuilder : ICommandBuilder
{
    public int Priority => 70;

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var configCommand = new Command("config", "View and manage Ten Second Tom configuration");

        // Show subcommand
        var showCommand = new Command("show", "Display current configuration");
        var showSecretsOption = new Option<bool>("--show-secrets")
        {
            Description = "Show full API keys (last 4 characters by default)"
        };
        showCommand.Options.Add(showSecretsOption);
        showCommand.Options.Add(jsonOutputOption);

        showCommand.SetAction(async (parseResult) =>
        {
            bool showSecrets = parseResult.GetValue(showSecretsOption);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            var handler = serviceProvider.GetRequiredService<ShowConfig.Handler>();
            var sectionStore = serviceProvider.GetRequiredService<IConfigurationSectionStore>();

            var command = new ShowConfig.Command
            {
                Action = ConfigAction.Show,
                SettingName = null,
                SettingValue = null,
                ShowSecrets = showSecrets
            };

            var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(result.Value));
                }
                else
                {
                    var configPath = sectionStore.GetConfigPath();
                    DisplayConfiguration(result.Value!, showSecrets, configPath);
                }
                return 0;
            }
            else
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = result.Error }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error.EscapeMarkup()}");
                }
                return 1;
            }
        });

        // Set subcommand
        var setCommand = new Command("set", "Update a configuration setting (use 'tom config llm' or 'tom config audio' for guided flows)");
        var settingNameArg = new Argument<string>("setting")
        {
            Description = "Setting name (llm-provider, api-key, memory-directory, ssh-key-path, log-level, retention-days). Use subcommands for guided flows."
        };
        var settingValueArg = new Argument<string?>("value")
        {
            Description = "New value for the setting",
            Arity = ArgumentArity.ZeroOrOne
        };

        setCommand.Arguments.Add(settingNameArg);
        setCommand.Arguments.Add(settingValueArg);
        setCommand.Options.Add(jsonOutputOption);

        setCommand.SetAction(async (parseResult) =>
        {
            string settingName = parseResult.GetValue(settingNameArg)!;
            string? settingValue = parseResult.GetValue(settingValueArg);
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            if (IsInteractiveShortcut(settingName))
            {
                var guidance = GetInteractiveSettingMessage(settingName);
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = guidance }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]{guidance.EscapeMarkup()}[/]");
                }
                return 1;
            }

            var handler = serviceProvider.GetRequiredService<ShowConfig.Handler>();

            var command = new ShowConfig.Command
            {
                Action = ConfigAction.Set,
                SettingName = settingName,
                SettingValue = settingValue,
                ShowSecrets = false
            };

            var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = true, message = $"Updated {settingName}" }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] Updated [yellow]{settingName.EscapeMarkup()}[/] successfully");
                }
                return 0;
            }
            else
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = result.Error }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error.EscapeMarkup()}");
                }
                return 1;
            }
        });

        // Validate subcommand
        var validateCommand = new Command("validate", "Validate current configuration");
        validateCommand.Options.Add(jsonOutputOption);

        validateCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);

            var handler = serviceProvider.GetRequiredService<ShowConfig.Handler>();

            var command = new ShowConfig.Command
            {
                Action = ConfigAction.Validate,
                SettingName = null,
                SettingValue = null,
                ShowSecrets = false
            };

            var result = await handler.Handle(command, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = true, message = "Configuration is valid" }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓[/] Configuration is valid");
                }
                return 0;
            }
            else
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = result.Error }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error.EscapeMarkup()}");
                }
                return 1;
            }
        });

        // Discover and register config subcommands from feature slices via assembly scanning
        var subcommandBuilders = DiscoverConfigSubcommandBuilders();
        foreach (var builder in subcommandBuilders)
        {
            var subcommand = builder.BuildConfigSubcommand(serviceProvider, jsonOutputOption);
            if (subcommand != null)
            {
                configCommand.Subcommands.Add(subcommand);
            }
        }

        configCommand.Subcommands.Add(showCommand);
        configCommand.Subcommands.Add(setCommand);
        configCommand.Subcommands.Add(validateCommand);

        return configCommand;
    }

    /// <summary>
    /// Discovers all IConfigSubcommandBuilder implementations via assembly scanning.
    /// Follows the same pattern as MediatR and FluentValidation auto-discovery.
    /// </summary>
    /// <returns>Collection of discovered subcommand builders.</returns>
    private static IEnumerable<IConfigSubcommandBuilder> DiscoverConfigSubcommandBuilders() =>
        typeof(ConfigCommandBuilder).Assembly
            .GetTypes()
            .Where(t =>
                typeof(IConfigSubcommandBuilder).IsAssignableFrom(t) &&
                !t.IsInterface &&
                !t.IsAbstract)
            .Select(Activator.CreateInstance)
            .OfType<IConfigSubcommandBuilder>();

    private static void DisplayConfiguration(ConfigDisplay config, bool showSecrets, string configFilePath)
    {
        var table = new Spectre.Console.Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[yellow]Setting[/]")
            .AddColumn("[yellow]Value[/]");

        // Application Directory Configuration
        table.AddRow("[yellow]Application Directory[/]", "");
        table.AddRow("  Root Directory", config.RootDirectory?.EscapeMarkup() ?? "[dim]Not set[/]");
        table.AddRow("", ""); // Spacer

        // SSH Configuration
        table.AddRow("[yellow]SSH Configuration[/]", "");
        table.AddRow("  Key Path", config.Ssh.KeyPath?.EscapeMarkup() ?? "[dim]Not set[/]");
        table.AddRow("  Key Source", config.Ssh.KeySource?.ToString() ?? "[dim]Not set[/]");
        if (!string.IsNullOrWhiteSpace(config.Ssh.AgentSocketPath))
        {
            table.AddRow("  Agent Socket", config.Ssh.AgentSocketPath.EscapeMarkup());
        }

        // LLM Configuration
        table.AddRow("[yellow]LLM Configuration[/]", "");
        table.AddRow("  Provider", config.Llm.Provider.ToString());

        // Model - show friendly name if available
        string modelDisplay = "[dim]Not set[/]";
        if (!string.IsNullOrEmpty(config.Llm.Model))
        {
            var model = ModelRegistry.GetById(config.Llm.Model);
            modelDisplay = model != null
                ? $"{model.DisplayName.EscapeMarkup()} ({model.CostTier})"
                : config.Llm.Model.EscapeMarkup();
        }
        table.AddRow("  Model", modelDisplay);

        string apiKeyDisplay = showSecrets
            ? (config.Llm.ApiKey?.EscapeMarkup() ?? "[dim]Not set[/]")
            : MaskApiKey(config.Llm.ApiKey);
        table.AddRow("  API Key", apiKeyDisplay);

        // Max Input Tokens
        var maxTokensDisplay = config.Llm.MaxInputTokens.HasValue
            ? config.Llm.MaxInputTokens.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
            : "[dim]Not set[/]";
        table.AddRow("  Max Input Tokens", maxTokensDisplay);

        // Storage Configuration
        table.AddRow("[yellow]Storage Configuration[/]", "");

        // Storage Provider
        var providerDisplay = config.Storage.ProviderId switch
        {
            "default" => "Default (Local File System)",
            "obsidian" => "Obsidian Vault",
            _ => config.Storage.ProviderId ?? "default"
        };
        table.AddRow("  Provider", providerDisplay);

        // Provider Path (for external providers like Obsidian)
        if (!string.IsNullOrWhiteSpace(config.Storage.ProviderPath))
        {
            table.AddRow("  Provider Path", config.Storage.ProviderPath.EscapeMarkup());
        }

        // Memory Subdirectory (isolation subdirectory within provider)
        if (!string.IsNullOrWhiteSpace(config.Storage.MemorySubdirectory))
        {
            table.AddRow("  Memory Subdirectory", config.Storage.MemorySubdirectory.EscapeMarkup());
        }

        table.AddRow("  Create If Missing", config.Storage.CreateIfMissing ? "Yes" : "No");

        // Optional Configuration
        table.AddRow("[yellow]Optional Configuration[/]", "");
        table.AddRow("  Log Level", config.Optional.LogLevel.ToString());
        table.AddRow("  Retention Days", config.Optional.RetentionDays == -1
            ? "Unlimited"
            : config.Optional.RetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture));
        table.AddRow("  Telemetry", config.Optional.EnableTelemetry ? "Enabled" : "Disabled");

        // Audio Configuration
        table.AddRow("[yellow]Audio Configuration[/]", "");
        table.AddRow("  STT Provider", config.Audio.SttProvider);

        if (!string.IsNullOrWhiteSpace(config.Audio.SttApiKey))
        {
            string sttApiKeyDisplay = showSecrets
                ? config.Audio.SttApiKey
                : MaskApiKey(config.Audio.SttApiKey);
            table.AddRow("  STT API Key", sttApiKeyDisplay);
        }

        if (!string.IsNullOrWhiteSpace(config.Audio.SttModel))
        {
            table.AddRow("  STT Model", config.Audio.SttModel);
        }

        table.AddRow("  Keep Files", config.Audio.KeepFiles ? "Yes" : "No");

        // Audio Recorder Configuration
        table.AddRow("  [dim]Recorder:[/]", "");
        table.AddRow("    Input Volume", config.Audio.Recorder.InputVolume.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        table.AddRow("    Noise Reduction", config.Audio.Recorder.EnableNoiseReduction ? "Enabled" : "Disabled");
        table.AddRow("    Frequency Filters", config.Audio.Recorder.EnableFrequencyFilters ? "Enabled" : "Disabled");

        // Audio Preprocessing Configuration
        table.AddRow("  [dim]Preprocessing:[/]", "");
        table.AddRow("    Remove Silence", config.Audio.Preprocessing.RemoveSilence ? "Enabled" : "Disabled");
        if (config.Audio.Preprocessing.RemoveSilence)
        {
            table.AddRow("    Silence Threshold", $"{config.Audio.Preprocessing.SilenceThresholdDb} dB");
            table.AddRow("    Min Silence Duration", $"{config.Audio.Preprocessing.MinimumSilenceDurationMs} ms");
        }

        // Metadata
        table.AddRow("[yellow]Metadata[/]", "");
        table.AddRow("  Created", $"{config.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        if (config.LastModifiedAt.HasValue)
        {
            table.AddRow("  Modified", $"{config.LastModifiedAt.Value:yyyy-MM-dd HH:mm:ss}");
        }
        table.AddRow("  Version", config.ConfigurationVersion ?? "[dim]1.0[/]");

        AnsiConsole.Write(table);

        // Add clickable link to config file
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Configuration file:[/] [link]{configFilePath.EscapeMarkup()}[/]");
    }

    private static string MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "[dim]Not set[/]";

        if (apiKey.Length <= 4)
            return "••••";

        return $"••••{apiKey[^4..]}";
    }

    private static bool IsInteractiveShortcut(string settingName) =>
        settingName.Equals("llm", StringComparison.OrdinalIgnoreCase) ||
        settingName.Equals("audio", StringComparison.OrdinalIgnoreCase);

    private static string GetInteractiveSettingMessage(string settingName) =>
        settingName.Equals("llm", StringComparison.OrdinalIgnoreCase)
            ? "Use 'tom config llm' to configure LLM provider and model interactively."
            : "Use 'tom config audio' to configure audio settings interactively.";
}
