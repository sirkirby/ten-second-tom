using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Configuration;
using TenSecondTom.Infrastructure.Notifications.Migrations;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Options;
using TenSecondTom.Shared.Results;
using Xunit;

namespace TenSecondTom.Tests.Infrastructure.Notifications.Migrations;

public sealed class NotificationOptionsMigrationTests
{
    [Fact]
    public async Task MigrateAsync_WhenSectionMissing_WritesDefaults()
    {
        // Arrange
        using var emptyConfig = JsonDocument.Parse("{}\n");
        var loggerMock = new Mock<ILogger<NotificationOptionsMigration>>();
        var storeMock = new Mock<IConfigurationSectionStore>();
        storeMock.Setup(s => s.ReadFullConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JsonDocument>.Success(emptyConfig));

        NotificationOptions? persistedOptions = null;
        storeMock.Setup(s => s.WriteSectionAsync(
                NotificationOptions.SectionPath,
                It.IsAny<NotificationOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, NotificationOptions, CancellationToken>((_, options, _) => persistedOptions = options)
            .ReturnsAsync(Result<string>.Success("config.json"));

        var migration = new NotificationOptionsMigration();
        using var provider = BuildServiceProvider(loggerMock, storeMock);

        // Act
        var migrated = await migration.MigrateAsync(provider, CancellationToken.None);

        // Assert
        migrated.Should().BeTrue();
        storeMock.Verify(s => s.WriteSectionAsync(NotificationOptions.SectionPath, It.IsAny<NotificationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        persistedOptions.Should().NotBeNull();
        persistedOptions!.Enabled.Should().BeTrue();
        persistedOptions.DefaultTimeoutSeconds.Should().Be(30);
        persistedOptions.DefaultPriority.Should().Be(NotificationPriority.Normal);
        persistedOptions.SilentFallback.Should().BeTrue();
        persistedOptions.ExtensionDirectory.Should().BeNull();
    }

    [Fact]
    public async Task MigrateAsync_WhenSectionExists_SkipsWrite()
    {
        // Arrange
        using var config = JsonDocument.Parse("""
        {
          "TenSecondTom": {
            "Notifications": {
              "Enabled": false
            }
          }
        }
        """);

        var loggerMock = new Mock<ILogger<NotificationOptionsMigration>>();
        var storeMock = new Mock<IConfigurationSectionStore>();
        storeMock.Setup(s => s.ReadFullConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JsonDocument>.Success(config));

        var migration = new NotificationOptionsMigration();
        using var provider = BuildServiceProvider(loggerMock, storeMock);

        // Act
        var migrated = await migration.MigrateAsync(provider, CancellationToken.None);

        // Assert
        migrated.Should().BeFalse();
        storeMock.Verify(s => s.WriteSectionAsync(NotificationOptions.SectionPath, It.IsAny<NotificationOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MigrateAsync_WhenInspectionFails_AttemptsToWriteDefaults()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<NotificationOptionsMigration>>();
        var storeMock = new Mock<IConfigurationSectionStore>();
        storeMock.Setup(s => s.ReadFullConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<JsonDocument>.Failure("invalid json"));

        storeMock.Setup(s => s.WriteSectionAsync(
                NotificationOptions.SectionPath,
                It.IsAny<NotificationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success("config.json"));

        var migration = new NotificationOptionsMigration();
        using var provider = BuildServiceProvider(loggerMock, storeMock);

        // Act
        var migrated = await migration.MigrateAsync(provider, CancellationToken.None);

        // Assert
        migrated.Should().BeTrue();
        storeMock.Verify(s => s.WriteSectionAsync(NotificationOptions.SectionPath, It.IsAny<NotificationOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ServiceProvider BuildServiceProvider(
        Mock<ILogger<NotificationOptionsMigration>> loggerMock,
        Mock<IConfigurationSectionStore> storeMock)
    {
        var services = new ServiceCollection();
        services.AddSingleton(loggerMock.Object);
        services.AddSingleton(storeMock.Object);
        return services.BuildServiceProvider();
    }
}
