using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

/// <summary>
/// Unit tests for WeeklyEntry record (inherits MemoryEntry).
/// Tests weekly summary structure and validation rules.
/// </summary>
public sealed class WeeklyEntryTests
{
    [Fact]
    public void Create_WithValidWeeklySummary_ShouldSucceed()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var summary = new WeeklySummary
        {
            TopAccomplishments = new List<string>
            {
                "Shipped feature X",
                "Fixed critical bug",
                "Improved test coverage"
            },
            TopChallenges = new List<string>
            {
                "Integration issues",
                "Performance bottleneck",
                "Team coordination"
            },
            DateRange = new DateRange
            {
                StartDate = DateTimeOffset.UtcNow.AddDays(-6),
                EndDate = DateTimeOffset.UtcNow
            }
        };

        // Act
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "What did I accomplish this week?",
            LlmResponse = "## Top Accomplishments\n1. Shipped feature X",
            Metadata = CreateValidMetadata(),
            Summary = summary
        };

        // Assert
        entry.Should().NotBeNull();
        entry.Summary.Should().Be(summary);
        entry.Summary.TopAccomplishments.Should().HaveCount(3);
        entry.Summary.TopChallenges.Should().HaveCount(3);
    }

    [Fact]
    public void WeeklySummary_TopAccomplishments_MustHaveExactlyThree()
    {
        // Arrange & Act
        var summary = new WeeklySummary
        {
            TopAccomplishments = new List<string>
            {
                "First accomplishment",
                "Second accomplishment",
                "Third accomplishment"
            },
            TopChallenges = new List<string> { "Challenge 1", "Challenge 2", "Challenge 3" },
            DateRange = CreateValidDateRange()
        };

        // Assert
        summary.TopAccomplishments.Should().HaveCount(3);
    }

    [Fact]
    public void WeeklySummary_TopChallenges_MustHaveExactlyThree()
    {
        // Arrange & Act
        var summary = new WeeklySummary
        {
            TopAccomplishments = new List<string> { "A1", "A2", "A3" },
            TopChallenges = new List<string>
            {
                "First challenge",
                "Second challenge",
                "Third challenge"
            },
            DateRange = CreateValidDateRange()
        };

        // Assert
        summary.TopChallenges.Should().HaveCount(3);
    }

    [Fact]
    public void DateRange_StartDate_MustBeBeforeEndDate()
    {
        // Arrange
        var startDate = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2025, 10, 7, 0, 0, 0, TimeSpan.Zero);

        // Act
        var dateRange = new DateRange
        {
            StartDate = startDate,
            EndDate = endDate
        };

        // Assert
        dateRange.StartDate.Should().BeBefore(dateRange.EndDate);
    }

    [Fact]
    public void DateRange_DurationInDays_ShouldCalculateCorrectly()
    {
        // Arrange
        var dateRange = new DateRange
        {
            StartDate = new DateTimeOffset(2025, 10, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2025, 10, 7, 23, 59, 59, TimeSpan.Zero)
        };

        // Act
        int duration = dateRange.DurationInDays;

        // Assert
        duration.Should().Be(6); // 7 days minus 1 (inclusive start, inclusive end)
    }

    [Fact]
    public void DateRange_DurationInDays_ValidRange_ThreeToDays()
    {
        // Arrange
        var threeDay = new DateRange
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-3),
            EndDate = DateTimeOffset.UtcNow
        };

        var tenDay = new DateRange
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-10),
            EndDate = DateTimeOffset.UtcNow
        };

        // Act & Assert
        threeDay.DurationInDays.Should().BeInRange(3, 10);
        tenDay.DurationInDays.Should().BeInRange(3, 10);
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
            Metadata = CreateValidMetadata(),
            Summary = CreateValidWeeklySummary()
        };

        // Assert
        entry.Should().BeAssignableTo<MemoryEntry>();
    }

    [Fact]
    public void WeeklyEntry_FilePath_ShouldUseThisWeekDirectory()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);
        var entry = new WeeklyEntry
        {
            EntryId = "thisweek-2025-40-1",
            Command = "thisweek",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata(),
            Summary = CreateValidWeeklySummary()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        filePath.Should().Be(".memory/thisweek/2025-40_1.md");
    }

    [Fact]
    public void WeeklySummary_OptionalFields_CanBeNull()
    {
        // Arrange & Act
        var summary = new WeeklySummary
        {
            TopAccomplishments = new List<string> { "A1", "A2", "A3" },
            TopChallenges = new List<string> { "C1", "C2", "C3" },
            DateRange = CreateValidDateRange(),
            KeyInsights = null,
            GoalsForNextWeek = null
        };

        // Assert
        summary.KeyInsights.Should().BeNull();
        summary.GoalsForNextWeek.Should().BeNull();
    }

    [Fact]
    public void WeeklySummary_WithKeyInsights_ShouldStoreInsights()
    {
        // Arrange & Act
        var summary = new WeeklySummary
        {
            TopAccomplishments = new List<string> { "A1", "A2", "A3" },
            TopChallenges = new List<string> { "C1", "C2", "C3" },
            DateRange = CreateValidDateRange(),
            KeyInsights = new List<string>
            {
                "Team collaboration improved",
                "Need better time management"
            }
        };

        // Assert
        summary.KeyInsights.Should().NotBeNull();
        summary.KeyInsights.Should().HaveCount(2);
    }

    [Fact]
    public void WeeklySummary_WithGoalsForNextWeek_ShouldStoreGoals()
    {
        // Arrange & Act
        var summary = new WeeklySummary
        {
            TopAccomplishments = new List<string> { "A1", "A2", "A3" },
            TopChallenges = new List<string> { "C1", "C2", "C3" },
            DateRange = CreateValidDateRange(),
            GoalsForNextWeek = new List<string>
            {
                "Complete feature Y",
                "Improve test coverage",
                "Refactor module Z"
            }
        };

        // Assert
        summary.GoalsForNextWeek.Should().NotBeNull();
        summary.GoalsForNextWeek.Should().HaveCount(3);
    }

    [Fact]
    public void DateRange_SameStartAndEndDate_ShouldHaveZeroDuration()
    {
        // Arrange
        var date = new DateTimeOffset(2025, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var dateRange = new DateRange
        {
            StartDate = date,
            EndDate = date
        };

        // Act
        int duration = dateRange.DurationInDays;

        // Assert
        duration.Should().Be(0);
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

    private static DateRange CreateValidDateRange()
    {
        return new DateRange
        {
            StartDate = DateTimeOffset.UtcNow.AddDays(-6),
            EndDate = DateTimeOffset.UtcNow
        };
    }

    private static WeeklySummary CreateValidWeeklySummary()
    {
        return new WeeklySummary
        {
            TopAccomplishments = new List<string> { "A1", "A2", "A3" },
            TopChallenges = new List<string> { "C1", "C2", "C3" },
            DateRange = CreateValidDateRange()
        };
    }
}
