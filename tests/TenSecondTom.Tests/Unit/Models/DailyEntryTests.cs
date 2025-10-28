using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

/// <summary>
/// Unit tests for DailyEntry record (inherits MemoryEntry).
/// </summary>
public sealed class DailyEntryTests
{
    [Fact]
    public void Create_WithValidProperties_ShouldSucceed()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.UtcNow;
        var entry = new DailyEntry
        {
            EntryId = "today-10-20-2025-1",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Had a productive day working on features.",
            LlmResponse = "## Key Events\n- Team meeting\n- Completed feature X\n\n## Themes\n- Productivity\n- Collaboration",
            Metadata = CreateValidMetadata()
        };

        // Assert
        entry.Should().NotBeNull();
        entry.EntryId.Should().Be("today-10-20-2025-1");
        entry.Command.Should().Be("today");
        entry.UserInput.Should().Contain("productive day");
        entry.LlmResponse.Should().Contain("Team meeting");
    }

    [Fact]
    public void DailyEntry_InheritsFromMemoryEntry()
    {
        // Arrange & Act
        var entry = new DailyEntry
        {
            EntryId = "today-10-20-2025-1",
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test input",
            LlmResponse = "Test response",
            Metadata = CreateValidMetadata()
        };

        // Assert
        entry.Should().BeAssignableTo<MemoryEntry>();
    }

    [Fact]
    public void DailyEntry_FilePath_ShouldUseTodayDirectory()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 20, 14, 30, 0, TimeSpan.Zero);
        var entry = new DailyEntry
        {
            EntryId = "today-10-20-2025-1",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        filePath.Should().Be("today/10-20-2025_1.md");
    }

    [Fact]
    public void DailyEntry_MultipleEntriesSameDay_ShouldHaveDifferentFilePaths()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 20, 14, 30, 0, TimeSpan.Zero);
        
        var entry1 = new DailyEntry
        {
            EntryId = "today-10-20-2025-1",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "First entry",
            LlmResponse = "Response 1",
            Metadata = CreateValidMetadata()
        };

        var entry2 = new DailyEntry
        {
            EntryId = "today-10-20-2025-2",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 2,
            UserInput = "Second entry",
            LlmResponse = "Response 2",
            Metadata = CreateValidMetadata()
        };

        // Act & Assert
        entry1.FilePath.Should().Be("today/10-20-2025_1.md");
        entry2.FilePath.Should().Be("today/10-20-2025_2.md");
        entry1.FilePath.Should().NotBe(entry2.FilePath);
    }

    private static MemoryEntryMetadata CreateValidMetadata()
    {
        return new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 500,
            ProcessingDuration = TimeSpan.FromSeconds(2.5)
        };
    }
}
