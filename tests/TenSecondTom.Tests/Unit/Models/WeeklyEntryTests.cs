using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

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
    public void WeeklyEntry_FilePath_ShouldUseThisWeekDirectory()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero); // Thursday, Oct 2, 2025, Week 40
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        filePath.Should().Be("thisweek/2025-40-Thu-1.md");
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
