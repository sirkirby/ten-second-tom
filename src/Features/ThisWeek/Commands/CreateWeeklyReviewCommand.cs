using TenSecondTom.Shared.Contracts;
using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.ThisWeek.Commands;

/// <summary>
/// Command to create a weekly review entry by aggregating daily entries.
/// </summary>
public sealed record CreateWeeklyReviewCommand : IRequest<Result<WeeklyEntry>>
{
    /// <summary>
    /// Gets the optional custom date range for the weekly review.
    /// If not specified, defaults to the last 7 days.
    /// </summary>
    public DateRange? CustomDateRange { get; init; }

    /// <summary>
    /// Gets the optional LLM provider override (e.g., "OpenAI", "Anthropic").
    /// If not specified, uses the default provider from configuration.
    /// </summary>
    public string? LlmProviderOverride { get; init; }
}
