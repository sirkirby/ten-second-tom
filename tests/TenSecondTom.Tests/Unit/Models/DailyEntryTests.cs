using FluentAssertions;
using TenSecondTom.Shared.Models;

namespace TenSecondTom.Tests.Unit.Models;

/// <summary>
/// Unit tests for DailyEntry record (inherits MemoryEntry).
/// Tests daily summary structure and validation.
/// </summary>
public sealed class DailyEntryTests
{
    [Fact]
    public void Create_WithValidDailySummary_ShouldSucceed()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var summary = new DailySummary
        {
            KeyEvents = new List<string> { "Team meeting", "Completed feature X" },
            Themes = new List<string> { "Productivity", "Collaboration" },
            TodoItems = new List<TodoItem>
            {
                new() { Description = "Review PR", IsCompleted = false },
                new() { Description = "Write tests", IsCompleted = true }
            },
            ImportantPeople = new List<string> { "John", "Sarah" },
            NotableTasks = new List<string> { "Design document review" }
        };

        // Act
        var entry = new DailyEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 1,
            UserInput = "What happened today?\n> Had productive meetings.",
            LlmResponse = "## Key Events\n- Team meeting\n- Completed feature X",
            Metadata = CreateValidMetadata(),
            Summary = summary
        };

        // Assert
        entry.Should().NotBeNull();
        entry.Summary.Should().Be(summary);
        entry.Summary.KeyEvents.Should().HaveCount(2);
        entry.Summary.Themes.Should().HaveCount(2);
        entry.Summary.TodoItems.Should().HaveCount(2);
    }

    [Fact]
    public void DailySummary_AtLeastOneSectionShouldHaveContent()
    {
        // Arrange & Act
        var summaryWithEvents = new DailySummary
        {
            KeyEvents = new List<string> { "Something happened" }
        };

        var summaryWithThemes = new DailySummary
        {
            Themes = new List<string> { "Theme" }
        };

        var summaryWithTodos = new DailySummary
        {
            TodoItems = new List<TodoItem> { new() { Description = "Task" } }
        };

        // Assert
        summaryWithEvents.KeyEvents.Should().NotBeEmpty();
        summaryWithThemes.Themes.Should().NotBeEmpty();
        summaryWithTodos.TodoItems.Should().NotBeEmpty();
    }

    [Fact]
    public void DailySummary_AllSectionsCanBeEmpty()
    {
        // Arrange & Act
        var summary = new DailySummary();

        // Assert
        summary.KeyEvents.Should().BeEmpty();
        summary.Themes.Should().BeEmpty();
        summary.TodoItems.Should().BeEmpty();
        summary.ImportantPeople.Should().BeEmpty();
        summary.NotableTasks.Should().BeEmpty();
    }

    [Fact]
    public void TodoItem_WithDescription_ShouldBeValid()
    {
        // Arrange & Act
        var todoItem = new TodoItem
        {
            Description = "Complete unit tests",
            IsCompleted = false
        };

        // Assert
        todoItem.Description.Should().Be("Complete unit tests");
        todoItem.IsCompleted.Should().BeFalse();
        todoItem.DueDate.Should().BeNull();
    }

    [Fact]
    public void TodoItem_WithDueDate_ShouldStoreDate()
    {
        // Arrange
        var dueDate = DateTimeOffset.UtcNow.AddDays(3);

        // Act
        var todoItem = new TodoItem
        {
            Description = "Complete review",
            IsCompleted = false,
            DueDate = dueDate
        };

        // Assert
        todoItem.DueDate.Should().Be(dueDate);
    }

    [Fact]
    public void TodoItem_Completed_ShouldHaveIsCompletedTrue()
    {
        // Arrange & Act
        var todoItem = new TodoItem
        {
            Description = "Finished task",
            IsCompleted = true
        };

        // Assert
        todoItem.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void DailyEntry_InheritsFromMemoryEntry()
    {
        // Arrange & Act
        var entry = new DailyEntry
        {
            EntryId = "today-10-01-2025-1",
            Command = "today",
            Timestamp = DateTimeOffset.UtcNow,
            EntryNumber = 1,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata(),
            Summary = new DailySummary()
        };

        // Assert
        entry.Should().BeAssignableTo<MemoryEntry>();
    }

    [Fact]
    public void DailyEntry_FilePath_ShouldUseTodayDirectory()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2025, 10, 2, 14, 30, 0, TimeSpan.Zero);
        var entry = new DailyEntry
        {
            EntryId = "today-10-02-2025-2",
            Command = "today",
            Timestamp = timestamp,
            EntryNumber = 2,
            UserInput = "Test",
            LlmResponse = "Response",
            Metadata = CreateValidMetadata(),
            Summary = new DailySummary()
        };

        // Act
        string filePath = entry.FilePath;

        // Assert
        filePath.Should().Be(".memory/today/10-02-2025_2.md");
    }

    [Fact]
    public void DailySummary_WithMultipleTodoItems_ShouldMaintainOrder()
    {
        // Arrange & Act
        var summary = new DailySummary
        {
            TodoItems = new List<TodoItem>
            {
                new() { Description = "First task" },
                new() { Description = "Second task" },
                new() { Description = "Third task" }
            }
        };

        // Assert
        summary.TodoItems.Should().HaveCount(3);
        summary.TodoItems[0].Description.Should().Be("First task");
        summary.TodoItems[1].Description.Should().Be("Second task");
        summary.TodoItems[2].Description.Should().Be("Third task");
    }

    [Fact]
    public void DailySummary_KeyEvents_CanContainMultipleEvents()
    {
        // Arrange & Act
        var summary = new DailySummary
        {
            KeyEvents = new List<string>
            {
                "Morning standup meeting",
                "Lunch with team",
                "Code review session",
                "Deployment to staging"
            }
        };

        // Assert
        summary.KeyEvents.Should().HaveCount(4);
        summary.KeyEvents.Should().Contain("Morning standup meeting");
    }

    [Fact]
    public void DailySummary_Themes_CapturesHighLevelPatterns()
    {
        // Arrange & Act
        var summary = new DailySummary
        {
            Themes = new List<string>
            {
                "Collaboration",
                "Problem-solving",
                "Learning"
            }
        };

        // Assert
        summary.Themes.Should().HaveCount(3);
        summary.Themes.Should().Contain("Collaboration");
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
