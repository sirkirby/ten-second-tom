using System.Collections.Generic;
using System.Globalization;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.ThisWeek.Migrations;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Options;
using Xunit;

namespace TenSecondTom.Tests.Features.ThisWeek;

public sealed class ThisWeekMigrationTests
{
    [Fact]
    public async Task MigrateAsync_MovesLegacyWeeklyFilesToNoteDirectory()
    {
        // Arrange
        const string storageRoot = "/storage";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), storageRoot);
        string legacyDirectory = fileSystem.Path.Combine(storageRoot, DirectoryNames.ThisWeek);
        fileSystem.Directory.CreateDirectory(legacyDirectory);

        string legacyFileName = "2025-40-Thu-1.md";
        string legacyFilePath = fileSystem.Path.Combine(legacyDirectory, legacyFileName);
        fileSystem.File.WriteAllText(legacyFilePath, "Weekly review content");

        using var services = BuildServiceProvider(fileSystem, storageRoot);
        var migration = new ThisWeekMigration();

        // Act
        bool migrated = await migration.MigrateAsync(services, CancellationToken.None);

        // Assert
        migrated.Should().BeTrue();

        var start = ISOWeek.ToDateTime(2025, 40, DayOfWeek.Monday);
        var end = start.AddDays(6);
        string expectedFileName = $"{start:MM-dd-yyyy}_{end:MM-dd-yyyy}_1_generated.md";
        string noteDirectory = fileSystem.Path.Combine(storageRoot, DirectoryNames.Note);
        string destinationFile = fileSystem.Path.Combine(noteDirectory, expectedFileName);

        fileSystem.File.Exists(destinationFile).Should().BeTrue();
        fileSystem.File.ReadAllText(destinationFile).Should().Contain("Weekly review content");
        fileSystem.Directory.Exists(legacyDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task MigrateAsync_WithInvalidLegacyFiles_ReturnsFalseAndLeavesDirectory()
    {
        // Arrange
        const string storageRoot = "/storage";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), storageRoot);
        string legacyDirectory = fileSystem.Path.Combine(storageRoot, DirectoryNames.ThisWeek);
        fileSystem.Directory.CreateDirectory(legacyDirectory);

        string invalidFilePath = fileSystem.Path.Combine(legacyDirectory, "invalid-file.md");
        fileSystem.File.WriteAllText(invalidFilePath, "orphan content");

        using var services = BuildServiceProvider(fileSystem, storageRoot);
        var migration = new ThisWeekMigration();

        // Act
        bool migrated = await migration.MigrateAsync(services, CancellationToken.None);

        // Assert
        migrated.Should().BeFalse();
        fileSystem.File.Exists(invalidFilePath).Should().BeTrue();
        fileSystem.Directory.Exists(legacyDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task MigrateAsync_WhenLegacyDirectoryMissing_ReturnsFalse()
    {
        // Arrange
        const string storageRoot = "/storage";
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), storageRoot);
        using var services = BuildServiceProvider(fileSystem, storageRoot);
        var migration = new ThisWeekMigration();

        // Act
        bool migrated = await migration.MigrateAsync(services, CancellationToken.None);

        // Assert
        migrated.Should().BeFalse();
    }

    private static ServiceProvider BuildServiceProvider(IFileSystem fileSystem, string storageRoot)
    {
        var services = new ServiceCollection();
        services.AddSingleton(fileSystem);
        services.AddSingleton<IOptions<StorageOptions>>(Options.Create(new StorageOptions
        {
            RootDirectory = storageRoot
        }));
        services.AddSingleton<ILogger<ThisWeekMigration>>(NullLogger<ThisWeekMigration>.Instance);
        return services.BuildServiceProvider();
    }
}
