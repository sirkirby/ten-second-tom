using System;
using System.CommandLine;
using System.Linq;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Features.Llm;

/// <summary>
/// Builds the 'config llm' subcommand owned by the LLM feature so other features
/// don't need to reference ConfigureLlm directly.
/// Auto-discovered via assembly scanning of IConfigSubcommandBuilder implementations.
/// </summary>
public sealed class LlmConfigCommandBuilder : IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'config llm' subcommand.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured 'llm' subcommand.</returns>
    public Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var llmCommand = new Command("llm", "Configure LLM provider and model");

        var providerOption = new Option<string?>("--provider")
        {
            Description = "LLM provider to use (openai or anthropic). When specified, skips provider prompt."
        };

        var modelOption = new Option<string?>("--model")
        {
            Description = "LLM model identifier to use (e.g., gpt-4o-mini, claude-3-5-sonnet). When specified, skips model prompt."
        };

        var apiKeyOption = new Option<string?>("--api-key")
        {
            Description = "API key for the selected provider. Required when setting provider to OpenAI and no key exists."
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

        llmCommand.Options.Add(providerOption);
        llmCommand.Options.Add(modelOption);
        llmCommand.Options.Add(apiKeyOption);
        llmCommand.Options.Add(maxTokensOption);
        llmCommand.Options.Add(listProvidersOption);
        llmCommand.Options.Add(listModelsOption);
        llmCommand.Options.Add(jsonOutputOption);

        llmCommand.SetAction(async (parseResult) =>
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
                    AnsiConsole.MarkupLine($"[red]✗[/] Invalid provider '{providerValue}'. Valid options: openai, anthropic.");
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
                    AnsiConsole.MarkupLine("[red]✗[/] --provider is required when using --list-models.");
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
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error?.EscapeMarkup() ?? "LLM configuration failed"}");
                }
                return 1;
            }
        });

        return llmCommand;

        static void DisplayProviderList(bool jsonOutput)
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
                AnsiConsole.WriteLine(JsonSerializer.Serialize(new
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

        static async Task<int> DisplayModelListAsync(LlmProvider provider, bool jsonOutput, IServiceProvider serviceProvider)
        {
            // Special handling for LocalOpenAiCompatible - fetch from configured server
            if (provider == LlmProvider.LocalOpenAiCompatible)
            {
                var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TenSecondTom.Shared.Options.LlmOptions>>();
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
                        AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = "Local LLM not configured. Run 'tom config llm' first."
                        }));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[red]✗[/] Local LLM not configured. Run 'tom config llm' first.");
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

                    var response = await client.GetAsync(modelsUrl);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        if (jsonOutput)
                        {
                            AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                            {
                                success = false,
                                error = $"Server responded with {response.StatusCode}"
                            }));
                        }
                        else
                        {
                            AnsiConsole.MarkupLine($"[red]✗[/] Could not fetch models: Server responded with {response.StatusCode}");
                        }
                        return 1;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var models = ParseModelsFromJson(content);

                    if (models.Count == 0)
                    {
                        if (jsonOutput)
                        {
                            AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                            {
                                success = false,
                                error = "No models returned from server"
                            }));
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("[red]✗[/] No models returned from server.");
                        }
                        return 1;
                    }

                    if (jsonOutput)
                    {
                        AnsiConsole.WriteLine(JsonSerializer.Serialize(new
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
                        AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                        {
                            success = false,
                            error = $"Could not connect: {ex.Message}"
                        }));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]✗[/] Could not connect to local server: {ex.Message}");
                    }
                    return 1;
                }
            }

            // For other providers, use ModelRegistry
            var registryModels = ModelRegistry.GetByProvider(provider);

            if (registryModels.Count == 0)
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"No models registered for provider {provider}"
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] No models registered for provider {provider}.");
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

                AnsiConsole.WriteLine(JsonSerializer.Serialize(payload));
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

        static List<string> ParseModelsFromJson(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var models = new List<string>();

                // Try OpenAI format: { "data": [ { "id": "model-name" } ] }
                if (doc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    foreach (var item in dataArray.EnumerateArray())
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
                    foreach (var item in modelsArray.EnumerateArray())
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
}

