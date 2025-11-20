using System;
using System;
using System.CommandLine;
using System.Linq;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spectre.Console;
using TenSecondTom.Infrastructure.Cli;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Features.Storage;

/// <summary>
/// Builds the 'config storage' subcommand for the Storage feature slice.
/// Keeps storage configuration CLI knowledge co-located with the storage feature.
/// Auto-discovered via assembly scanning of IConfigSubcommandBuilder implementations.
/// </summary>
public sealed class StorageConfigCommandBuilder : IConfigSubcommandBuilder
{
    /// <summary>
    /// Builds the 'config storage' subcommand.
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency injection.</param>
    /// <param name="jsonOutputOption">Global JSON output option to add to the command.</param>
    /// <returns>The configured 'storage' subcommand.</returns>
    public Command? BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var storageCommand = new Command("storage", "Configure storage provider and paths");

        var rootOption = new Option<string?>("--root-directory")
        {
            Description = "Overrides the Ten Second Tom root directory (where config and templates live)."
        };

        var providerOption = new Option<string?>("--provider")
        {
            Description = "Storage provider ID (default or obsidian)."
        };

        var providerPathOption = new Option<string?>("--provider-path")
        {
            Description = "Provider-specific path (e.g., Obsidian vault path). Required for obsidian provider."
        };

        var subdirectoryOption = new Option<string?>("--subdirectory")
        {
            Description = "Optional memory subdirectory under the provider path."
        };

        var listProvidersOption = new Option<bool>("--list-providers")
        {
            Description = "List available storage providers and their descriptions."
        };

        storageCommand.Options.Add(rootOption);
        storageCommand.Options.Add(providerOption);
        storageCommand.Options.Add(providerPathOption);
        storageCommand.Options.Add(subdirectoryOption);
        storageCommand.Options.Add(listProvidersOption);
        storageCommand.Options.Add(jsonOutputOption);

        storageCommand.SetAction(async (parseResult) =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            bool listProviders = parseResult.GetValue(listProvidersOption);
            var rootOverride = parseResult.GetValue(rootOption);
            var providerOverride = parseResult.GetValue(providerOption);
            var providerPathOverride = parseResult.GetValue(providerPathOption);
            var subdirectoryOverride = parseResult.GetValue(subdirectoryOption);

            var storageProviderFactory = serviceProvider.GetRequiredService<IStorageProviderFactory>();

            if (listProviders)
            {
                DisplayStorageProviders(storageProviderFactory.GetAvailableProviders(), jsonOutput);
                return 0;
            }

            var mediator = serviceProvider.GetRequiredService<IMediator>();
            var sectionStore = serviceProvider.GetRequiredService<IConfigurationSectionStore>();
            var storageOptions = serviceProvider.GetService<IOptions<StorageOptions>>()?.Value;

            var storageConfigResult = await sectionStore.ReadSectionAsync<StorageSettings>(
                StorageOptions.SectionName,
                CancellationToken.None).ConfigureAwait(false);

            var existingStorage = storageConfigResult.IsSuccess
                ? storageConfigResult.Value
                : new StorageSettings();

            var existingRoot = storageOptions?.RootDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    DirectoryNames.ApplicationRoot);

            var configureStorageCommand = new ConfigureStorage.Command
            {
                Force = true,
                ExistingRootDirectory = existingRoot,
                ExistingStorage = existingStorage,
                RootDirectoryOverride = rootOverride,
                ProviderIdOverride = providerOverride,
                ProviderPathOverride = providerPathOverride,
                MemorySubdirectoryOverride = subdirectoryOverride
            };

            var result = await mediator.Send(configureStorageCommand, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                if (jsonOutput)
                {
                    var config = result.Value!;
                    AnsiConsole.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        success = true,
                        rootDirectory = config.RootDirectory,
                        providerId = config.Storage.ProviderId,
                        providerPath = config.Storage.ProviderPath
                    }));
                }
                // Success message already displayed by ConfigureStorage.Handler
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
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error?.EscapeMarkup() ?? "Storage configuration failed"}");
                }
                return 1;
            }
        });

        return storageCommand;

        static void DisplayStorageProviders(
            IReadOnlyCollection<StorageProviderMetadata> providers,
            bool jsonOutput)
        {
            if (providers.Count == 0)
            {
                if (jsonOutput)
                {
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "No storage providers are registered."
                    }));
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]✗[/] No storage providers are registered.");
                }

                return;
            }

            if (jsonOutput)
            {
                AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                {
                    success = true,
                    providers = providers.Select(p => new
                    {
                        id = p.ProviderId,
                        name = p.DisplayName,
                        description = p.Description
                    })
                }));
                return;
            }

            var table = new Table();
            table.AddColumn("Provider Id");
            table.AddColumn("Name");
            table.AddColumn("Description");

            foreach (var provider in providers)
            {
                table.AddRow(provider.ProviderId, provider.DisplayName, provider.Description);
            }

            AnsiConsole.Write(table);
        }
    }
}
