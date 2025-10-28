namespace TenSecondTom.Shared.Models;

/// <summary>
/// Represents a weekly memory entry.
/// </summary>
/// <remarks>
/// A weekly entry is a thin wrapper around MemoryEntry that provides type distinction.
/// The prompt template defines the output structure, and the LlmResponse field contains
/// the complete output from the LLM. No additional parsing or structure is imposed.
/// </remarks>
public record WeeklyEntry : MemoryEntry
{
    // No additional properties - this is a type marker for weekly entries
}

/// <summary>
/// Represents a date range used for weekly reviews.
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
