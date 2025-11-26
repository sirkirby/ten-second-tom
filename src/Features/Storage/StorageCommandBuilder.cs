using System.CommandLine;
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
/// Builds the top-level <c>storage</c> CLI command with subcommands:
/// <list type="bullet">
///   <item><c>storage config</c> - Configure storage provider and paths</item>
///   <item><c>storage list-providers</c> - List available storage providers</item>
/// </list>
/// </summary>
public sealed class StorageCommandBuilder : ICommandBuilder
{
    /// <inheritdoc />
    public int Priority => 85; // Management commands range

    /// <inheritdoc />
    public Command? BuildCommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(jsonOutputOption);

        var storageCommand = new Command("storage", "Storage management commands");
        storageCommand.Options.Add(jsonOutputOption);

        // Add subcommands
        storageCommand.Subcommands.Add(BuildConfigSubcommand(serviceProvider, jsonOutputOption));
        storageCommand.Subcommands.Add(BuildListProvidersSubcommand(serviceProvider, jsonOutputOption));

        return storageCommand;
    }

    private static Command BuildConfigSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var configCommand = new Command("config", "Configure storage provider and paths");

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

        configCommand.Options.Add(rootOption);
        configCommand.Options.Add(providerOption);
        configCommand.Options.Add(providerPathOption);
        configCommand.Options.Add(subdirectoryOption);
        configCommand.Options.Add(jsonOutputOption);

        configCommand.SetAction(async parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var rootOverride = parseResult.GetValue(rootOption);
            var providerOverride = parseResult.GetValue(providerOption);
            var providerPathOverride = parseResult.GetValue(providerPathOption);
            var subdirectoryOverride = parseResult.GetValue(subdirectoryOption);

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
                    AnsiConsole.WriteLine(JsonSerializer.Serialize(new
                    {
                        success = true,
                        rootDirectory = config.RootDirectory,
                        providerId = config.Storage.ProviderId,
                        providerPath = config.Storage.ProviderPath
                    }));
                }
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
                    AnsiConsole.MarkupLine($"[red]✗[/] {result.Error?.EscapeMarkup() ?? "Storage configuration failed"}");
                }
                return 1;
            }
        });

        return configCommand;
    }

    private static Command BuildListProvidersSubcommand(IServiceProvider serviceProvider, Option<bool> jsonOutputOption)
    {
        var listCommand = new Command("list-providers", "List available storage providers");
        listCommand.Options.Add(jsonOutputOption);

        listCommand.SetAction(parseResult =>
        {
            bool jsonOutput = parseResult.GetValue(jsonOutputOption);
            var storageProviderFactory = serviceProvider.GetRequiredService<IStorageProviderFactory>();
            var providers = storageProviderFactory.GetAvailableProviders();

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
                    AnsiConsole.MarkupLine("[red]Error:[/] No storage providers are registered.");
                }
                return Task.FromResult(1);
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
            }
            else
            {
                AnsiConsole.MarkupLine("[cyan]Available Storage Providers:[/]");
                var table = new Table()
                    .AddColumn("Provider Id")
                    .AddColumn("Name")
                    .AddColumn("Description");

                foreach (var provider in providers)
                {
                    table.AddRow(
                        provider.ProviderId.EscapeMarkup(),
                        provider.DisplayName.EscapeMarkup(),
                        provider.Description.EscapeMarkup());
                }

                AnsiConsole.Write(table);
            }

            return Task.FromResult(0);
        });

        return listCommand;
    }
}
