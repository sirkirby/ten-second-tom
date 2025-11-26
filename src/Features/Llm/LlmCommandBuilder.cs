using System.CommandLine;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Abstractions.Models;
using TenSecondTom.Infrastructure.Llm;

namespace TenSecondTom.Features.Llm;

/// <summary>
/// Command builder for LLM management commands.
/// Provides CLI commands for listing and downloading models for providers that support model management.
/// Structure: /llm [list|download|config]
/// </summary>
public sealed class LlmCommandBuilder : ICommandBuilder
{
    public int Priority => 75; // Management command

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var llmCommand = new Command("llm", "Manage LLM models and configuration");
        llmCommand.Options.Add(jsonOutputOption);

        // Add subcommands
        llmCommand.Subcommands.Add(BuildListSubcommand(serviceProvider, jsonOutputOption));
        llmCommand.Subcommands.Add(BuildDownloadSubcommand(serviceProvider, jsonOutputOption));
        llmCommand.Subcommands.Add(BuildConfigSubcommand(serviceProvider, jsonOutputOption));

        // Default action when no subcommand specified
        llmCommand.SetAction(parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "No subcommand specified. Use 'llm list', 'llm download', or 'llm config'." }));
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No subcommand specified.[/]");
                AnsiConsole.MarkupLine("[dim]Available subcommands: list, download, config[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]  llm list              List available models[/]");
                AnsiConsole.MarkupLine("[dim]  llm download <model>  Download a specific model[/]");
                AnsiConsole.MarkupLine("[dim]  llm config            Configure LLM provider and model[/]");
            }
            return Task.FromResult(1);
        });

        return llmCommand;
    }

    /// <summary>
    /// Builds the 'llm list' subcommand for listing available models.
    /// </summary>
    private static Command BuildListSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var listCommand = new Command("list", "List available models for the configured or specified provider");

        var providerOption = new Option<string?>("--provider")
        {
            Description = "Specify the provider (overrides configured provider)"
        };
        listCommand.Options.Add(providerOption);
        listCommand.Options.Add(jsonOutputOption);

        listCommand.SetAction(async parseResult =>
        {
            var jsonOutput = parseResult.GetValue(jsonOutputOption);
            var providerArg = parseResult.GetValue(providerOption);

            var (provider, llmProvider) = await ResolveProviderAsync(serviceProvider, providerArg, jsonOutput);
            if (llmProvider == null)
            {
                return 1;
            }

            // Check if provider supports model management
            if (llmProvider is not ISupportsModelManagement modelManagement)
            {
                return await HandleLocalOpenAiListAsync(serviceProvider, provider, jsonOutput);
            }

            var models = await modelManagement.ListModelsAsync(CancellationToken.None);

            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = true, provider = provider.ToString(), models }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[cyan]Available models for {provider}:[/]");
                foreach (var model in models)
                {
                    AnsiConsole.MarkupLine($"  [green]{model}[/]");
                }
            }
            return 0;
        });

        return listCommand;
    }

    /// <summary>
    /// Builds the 'llm download' subcommand for downloading models.
    /// </summary>
    private static Command BuildDownloadSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var downloadCommand = new Command("download", "Download a specific model by ID");

        var modelArgument = new Argument<string>("model")
        {
            Description = "The model ID to download"
        };
        downloadCommand.Arguments.Add(modelArgument);

        var providerOption = new Option<string?>("--provider")
        {
            Description = "Specify the provider (overrides configured provider)"
        };
        downloadCommand.Options.Add(providerOption);
        downloadCommand.Options.Add(jsonOutputOption);

        downloadCommand.SetAction(async parseResult =>
        {
            var jsonOutput = parseResult.GetValue(jsonOutputOption);
            var providerArg = parseResult.GetValue(providerOption);
            var modelId = parseResult.GetValue(modelArgument);

            if (string.IsNullOrWhiteSpace(modelId))
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = "Model ID is required" }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]Error: Model ID is required[/]");
                }
                return 1;
            }

            var (provider, llmProvider) = await ResolveProviderAsync(serviceProvider, providerArg, jsonOutput);
            if (llmProvider == null)
            {
                return 1;
            }

            // Check if provider supports model management
            if (llmProvider is not ISupportsModelManagement modelManagement)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Provider '{provider}' does not support model downloads" }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Provider '{provider}' does not support model downloads.[/]");
                }
                return 1;
            }

            if (jsonOutput)
            {
                // For JSON output, download without visual progress
                var result = await modelManagement.DownloadModelAsync(modelId, cancellationToken: CancellationToken.None);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = result.IsSuccess,
                    provider = provider.ToString(),
                    model = modelId,
                    error = result.IsSuccess ? null : result.Error
                }));
                return result.IsSuccess ? 0 : 1;
            }

            // Show progress bar for interactive download
            Shared.Results.Result? downloadResult = null;
            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var progressTask = ctx.AddTask($"Downloading model '{modelId}'", maxValue: 100);

                    downloadResult = await modelManagement.DownloadModelAsync(
                        modelId,
                        progress => progressTask.Value = progress,
                        CancellationToken.None);

                    progressTask.Value = 100;
                });

            if (downloadResult?.IsSuccess == true)
            {
                AnsiConsole.MarkupLine($"[green] Model '{modelId}' is ready[/]");
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red] Failed to download model: {downloadResult?.Error ?? "Unknown error"}[/]");
                return 1;
            }
        });

        return downloadCommand;
    }

    /// <summary>
    /// Builds the 'llm config' subcommand for configuring LLM settings.
    /// </summary>
    private static Command BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var configCommand = new Command("config", "Configure LLM provider and model");

        var providerOption = new Option<string?>("--provider")
        {
            Description = "LLM provider to use (openai, anthropic, BuiltInLocal, LocalOpenAiCompatible). When specified, skips provider prompt."
        };

        var modelOption = new Option<string?>("--model")
        {
            Description = "LLM model identifier to use (e.g., gpt-4o-mini, claude-3-5-sonnet). When specified, skips model prompt."
        };

        var apiKeyOption = new Option<string?>("--api-key")
        {
            Description = "API key for the selected provider. Required when setting provider to OpenAI/Anthropic and no key exists."
        };

        var maxTokensOption = new Option<int?>("--max-input-tokens")
        {
            Description = "Maximum input tokens for prompts. Defaults to provider-specific value.",
            Arity = ArgumentArity.ZeroOrOne,
            AllowMultipleArgumentsPerToken = false
        };

        var listProvidersOption = new Option<bool>("--list-providers")
        {
            Description = "List all available LLM providers and their default models."
        };

        var listModelsOption = new Option<bool>("--list-models")
        {
            Description = "List models for the specified provider (requires --provider)."
        };

        configCommand.Options.Add(providerOption);
        configCommand.Options.Add(modelOption);
        configCommand.Options.Add(apiKeyOption);
        configCommand.Options.Add(maxTokensOption);
        configCommand.Options.Add(listProvidersOption);
        configCommand.Options.Add(listModelsOption);
        configCommand.Options.Add(jsonOutputOption);

        configCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            bool listProviders = parseResult.GetValue(listProvidersOption);
            bool listModels = parseResult.GetValue(listModelsOption);

            LlmProvider? providerOverride = null;
            var providerValue = parseResult.GetValue(providerOption);
            if (!string.IsNullOrWhiteSpace(providerValue))
            {
                if (!Enum.TryParse<LlmProvider>(providerValue, ignoreCase: true, out var parsedProvider))
                {
                    AnsiConsole.MarkupLine($"[red][/] Invalid provider '{providerValue}'. Valid options: openai, anthropic, BuiltInLocal, LocalOpenAiCompatible.");
                    return 1;
                }

                providerOverride = parsedProvider;
            }

            if (listProviders)
            {
                DisplayProviderList(jsonOutput);
                return 0;
            }

            if (listModels)
            {
                if (!providerOverride.HasValue)
                {
                    AnsiConsole.MarkupLine("[red][/] --provider is required when using --list-models.");
                    return 1;
                }

                return await DisplayModelListAsync(providerOverride.Value, jsonOutput, serviceProvider);
            }

            var configureCommand = new ConfigureLlm.Command
            {
                Force = true,
                ProviderOverride = providerOverride,
                ModelOverride = parseResult.GetValue(modelOption),
                ApiKeyOverride = parseResult.GetValue(apiKeyOption),
                MaxInputTokensOverride = parseResult.GetValue(maxTokensOption)
            };

            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(configureCommand, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    var config = result.Value!;
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = true,
                        provider = config.Provider.ToString(),
                        model = config.Model
                    }));
                }
                // Success message already displayed by ConfigureLlm.Handler
                return 0;
            }
            else
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(new { success = false, error = result.Error }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red][/] {result.Error?.EscapeMarkup() ?? "LLM configuration failed"}");
                }
                return 1;
            }
        });

        return configCommand;
    }

    /// <summary>
    /// Resolves the provider to use based on the argument or configured default.
    /// </summary>
    private static async Task<(LlmProvider provider, ILlmProvider? llmProvider)> ResolveProviderAsync(
        IServiceProvider serviceProvider,
        string? providerArg,
        bool jsonOutput)
    {
        var options = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;

        LlmProvider provider;
        if (!string.IsNullOrEmpty(providerArg))
        {
            if (!Enum.TryParse<LlmProvider>(providerArg, true, out provider))
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Invalid provider: {providerArg}" }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]Error: Invalid provider '{providerArg}'[/]");
                    AnsiConsole.MarkupLine($"[yellow]Valid providers: {string.Join(", ", Enum.GetNames<LlmProvider>())}[/]");
                }
                return (default, null);
            }
        }
        else
        {
            provider = options.Provider;
        }

        // Get the provider instance (only for providers that support model management)
        ILlmProvider? llmProvider = provider switch
        {
            LlmProvider.BuiltInLocal => serviceProvider.GetRequiredService<BuiltInLocalLlmProvider>(),
            LlmProvider.LocalOpenAiCompatible => serviceProvider.GetService<LocalOpenAiCompatibleLlmProvider>(),
            _ => null
        };

        // Check if provider supports model management
        if (llmProvider is not ISupportsModelManagement)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { success = false, error = $"Provider '{provider}' does not support model management" }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Provider '{provider}' does not support model management.[/]");
                AnsiConsole.MarkupLine("[dim]Model management is only available for local providers.[/]");
            }
            return (provider, null);
        }

        return await Task.FromResult((provider, llmProvider));
    }

    /// <summary>
    /// Special handling for LocalOpenAiCompatible - list models from configured server.
    /// </summary>
    private static async Task<int> HandleLocalOpenAiListAsync(
        IServiceProvider serviceProvider,
        LlmProvider provider,
        bool jsonOutput)
    {
        if (provider != LlmProvider.LocalOpenAiCompatible)
        {
            return 1;
        }

        var options = serviceProvider.GetRequiredService<IOptions<LlmOptions>>();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        string? baseUrl = null;
        if (options.Value.Providers.TryGetValue("LocalOpenAiCompatible", out var config) &&
            config.TryGetValue("BaseUrl", out var url))
        {
            baseUrl = url;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Local LLM not configured. Run 'tom llm config' first."
                }));
            }
            else
            {
                AnsiConsole.MarkupLine("[red][/] Local LLM not configured. Run 'tom llm config' first.");
            }
            return 1;
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            baseUrl = baseUrl.TrimEnd('/');
            string modelsUrl = baseUrl.EndsWith("/v1")
                ? $"{baseUrl}/models"
                : $"{baseUrl}/v1/models";

            using var response = await client.GetAsync(modelsUrl);

            if (!response.IsSuccessStatusCode)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Server responded with {response.StatusCode}"
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red][/] Could not fetch models: Server responded with {response.StatusCode}");
                }
                return 1;
            }

            var content = await response.Content.ReadAsStringAsync();
            var models = ParseModelsFromJson(content);

            if (models.Count == 0)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "No models returned from server"
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red][/] No models returned from server.");
                }
                return 1;
            }

            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    provider = provider.ToString(),
                    baseUrl,
                    models
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[green]Available models from {baseUrl}:[/]");
                var table = new Table();
                table.AddColumn("Model ID");

                foreach (var model in models)
                {
                    table.AddRow(model);
                }

                AnsiConsole.Write(table);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Could not connect: {ex.Message}"
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red][/] Could not connect to local server: {ex.Message}");
            }
            return 1;
        }
    }

    private static void DisplayProviderList(bool jsonOutput)
    {
        var providers = Enum.GetValues<LlmProvider>()
            .Select(provider =>
            {
                string defaultModel;
                try
                {
                    defaultModel = ModelRegistry.GetDefault(provider).Id;
                }
                catch (InvalidOperationException)
                {
                    defaultModel = "N/A";
                }

                return new
                {
                    Provider = provider.ToString(),
                    DefaultModel = defaultModel
                };
            })
            .ToList();

        if (jsonOutput)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                success = true,
                providers
            }));
            return;
        }

        var table = new Table();
        table.AddColumn("Provider");
        table.AddColumn("Default Model");

        foreach (var provider in providers)
        {
            table.AddRow(provider.Provider, provider.DefaultModel);
        }

        AnsiConsole.Write(table);
    }

    private static async Task<int> DisplayModelListAsync(LlmProvider provider, bool jsonOutput, IServiceProvider serviceProvider)
    {
        // Special handling for LocalOpenAiCompatible - fetch from configured server
        if (provider == LlmProvider.LocalOpenAiCompatible)
        {
            return await HandleLocalOpenAiListAsync(serviceProvider, provider, jsonOutput);
        }

        // For other providers, use ModelRegistry
        var registryModels = ModelRegistry.GetByProvider(provider);

        if (registryModels.Count == 0)
        {
            if (jsonOutput)
            {
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No models registered for provider {provider}"
                }));
            }
            else
            {
                AnsiConsole.MarkupLine($"[red][/] No models registered for provider {provider}.");
            }
            return 1;
        }

        if (jsonOutput)
        {
            var payload = new
            {
                success = true,
                provider = provider.ToString(),
                models = registryModels.Select(m => new
                {
                    id = m.Id,
                    name = m.DisplayName,
                    tier = m.CostTier,
                    @default = m.IsDefault
                })
            };

            Console.WriteLine(JsonSerializer.Serialize(payload));
            return 0;
        }

        var modelTable = new Table();
        modelTable.AddColumn("Model Id");
        modelTable.AddColumn("Display Name");
        modelTable.AddColumn("Tier");
        modelTable.AddColumn("Default");

        foreach (var model in registryModels)
        {
            modelTable.AddRow(model.Id, model.DisplayName, model.CostTier, model.IsDefault ? "Yes" : "No");
        }

        AnsiConsole.Write(modelTable);
        return 0;
    }

    private static List<string> ParseModelsFromJson(string jsonResponse)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonResponse);
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
