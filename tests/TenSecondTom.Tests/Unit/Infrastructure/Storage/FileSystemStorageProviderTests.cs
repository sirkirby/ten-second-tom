using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TenSecondTom.Infrastructure.Storage;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Tests.Unit.Infrastructure.Storage;

/// <summary>
/// Unit tests for FileSystemStorageProvider implementation.
/// Tests file I/O operations with mocked file system access.
/// </summary>
public sealed class FileSystemStorageProviderTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly Mock<ILogger<FileSystemStorageProvider>> _mockLogger;

    public FileSystemStorageProviderTests()
    {
        // Create a unique test directory for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"tom-test-{Guid.NewGuid()}");
        _mockLogger = new Mock<ILogger<FileSystemStorageProvider>>();
    }

    public void Dispose()
    {
        // Clean up test directory after each test
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesMarkdownFileWithYamlFrontmatter()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = new DailyEntry
        {
            EntryId = "today-10-02-2025-1",
            Command = "today",
            Timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero),
            EntryNumber = 1,
            UserInput = "Test user input",
            LlmResponse = "Test LLM response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4",
                TokensUsed = 150,
                ProcessingDuration = TimeSpan.FromSeconds(2.5)
            }
        };

        // Act
        Result<MemoryEntry> result = await provider.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        string expectedPath = Path.Combine(_testDirectory, "today", "10-02-2025_1.md");
        File.Exists(expectedPath).Should().BeTrue();

        string content = await File.ReadAllTextAsync(expectedPath);
        content.Should().Contain("---"); // YAML frontmatter markers
        content.Should().Contain("entry-id: today-10-02-2025-1");
        content.Should().Contain("command: today");
        content.Should().Contain("llm-provider: OpenAI");
        content.Should().Contain("Test user input");
        content.Should().Contain("Test LLM response");
    }

    [Fact]
    public async Task SaveAsync_FollowsFilePathPattern()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero));

        // Act
        Result<MemoryEntry> result = await provider.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        string expectedPath = Path.Combine(_testDirectory, "today", "10-02-2025_1.md");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_IncrementsEntryNumberForMultipleSameDayEntries()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var date = new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero);

        var entry1 = CreateTestDailyEntry("today-10-02-2025-1", 1, date);
        var entry2 = CreateTestDailyEntry("today-10-02-2025-2", 2, date.AddHours(2));

        // Act
        await provider.SaveAsync(entry1, CancellationToken.None);
        await provider.SaveAsync(entry2, CancellationToken.None);

        // Assert
        File.Exists(Path.Combine(_testDirectory, "today", "10-02-2025_1.md")).Should().BeTrue();
        File.Exists(Path.Combine(_testDirectory, "today", "10-02-2025_2.md")).Should().BeTrue();
    }

    [Fact]
    public async Task GetEntriesAsync_ReadsAndParsesMarkdownFiles()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        await provider.SaveAsync(entry, CancellationToken.None);

        // Act
        Result<IReadOnlyList<MemoryEntry>> result = await provider.GetEntriesAsync(
            "today",
            new DateTime(2025, 10, 1),
            new DateTime(2025, 10, 3),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].EntryId.Should().Be("today-10-02-2025-1");
        result.Value[0].UserInput.Should().Be("Test user input");
    }

    [Fact]
    public async Task CountEntriesAsync_CountsFilesMatchingPattern()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var date = new DateTime(2025, 10, 2);

        var entry1 = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(date, TimeSpan.Zero));
        var entry2 = CreateTestDailyEntry("today-10-02-2025-2", 2, new DateTimeOffset(date.AddHours(2), TimeSpan.Zero));

        await provider.SaveAsync(entry1, CancellationToken.None);
        await provider.SaveAsync(entry2, CancellationToken.None);

        // Act
        Result<int> result = await provider.CountEntriesAsync("today", date, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }

    [Fact]
    public async Task SearchEntriesAsync_SearchesFileContent()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry1 = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        var entry2 = CreateTestDailyEntry("today-10-03-2025-1", 1, new DateTimeOffset(2025, 10, 3, 14, 0, 0, TimeSpan.Zero));
        
        // Modify entry2 to contain searchable content
        var modifiedEntry2 = entry2 with { UserInput = "Meeting with the team about the project" };

        await provider.SaveAsync(entry1, CancellationToken.None);
        await provider.SaveAsync(modifiedEntry2, CancellationToken.None);

        // Act
        Result<IReadOnlyList<MemoryEntry>> result = await provider.SearchEntriesAsync(
            "meeting",
            null,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value[0].EntryId.Should().Be("today-10-03-2025-1");
    }

    [Fact]
    public async Task SearchEntriesAsync_IsCaseInsensitive()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        var modifiedEntry = entry with { UserInput = "IMPORTANT MEETING" };
        await provider.SaveAsync(modifiedEntry, CancellationToken.None);

        // Act
        Result<IReadOnlyList<MemoryEntry>> result = await provider.SearchEntriesAsync(
            "important",
            null,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteEntriesAsync_RemovesFiles()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry1 = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        var entry2 = CreateTestDailyEntry("today-10-03-2025-1", 1, new DateTimeOffset(2025, 10, 3, 14, 0, 0, TimeSpan.Zero));

        await provider.SaveAsync(entry1, CancellationToken.None);
        await provider.SaveAsync(entry2, CancellationToken.None);

        // Act
        Result<int> result = await provider.DeleteEntriesAsync(
            new DateTime(2025, 10, 2),
            new DateTime(2025, 10, 2),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        File.Exists(Path.Combine(_testDirectory, "today", "10-02-2025_1.md")).Should().BeFalse();
        File.Exists(Path.Combine(_testDirectory, "today", "10-03-2025_1.md")).Should().BeTrue();
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_RespectsRetentionPolicy()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var now = DateTime.UtcNow;
        var oldEntry = CreateTestDailyEntry("today-08-01-2025-1", 1, new DateTimeOffset(now.AddDays(-40), TimeSpan.Zero));
        var recentEntry = CreateTestDailyEntry("today-10-01-2025-1", 1, new DateTimeOffset(now.AddDays(-1), TimeSpan.Zero));

        await provider.SaveAsync(oldEntry, CancellationToken.None);
        await provider.SaveAsync(recentEntry, CancellationToken.None);

        // Act - Purge entries older than 30 days
        Result<int> result = await provider.PurgeExpiredEntriesAsync(
            RetentionPolicy.Days30,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1); // Only old entry should be purged
    }

    [Fact]
    public async Task PurgeExpiredEntriesAsync_SkipsIndefiniteRetention()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var oldEntry = CreateTestDailyEntry("today-08-01-2025-1", 1, new DateTimeOffset(DateTime.UtcNow.AddDays(-400), TimeSpan.Zero));
        await provider.SaveAsync(oldEntry, CancellationToken.None);

        // Act
        Result<int> result = await provider.PurgeExpiredEntriesAsync(
            RetentionPolicy.Indefinite,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0); // No entries should be purged
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));

        // Act
        Result<MemoryEntry> result = await provider.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Directory.Exists(Path.Combine(_testDirectory, "today")).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ReturnsFailureOnIOError()
    {
        // Arrange - Use a path that will definitely fail across all environments
        // Try to write to root system directory which should be read-only or restricted
        string invalidPath = OperatingSystem.IsWindows() 
            ? "C:\\Windows\\System32\\test" 
            : "/dev/null/test"; // On Unix, /dev/null is a character device, not a directory
        
        var provider = new FileSystemStorageProvider(invalidPath, _mockLogger.Object);
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));

        // Act
        Result<MemoryEntry> result = await provider.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Failed to save");
    }

    [Fact]
    public async Task GetEntryByIdAsync_RetrievesSpecificEntry()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        await provider.SaveAsync(entry, CancellationToken.None);

        // Act
        Result<MemoryEntry?> result = await provider.GetEntryByIdAsync("today-10-02-2025-1", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.EntryId.Should().Be("today-10-02-2025-1");
    }

    [Fact]
    public async Task GetEntryByIdAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);

        // Act
        Result<MemoryEntry?> result = await provider.GetEntryByIdAsync("nonexistent-entry", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_HandlesWeeklyEntries()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero), // Thursday, Oct 2, 2025, Week 40
            EntryNumber = 1,
            UserInput = "Weekly summary input",
            LlmResponse = "Weekly summary response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "Anthropic",
                LlmModel = "claude-3-sonnet-20240229"
            }
        };

        // Act
        Result<MemoryEntry> result = await provider.SaveAsync(entry, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        string expectedPath = Path.Combine(_testDirectory, "thisweek", "2025-40-Thu-1.md");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task SearchEntriesAsync_ExcludesTemplatesDirectory()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);

        
        // Create a regular memory entry
        var memoryEntry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        var modifiedEntry = memoryEntry with { UserInput = "Template test content" };
        await provider.SaveAsync(modifiedEntry, CancellationToken.None);

        // Create a template file that should be excluded
        string templatesDir = Path.Combine(_testDirectory, "templates");
        Directory.CreateDirectory(templatesDir);
        string templateFile = Path.Combine(templatesDir, "test-template.md");
        await File.WriteAllTextAsync(templateFile, "Template test content");

        // Act
        Result<IReadOnlyList<MemoryEntry>> result = await provider.SearchEntriesAsync(
            "Template test content",
            null,
            null,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1, "only the memory entry should be found, not the template file");
        result.Value[0].EntryId.Should().Be("today-10-02-2025-1");
    }

    [Fact]
    public async Task DeleteEntriesAsync_ExcludesTemplatesDirectory()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);
        
        // Create a memory entry
        var entry = CreateTestDailyEntry("today-10-02-2025-1", 1, new DateTimeOffset(2025, 10, 2, 14, 0, 0, TimeSpan.Zero));
        await provider.SaveAsync(entry, CancellationToken.None);

        // Create a template file that should be excluded from deletion
        string templatesDir = Path.Combine(_testDirectory, "templates");
        Directory.CreateDirectory(templatesDir);
        string templateFile = Path.Combine(templatesDir, "test-template.md");
        await File.WriteAllTextAsync(templateFile, "---\nentry-id: template-1\n---\nTemplate content");

        // Act
        Result<int> result = await provider.DeleteEntriesAsync(
            DateTime.MinValue,
            DateTime.MaxValue,
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1, "only the memory entry should be deleted");
        File.Exists(templateFile).Should().BeTrue("template file should not be deleted");
    }

    [Fact]
    public async Task GetEntryByIdAsync_ExcludesTemplatesDirectory()
    {
        // Arrange
        var provider = new FileSystemStorageProvider(_testDirectory, _mockLogger.Object);

        // Create a template file with an entry ID that should be excluded
        string templatesDir = Path.Combine(_testDirectory, "templates");
        Directory.CreateDirectory(templatesDir);
        string templateFile = Path.Combine(templatesDir, "test-template.md");
        await File.WriteAllTextAsync(templateFile, "---\nentry-id: template-test-1\n---\nTemplate content");

        // Act
        Result<MemoryEntry?> result = await provider.GetEntryByIdAsync(
            "template-test-1",
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull("template files should be excluded from entry lookup");
    }

    /// <summary>
    /// Helper method to create a test DailyEntry with minimal required fields.
    /// </summary>
    private static DailyEntry CreateTestDailyEntry(string entryId, int entryNumber, DateTimeOffset timestamp)
    {
        return new DailyEntry
        {
            EntryId = entryId,
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = entryNumber,
            UserInput = "Test user input",
            LlmResponse = "Test LLM response",
            Metadata = new MemoryEntryMetadata
            {
                LlmProvider = "OpenAI",
                LlmModel = "gpt-4"
            }
        };
    }
}
