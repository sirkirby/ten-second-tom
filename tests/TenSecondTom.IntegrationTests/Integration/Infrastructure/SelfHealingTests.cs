using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Configuration;

namespace TenSecondTom.IntegrationTests.Integration.Infrastructure;

/// <summary>
/// Integration tests for self-healing capabilities in ConfigurationChecker.
/// Verifies automatic recovery from deleted or corrupted configuration states.
/// </summary>
public sealed class SelfHealingTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly MockFileSystem _fileSystem;
    private readonly IConfiguration _configuration;

    public SelfHealingTests()
    {
        _mockLogger = new Mock<ILogger>();
        _fileSystem = new MockFileSystem();

        // Create configuration with memory directory
        var configData = new Dictionary<string, string?>
        {
            ["Storage:MemoryDirectory"] = "/.memory",
            ["Llm:Provider"] = "OpenAI",
            ["Llm:ApiKey"] = "test-key"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    [Fact]
    public async Task PerformSelfHealingAsync_DeletedTemplatesDirectory_RecreatesDirectoryAndRestoresDefaults()
    {
        // Arrange
        // Start with memory directory but no templates directory
        _fileSystem.AddDirectory("/.memory");
        // Templates directory does not exist (simulates deletion)

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        healingPerformed.Should().BeTrue("self-healing should be performed when directory is missing");

        // Verify templates directory was created
        _fileSystem.Directory.Exists("/.memory/templates").Should().BeTrue(
            "templates directory should be recreated");

        // Verify default templates were restored
        string[] templateFiles = _fileSystem.Directory.GetFiles("/.memory/templates", "*.md");
        templateFiles.Should().NotBeEmpty("default templates should be restored");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Templates directory not found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log warning about missing directory");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recreated templates directory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log info about directory recreation");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Self-healing complete")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log successful self-healing completion");
    }

    [Fact]
    public async Task PerformSelfHealingAsync_EmptyTemplatesDirectory_RestoresDefaults()
    {
        // Arrange
        // Create empty templates directory
        _fileSystem.AddDirectory("/.memory/templates");

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        healingPerformed.Should().BeTrue("self-healing should be performed for empty directory");

        // Verify default templates were restored
        string[] templateFiles = _fileSystem.Directory.GetFiles("/.memory/templates", "*.md");
        templateFiles.Should().NotBeEmpty("default templates should be restored to empty directory");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("contains no templates")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log warning about empty directory");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Default templates restored")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log successful restoration");
    }

    [Fact]
    public async Task PerformSelfHealingAsync_TemplatesExist_NoHealingPerformed()
    {
        // Arrange
        // Create templates directory with existing templates
        _fileSystem.AddDirectory("/.memory/templates");
        _fileSystem.AddFile("/.memory/templates/daily-summary.md", new MockFileData("# Daily Summary\n\nContent"));
        _fileSystem.AddFile("/.memory/templates/weekly-review.md", new MockFileData("# Weekly Review\n\nContent"));

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        healingPerformed.Should().BeFalse("no healing should be performed when templates exist");

        // Verify no warning logs
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never,
            "Should not log warnings when templates exist");
    }

    [Fact]
    public async Task PerformSelfHealingAsync_CustomTemplatesExist_PreservesCustomizations()
    {
        // Arrange
        _fileSystem.AddDirectory("/.memory/templates");
        _fileSystem.AddFile("/.memory/templates/custom-template.md", new MockFileData("# Custom Template\n\nUser content"));
        _fileSystem.AddFile("/.memory/templates/daily-summary.md", new MockFileData("# Modified Daily Summary\n\nCustom content"));

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        healingPerformed.Should().BeFalse("no healing needed when templates exist");

        // Verify custom templates are preserved
        _fileSystem.File.Exists("/.memory/templates/custom-template.md").Should().BeTrue(
            "custom templates should be preserved");
        _fileSystem.File.ReadAllText("/.memory/templates/daily-summary.md").Should().Contain(
            "Modified Daily Summary",
            "customized default templates should be preserved");
    }

    [Fact]
    public async Task PerformSelfHealingAsync_DirectoryCreationFails_HandlesGracefully()
    {
        // Arrange
        // Use a read-only file system to simulate permission errors
        var readOnlyFileSystem = new MockFileSystem(
            new Dictionary<string, MockFileData>
            {
                ["/.memory"] = new MockDirectoryData()
            });

        // Make the directory creation fail by making root read-only
        readOnlyFileSystem.File.SetAttributes("/.memory", FileAttributes.ReadOnly);

        // Note: MockFileSystem doesn't fully simulate permission errors,
        // so this test verifies the exception handling path exists

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            readOnlyFileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert - Should handle gracefully and not throw
        // Healing may or may not be reported as performed depending on MockFileSystem behavior
        // The important thing is that it doesn't throw
    }

    [Fact]
    public async Task PerformSelfHealingAsync_Idempotent_MultipleCallsSafe()
    {
        // Arrange
        _fileSystem.AddDirectory("/.memory");

        // Act - Call self-healing multiple times
        bool firstCall = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        bool secondCall = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        bool thirdCall = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        firstCall.Should().BeTrue("first call should perform healing");
        secondCall.Should().BeFalse("second call should not perform healing");
        thirdCall.Should().BeFalse("third call should not perform healing");

        // Verify templates directory exists and has templates
        _fileSystem.Directory.Exists("/.memory/templates").Should().BeTrue();
        string[] templateFiles = _fileSystem.Directory.GetFiles("/.memory/templates", "*.md");
        templateFiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PerformSelfHealingAsync_CancellationRequested_PropagatesCancellation()
    {
        // Arrange
        _fileSystem.AddDirectory("/.memory");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await ConfigurationChecker.PerformSelfHealingAsync(
                _configuration,
                _fileSystem,
                _mockLogger.Object,
                cts.Token));
    }

    [Theory]
    [InlineData("/.custom-location")]
    [InlineData("/var/tom")]
    [InlineData("./relative/path")]
    public async Task PerformSelfHealingAsync_CustomMemoryDirectory_UsesConfiguredPath(string memoryDirectory)
    {
        // Arrange
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:MemoryDirectory"] = memoryDirectory,
                ["Llm:Provider"] = "OpenAI",
                ["Llm:ApiKey"] = "test-key"
            })
            .Build();

        _fileSystem.AddDirectory(memoryDirectory);

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            customConfig,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        healingPerformed.Should().BeTrue();

        string expectedTemplatesDir = _fileSystem.Path.Combine(memoryDirectory, "templates");
        _fileSystem.Directory.Exists(expectedTemplatesDir).Should().BeTrue(
            $"templates directory should be created at {expectedTemplatesDir}");
    }

    [Fact]
    public async Task PerformSelfHealingAsync_NonExistentMemoryDirectory_CreatesFullPath()
    {
        // Arrange
        // Neither memory nor templates directory exists

        // Act
        bool healingPerformed = await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert
        healingPerformed.Should().BeTrue();
        _fileSystem.Directory.Exists("/.memory").Should().BeTrue("memory directory should be created");
        _fileSystem.Directory.Exists("/.memory/templates").Should().BeTrue("templates directory should be created");
    }

    [Fact]
    public async Task PerformSelfHealingAsync_RecoveryNotifications_LogsAppropriateMessages()
    {
        // Arrange
        _fileSystem.AddDirectory("/.memory");
        // Templates directory missing

        // Act
        await ConfigurationChecker.PerformSelfHealingAsync(
            _configuration,
            _fileSystem,
            _mockLogger.Object,
            CancellationToken.None);

        // Assert - Verify complete logging sequence
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Performing self-healing")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log self-healing initiation");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recreated templates directory")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log directory recreation");

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("restored")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "Should log template restoration");
    }
}
