namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a daily memory entry with structured summary.
/// Inherits from MemoryEntry and adds daily-specific summary fields.
/// </summary>
public record DailyEntry : MemoryEntry
{
    /// <summary>
    /// Gets the structured summary of the daily entry.
    /// </summary>
    public required DailySummary Summary { get; init; }
}

/// <summary>
/// Structured summary extracted from daily reflection.
/// </summary>
public record DailySummary
{
    /// <summary>
    /// Gets the key events that happened during the day.
    /// </summary>
    public IReadOnlyList<string> KeyEvents { get; init; } = new List<string>();

    /// <summary>
    /// Gets the high-level themes or patterns from the day.
    /// </summary>
    public IReadOnlyList<string> Themes { get; init; } = new List<string>();

    /// <summary>
    /// Gets the to-do items mentioned or identified.
    /// </summary>
    public IReadOnlyList<TodoItem> TodoItems { get; init; } = new List<TodoItem>();

    /// <summary>
    /// Gets the important people mentioned during the day.
    /// </summary>
    public IReadOnlyList<string> ImportantPeople { get; init; } = new List<string>();

    /// <summary>
    /// Gets the notable tasks or activities that require follow-up.
    /// </summary>
    public IReadOnlyList<string> NotableTasks { get; init; } = new List<string>();
}

/// <summary>
/// Represents a to-do item extracted from daily reflection.
/// </summary>
public record TodoItem
{
    /// <summary>
    /// Gets the description of the task.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether the task is completed.
    /// </summary>
    public bool IsCompleted { get; init; }

    /// <summary>
    /// Gets the optional due date for the task.
    /// </summary>
    public DateTimeOffset? DueDate { get; init; }
}
