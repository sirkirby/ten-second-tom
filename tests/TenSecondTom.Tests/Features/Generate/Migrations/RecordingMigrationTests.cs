using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TenSecondTom.Features.Generate.Migrations;
using TenSecondTom.Shared.Options;
using Xunit;

namespace TenSecondTom.Tests.Features.Generate.Migrations;

public sealed class RecordingMigrationTests
{
    [Fact]
    public async Task MigrateAsync_WithExistingRecordingId_DoesNotDuplicateField()
    {
        // Arrange
        const string storageRoot = "/storage";
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), storageRoot);
        var recordingDirectory = mockFileSystem.Path.Combine(storageRoot, "recording");
        mockFileSystem.Directory.CreateDirectory(recordingDirectory);

        var legacyFilePath = mockFileSystem.Path.Combine(recordingDirectory, "10-31-2025_1.txt");
        var legacyContent = """
            ---
            recording-id: a2425e7f-da78-4c74-8e44-841301cf05ae
            date: 2025-11-22 15:00:30
            duration: 11.68
            ---

            Example recording content.
            """;
        mockFileSystem.File.WriteAllText(legacyFilePath, legacyContent);

        var migration = new RecordingMigration(mockFileSystem, NullLogger<RecordingMigration>.Instance);
        using var serviceProvider = BuildServiceProvider(mockFileSystem, storageRoot);

        // Act
        var migrationSucceeded = await migration.MigrateAsync(serviceProvider, CancellationToken.None);

        // Assert
        migrationSucceeded.Should().BeTrue();

        var migratedFilePath = mockFileSystem.Path.ChangeExtension(legacyFilePath, ".md");
        mockFileSystem.File.Exists(migratedFilePath).Should().BeTrue();

        var migratedContent = mockFileSystem.File.ReadAllText(migratedFilePath);
        Regex.Matches(migratedContent, "recording-id:", RegexOptions.IgnoreCase).Count.Should().Be(1);
    }

    [Fact]
    public async Task MigrateAsync_WithoutRecordingId_InsertsFrontMatterField()
    {
        // Arrange
        const string storageRoot = "/storage";
        var mockFileSystem = new MockFileSystem(new Dictionary<string, MockFileData>(), storageRoot);
        var recordingDirectory = mockFileSystem.Path.Combine(storageRoot, "recording");
        mockFileSystem.Directory.CreateDirectory(recordingDirectory);

        var legacyFilePath = mockFileSystem.Path.Combine(recordingDirectory, "11-01-2025_3.txt");
        var legacyContent = """
            ---
            date: 2025-11-22 15:00:30
            duration: 11.68
            ---

            Example recording content.
            """;
        mockFileSystem.File.WriteAllText(legacyFilePath, legacyContent);

        var migration = new RecordingMigration(mockFileSystem, NullLogger<RecordingMigration>.Instance);
        using var serviceProvider = BuildServiceProvider(mockFileSystem, storageRoot);

        // Act
        var migrationSucceeded = await migration.MigrateAsync(serviceProvider, CancellationToken.None);

        // Assert
        migrationSucceeded.Should().BeTrue();

        var migratedFilePath = mockFileSystem.Path.ChangeExtension(legacyFilePath, ".md");
        mockFileSystem.File.Exists(migratedFilePath).Should().BeTrue();

        var migratedContent = mockFileSystem.File.ReadAllText(migratedFilePath);
        Regex.Matches(migratedContent, @"recording-id:\s", RegexOptions.IgnoreCase).Count.Should().Be(1);
        Regex.IsMatch(migratedContent, @"^---\r?\nrecording-id:\s+[a-z0-9-]+", RegexOptions.IgnoreCase)
            .Should().BeTrue();
    }

    private static ServiceProvider BuildServiceProvider(IFileSystem fileSystem, string storageRoot)
    {
        var services = new ServiceCollection();
        services.AddSingleton(fileSystem);
        services.AddSingleton<IOptions<StorageOptions>>(Options.Create(new StorageOptions
        {
            RootDirectory = storageRoot
        }));
        services.AddSingleton<ILogger<RecordingMigration>>(NullLogger<RecordingMigration>.Instance);
        return services.BuildServiceProvider();
    }
}
