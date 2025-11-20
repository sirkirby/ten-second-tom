using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Options;

namespace TenSecondTom.Tests.Features.Storage;

public sealed class StorageConfigCommandBuilderTests
{
    [Fact]
    public async Task Invoke_ListProviders_WritesProvidersAndReturnsZero()
    {
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        var sectionStore = new Mock<IConfigurationSectionStore>(MockBehavior.Strict);
        var storageOptions = Options.Create(new StorageOptions());
        var providerFactory = new Mock<IStorageProviderFactory>();
        providerFactory.Setup(f => f.GetAvailableProviders())
            .Returns(new[]
            {
                new StorageProviderMetadata("default", "Default File System", "Stores data locally"),
                new StorageProviderMetadata("obsidian", "Obsidian Vault", "Stores data in an Obsidian vault")
            });

        var command = BuildCommand(mediator.Object, sectionStore.Object, storageOptions, providerFactory.Object);

        var exitCode = await InvokeAsync(command, "--list-providers");

        exitCode.Should().Be(0);
        providerFactory.Verify(f => f.GetAvailableProviders(), Times.Once);
        mediator.VerifyNoOtherCalls();
        sectionStore.VerifyNoOtherCalls();
    }

    private static Command BuildCommand(
        IMediator mediator,
        IConfigurationSectionStore sectionStore,
        IOptions<StorageOptions> storageOptions,
        IStorageProviderFactory storageProviderFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(mediator);
        services.AddSingleton(sectionStore);
        services.AddSingleton(storageOptions);
        services.AddSingleton(storageProviderFactory);

        var serviceProvider = services.BuildServiceProvider();
        var builder = new TenSecondTom.Features.Storage.StorageConfigCommandBuilder();
        var jsonOption = new Option<bool>("--json");

        return builder.BuildConfigSubcommand(serviceProvider, jsonOption)!;
    }

    private static Task<int> InvokeAsync(Command command, string args)
    {
        var parseResult = command.Parse(args);
        return parseResult.InvokeAsync();
    }
}

