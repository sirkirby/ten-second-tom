using TenSecondTom.Shared.Models;
using TenSecondTom.Shared.Results;

namespace TenSecondTom.Features.Search.Queries;

/// <summary>
/// Marker interface for request/response pattern.
/// Indicates this query returns a specific response type.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Marker interface for CQRS pattern")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public interface IRequest<out TResponse>
{
}

/// <summary>
/// Query to search memory entries by text query with optional date range filters.
/// Implements CQRS pattern for read operations.
/// </summary>
/// <param name="Query">The search query text to match against entries.</param>
/// <param name="StartDate">Optional start date filter (inclusive).</param>
/// <param name="EndDate">Optional end date filter (inclusive).</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "Public API by design")]
public sealed record SearchMemoriesQuery(
    string Query,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<Result<IReadOnlyList<MemoryEntry>>>;
