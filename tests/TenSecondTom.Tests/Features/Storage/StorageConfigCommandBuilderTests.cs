using System.CommandLine;
using System.CommandLine.Parsing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using TenSecondTom.Shared.Options;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.Tests.Features.Storage;

public sealed class StorageConfigCommandBuilderTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;

    public StorageConfigCommandBuilderTests()
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

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        services.AddSingleton(sectionStore.Object);
        services.AddSingleton(storageOptions);
        services.AddSingleton(providerFactory.Object);

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Invoke_ListProviders_WritesProvidersAndReturnsZero()
    {
        // Arrange
        var builder = new TenSecondTom.Features.Storage.StorageConfigCommandBuilder();
        var jsonOption = new Option<bool>("--json");
        var command = builder.BuildConfigSubcommand(_serviceProvider, jsonOption)!;

        // Act
        var exitCode = await InvokeAsync(command, "--list-providers");

        // Assert
        exitCode.Should().Be(0);
        var providerFactory = _serviceProvider.GetRequiredService<IStorageProviderFactory>();
        var mock = Mock.Get(providerFactory);
        mock.Verify(f => f.GetAvailableProviders(), Times.Once);
    }

    private static Task<int> InvokeAsync(Command command, string args)
    {
        var parseResult = command.Parse(args);
        return parseResult.InvokeAsync();
    }

    public void Dispose()
    {
        _serviceProvider?.Dispose();
    }
}

