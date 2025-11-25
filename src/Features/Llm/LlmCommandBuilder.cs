using System.CommandLine;
using System.CommandLine.Invocation;
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
/// </summary>
public sealed class LlmCommandBuilder : ICommandBuilder
{
    public int Priority => 75; // Management command

    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var command = new Command("llm", "Manage LLM models and configuration");
        command.Options.Add(jsonOutputOption);

        // Add --list-models option
        var listModelsOption = new Option<bool>("--list-models")
        {
            Description = "List available models for the configured or specified provider"
        };
        command.Options.Add(listModelsOption);

        // Add --download-model option with argument
        var downloadModelOption = new Option<string?>("--download-model")
        {
            Description = "Download a specific model by ID"
        };
        command.Options.Add(downloadModelOption);

        // Add --provider option
        var providerOption = new Option<string?>("--provider")
        {
            Description = "Specify the provider (overrides configured provider)"
        };
        command.Options.Add(providerOption);

        command.SetAction(async parseResult =>
        {
            var jsonOutput = parseResult.GetValue(jsonOutputOption);
            var listModels = parseResult.GetValue(listModelsOption);
            var downloadModel = parseResult.GetValue(downloadModelOption);
            var providerArg = parseResult.GetValue(providerOption);

            var options = serviceProvider.GetRequiredService<IOptions<LlmOptions>>().Value;

            // Determine which provider to use
            LlmProvider provider;
            if (!string.IsNullOrEmpty(providerArg))
            {
                if (!Enum.TryParse<LlmProvider>(providerArg, true, out provider))
                {
                    if (jsonOutput)
                    {
                        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = $"Invalid provider: {providerArg}" }));
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[red]Error: Invalid provider '{providerArg}'[/]");
                        AnsiConsole.MarkupLine($"[yellow]Valid providers: {string.Join(", ", Enum.GetNames<LlmProvider>())}[/]");
                    }
                    return 1;
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
            if (llmProvider is not ISupportsModelManagement modelManagement)
            {
                if (jsonOutput)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = $"Provider '{provider}' does not support model management" }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]Provider '{provider}' does not support model management.[/]");
                    AnsiConsole.MarkupLine("[dim]Model management is only available for local providers.[/]");
                }
                return 1;
            }

            // Handle --list-models
            if (listModels)
            {
                var models = await modelManagement.ListModelsAsync(CancellationToken.None);

                if (jsonOutput)
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = true, provider = provider.ToString(), models }));
                }
                else
                {
                    AnsiConsole.MarkupLine($"[cyan]Available models for {provider}:[/]");
                    foreach (var model in models)
                    {
                        AnsiConsole.MarkupLine($"  • [green]{model}[/]");
                    }
                }
                return 0;
            }

            // Handle --download-model
            if (!string.IsNullOrEmpty(downloadModel))
            {
                if (jsonOutput)
                {
                    // For JSON output, download without visual progress
                    var result = await modelManagement.DownloadModelAsync(downloadModel, cancellationToken: CancellationToken.None);
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = result.IsSuccess,
                        provider = provider.ToString(),
                        model = downloadModel,
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
                        var progressTask = ctx.AddTask($"Downloading model '{downloadModel}'", maxValue: 100);

                        downloadResult = await modelManagement.DownloadModelAsync(
                            downloadModel,
                            progress => progressTask.Value = progress,
                            CancellationToken.None);

                        progressTask.Value = 100;
                    });

                if (downloadResult?.IsSuccess == true)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Model '{downloadModel}' is ready[/]");
                    return 0;
                }
                else
                {
                    AnsiConsole.MarkupLine($"[red]✗ Failed to download model: {downloadResult?.Error ?? "Unknown error"}[/]");
                    return 1;
                }
            }

            // No action specified
            if (jsonOutput)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { success = false, error = "No action specified. Use --list-models or --download-model." }));
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No action specified.[/]");
                AnsiConsole.MarkupLine("[dim]Use --list-models to see available models or --download-model <model-id> to download a model.[/]");
            }
            return 1;
        });

        return command;
    }
}
