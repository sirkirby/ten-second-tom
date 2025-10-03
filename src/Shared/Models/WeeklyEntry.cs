namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a weekly memory entry with structured summary.
/// Inherits from MemoryEntry and adds weekly-specific summary fields.
/// </summary>
public record WeeklyEntry : MemoryEntry
{
    /// <summary>
    /// Gets the structured summary of the weekly entry.
    /// </summary>
    public required WeeklySummary Summary { get; init; }
}

/// <summary>
/// Structured summary extracted from weekly reflection.
/// </summary>
public record WeeklySummary
{
    /// <summary>
    /// Gets the top 3 accomplishments from the week.
    /// Must contain exactly 3 items.
    /// </summary>
    public required IReadOnlyList<string> TopAccomplishments { get; init; }

    /// <summary>
    /// Gets the top 3 challenges from the week.
    /// Must contain exactly 3 items.
    /// </summary>
    public required IReadOnlyList<string> TopChallenges { get; init; }

    /// <summary>
    /// Gets the date range for the weekly review.
    /// Duration must be between 3-10 days.
    /// </summary>
    public required DateRange DateRange { get; init; }

    /// <summary>
    /// Gets optional key insights or learnings from the week.
    /// </summary>
    public IReadOnlyList<string>? KeyInsights { get; init; }

    /// <summary>
    /// Gets optional goals or priorities for the next week.
    /// </summary>
    public IReadOnlyList<string>? GoalsForNextWeek { get; init; }
}

/// <summary>
/// Represents a date range with validation.
/// </summary>
public record DateRange
{
    /// <summary>
    /// Gets the start date of the range.
    /// </summary>
    public required DateTimeOffset StartDate { get; init; }

    /// <summary>
    /// Gets the end date of the range.
    /// </summary>
    public required DateTimeOffset EndDate { get; init; }

    /// <summary>
    /// Gets the duration in days (end date - start date).
    /// For weekly reviews, this should be between 3-10 days.
    /// </summary>
    public int DurationInDays => (EndDate.Date - StartDate.Date).Days;
}
