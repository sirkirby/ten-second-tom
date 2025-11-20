using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Features.Storage;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Abstractions.UI;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Features.Storage;

public sealed class ConfigureStorageHandlerTests
{
    [Fact]
    public async Task Handle_WithOverrides_UpdatesStorageConfiguration()
    {
        var sectionStore = new Mock<IConfigurationSectionStore>();
        sectionStore
            .Setup(s => s.ReadSectionAsync<StorageSettings>("TenSecondTom:Storage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StorageSettings>.Success(new StorageSettings
            {
                ProviderId = StorageProviderIds.Default
            }));

        Dictionary<string, object>? persistedSections = null;

        sectionStore
            .Setup(s => s.WriteMultipleSectionsAsync(It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<Dictionary<string, object>, CancellationToken>((dict, _) => persistedSections = dict)
            .ReturnsAsync(Result<string>.Success("config.json"));

        var wizard = new Mock<ISetupWizardUI>();
        wizard.Setup(w => w.ShowSuccess(It.IsAny<string>()));
        wizard.Setup(w => w.ShowStatus(It.IsAny<string>()));

        var providerFactory = new Mock<IStorageProviderFactory>();
        providerFactory
            .Setup(f => f.GetAvailableProviders())
            .Returns(new[]
            {
                new StorageProviderMetadata(StorageProviderIds.Default, "Default", "Default provider"),
                new StorageProviderMetadata(StorageProviderIds.Obsidian, "Obsidian", "Obsidian vault")
            });

        var handler = new ConfigureStorage.Handler(
            sectionStore.Object,
            wizard.Object,
            providerFactory.Object,
            Mock.Of<ILogger<ConfigureStorage.Handler>>());

        var command = new ConfigureStorage.Command
        {
            Force = true,
            ExistingRootDirectory = "/Users/test/ten-second-tom",
            ExistingStorage = new StorageSettings { ProviderId = StorageProviderIds.Default },
            ProviderIdOverride = StorageProviderIds.Obsidian,
            ProviderPathOverride = "/Users/test/vault",
            MemorySubdirectoryOverride = "memory"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Storage.ProviderId.Should().Be(StorageProviderIds.Obsidian);
        result.Value!.Storage.ProviderPath.Should().Be("/Users/test/vault");

        persistedSections.Should().NotBeNull();
        persistedSections!["TenSecondTom:RootDirectory"].Should().Be("/Users/test/ten-second-tom");
        var storage = persistedSections["TenSecondTom:Storage"].Should().BeOfType<StorageSettings>().Subject;
        storage.ProviderId.Should().Be(StorageProviderIds.Obsidian);
        storage.ProviderPath.Should().Be("/Users/test/vault");
        storage.MemorySubdirectory.Should().Be("memory");

        wizard.Verify(w => w.ShowSuccess(It.IsAny<string>()), Times.Once);
    }
}

