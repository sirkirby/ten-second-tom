using FluentAssertions;
using TenSecondTom.Shared.Constants;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Models;

/// <summary>
/// Unit tests for WeeklyEntry record (inherits MemoryEntry).
/// </summary>
public sealed class WeeklyEntryTests
{
    [Fact]
    public void Create_WithValidProperties_ShouldSucceed()
    {
        // Arrange & Act
        var timestamp = DateTimeOffset.UtcNow;
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "What did I accomplish this week?",
            LlmResponse = "## Top Accomplishments\n1. Shipped feature X\n2. Fixed critical bug\n3. Improved test coverage",
            Metadata = CreateValidMetadata()
        };

        // Assert
        entry.Should().NotBeNull();
        entry.EntryId.Should().Be("thisweek-2025-40-1");
        entry.Command.Should().Be("thisweek");
        entry.UserInput.Should().Be("What did I accomplish this week?");
        entry.LlmResponse.Should().Contain("Shipped feature X");
    }

    [Fact]
    public void WeeklyEntry_InheritsFromMemoryEntry()
    {
        // Arrange & Act
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata()
        };

        // Assert
        entry.Should().BeAssignableTo<MemoryEntry>();
    }

    [Fact]
    public void WeeklyEntry_FilePath_ShouldUseNoteDirectoryWithDateRange()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero); // Thursday, Oct 2, 2025, Week 40
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = CommandNames.ThisWeek,
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        var (start, end) = GetWeekRange(timestamp.Date);
        filePath.Should().Be($"note/{start:MM-dd-yyyy}_{end:MM-dd-yyyy}_1_generated.md");
    }

    private static (DateTime Start, DateTime End) GetWeekRange(DateTime referenceDate)
    {
        var normalized = referenceDate.Date;
        var daysSinceMonday = ((int)normalized.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = normalized.AddDays(-daysSinceMonday);
        return (start, start.AddDays(6));
    }

    private static MemoryEntryMetadata CreateValidMetadata()
    {
        return new MemoryEntryMetadata
        {
            LlmProvider = "OpenAI",
            LlmModel = "gpt-4",
            TokensUsed = 1500,
            ProcessingDuration = TimeSpan.FromSeconds(3.2)
        };
    }
}
